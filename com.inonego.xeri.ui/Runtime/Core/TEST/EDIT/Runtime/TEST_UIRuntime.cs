/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UIRuntime.cs
수정일 : 2026-10-07

# 설명
UIRuntime의 Root Presentation, Context Authority, Scene 구성과 초기화·종료 실패 정리를 검증한다.

# 테스트 구성
 I: Singleton 공개와 Initialize rollback
 R: Runtime 종료 실패와 Terminal 정리
 S: Screen 정리 실패와 Terminal Shutdown
 A: Main/Child Context tree와 Context Authority
 C: Host·Scene 구성 검증
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using PanelSettings = UnityEngine.UIElements.PanelSettings;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// UI Composition Root의 실패 원자성과 역순 정리 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_UIRuntime
    {

    #region 헬퍼 타입

        // ============================================================
        /// <summary>
        /// 획득·반환 호출과 local-space 인자를 기록하는 Provider.
        /// </summary>
        // ============================================================
        private sealed class TestProvider : IGameObjectProvider
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Provider 기본 부모.
            /// </summary>
            // ------------------------------------------------------------
            public Transform Parent
            {
                get;
                set;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 누적 획득 호출 수.
            /// </summary>
            // ------------------------------------------------------------
            public int AcquireCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 누적 반환 호출 수.
            /// </summary>
            // ------------------------------------------------------------
            public int ReleaseCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 앞으로 실패시킬 반환 호출 수.
            /// </summary>
            // ------------------------------------------------------------
            public int ReleaseFailuresRemaining { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 반환 호출에서 실패 주입 전에 실행할 테스트 callback.
            /// </summary>
            // ------------------------------------------------------------
            public Action<GameObject> Releasing { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 마지막 획득의 worldPositionStays 인자.
            /// </summary>
            // ------------------------------------------------------------
            public bool LastAcquireWorldPositionStays { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 마지막 반환의 worldPositionStays 인자.
            /// </summary>
            // ------------------------------------------------------------
            public bool LastReleaseWorldPositionStays { get; private set; }

            private readonly Func<Transform, GameObject> acquire = null;

            // ------------------------------------------------------------
            /// <summary>
            /// GameObject 생성 함수를 사용하는 테스트 Provider를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestProvider(Func<Transform, GameObject> acquire) : base()
            {
                this.acquire = acquire ?? throw new ArgumentNullException(nameof(acquire));
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 현재 Parent에 테스트 GameObject를 획득한다.
            /// </summary>
            // ------------------------------------------------------------
            public GameObject Acquire(bool worldPositionStays = true)
            {
                AcquireCount++;
                LastAcquireWorldPositionStays = worldPositionStays;
                return acquire(Parent);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 이 테스트에서는 비동기 획득을 지원하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public Awaitable<GameObject> AcquireAsync(bool worldPositionStays = true)
            {
                throw new NotSupportedException();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 반환 호출과 좌표계 인자를 기록한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release
            (
                GameObject gameObject,
                bool worldPositionStays = true
            )
            {
                ReleaseCount++;
                LastReleaseWorldPositionStays = worldPositionStays;
                Releasing?.Invoke(gameObject);

                if (ReleaseFailuresRemaining > 0)
                {
                    ReleaseFailuresRemaining--;
                    throw new InvalidOperationException("injected provider release failure");
                }
            }
        }

        // ============================================================
        /// <summary>
        /// 초기화 실패 롤백에서 Screen Source 반환을 기록한다.
        /// </summary>
        // ============================================================
        private sealed class TestScreenSource : IScreenSource
        {
            // ------------------------------------------------------------
            /// <summary>
            /// ScreenInstance를 반환하기 전에 호출할 테스트 callback.
            /// </summary>
            // ------------------------------------------------------------
            public Action Acquiring { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 누적 획득 호출 수.
            /// </summary>
            // ------------------------------------------------------------
            public int AcquireCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 누적 반환 호출 수.
            /// </summary>
            // ------------------------------------------------------------
            public int ReleaseCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 단순 Screen backend를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public ScreenInstance Acquire(ScreenViewScope scope)
            {
                AcquireCount++;
                Acquiring?.Invoke();
                return new ScreenInstance(new TestScreenDriver());
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Screen backend 반환을 기록한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release(ScreenInstance instance)
            {
                ReleaseCount++;
            }
        }

        // ============================================================
        /// <summary>
        /// 즉시 Transition에 사용할 단순 Screen backend.
        /// </summary>
        // ============================================================
        private sealed class TestScreenDriver :
            IScreenDriver,
            IPresentationAlphaTarget,
            IPresentationVisibilityTarget
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 backend는 항상 유효하다.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsValid => true;

            // ------------------------------------------------------------
            /// <summary>
            /// Screen Alpha 상태.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationAlpha Alpha { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Screen Visibility 상태.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationVisibility Visibility { get; }


            // ------------------------------------------------------------
            /// <summary>
            /// 기본 Focus는 사용하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public object DefaultFocus => null;

            public TestScreenDriver() : base()
            {
                Alpha = new PresentationAlpha(this);
                Visibility = new PresentationVisibility(this);
            }

            bool IPresentationAlphaTarget.IsValid => IsValid;
            float IPresentationAlphaTarget.Alpha => appliedAlpha;
            private float appliedAlpha = 1.0f;

            bool IPresentationVisibilityTarget.IsValid => IsValid;
            bool IPresentationVisibilityTarget.IsVisible => isVisible;
            private bool isVisible = true;

            void IPresentationAlphaTarget.SetAlpha(float alpha)
            {
                appliedAlpha = alpha;
            }

            void IPresentationVisibilityTarget.SetVisible(bool visible)
            {
                isVisible = visible;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Screen은 유효한 임의 Focus 대상을 자신의 범위로 취급한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool ContainsFocus(object target)
            {
                return target != null;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 상호작용 상태는 이 계약 테스트에서 별도로 기록하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetInteractable(bool interactable)
            {
                // NONE
            }
        }

        // ============================================================
        /// <summary>
        /// 조립된 Runtime과 Scene Fade Provider를 묶는다.
        /// </summary>
        // ============================================================
        private sealed class RuntimeFixture
        {
            public UIRuntime Runtime { get; set; }
            public UISceneFadeSource FadeSource { get; set; }
            public UISettingsAsset Settings { get; set; }
            public TestProvider FadeProvider { get; set; }
            public Transform PresentationRoot { get; set; }
        }

        // ============================================================
        /// <summary>
        /// Dispose 호출을 기록한 뒤 예외를 던지는 하위 Handle.
        /// </summary>
        // ============================================================
        private sealed class ThrowingHandle : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                throw new InvalidOperationException("injected runtime screen child failure");
            }
        }

        // ======================================================================
        /// <summary>
        /// Context authority 전환의 native Focus 적용 실패를 one-shot으로 주입한다.
        /// </summary>
        // ======================================================================
        private sealed class AuthorityFocusDriver : FocusDriverBehaviour
        {
            public object Target { get; } = new object();
            public int SelectFailureCount { get; set; } = 0;
            public int CurrentFailureCount { get; set; } = 0;

            public override object Current
            {
                get
                {
                    if (CurrentFailureCount > 0)
                    {
                        CurrentFailureCount--;
                        throw new InvalidOperationException("injected authority focus current failure");
                    }

                    return null;
                }
            }

            public override bool CanSelect(object target)
            {
                return ReferenceEquals(target, Target);
            }

            public override bool IsValid(object target)
            {
                return ReferenceEquals(target, Target);
            }

            public override void Select(object target)
            {
                if (SelectFailureCount <= 0) return;

                SelectFailureCount--;
                throw new InvalidOperationException("injected authority focus failure");
            }

            public override object FindFallback()
            {
                return Target;
            }
        }

        private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

        // ------------------------------------------------------------
        /// <summary>
        /// private 직렬화 필드를 설정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SetField
        (
            object target,
            string name,
            object value
        )
        {
            FieldInfo field = null;

            for
            (
                var type = target.GetType();
                type != null && field == null;
                type = type.BaseType
            )
            {
                field = type.GetField
                (
                    name,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly
                );
            }

            Assert.IsNotNull(field, $"{target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Runtime 테스트가 사용하는 UGUI Layer와 semantic placement를 가진 Layout을 생성한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private PresentationLayout CreateLayout()
        {
            var layers = new List<PresentationLayerDefinition>
            {
                new PresentationLayerDefinition
                (
                    "Test",
                    "Test",
                    100,
                    PresentationBackend.UGUI
                ),
            };
            var layout = PresentationTestScope.CreateLayout
            (
                layers,
                Array.Empty<PresentationPlacementDefinition>()
            );
            ownedObjects.Add(layout);
            return layout;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 유효한 UGUI Scene Fade View를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private GameObject CreateFadeView(Transform parent)
        {
            var gameObject = new GameObject
            (
                "Fade View",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(UGUISceneFadeDriver)
            );
            gameObject.transform.SetParent(parent, false);
            var driver = gameObject.GetComponent<UGUISceneFadeDriver>();
            SetField(driver, "image", gameObject.GetComponent<Image>());
            SetField(driver, "canvasGroup", gameObject.GetComponent<CanvasGroup>());
            return gameObject;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Scene Fade Driver가 없는 잘못된 View를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private GameObject CreateInvalidFadeView(Transform parent)
        {
            var gameObject = new GameObject("Invalid Fade View", typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 실제 Runtime Initialize에 필요한 최소 Host와 Settings를 조립한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private RuntimeFixture CreateRuntimeFixture()
        {
            var host = new GameObject("UI Host");
            host.SetActive(false);
            ownedObjects.Add(host);

            var eventSystem = host.AddComponent<EventSystem>();
            var inputModule = host.AddComponent<InputSystemUIInputModule>();
            var uguiFocus = host.AddComponent<UGUIFocusDriver>();
            var uitkFocus = host.AddComponent<UITKFocusDriver>();
            var focus = host.AddComponent<UIFocusDriver>();
            var input = host.AddComponent<InputSystemScreenInputDriver>();
            var fadeSource = host.AddComponent<UGUISceneFadeSource>();
            var runtime = host.AddComponent<UIRuntime>();

            var presentationRootObject = new GameObject("Presentation Root", typeof(RectTransform));
            presentationRootObject.transform.SetParent(host.transform, false);
            SetField(uguiFocus, "eventSystem", eventSystem);

            var uiActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplayActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var ui = new InputActionMap("UI");
            var cancel = ui.AddAction("Cancel", InputActionType.Button);
            var submit = ui.AddAction("Submit", InputActionType.Button);
            var point = ui.AddAction("Point", InputActionType.PassThrough);
            var navigate = ui.AddAction("Navigate", InputActionType.PassThrough);
            var click = ui.AddAction("Click", InputActionType.PassThrough);
            var scrollWheel = ui.AddAction("ScrollWheel", InputActionType.PassThrough);
            ui.AddAction("Pause", InputActionType.Button);
            var gameplay = new InputActionMap("Player");
            gameplay.AddAction("Move", InputActionType.Value);
            uiActions.AddActionMap(ui);
            gameplayActions.AddActionMap(gameplay);
            inputModule.actionsAsset = uiActions;
            ownedObjects.Add(uiActions);
            ownedObjects.Add(gameplayActions);
            var inputReferences = new[]
            {
                InputActionReference.Create(point),
                InputActionReference.Create(navigate),
                InputActionReference.Create(submit),
                InputActionReference.Create(cancel),
                InputActionReference.Create(click),
                InputActionReference.Create(scrollWheel),
            };
            inputModule.point = inputReferences[0];
            inputModule.move = inputReferences[1];
            inputModule.submit = inputReferences[2];
            inputModule.cancel = inputReferences[3];
            inputModule.leftClick = inputReferences[4];
            inputModule.scrollWheel = inputReferences[5];

            for (var i = 0; i < inputReferences.Length; i++)
            {
                ownedObjects.Add(inputReferences[i]);
            }

            var fadeProvider = new TestProvider(CreateFadeView);
            var layout = CreateLayout();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();
            SetField(settings, "defaultLayout", layout);
            SetField(settings, "uitkPanelSettingsTemplate", panelSettings);
            SetField
            (
                settings,
                "presentationDestinations",
                new[]
                {
                    new PresentationDestinationDefinition
                    (
                        PresentationDestinationID.System,
                        "Test"
                    ),
                }
            );
            SetField
            (
                settings,
                "backendSupport",
                PresentationBackendSupport.UGUI
            );
            SetField(settings, "uiActionMap", "UI");
            SetField(settings, "gameplayActionsAsset", gameplayActions);
            SetField(settings, "gameplayActionMap", "Player");
            SetField
            (
                settings,
                "releaseActionNames",
                new[]
                {
                    "Cancel",
                    "Submit",
                    "Pause",
                }
            );
            ownedObjects.Add(panelSettings);
            ownedObjects.Add(settings);

            SetField(fadeSource, "viewProvider", fadeProvider);
            SetField(runtime, "presentationRoot", presentationRootObject.transform);
            SetField(runtime, "focusDriver", focus);
            SetField(runtime, "sceneFadeSource", fadeSource);
            SetField(runtime, "eventSystem", eventSystem);
            SetField(runtime, "inputModule", inputModule);
            SetField(runtime, "inputDriver", input);
            host.SetActive(true);

            return new RuntimeFixture
            {
                Runtime = runtime,
                FadeSource = fadeSource,
                Settings = settings,
                FadeProvider = fadeProvider,
                PresentationRoot = presentationRootObject.transform,
            };
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// EventSystem 없이 UITK native backend만 사용하는 최소 Runtime 구성을 조립한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private RuntimeFixture CreateUITKOnlyRuntimeFixture()
        {
            var host = new GameObject("UITK-only UI Host");
            host.SetActive(false);
            ownedObjects.Add(host);

            host.AddComponent<UITKFocusDriver>();
            var focus = host.AddComponent<UIFocusDriver>();
            var input = host.AddComponent<InputSystemScreenInputDriver>();
            var fadeSource = host.AddComponent<UITKSceneFadeSource>();
            var runtime = host.AddComponent<UIRuntime>();

            var presentationRootObject = new GameObject("UITK Presentation Root");
            presentationRootObject.transform.SetParent(host.transform, false);

            var uiActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplayActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var ui = new InputActionMap("UI");
            ui.AddAction("Cancel", InputActionType.Button);
            ui.AddAction("Submit", InputActionType.Button);
            var gameplay = new InputActionMap("Player");
            gameplay.AddAction("Move", InputActionType.Value);
            uiActions.AddActionMap(ui);
            gameplayActions.AddActionMap(gameplay);
            ownedObjects.Add(uiActions);
            ownedObjects.Add(gameplayActions);

            var layout = PresentationTestScope.CreateLayout
            (
                new[]
                {
                    new PresentationLayerDefinition
                    (
                        "UITK",
                        "UITK",
                        100,
                        PresentationBackend.UITK
                    ),
                },
                Array.Empty<PresentationPlacementDefinition>()
            );
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();
            SetField(settings, "defaultLayout", layout);
            SetField(settings, "uitkPanelSettingsTemplate", panelSettings);
            SetField
            (
                settings,
                "presentationDestinations",
                new[]
                {
                    new PresentationDestinationDefinition
                    (
                        PresentationDestinationID.System,
                        "UITK"
                    ),
                }
            );
            SetField
            (
                settings,
                "backendSupport",
                PresentationBackendSupport.UITK
            );
            SetField(settings, "uiActionsAsset", uiActions);
            SetField(settings, "uiActionMap", "UI");
            SetField(settings, "gameplayActionsAsset", gameplayActions);
            SetField(settings, "gameplayActionMap", "Player");
            SetField
            (
                settings,
                "releaseActionNames",
                new[]
                {
                    "Cancel",
                    "Submit",
                }
            );
            ownedObjects.Add(layout);
            ownedObjects.Add(panelSettings);
            ownedObjects.Add(settings);

            var fadeView = Resources.Load<UnityEngine.UIElements.VisualTreeAsset>
            (
                "Xeri/UI/TEST_UITKUI"
            );
            Assert.IsNotNull(fadeView);
            SetField(fadeSource, "viewAsset", fadeView);
            SetField(fadeSource, "rootName", "SceneFade");
            SetField(runtime, "presentationRoot", presentationRootObject.transform);
            SetField(runtime, "focusDriver", focus);
            SetField(runtime, "sceneFadeSource", fadeSource);
            SetField(runtime, "inputDriver", input);
            host.SetActive(true);

            return new RuntimeFixture
            {
                Runtime = runtime,
                FadeSource = fadeSource,
                Settings = settings,
                FadeProvider = null,
                PresentationRoot = presentationRootObject.transform,
            };
        }

    #endregion

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트마다 UI Runtime Singleton 기본 Slot을 격리한다.
        /// </summary>
        // ------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            UIRuntime.Clear();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트에서 만든 Unity Object를 역순 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void TearDown()
        {
            for (var i = ownedObjects.Count - 1; i >= 0; i--)
            {
                if (ownedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }

            ownedObjects.Clear();
            UIRuntime.Clear();
        }

    #endregion

    #region I-1: 싱글턴 공개 수명

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 초기화 완료 Runtime만 Current로 공개하고,
        /// <br/> Shutdown 시작 뒤에는 같은 Runtime을 더 이상 조회하지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_초기화성공_Current공개와Shutdown해제()
        {
            var fixture = CreateRuntimeFixture();

            Assert.IsFalse(UIRuntime.TryCurrent(out _));

            fixture.Runtime.Initialize(fixture.Settings);

            Assert.AreSame(fixture.Runtime, UIRuntime.Current);
            Assert.IsTrue(UIRuntime.Current.IsInitialized);

            fixture.Runtime.Shutdown();

            Assert.IsFalse(UIRuntime.TryCurrent(out _));
            Assert.IsTrue(fixture.Runtime.IsReleased);
        }

    #endregion

    #region I-2: UITK-전용 호스트

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> UGUI Layer가 없는 Runtime은 EventSystem/InputModule 없이 초기화된다.
        /// <br/> UI Action Asset만 명시하면 된다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_UITKOnly_EventSystem없이초기화성공()
        {
            var fixture = CreateUITKOnlyRuntimeFixture();
            var unrelatedEventSystem = new GameObject
            (
                "Unrelated EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            ownedObjects.Add(unrelatedEventSystem);

            Assert.IsNull(fixture.Runtime.GetComponent<EventSystem>());
            Assert.IsNull(fixture.Runtime.GetComponent<InputSystemUIInputModule>());

            fixture.Runtime.Initialize(fixture.Settings);

            Assert.IsTrue(fixture.Runtime.IsInitialized);
            Assert.IsNotNull(fixture.Runtime.RootPresentation);
            Assert.IsNotNull(fixture.Runtime.SceneFader);

            fixture.Runtime.Shutdown();

            Assert.IsTrue(fixture.Runtime.IsReleased);
        }

    #endregion

    #region I-3: 초기화 구독자 실패

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> OnInitialized 구독자 실패가 열린 Screen을 먼저 반환한 뒤,
        /// <br/> OnReleasing을 호출하고 Root PresentationSession과 Runtime Core를
        /// <br/> 모두 정리하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_OnInitialized실패_Screen후OnReleasing과Core롤백()
        {
            var fixture = CreateRuntimeFixture();
            var source = new TestScreenSource();
            var releasingCount = 0;
            var sourceReleasedBeforeReleasing = false;

            fixture.Runtime.OnInitialized += runtime =>
            {
                Assert.AreSame(runtime, UIRuntime.Current);
                Assert.IsTrue(UIRuntime.Current.IsInitialized);
                runtime.Main.ScreenRegistry.Register
                (
                    new ScreenOptions
                    (
                        "Boot",
                        openDuration: 0.0f,
                        closeDuration: 0.0f
                    ),
                    PresentationTarget.Local("Test"),
                    source
                );
                var response = runtime.Main.Screens.Open("Boot");
                Assert.IsTrue(response.Accepted);
            };
            fixture.Runtime.OnInitialized += _ =>
            {
                throw new InvalidOperationException("injected initialized subscriber failure");
            };
            fixture.Runtime.OnReleasing += _ =>
            {
                releasingCount++;
                sourceReleasedBeforeReleasing = source.ReleaseCount == 1;
            };

            Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(1, releasingCount);
            Assert.IsTrue(sourceReleasedBeforeReleasing);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
            Assert.IsFalse(UIRuntime.TryCurrent(out _));
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 초기화 observer 하나가 실패해도 뒤 observer를 호출한 뒤 전체 초기화를 롤백한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_OnInitializedObserver실패_뒤Observer호출후Rollback()
        {
            var fixture = CreateRuntimeFixture();
            var observerCount = 0;
            fixture.Runtime.OnInitialized += _ =>
                throw new InvalidOperationException("injected initialized observer failure");
            fixture.Runtime.OnInitialized += _ => observerCount++;

            Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            Assert.AreEqual(1, observerCount);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
            Assert.IsFalse(UIRuntime.TryCurrent(out _));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> OnInitialized 중 명시적 Shutdown이 초기화 성공으로 반환되지 않고,
        /// <br/> 이미 끝난 소유 리소스를 후속 종료에서 다시 정리하지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_OnInitialized중Shutdown_초기화실패와Terminal종료()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.OnInitialized += runtime => runtime.Shutdown();

            Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
            Assert.IsFalse(UIRuntime.TryCurrent(out _));
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
        }

    #endregion

    #region I-4: 필수 페이드 구성 실패

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Fade Driver 누락이 초기화 시점에 관찰되고 Root
        /// <br/> PresentationSession까지 롤백되는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_FadeDriver누락_초기화실패와RootPresentation롤백()
        {
            var fixture = CreateRuntimeFixture();
            var invalidFadeProvider = new TestProvider(CreateInvalidFadeView);
            SetField(fixture.FadeSource, "viewProvider", invalidFadeProvider);

            Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            Assert.AreEqual(1, invalidFadeProvider.AcquireCount);
            Assert.AreEqual(1, invalidFadeProvider.ReleaseCount);
            Assert.IsFalse(invalidFadeProvider.LastAcquireWorldPositionStays);
            Assert.IsFalse(invalidFadeProvider.LastReleaseWorldPositionStays);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(0, fixture.FadeProvider.AcquireCount);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.IsNull(fixture.Runtime.RootPresentation);
        }

    #endregion

    #region R-1: 종료 구독자 실패

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> OnReleasing 실패가 표시·Presentation 정리를 막지 않고,
        /// <br/> 반복 Shutdown에서 구독자를 다시 호출하지 않는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_OnReleasing실패_Core정리완료()
        {
            var fixture = CreateRuntimeFixture();
            var releasingCount = 0;
            var servicesAvailableToSubscriber = false;
            fixture.Runtime.OnReleasing += _ =>
            {
                throw new InvalidOperationException("injected releasing subscriber failure");
            };
            fixture.Runtime.OnReleasing += runtime =>
            {
                releasingCount++;
                servicesAvailableToSubscriber =
                    runtime.Main != null &&
                    runtime.Main.Modals != null &&
                    runtime.RootPresentation != null;
            };
            fixture.Runtime.Initialize(fixture.Settings);

            Assert.Throws<AggregateException>(fixture.Runtime.Shutdown);

            Assert.AreEqual(1, releasingCount);
            Assert.IsTrue(servicesAvailableToSubscriber);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
            Assert.IsTrue(fixture.Runtime.IsReleased);

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.AreEqual(1, releasingCount);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 활성 Layer usage가 Root PresentationSession 종료를 상태 변경 전에 거부하고,
        /// <br/> usage 반환 뒤 후속 Shutdown이 남은 Session을 종료하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_활성LayerUsage_후속Shutdown에서RootPresentation종료()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var layerRegistry = fixture.Runtime.RootPresentation.LayerRegistry;
            Assert.IsTrue
            (
                layerRegistry.TryAcquireUsage
                (
                    "Test",
                    out _,
                    out var usage
                )
            );

            Assert.Throws<AggregateException>(fixture.Runtime.Shutdown);

            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.IsNotNull(fixture.Runtime.RootPresentation);
            Assert.AreEqual(1, fixture.PresentationRoot.childCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);

            usage.Dispose();

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.IsNull(fixture.Runtime.RootPresentation);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
            Assert.Throws<ObjectDisposedException>
            (
                () => layerRegistry.TryGet("Test", out _)
            );
        }

    #endregion

    #region S-1: 스크린 정리 실패와 종료 상태 종료

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Screen Source 획득 중 Runtime이 종료되면
        /// <br/> 뒤늦게 반환된 Instance를 한 번 반환하고,
        /// <br/> 종료된 Screen Stack에 Session을 공개하지 않는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Screen획득중Shutdown_늦은Instance반환하고Stack미공개()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var source = new TestScreenSource
            {
                Acquiring = fixture.Runtime.Shutdown,
            };
            fixture.Runtime.Main.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Interrupted",
                    openDuration: 0.0f,
                    closeDuration: 0.0f
                ),
                PresentationTarget.Local("Test"),
                source
            );
            var screens = fixture.Runtime.Main.Screens;

            var response = screens.Open("Interrupted");

            Assert.AreEqual(ScreenOpenKind.Rejected, response.Kind);
            Assert.AreEqual(0, screens.Count);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsTrue(fixture.Runtime.IsReleased);

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.AreEqual(1, source.ReleaseCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Screen 자식 정리가 실패해도 Scene 구독과 나머지 소유권을 한 번씩 정리하고,
        /// <br/> Runtime을 Terminal 상태로 확정하여 다음 Shutdown이 no-op인지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Screen정리실패_TerminalShutdown()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var source = new TestScreenSource();
            fixture.Runtime.Main.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Cleanup",
                    openDuration: 0.0f,
                    closeDuration: 0.0f
                ),
                PresentationTarget.Local("Test"),
                source
            );
            var response = fixture.Runtime.Main.Screens.Open("Cleanup");
            var child = new ThrowingHandle();
            response.Session.RegisterChild(child);

            Assert.Throws<AggregateException>(fixture.Runtime.Shutdown);

            Assert.IsFalse(fixture.Runtime.IsReleasing);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(ScreenState.Closed, response.Session.State);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(1, child.DisposeCount);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);

            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(ScreenState.Closed, response.Session.State);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(1, child.DisposeCount);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
        }

    #endregion

    #region A-1: 메인과 자식 컨텍스트 트리

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Runtime 초기화가 Main의 고정 Controller를 완성하고,
        /// <br/> 공개 Main Dispose는 상태 변경 전에 거부되는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Main구성_직접Dispose거부와소유권유지()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var main = fixture.Runtime.Main;

            Assert.IsNotNull(main);
            Assert.AreSame(fixture.Runtime.RootPresentation, main.Presentation);
            Assert.IsNotNull(main.ScreenRegistry);
            Assert.IsNotNull(main.Screens);
            Assert.IsNotNull(main.Modals);
            Assert.IsTrue(main.IsEffective);
            Assert.IsTrue(main.IsOnActivePath);

            Assert.Throws<InvalidOperationException>(main.Dispose);
            Assert.Throws<InvalidOperationException>(main.ScreenRegistry.Dispose);
            Assert.Throws<InvalidOperationException>(main.Modals.Dispose);

            Assert.IsFalse(main.IsDisposing);
            Assert.IsFalse(main.IsDisposed);
            Assert.AreSame(main, fixture.Runtime.Main);
            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.IsTrue(main.IsDisposed);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 소유 Root Presentation의 직접 Dispose를 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_RootPresentation_직접Dispose거부와Shutdown소유권유지()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var root = fixture.Runtime.RootPresentation;

            Assert.Throws<InvalidOperationException>(root.Dispose);
            Assert.Throws<InvalidOperationException>(fixture.Runtime.SceneFader.Dispose);

            Assert.IsFalse(root.IsDisposed);
            Assert.AreSame(root, fixture.Runtime.RootPresentation);
            Assert.AreSame(root, fixture.Runtime.Main.Presentation);

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);

            Assert.IsTrue(root.IsDisposed);
            Assert.IsNull(fixture.Runtime.RootPresentation);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Child가 같은 표시 공간에서도 독립 Screen Registry와 Focus 권한을 갖고,
        /// <br/> Parent 종료가 Grandchild까지 정리한 뒤 Main Focus를 복원하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_ChildContext_독립Registry와재귀종료Focus복원()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var main = fixture.Runtime.Main;
            var child = main.CreateChild();
            var grandchild = child.CreateChild();
            var mainRegistration = main.ScreenRegistry.Register
            (
                new ScreenOptions("Shared ID"),
                PresentationTarget.Local("Test"),
                new TestScreenSource()
            );
            var childRegistration = child.ScreenRegistry.Register
            (
                new ScreenOptions("Shared ID"),
                PresentationTarget.Local("Test"),
                new TestScreenSource()
            );

            Assert.AreSame(main.Presentation, child.Presentation);
            Assert.AreNotSame(main.ScreenRegistry, child.ScreenRegistry);
            Assert.IsFalse(mainRegistration.IsDisposed);
            Assert.IsFalse(childRegistration.IsDisposed);
            Assert.IsTrue(main.IsEffective);
            Assert.IsFalse(child.IsEffective);

            grandchild.SetBaseAuthority();

            Assert.IsFalse(main.IsEffective);
            Assert.IsTrue(main.IsOnActivePath);
            Assert.IsFalse(child.IsEffective);
            Assert.IsTrue(child.IsOnActivePath);
            Assert.IsTrue(grandchild.IsEffective);

            child.Dispose();

            Assert.IsTrue(child.IsDisposed);
            Assert.IsTrue(grandchild.IsDisposed);
            Assert.IsTrue(childRegistration.IsDisposed);
            Assert.IsFalse(mainRegistration.IsDisposed);
            Assert.IsTrue(main.IsEffective);
            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
        }

        [Test]
        public void TEST_UIContext_ChildPresentation_직접ParentSession관계만허용()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var main = fixture.Runtime.Main;
            Assert.IsTrue
            (
                fixture.Runtime.RootPresentation.LayerRegistry.TryGet
                (
                    "Test",
                    out var rootLayer
                )
            );
            var rootHostTransform =
                ((IPresentationLayerDriver<RectTransform>)rootLayer).Root;
            var childLayout = PresentationTestScope.CreateLayout
            (
                new[]
                {
                    new PresentationLayerDefinition
                    (
                        "Child",
                        "Child",
                        0,
                        PresentationBackend.UGUI
                    ),
                },
                Array.Empty<PresentationPlacementDefinition>()
            );
            ownedObjects.Add(childLayout);
            var childPresentation =
                fixture.Runtime.RootPresentation.CreateChild(childLayout, rootHostTransform);
            Assert.IsTrue
            (
                childPresentation.LayerRegistry.TryGet
                (
                    "Child",
                    out var childLayer
                )
            );
            var childHostTransform =
                ((IPresentationLayerDriver<RectTransform>)childLayer).Root;
            var grandchildPresentation =
                childPresentation.CreateChild(childLayout, childHostTransform);
            var childContext = main.CreateChild(childPresentation);

            Assert.Throws<InvalidOperationException>
            (
                () => main.CreateChild(grandchildPresentation)
            );

            var grandchildContext = childContext.CreateChild(grandchildPresentation);
            Assert.AreSame(grandchildPresentation, grandchildContext.Presentation);

            childContext.Dispose();
            grandchildPresentation.Dispose();
            childPresentation.Dispose();
            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Runtime Shutdown이 Main 아래 남은 Child Tree를 모두
        /// <br/> Terminal 상태로 종료하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Shutdown_Main과ChildTree전체종료()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var main = fixture.Runtime.Main;
            var child = main.CreateChild();
            var grandchild = child.CreateChild();

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);

            Assert.IsTrue(main.IsDisposed);
            Assert.IsTrue(child.IsDisposed);
            Assert.IsTrue(grandchild.IsDisposed);
            Assert.IsTrue(fixture.Runtime.IsReleased);
        }

    #endregion

    #region A-2: 기본와 오버라이드 컨텍스트 권한

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Override가 활성인 동안 Base를 A에서 B로 바꿔도 Override를 유지하고,
        /// <br/> 해제 뒤 최신 Base B와 살아 있는 Screen lifetime을 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_ContextOverride중Base교체_해제후최신Base복원()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var main = fixture.Runtime.Main;
            var first = main.CreateChild();
            var second = main.CreateChild();
            var overlay = main.CreateChild();
            first.ScreenRegistry.Register
            (
                new ScreenOptions("Shared ID"),
                PresentationTarget.Local("Test"),
                new TestScreenSource()
            );
            second.ScreenRegistry.Register
            (
                new ScreenOptions("Shared ID"),
                PresentationTarget.Local("Test"),
                new TestScreenSource()
            );
            first.SetBaseAuthority();

            Assert.IsTrue(first.Screens.Open("Shared ID").Accepted);
            Assert.IsTrue(second.Screens.Open("Shared ID").Accepted);
            Assert.IsTrue(first.IsEffective);
            Assert.AreEqual(1, first.Screens.Count);
            Assert.AreEqual(1, second.Screens.Count);

            var authorityOverride = overlay.PushAuthorityOverride();

            Assert.IsTrue(overlay.IsEffective);
            Assert.IsFalse(first.IsEffective);
            Assert.IsTrue(main.IsOnActivePath);
            Assert.IsTrue(overlay.IsOnActivePath);
            Assert.IsFalse(first.IsOnActivePath);

            second.SetBaseAuthority();

            Assert.IsTrue(overlay.IsEffective);
            Assert.IsFalse(second.IsEffective);
            Assert.AreEqual(1, first.Screens.Count);
            Assert.AreEqual(1, second.Screens.Count);

            authorityOverride.Dispose();

            Assert.IsFalse(overlay.IsEffective);
            Assert.IsTrue(second.IsEffective);
            Assert.IsTrue(second.IsOnActivePath);
            Assert.IsFalse(first.IsOnActivePath);
            Assert.AreEqual(1, first.Screens.Count);
            Assert.AreEqual(1, second.Screens.Count);
            Assert.AreSame(first.Presentation, second.Presentation);
            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Base Context authority 적용 실패는 이전 Base topology를 복원하고
        /// <br/> 같은 authority 전환의 재시도를 허용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_SetBaseAuthority_Focus실패_TopologyRollback후재시도()
        {
            var fixture = CreateRuntimeFixture();
            var focus = fixture.Runtime.gameObject.AddComponent<AuthorityFocusDriver>();
            fixture.Runtime.Initialize(fixture.Settings);
            var main = fixture.Runtime.Main;
            var child = main.CreateChild();
            focus.SelectFailureCount = 1;

            Assert.Throws<InvalidOperationException>(child.SetBaseAuthority);

            Assert.IsTrue(main.IsEffective);
            Assert.IsTrue(main.IsOnActivePath);
            Assert.IsFalse(child.IsEffective);
            Assert.IsFalse(child.IsOnActivePath);

            Assert.DoesNotThrow(child.SetBaseAuthority);
            Assert.IsFalse(main.IsEffective);
            Assert.IsTrue(main.IsOnActivePath);
            Assert.IsTrue(child.IsEffective);
            Assert.IsTrue(child.IsOnActivePath);

            child.Dispose();
            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Context Override 획득 실패는 Override topology를 제거하고
        /// <br/> 재시도 가능한 이전 Base authority를 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_PushAuthorityOverride_Focus실패_TopologyRollback후재시도()
        {
            var fixture = CreateRuntimeFixture();
            var focus = fixture.Runtime.gameObject.AddComponent<AuthorityFocusDriver>();
            fixture.Runtime.Initialize(fixture.Settings);
            var main = fixture.Runtime.Main;
            var overlay = main.CreateChild();
            focus.SelectFailureCount = 1;

            Assert.Throws<InvalidOperationException>
            (
                () => overlay.PushAuthorityOverride()
            );

            Assert.IsTrue(main.IsEffective);
            Assert.IsTrue(main.IsOnActivePath);
            Assert.IsFalse(overlay.IsEffective);
            Assert.IsFalse(overlay.IsOnActivePath);

            var authority = overlay.PushAuthorityOverride();

            Assert.IsTrue(overlay.IsEffective);
            Assert.IsTrue(overlay.IsOnActivePath);

            authority.Dispose();

            Assert.IsTrue(main.IsEffective);
            Assert.IsFalse(overlay.IsEffective);
            overlay.Dispose();
            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Override 해제 authority cleanup이 실패해도 논리 topology는 Base로 전환하고
        /// <br/> 다음 refresh에서 stale Context cleanup을 다시 시도한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Override해제_Cleanup실패_후속Refresh재시도()
        {
            var fixture = CreateRuntimeFixture();
            var focus = fixture.Runtime.gameObject.AddComponent<AuthorityFocusDriver>();
            fixture.Runtime.Initialize(fixture.Settings);
            var main = fixture.Runtime.Main;
            var overlay = main.CreateChild();
            var authority = overlay.PushAuthorityOverride();

            Assert.IsTrue(overlay.IsEffective);
            Assert.IsTrue(overlay.Screens.IsInputContributionEnabled);

            focus.CurrentFailureCount = 1;

            Assert.Throws<InvalidOperationException>(authority.Dispose);

            Assert.IsTrue(authority.IsDisposed);
            Assert.IsTrue(main.IsEffective);
            Assert.IsTrue(main.IsOnActivePath);
            Assert.IsFalse(overlay.IsEffective);
            Assert.IsFalse(overlay.IsOnActivePath);
            Assert.IsTrue(overlay.Screens.IsInputContributionEnabled);

            Assert.DoesNotThrow(fixture.Runtime.RefreshContextAuthority);

            Assert.IsFalse(overlay.Screens.IsInputContributionEnabled);

            overlay.Dispose();
            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
        }

    #endregion

    #region C-1: 싱글턴과 씬 구성 중복

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 기본 Slot을 이미 소유한 Runtime이 있으면 후속 초기화를 거부하고,
        /// <br/> 기존 Current와 그 Runtime의 사용 가능 상태를 보존하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_기본Slot중복_기존Current유지()
        {
            var currentFixture = CreateRuntimeFixture();
            currentFixture.Runtime.Initialize(currentFixture.Settings);
            var duplicateFixture = CreateRuntimeFixture();

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => duplicateFixture.Runtime.Initialize(duplicateFixture.Settings)
            );

            StringAssert.Contains("기본 Slot", exception.Message);
            Assert.AreSame(currentFixture.Runtime, UIRuntime.Current);
            Assert.IsTrue(currentFixture.Runtime.IsInitialized);
            Assert.IsFalse(duplicateFixture.Runtime.IsInitialized);
            Assert.IsTrue(duplicateFixture.Runtime.IsReleased);
            Assert.AreEqual(0, duplicateFixture.PresentationRoot.childCount);
            Assert.AreEqual(0, duplicateFixture.FadeProvider.AcquireCount);

            Assert.DoesNotThrow(currentFixture.Runtime.Shutdown);
            Assert.IsFalse(UIRuntime.TryCurrent(out _));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 유한하지 않은 기본 Fade 시간을 자원 획득 전에 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_NaNFade시간_초기화전거부()
        {
            var fixture = CreateRuntimeFixture();
            SetField(fixture.Settings, "defaultFadeDuration", float.NaN);

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            StringAssert.Contains("유한한 0 이상", exception.Message);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(0, fixture.FadeProvider.AcquireCount);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 정의되지 않은 Backend capability를 초기화 전에 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_정의되지않은BackendCapability_초기화전거부()
        {
            var fixture = CreateRuntimeFixture();
            SetField
            (
                fixture.Settings,
                "backendSupport",
                (PresentationBackendSupport)(1 << 6)
            );

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            StringAssert.Contains("정의되지 않은 Presentation Backend capability", exception.Message);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(0, fixture.FadeProvider.AcquireCount);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 정의되지 않은 Layer Backend를 초기화 전에 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_정의되지않은LayerBackend_초기화전거부()
        {
            var fixture = CreateRuntimeFixture();
            SetField
            (
                fixture.Settings.DefaultLayout.Layers[0],
                "backend",
                (PresentationBackend)99
            );

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            StringAssert.Contains("backend 값", exception.Message);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(0, fixture.FadeProvider.AcquireCount);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// SupportsBackend가 정의되지 않은 Backend를 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UISettings_SupportsBackend_정의되지않은Backend거부()
        {
            var fixture = CreateRuntimeFixture();

            Assert.Throws<ArgumentOutOfRangeException>
            (
                () => fixture.Settings.SupportsBackend((PresentationBackend)99)
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// EventSystem 필수 UI Action Reference가 비면 자원 획득 전에 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_InputModulePoint참조누락_초기화전거부()
        {
            var fixture = CreateRuntimeFixture();
            var inputModule = fixture.Runtime.GetComponent<InputSystemUIInputModule>();
            inputModule.point = null;

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            StringAssert.Contains("Point Action", exception.Message);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(0, fixture.FadeProvider.AcquireCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// App 수명 Host 밖의 Presentation Root 구성을 초기화 전에 거부하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Host외부PresentationRoot_초기화전거부()
        {
            var fixture = CreateRuntimeFixture();
            var externalRoot = new GameObject("External Presentation Root", typeof(RectTransform));
            ownedObjects.Add(externalRoot);
            SetField(fixture.Runtime, "presentationRoot", externalRoot.transform);

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            StringAssert.Contains("Runtime Host 내부", exception.Message);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 직렬화된 LayerHost를 Root Session composition에 전달한다.
        /// <br/> Shutdown 뒤 Scene-authored Layer의 외부 소유 상태를 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_직렬화LayerHost_초기화와Shutdown에서Borrow복원()
        {
            var fixture = CreateRuntimeFixture();
            var layerObject = new GameObject
            (
                "Scene Layer Host",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UGUIPresentationOutput),
                typeof(UGUIPresentationLayerHost)
            );
            layerObject.transform.SetParent(fixture.Runtime.transform, false);
            ownedObjects.Add(layerObject);
            var canvas = layerObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 41;
            var layerRoot = layerObject.GetComponent<RectTransform>();
            var authoredContainer = UGUIPresentationSurface.CreateRect
            (
                "Authored Container",
                layerRoot
            );
            var managedRoot = UGUIPresentationSurface.CreateRect
            (
                "Managed Root",
                authoredContainer
            );
            var layerHost = layerObject.GetComponent<UGUIPresentationLayerHost>();
            SetField(layerHost, "layerID", "Test");
            SetField(layerHost, "managedRoot", managedRoot);
            SetField
            (
                fixture.Runtime,
                "presentationLayerHosts",
                new PresentationLayerHost[]
                {
                    layerHost,
                }
            );
            layerObject.SetActive(false);

            fixture.Runtime.Initialize(fixture.Settings);

            Assert.IsTrue(layerObject.activeSelf);
            Assert.AreEqual(100, canvas.sortingOrder);
            Assert.AreSame(layerRoot, authoredContainer.parent);
            Assert.AreSame(authoredContainer, managedRoot.parent);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.IsTrue
            (
                fixture.Runtime.RootPresentation.LayerRegistry.TryGet
                (
                    "Test",
                    out var layerDriver
                )
            );
            Assert.AreSame
            (
                managedRoot,
                ((IPresentationLayerDriver<RectTransform>)layerDriver).Root
            );

            fixture.Runtime.Shutdown();

            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.IsFalse(layerObject.activeSelf);
            Assert.AreEqual(41, canvas.sortingOrder);
            Assert.AreSame(layerRoot, authoredContainer.parent);
            Assert.AreSame(authoredContainer, managedRoot.parent);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 한 Runtime Host에 Scene Fade Source가 둘이면 초기화 전에 명시적으로 거부한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_SceneFadeSource중복_초기화전거부()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.gameObject.AddComponent<UGUISceneFadeSource>();

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            StringAssert.Contains("Scene Fade Source가 정확히 하나", exception.Message);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(0, fixture.PresentationRoot.childCount);
            Assert.AreEqual(0, fixture.FadeProvider.AcquireCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Runtime 초기화 뒤 추가된 EventSystem을 명시적으로 거부하고,
        /// <br/> 중복 제거 뒤 기존 Runtime 구성이 그대로 유효한지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_후속SceneEventSystem중복_기존Runtime유지하고명시적거부()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var duplicateRoot = new GameObject("Duplicate EventSystem");
            ownedObjects.Add(duplicateRoot);
            duplicateRoot.AddComponent<EventSystem>();

            var exception = Assert.Throws<InvalidOperationException>
            (
                fixture.Runtime.ValidateSceneComposition
            );

            StringAssert.Contains("다른 EventSystem", exception.Message);
            Assert.IsTrue(fixture.Runtime.IsInitialized);

            UnityEngine.Object.DestroyImmediate(duplicateRoot);

            Assert.DoesNotThrow(fixture.Runtime.ValidateSceneComposition);
            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.IsTrue(fixture.Runtime.IsReleased);
        }

    #endregion

    }
}
