/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UITKUI.cs
수정일 : 2026-09-20

# 설명
UITK Placement, Interaction, Layer, Screen, Modal, Fade와 혼합 Profile의 공개 Runtime 경로를 검증한다.

# 테스트 구성
 P: backend 공통 Placement와 UITK Interaction
 L: PanelRenderer Layer와 Order
 D: Screen·Modal·Fade Driver
 C: Screen Close와 Visual Tree 반환
 R: Runtime 혼합 Profile과 종료
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// UI Toolkit UI의 실제 VisualElement 경계 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_UITKUI
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// VisualElement Root를 제공하는 테스트 Layer Driver.
        /// </summary>
        // ============================================================
        private sealed class TestLayerDriver : IPresentationLayerDriver<VisualElement>
        {
            public VisualElement Root { get; }

            public TestLayerDriver(VisualElement root) : base()
            {
                Root = root;
            }

            public bool Validate(PresentationLayerAsset asset, out string error)
            {
                error = asset == null || Root == null ? "invalid" : "";
                return string.IsNullOrEmpty(error);
            }

            public void SetOrder(int order)
            {
                // NONE
            }

            public void SetActive(bool active)
            {
                Root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ======================================================================
        /// <summary>
        /// Content Root 접근 중 동기 callback을 발생시키는 테스트 Layer Root.
        /// </summary>
        // ======================================================================
        private sealed class CallbackLayerRoot : VisualElement
        {
            public Action AccessingContent { get; set; }

            public override VisualElement contentContainer
            {
                get
                {
                    var callback = AccessingContent;
                    AccessingContent = null;
                    callback?.Invoke();
                    return base.contentContainer;
                }
            }
        }

        // ============================================================
        /// <summary>
        /// Visual Tree Screen을 생성하고 제거하는 테스트 Source.
        /// </summary>
        // ============================================================
        private sealed class TestScreenSource : IScreenSource
        {
            public VisualElement ScreenRoot { get; private set; }
            public int ReleaseCount { get; private set; }

            public ScreenInstance Acquire(ScreenViewScope scope)
            {
                if (!(scope.Layer is IPresentationLayerDriver<VisualElement> layer))
                {
                    throw new InvalidOperationException("VisualElement Layer가 필요합니다.");
                }

                ScreenRoot = new VisualElement
                {
                    name = "RuntimeScreen",
                };
                layer.Root.Add(ScreenRoot);
                return new ScreenInstance(new UITKScreenDriver(ScreenRoot));
            }

            public void Release(ScreenInstance instance)
            {
                ReleaseCount++;
                ScreenRoot.RemoveFromHierarchy();
            }
        }

        // ============================================================
        /// <summary>
        /// 즉시 완료되는 Presentation Transitioner.
        /// </summary>
        // ============================================================
        private sealed class ImmediateTransitioner : IPresentationTransitioner
        {
            public PresentationTransitionHandle Play
            (
                PresentationTransitionParams parameters,
                Action onCompleted,
                Action<Exception> onFailed
            )
            {
                parameters.Target.Set(parameters.EndValue);
                var handle = new PresentationTransitionHandle(null);
                handle.Complete();
                onCompleted?.Invoke();
                return handle;
            }

            public void Dispose()
            {
                // NONE
            }
        }

        // ============================================================
        /// <summary>
        /// Screen Focus 계약에 필요한 최소 메모리 Driver.
        /// </summary>
        // ============================================================
        private sealed class TestFocusDriver : IFocusDriver
        {
            public object Current { get; private set; }

            public bool IsValid(object target)
            {
                return target != null;
            }

            public void Select(object target)
            {
                Current = target;
            }

            public object FindFallback()
            {
                return null;
            }
        }

        // ============================================================
        /// <summary>
        /// 즉시 반환되는 Screen Input Session Driver.
        /// </summary>
        // ============================================================
        private sealed class TestInputDriver : IScreenInputDriver
        {
            private readonly List<ScreenInputSession> sessions =
                new List<ScreenInputSession>();

            public ScreenInputSession Acquire(ScreenOptions options)
            {
                var session = new ScreenInputSession(options, Release);
                sessions.Add(session);
                return session;
            }

            public void BeginBatch()
            {
                // NONE
            }

            public void EndBatch()
            {
                // NONE
            }

            public void ForceReleaseAll()
            {
                for (var i = sessions.Count - 1; i >= 0; i--)
                {
                    sessions[i].MarkReleased();
                }

                sessions.Clear();
            }

            public void Dispose()
            {
                ForceReleaseAll();
            }

            private void Release
            (
                ScreenInputSession session,
                bool waitForInputRelease,
                bool retainCursorWhileAwaitingRelease
            )
            {
                sessions.Remove(session);
                session.MarkReleased();
            }
        }

        // ============================================================
        /// <summary>
        /// Runtime Profile Layer의 획득·반환을 기록하는 Provider.
        /// </summary>
        // ============================================================
        private sealed class TestProvider : IGameObjectProvider
        {
            public Transform Parent { get; set; }
            public int AcquireCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public bool LastReleasedActiveSelf { get; private set; }

            private readonly Func<Transform, GameObject> acquire = null;

            public TestProvider(Func<Transform, GameObject> acquire) : base()
            {
                this.acquire = acquire ?? throw new ArgumentNullException(nameof(acquire));
            }

            public GameObject Acquire(bool worldPositionStays = true)
            {
                AcquireCount++;
                return acquire(Parent);
            }

            public Awaitable<GameObject> AcquireAsync(bool worldPositionStays = true)
            {
                throw new NotSupportedException();
            }

            public void Release
            (
                GameObject gameObject,
                bool worldPositionStays = true
            )
            {
                ReleaseCount++;
                LastReleasedActiveSelf = gameObject.activeSelf;
            }
        }

        private readonly List<UnityEngine.Object> ownedObjects =
            new List<UnityEngine.Object>();

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 UXML Asset을 Resources에서 읽는다.
        /// </summary>
        // ------------------------------------------------------------
        private static VisualTreeAsset LoadViewAsset()
        {
            var asset = Resources.Load<VisualTreeAsset>
            (
                "Xeri/UI/TEST_UITKUI"
            );
            Assert.IsNotNull(asset);
            return asset;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 Layer Asset을 생성한다.
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
        /// 테스트 Layer Entry를 가진 Profile을 생성한다.
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Profile Provider가 반환할 실제 UITK Layer GameObject를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private GameObject CreateLayerObject(Transform parent)
        {
            var gameObject = new GameObject("UITK Runtime Layer");
            gameObject.SetActive(false);
            gameObject.transform.SetParent(parent, false);
            ownedObjects.Add(gameObject);
            var panelRenderer = gameObject.AddComponent<PanelRenderer>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            ownedObjects.Add(panelSettings);
            panelRenderer.panelSettings = panelSettings;
            gameObject.AddComponent<UITKLayerPanel>();
            return gameObject;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Profile Provider가 반환할 실제 UGUI Layer GameObject를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private GameObject CreateUGUILayerObject(Transform parent)
        {
            var gameObject = new GameObject
            (
                "UGUI Runtime Layer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUILayerCanvas)
            );
            gameObject.SetActive(false);
            gameObject.transform.SetParent(parent, false);
            ownedObjects.Add(gameObject);
            gameObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var driver = gameObject.GetComponent<UGUILayerCanvas>();
            SetField(driver, "root", gameObject.GetComponent<RectTransform>());
            SetField(driver, "canvas", gameObject.GetComponent<Canvas>());
            return gameObject;
        }

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

    #endregion

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트에서 생성한 Unity Object를 역순 제거한다.
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
        }

    #endregion

    #region P-1: Placement와 Interaction

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 같은 Top 정렬이 UGUI Y-up과 UITK Y-down에서 시각적으로 같은 방향을 가리키고,
        /// <br/> 요소 Pivot을 반영한 clamp가 전체 Rect를 Bounds 안에 유지하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_PlacementSolver_좌표방향과Pivot_동일정렬과Bounds유지()
        {
            var solver = new PlacementSolver();
            var bounds = new Rect(0.0f, 0.0f, 100.0f, 100.0f);
            var yUp = solver.Place
            (
                bounds,
                new Vector2(50.0f, 50.0f),
                new Vector2(20.0f, 10.0f),
                new PlacementOptions
                (
                    PlacementAlignment.Top,
                    Vector2.zero,
                    Vector2.zero,
                    clampToBounds: false,
                    coordinateSystem: PlacementCoordinateSystem.YUp
                )
            );
            var yDown = solver.Place
            (
                bounds,
                new Vector2(50.0f, 50.0f),
                new Vector2(20.0f, 10.0f),
                new PlacementOptions
                (
                    PlacementAlignment.Top,
                    Vector2.zero,
                    Vector2.zero,
                    clampToBounds: false,
                    coordinateSystem: PlacementCoordinateSystem.YDown
                )
            );
            var clamped = solver.Place
            (
                bounds,
                new Vector2(95.0f, 95.0f),
                new Vector2(20.0f, 10.0f),
                Vector2.zero,
                new PlacementOptions
                (
                    PlacementAlignment.Center,
                    Vector2.zero,
                    Vector2.zero,
                    coordinateSystem: PlacementCoordinateSystem.YDown
                )
            );

            Assert.AreEqual(55.0f, yUp.LocalPosition.y);
            Assert.AreEqual(45.0f, yDown.LocalPosition.y);
            Assert.AreEqual(new Vector2(80.0f, 90.0f), clamped.LocalPosition);
            Assert.IsTrue(clamped.WasClamped);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 중첩 UITK Blocker의 마지막 Lease 해제만 표시와 Picking을 종료하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKInteractionBlocker_중첩점유_마지막해제만비활성()
        {
            var element = new VisualElement();
            IInteractionBlocker blocker = new UITKInteractionBlocker(element);
            var first = blocker.Acquire();
            var second = blocker.Acquire();
            first.Dispose();

            Assert.AreEqual(DisplayStyle.Flex, element.style.display.value);
            Assert.AreEqual(PickingMode.Position, element.pickingMode);

            second.Dispose();

            Assert.AreEqual(DisplayStyle.None, element.style.display.value);
            Assert.AreEqual(PickingMode.Ignore, element.pickingMode);
        }

    #endregion

    #region L-1: PanelRenderer Layer

        // ----------------------------------------------------------------------
        /// <summary>
        /// Layer 등록이 PanelRenderer Root와 공통 Screen Overlay Order를 제공한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKLayerPanel_Register_PanelRendererOrder와Baseline적용()
        {
            var gameObject = new GameObject("UITK Layer");
            gameObject.SetActive(false);
            ownedObjects.Add(gameObject);
            var panelRenderer = gameObject.AddComponent<PanelRenderer>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            ownedObjects.Add(panelSettings);
            panelSettings.clearDepthStencil = false;
            panelRenderer.panelSettings = panelSettings;
            var driver = gameObject.AddComponent<UITKLayerPanel>();
            var registry = new PresentationLayerRegistry();

            var handle = registry.Register
            (
                CreateLayerAsset("Screen", 23),
                driver
            );

            Assert.AreSame(panelSettings, panelRenderer.panelSettings);
            Assert.AreEqual(0, panelSettings.sortingOrder);
            Assert.AreEqual(23, panelRenderer.sortingOrder);
            Assert.IsNotNull(driver.Root);
            Assert.IsTrue(driver.Root.ClassListContains("xeri-ui"));
            var baseline = Resources.Load<StyleSheet>
            (
                "Xeri/UI/Core/UIRuntimeBaseline"
            );
            Assert.IsNotNull(baseline);
            Assert.IsTrue(driver.Root.styleSheets.Contains(baseline));
            Assert.IsNull(panelSettings.targetTexture);
            Assert.IsFalse(panelSettings.forceGammaRendering);
            Assert.IsFalse(panelSettings.clearDepthStencil);

            handle.Dispose();
            registry.Dispose();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 공통 Screen Overlay Order와 비교할 수 없는 Target Texture Panel 등록을 거부한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKLayerPanel_Register_TargetTexturePanel거부()
        {
            var gameObject = new GameObject("UITK Render Texture Layer");
            ownedObjects.Add(gameObject);
            var panelRenderer = gameObject.AddComponent<PanelRenderer>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            ownedObjects.Add(panelSettings);
            var targetTexture = new RenderTexture(16, 16, 0);
            ownedObjects.Add(targetTexture);
            panelSettings.targetTexture = targetTexture;
            panelRenderer.panelSettings = panelSettings;
            var driver = gameObject.AddComponent<UITKLayerPanel>();
            var registry = new PresentationLayerRegistry();

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => registry.Register
                (
                    CreateLayerAsset("UITK Render Texture", 23),
                    driver
                )
            );

            StringAssert.Contains("Target Texture", exception.Message);
            registry.Dispose();
        }

    #endregion

    #region D-1: Screen·Modal·Fade Driver

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Screen Driver의 Presentation State가 초기값을 읽고 변경값을 UITK backend에 적용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKScreenDriver_PresentationState_초기값과변경적용()
        {
            var parent = new VisualElement();
            var screen = new VisualElement();
            screen.style.display = DisplayStyle.None;
            screen.style.opacity = 0.25f;
            parent.Add(screen);
            var driver = new UITKScreenDriver(screen);

            Assert.IsFalse(driver.Visibility.Base);
            Assert.AreEqual(0.25f, driver.Alpha.Base);

            driver.Visibility.Set(true);
            driver.Alpha.Set(0.75f);

            Assert.IsTrue(driver.Visibility.Base);
            Assert.AreEqual(0.75f, driver.Alpha.Base);
            Assert.AreEqual(DisplayStyle.Flex, screen.style.display.value);
            Assert.AreEqual(0.75f, screen.style.opacity.value);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Screen Driver와 Modal interaction backend가 각자 표시·상호작용 책임만 적용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKDrivers_표시Opacity와ModalInteraction분리적용()
        {
            var parent = new VisualElement();
            var screen = new VisualElement();
            parent.Add(screen);
            var screenDriver = new UITKScreenDriver(screen);
            var modalDriver = new UITKModalInteractionDriver(screen);

            screenDriver.Visibility.Set(false);
            screenDriver.SetInteractable(false);
            screenDriver.Alpha.Set(0.25f);
            modalDriver.SetTop(true);

            Assert.AreEqual(DisplayStyle.None, screen.style.display.value);
            Assert.AreEqual(0.25f, screen.style.opacity.value);
            Assert.IsTrue(screen.enabledSelf);
            Assert.AreEqual(PickingMode.Position, screen.pickingMode);

            modalDriver.SetTop(false);

            Assert.IsFalse(screen.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, screen.pickingMode);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Fade Source 반환과 종료가 생성한 Visual Tree를 남기지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKSceneFadeSource_AcquireRelease와Dispose_Tree제거()
        {
            var layerRoot = new VisualElement();
            var layer = new TestLayerDriver(layerRoot);
            var host = new GameObject("UITK Scene Fade Source");
            ownedObjects.Add(host);
            var source = host.AddComponent<UITKSceneFadeSource>();
            SetField(source, "viewAsset", LoadViewAsset());
            SetField(source, "rootName", "SceneFade");
            source.Initialize();
            var baselineCount = layerRoot.childCount;

            var first = source.Acquire(layer);
            var second = source.Acquire(layer);

            Assert.AreEqual(baselineCount + 2, layerRoot.childCount);
            Assert.IsTrue(first.Alpha.IsValid);
            Assert.IsTrue(second.Alpha.IsValid);

            source.Release(first);
            Assert.AreEqual(baselineCount + 1, layerRoot.childCount);
            Assert.IsFalse(first.Alpha.IsValid);

            source.Dispose();
            Assert.AreEqual(baselineCount, layerRoot.childCount);
            Assert.IsFalse(second.Alpha.IsValid);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Layer Add callback이 Source를 종료하면 뒤늦은 View를 공개하지 않고,
        /// <br/> 이번 획득이 추가한 Container를 즉시 제거한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKSceneFadeSource_Acquire중Dispose_뒤늦은View미공개()
        {
            var layerRoot = new CallbackLayerRoot();
            var layer = new TestLayerDriver(layerRoot);
            var sourceObject = new GameObject("UITK Scene Fade Source");
            ownedObjects.Add(sourceObject);
            var source = sourceObject.AddComponent<UITKSceneFadeSource>();
            SetField(source, "viewAsset", LoadViewAsset());
            SetField(source, "rootName", "SceneFade");
            source.Initialize();
            var baselineCount = layerRoot.childCount;
            layerRoot.AccessingContent = source.Dispose;

            Assert.Throws<ObjectDisposedException>
            (
                () => source.Acquire(layer)
            );

            Assert.AreEqual(baselineCount, layerRoot.childCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// UITK Fade Source가 UGUI Layer에서 View를 만들기 전에 명시적으로 실패한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKSceneFadeSource_UGUILayer_View생성전거부()
        {
            var sourceObject = new GameObject("UITK Scene Fade Source");
            ownedObjects.Add(sourceObject);
            var source = sourceObject.AddComponent<UITKSceneFadeSource>();
            SetField(source, "viewAsset", LoadViewAsset());
            SetField(source, "rootName", "SceneFade");
            source.Initialize();
            var layerObject = CreateUGUILayerObject(null);
            var layer = layerObject.GetComponent<UGUILayerCanvas>();

            Assert.Throws<InvalidOperationException>
            (
                () => source.Acquire(layer)
            );

            Assert.AreEqual(0, layer.Root.childCount);
            source.Dispose();
        }

    #endregion

    #region C-1: Screen Close

        // --------------------------------------------------------------------------------
        /// <summary>
        /// UITK Screen Close가 기존 Controller 경로를 거쳐 View와 Layer Usage를 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKScreen_Close_Controller경로로Tree와Usage반환()
        {
            var layerRoot = new VisualElement();
            var layerRegistry = new PresentationLayerRegistry();
            var layerHandle = layerRegistry.Register
            (
                CreateLayerAsset("Screen"),
                new TestLayerDriver(layerRoot)
            );
            var screenRegistry = new ScreenRegistry(layerRegistry);
            var source = new TestScreenSource();
            var registration = screenRegistry.Register
            (
                new ScreenOptions("Menu", "Screen"),
                source
            );
            var transitioner = new ImmediateTransitioner();
            var input = new TestInputDriver();
            var controller = new ScreenController
            (
                screenRegistry,
                layerRegistry,
                transitioner,
                new FocusController(new TestFocusDriver()),
                input
            );
            controller.Activate();

            var response = controller.Open("Menu");
            Assert.IsTrue(response.Accepted);
            Assert.IsTrue(layerHandle.HasConsumers);
            Assert.AreSame(layerRoot, source.ScreenRoot.parent);

            Assert.IsTrue(response.Session.Close());

            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsNull(source.ScreenRoot.parent);
            Assert.IsFalse(layerHandle.HasConsumers);

            controller.Clear();
            input.Dispose();
            transitioner.Dispose();
            registration.Dispose();
            screenRegistry.Dispose();
            layerHandle.Dispose();
            layerRegistry.Dispose();
        }

    #endregion

    #region R-1: Runtime 혼합 Profile

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 공통 Runtime이 UGUI·UITK Layer가 섞인 Profile과
        /// <br/> UITK Fade Source를 조립하고 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIRuntime_혼합Profile_두Layer등록과Shutdown전체반환()
        {
            var host = new GameObject("Mixed Runtime Host");
            host.SetActive(false);
            ownedObjects.Add(host);
            var eventSystem = host.AddComponent<EventSystem>();
            var inputModule = host.AddComponent<InputSystemUIInputModule>();
            var inputDriver = host.AddComponent<InputSystemScreenInputDriver>();
            var uguiFocus = host.AddComponent<UGUIFocusDriver>();
            var uitkFocus = host.AddComponent<UITKFocusDriver>();
            var focus = host.AddComponent<UIFocusDriver>();
            var fadeSource = host.AddComponent<UITKSceneFadeSource>();
            var runtime = host.AddComponent<UIRuntime>();
            var layerRoot = new GameObject("Layer Root");
            layerRoot.transform.SetParent(host.transform, false);
            ownedObjects.Add(layerRoot);
            SetField(uguiFocus, "eventSystem", eventSystem);
            SetField(fadeSource, "viewAsset", LoadViewAsset());
            SetField(fadeSource, "rootName", "SceneFade");

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

            var uitkProvider = new TestProvider(CreateLayerObject);
            var uguiProvider = new TestProvider(CreateUGUILayerObject);
            var profile = CreateProfile
            (
                (CreateLayerAsset("Fade"), uitkProvider),
                (CreateLayerAsset("UGUI", 10), uguiProvider)
            );
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

            SetField(runtime, "layerRoot", layerRoot.transform);
            SetField(runtime, "focusDriver", focus);
            SetField(runtime, "sceneFadeSource", fadeSource);
            SetField(runtime, "eventSystem", eventSystem);
            SetField(runtime, "inputModule", inputModule);
            SetField(runtime, "inputDriver", inputDriver);
            host.SetActive(true);

            runtime.Initialize(settings);

            Assert.IsTrue(runtime.IsInitialized);
            Assert.IsTrue(runtime.LayerRegistry.Contains("Fade"));
            Assert.IsTrue(runtime.LayerRegistry.Contains("UGUI"));
            Assert.AreEqual(1, uitkProvider.AcquireCount);
            Assert.AreEqual(1, uguiProvider.AcquireCount);

            runtime.Shutdown();

            Assert.IsTrue(runtime.IsReleased);
            Assert.AreEqual(1, uitkProvider.ReleaseCount);
            Assert.AreEqual(1, uguiProvider.ReleaseCount);
            Assert.IsFalse(uitkProvider.LastReleasedActiveSelf);
            Assert.IsFalse(uguiProvider.LastReleasedActiveSelf);
        }

    #endregion

    }
}
