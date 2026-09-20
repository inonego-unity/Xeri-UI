/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UIRuntime.cs
수정일 : 2026-09-20

# 설명
UIRuntime의 혼합 Layer Profile, 롤백, Scene 중복 구성과 초기화·종료 실패 정리를 검증한다.

# 테스트 구성
 P: Profile 획득 실패 롤백
 I: Singleton 공개와 OnInitialized 실패 롤백
 R: Runtime 종료 실패와 Terminal 정리
 S: Screen 정리 실패와 Terminal Shutdown
 M: Main과 Child Context 소유권
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
        /// 조립된 Runtime과 핵심 Provider를 묶는다.
        /// </summary>
        // ============================================================
        private sealed class RuntimeFixture
        {
            public UIRuntime Runtime { get; set; }
            public UGUISceneFadeSource FadeSource { get; set; }
            public UISettingsAsset Settings { get; set; }
            public TestProvider LayerProvider { get; set; }
            public TestProvider FadeProvider { get; set; }
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
            var field = target.GetType().GetField
            (
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.IsNotNull(field, $"{target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation Layer Asset을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private PresentationLayerAsset CreateLayerAsset
        (
            string id,
            int order = 0
        )
        {
            var asset = ScriptableObject.CreateInstance<PresentationLayerAsset>();
            SetField(asset, "id", id);
            SetField(asset, "order", order);
            ownedObjects.Add(asset);
            return asset;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// private LayerEntry 목록을 채운 Profile Asset을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private UIProfileAsset CreateProfile
        (
            params (PresentationLayerAsset Asset, IGameObjectProvider Provider)[] entries
        )
        {
            var profile = ScriptableObject.CreateInstance<UIProfileAsset>();
            var entryType = typeof(UIProfileAsset).GetNestedType
            (
                "LayerEntry",
                BindingFlags.NonPublic
            );
            Assert.IsNotNull(entryType);
            var listType = typeof(List<>).MakeGenericType(entryType);
            var list = (IList)Activator.CreateInstance(listType);

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = Activator.CreateInstance(entryType, true);
                SetField(entry, "asset", entries[i].Asset);
                SetField(entry, "provider", entries[i].Provider);
                list.Add(entry);
            }

            SetField(profile, "layers", list);
            ownedObjects.Add(profile);
            return profile;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime Profile에 사용할 공유 UGUI Layer Root를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private GameObject CreateLayerRoot
        (
            Transform parent,
            string name
        )
        {
            var gameObject = new GameObject
            (
                name,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUILayerCanvas)
            );
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var driver = gameObject.GetComponent<UGUILayerCanvas>();
            SetField(driver, "root", gameObject.GetComponent<RectTransform>());
            SetField(driver, "canvas", gameObject.GetComponent<Canvas>());
            return gameObject;
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

            var layerRootObject = new GameObject("Layer Root", typeof(RectTransform));
            layerRootObject.transform.SetParent(host.transform, false);
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

            var layerProvider = new TestProvider
            (
                parent => CreateLayerRoot(parent, "Fade Layer")
            );
            var fadeProvider = new TestProvider(CreateFadeView);
            var profile = CreateProfile((CreateLayerAsset("Fade"), layerProvider));
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();
            SetField(settings, "defaultProfile", profile);
            SetField(settings, "sceneFadeLayerID", "Fade");
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
            ownedObjects.Add(settings);

            SetField(fadeSource, "viewProvider", fadeProvider);
            SetField(runtime, "layerRoot", layerRootObject.transform);
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
                LayerProvider = layerProvider,
                FadeProvider = fadeProvider,
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

    #region P-1: Profile 획득 실패

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Profile 중간 Provider 실패가 앞서 획득한 Layer 인스턴스와 Registry를 롤백하고,
        /// <br/> Runtime 추적 Handle을 남기지 않는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Profile획득실패_Layer와Provider전체롤백()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var firstProvider = new TestProvider
            (
                parent => CreateLayerRoot(parent, "First Layer")
            );
            var secondProvider = new TestProvider
            (
                _ => throw new InvalidOperationException("injected provider acquire failure")
            );
            var profile = CreateProfile
            (
                (CreateLayerAsset("First", 1), firstProvider),
                (CreateLayerAsset("Second", 2), secondProvider)
            );

            Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.AcquireProfile(profile)
            );

            Assert.AreEqual(1, firstProvider.AcquireCount);
            Assert.AreEqual(1, firstProvider.ReleaseCount);
            Assert.IsFalse(firstProvider.LastAcquireWorldPositionStays);
            Assert.IsFalse(firstProvider.LastReleaseWorldPositionStays);
            Assert.AreEqual(1, secondProvider.AcquireCount);
            Assert.IsFalse(fixture.Runtime.LayerRegistry.Contains("First"));
            Assert.IsTrue(fixture.Runtime.IsInitialized);
            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Profile Provider 획득 중 Runtime이 종료되면
        /// <br/> 뒤늦게 반환된 인스턴스를 한 번 반환하고,
        /// <br/> 종료된 Profile Handle에 소유권을 연결하지 않는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Profile획득중Shutdown_미확정Instance한번반환()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var provider = new TestProvider
            (
                parent =>
                {
                    var instance = CreateLayerRoot(parent, "Interrupted Layer");
                    fixture.Runtime.Shutdown();
                    return instance;
                }
            );
            var profile = CreateProfile
            (
                (CreateLayerAsset("Interrupted", 1), provider)
            );

            Assert.Throws<ObjectDisposedException>
            (
                () => fixture.Runtime.AcquireProfile(profile)
            );

            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(1, provider.AcquireCount);
            Assert.AreEqual(1, provider.ReleaseCount);
            Assert.IsFalse(provider.LastReleaseWorldPositionStays);

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.AreEqual(1, provider.ReleaseCount);
        }

    #endregion

    #region I-1: Singleton 공개 수명

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

    #region I-2: 초기화 구독자 실패

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> OnInitialized 구독자 실패가 열린 Screen을 먼저 반환한 뒤,
        /// <br/> OnReleasing을 호출하고 Profile Provider와 Runtime Core를
        /// <br/> 모두 정리하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
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
                        "Fade",
                        openDuration: 0.0f,
                        closeDuration: 0.0f
                    ),
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
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
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
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
        }

    #endregion

    #region I-3: 필수 Fade 구성 실패

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Fade Driver 누락이 초기화 시점에 관찰되고 기본 Profile까지 롤백되는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_FadeDriver누락_초기화실패와기본Profile롤백()
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
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(0, fixture.FadeProvider.AcquireCount);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.IsNull(fixture.Runtime.LayerRegistry);
        }

    #endregion

    #region R-1: 종료 구독자 실패

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> OnReleasing 실패가 표시·Profile 정리를 막지 않고,
        /// <br/> 반복 Shutdown에서 구독자와 Provider를 다시 호출하지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_OnReleasing실패_Core정리완료()
        {
            var fixture = CreateRuntimeFixture();
            var releasingCount = 0;
            var servicesAvailableToSubscriber = false;
            fixture.Runtime.OnReleasing += runtime =>
            {
                releasingCount++;
                servicesAvailableToSubscriber =
                    runtime.Main != null &&
                    runtime.Main.Modals != null &&
                    runtime.LayerRegistry != null;
            };
            fixture.Runtime.OnReleasing += _ =>
            {
                throw new InvalidOperationException("injected releasing subscriber failure");
            };
            fixture.Runtime.Initialize(fixture.Settings);

            Assert.Throws<AggregateException>(fixture.Runtime.Shutdown);

            Assert.AreEqual(1, releasingCount);
            Assert.IsTrue(servicesAvailableToSubscriber);
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
            Assert.IsTrue(fixture.Runtime.IsReleased);

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.AreEqual(1, releasingCount);
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Provider 반환 실패 뒤 Terminal Runtime이 같은 반환을 다시 시도하지 않는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Provider반환실패_후속Shutdown에서반복하지않음()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            fixture.LayerProvider.ReleaseFailuresRemaining = 1;

            Assert.Throws<AggregateException>(fixture.Runtime.Shutdown);

            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);

            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 활성 Layer 소비자로 Profile 종료가 상태 변경 전에 거부돼도 소유권을 보존하고,
        /// <br/> 소비자 반환 뒤 후속 Shutdown이 Profile Provider를 반환하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_활성Layer소비자_후속Shutdown에서Profile반환()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var layerRegistry = fixture.Runtime.LayerRegistry;
            Assert.IsTrue
            (
                layerRegistry.TryAcquireUsage
                (
                    "Fade",
                    out _,
                    out var usage
                )
            );

            Assert.Throws<AggregateException>(fixture.Runtime.Shutdown);

            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(0, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);

            usage.Dispose();

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
            Assert.Throws<ObjectDisposedException>
            (
                () => layerRegistry.TryGet("Fade", out _)
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 최신 Profile 반환 callback이 다른 Profile을 먼저 종료해도
        /// <br/> Runtime 종료가 끝까지 진행되고 각 Provider 소유권을
        /// <br/> 정확히 한 번 반환하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Profile반환재진입_모든Profile한번종료()
        {
            var fixture = CreateRuntimeFixture();
            fixture.Runtime.Initialize(fixture.Settings);
            var firstProvider = new TestProvider
            (
                parent => CreateLayerRoot(parent, "First Dynamic Layer")
            );
            var secondProvider = new TestProvider
            (
                parent => CreateLayerRoot(parent, "Second Dynamic Layer")
            );
            var firstProfile = CreateProfile
            (
                (CreateLayerAsset("First Dynamic", 10), firstProvider)
            );
            var secondProfile = CreateProfile
            (
                (CreateLayerAsset("Second Dynamic", 20), secondProvider)
            );
            var firstHandle = fixture.Runtime.AcquireProfile(firstProfile);
            var secondHandle = fixture.Runtime.AcquireProfile(secondProfile);
            secondProvider.Releasing = _ => firstHandle.Dispose();

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);

            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.IsFalse(fixture.Runtime.IsReleasing);
            Assert.IsTrue(firstHandle.IsDisposed);
            Assert.IsTrue(secondHandle.IsDisposed);
            Assert.AreEqual(1, firstProvider.ReleaseCount);
            Assert.AreEqual(1, secondProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
        }

    #endregion

    #region S-1: Screen 정리 실패와 Terminal Shutdown

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
                    "Fade",
                    openDuration: 0.0f,
                    closeDuration: 0.0f
                ),
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
                    "Fade",
                    openDuration: 0.0f,
                    closeDuration: 0.0f
                ),
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
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);

            Assert.DoesNotThrow(fixture.Runtime.Shutdown);

            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(ScreenState.Closed, response.Session.State);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(1, child.DisposeCount);
            Assert.AreEqual(1, fixture.LayerProvider.ReleaseCount);
            Assert.AreEqual(1, fixture.FadeProvider.ReleaseCount);
        }

    #endregion

    #region M-1: Main과 Child Context 소유권

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
            Assert.AreSame(fixture.Runtime.LayerRegistry, main.LayerRegistry);
            Assert.IsNotNull(main.ScreenRegistry);
            Assert.IsNotNull(main.Screens);
            Assert.IsNotNull(main.Modals);
            Assert.IsTrue(main.HasFocus);

            Assert.Throws<InvalidOperationException>(main.Dispose);

            Assert.IsFalse(main.IsDisposing);
            Assert.IsFalse(main.IsDisposed);
            Assert.AreSame(main, fixture.Runtime.Main);
            Assert.DoesNotThrow(fixture.Runtime.Shutdown);
            Assert.IsTrue(main.IsDisposed);
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
                new ScreenOptions("Shared ID", "Fade"),
                new TestScreenSource()
            );
            var childRegistration = child.ScreenRegistry.Register
            (
                new ScreenOptions("Shared ID", "Fade"),
                new TestScreenSource()
            );

            Assert.AreSame(main.LayerRegistry, child.LayerRegistry);
            Assert.AreNotSame(main.ScreenRegistry, child.ScreenRegistry);
            Assert.IsFalse(mainRegistration.IsDisposed);
            Assert.IsFalse(childRegistration.IsDisposed);
            Assert.IsTrue(main.HasFocus);
            Assert.IsFalse(child.HasFocus);

            grandchild.Focus();

            Assert.IsFalse(main.HasFocus);
            Assert.IsFalse(child.HasFocus);
            Assert.IsTrue(grandchild.HasFocus);

            child.Dispose();

            Assert.IsTrue(child.IsDisposed);
            Assert.IsTrue(grandchild.IsDisposed);
            Assert.IsTrue(childRegistration.IsDisposed);
            Assert.IsFalse(mainRegistration.IsDisposed);
            Assert.IsTrue(main.HasFocus);
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

    #region C-1: Singleton과 Scene 구성 중복

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
            Assert.AreEqual(0, duplicateFixture.LayerProvider.AcquireCount);
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
            Assert.AreEqual(0, fixture.LayerProvider.AcquireCount);
            Assert.AreEqual(0, fixture.FadeProvider.AcquireCount);
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
            Assert.AreEqual(0, fixture.LayerProvider.AcquireCount);
            Assert.AreEqual(0, fixture.FadeProvider.AcquireCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// App 수명 Host 밖의 Layer Root 구성을 초기화 전에 거부하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_Host외부LayerRoot_초기화전거부()
        {
            var fixture = CreateRuntimeFixture();
            var externalRoot = new GameObject("External Layer Root", typeof(RectTransform));
            ownedObjects.Add(externalRoot);
            SetField(fixture.Runtime, "layerRoot", externalRoot.transform);

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => fixture.Runtime.Initialize(fixture.Settings)
            );

            StringAssert.Contains("Runtime Host 내부", exception.Message);
            Assert.IsFalse(fixture.Runtime.IsInitialized);
            Assert.IsTrue(fixture.Runtime.IsReleased);
            Assert.AreEqual(0, fixture.LayerProvider.AcquireCount);
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
            Assert.AreEqual(0, fixture.LayerProvider.AcquireCount);
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
