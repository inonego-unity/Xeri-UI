/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UITKUI.cs
수정일 : 2026-09-24

# 설명
UITK Placement, Interaction, Native Output, Screen, Modal과 Fade의 공개 Runtime 경계를 검증한다.

# 테스트 구성
 P: backend 공통 Placement와 UITK Interaction
 L: UITK Native Output materialization
 D: Screen·Modal·Fade Driver
 C: Screen Close와 Visual Tree 반환
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;
    using inonego.Xeri.UI.TEST;

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

            public bool Validate(out string error)
            {
                error = Root == null ? "invalid" : "";
                return string.IsNullOrEmpty(error);
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

            public ScreenInputSession Acquire
            (
                ScreenOptions options,
                bool contributionEnabled = true
            )
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

        // ======================================================================
        /// <summary>
        /// Runtime Native Output GameObject의 획득·반환을 기록하는 Provider.
        /// </summary>
        // ======================================================================
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
        /// 테스트 Provider가 반환할 실제 UGUI Native Output을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private GameObject CreateUGUISurfaceObject(Transform parent)
        {
            var gameObject = new GameObject
            (
                "UGUI Runtime Surface",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUIPresentationOutput)
            );
            gameObject.SetActive(false);
            gameObject.transform.SetParent(parent, false);
            ownedObjects.Add(gameObject);
            gameObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
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

    #region L-1: UITK Native Output materialization

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> LayerOrder가 PanelRenderer가 소유한 runtime PanelSettings
        /// <br/> native order에 직접 materialize된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_Initialize_LayerOrderNativeMaterialize()
        {
            var gameObject = new GameObject("UITK Output");
            gameObject.SetActive(false);
            ownedObjects.Add(gameObject);
            var panelRenderer = gameObject.AddComponent<PanelRenderer>();
            var template = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(template);
            ownedObjects.Add(template);
            template.clearDepthStencil = false;
            var output = gameObject.AddComponent<UITKPresentationOutput>();

            output.Initialize(template, 23);

            Assert.AreNotSame(template, panelRenderer.panelSettings);
            Assert.AreSame(output.RuntimePanelSettings, panelRenderer.panelSettings);
            Assert.AreEqual(23.0f, output.RuntimePanelSettings.sortingOrder);
            Assert.IsNull(output.RuntimePanelSettings.targetTexture);
            Assert.IsFalse(output.RuntimePanelSettings.forceGammaRendering);
            Assert.IsFalse(output.RuntimePanelSettings.clearDepthStencil);

            output.ReleaseRuntimeSettings();
            Assert.IsNull(panelRenderer.panelSettings);
            Assert.IsNull(output.RuntimePanelSettings);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// ThemeStyleSheet이 없는 PanelSettings 구성을 Native Output 경계에서 거부한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_Validate_Theme없음거부()
        {
            var gameObject = new GameObject("UITK Missing Theme Surface");
            gameObject.SetActive(false);
            ownedObjects.Add(gameObject);
            var panelRenderer = gameObject.AddComponent<PanelRenderer>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            ownedObjects.Add(panelSettings);
            panelSettings.themeStyleSheet = null;
            panelRenderer.panelSettings = panelSettings;
            var output = gameObject.AddComponent<UITKPresentationOutput>();

            Assert.IsNull(panelSettings.themeStyleSheet);
            Assert.IsFalse(output.Validate(out var error));
            StringAssert.Contains("ThemeStyleSheet", error);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Top-level ScreenOverlay output은 template의
        /// <br/> TargetTexture를 runtime copy에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_Initialize_TargetTextureTemplate정규화()
        {
            var gameObject = new GameObject("UITK Render Texture Template");
            gameObject.SetActive(false);
            ownedObjects.Add(gameObject);
            gameObject.AddComponent<PanelRenderer>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            ownedObjects.Add(panelSettings);
            var targetTexture = new RenderTexture(16, 16, 0);
            ownedObjects.Add(targetTexture);
            panelSettings.targetTexture = targetTexture;
            var output = gameObject.AddComponent<UITKPresentationOutput>();

            output.Initialize(panelSettings, 23);

            Assert.AreSame(targetTexture, panelSettings.targetTexture);
            Assert.IsNull(output.RuntimePanelSettings.targetTexture);
            Assert.AreEqual(23.0f, output.RuntimePanelSettings.sortingOrder);

            output.ReleaseRuntimeSettings();
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
            var layerObject = new GameObject("UGUI Layer", typeof(RectTransform));
            ownedObjects.Add(layerObject);
            var layer = new UGUIPresentationLayer(layerObject.GetComponent<RectTransform>());

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
        /// UITK Screen Close가 Session placement 경로를 거쳐 View와 Layer usage를 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKScreen_Close_SessionPlacementTree와Usage반환()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            ownedObjects.Add(panelSettings);
            var parentRootObject = new GameObject("Parent Presentation");
            ownedObjects.Add(parentRootObject);
            var parentLayer = PresentationTestScope.CreateLayer
            (
                "Parent",
                0,
                PresentationBackend.UITK
            );
            var parentPlan = new PresentationPlan
            (
                new List<PresentationPlanLayer>
                {
                    parentLayer,
                },
                new List<PresentationPlanPlacement>
                {
                    new PresentationPlanPlacement("Parent", parentLayer, 0),
                }
            );
            var parentPresentation = PresentationSession.CreateTopLevel
            (
                parentPlan,
                parentRootObject.transform,
                panelSettings,
                null
            );
            Assert.IsTrue
            (
                parentPresentation.TryAcquirePlacement
                (
                    "Parent",
                    out var parentPlacement,
                    out var parentUsage
                )
            );
            parentUsage.Dispose();
            var parentRoot =
                ((IPresentationLayerDriver<VisualElement>)parentPlacement).Root;
            var layout = ScriptableObject.CreateInstance<PresentationLayout>();
            ownedObjects.Add(layout);
            SetField
            (
                layout,
                "layers",
                new List<PresentationLayerDefinition>
                {
                    new PresentationLayerDefinition
                    (
                        "Screen",
                        "Screen",
                        0,
                        PresentationBackend.UITK
                    ),
                }
            );
            SetField
            (
                layout,
                "placements",
                new List<PresentationPlacementDefinition>
                {
                    new PresentationPlacementDefinition("Menu", "Screen", 0),
                }
            );
            var presentation = parentPresentation.CreateChild
            (
                layout,
                parentRoot
            );
            Assert.IsTrue
            (
                presentation.TryAcquirePlacement
                (
                    "Menu",
                    out var placement,
                    out var placementUsage
                )
            );
            placementUsage.Dispose();
            var placementRoot = ((IPresentationLayerDriver<VisualElement>)placement).Root;
            var screenRegistry = new ScreenRegistry();
            var source = new TestScreenSource();
            var registration = screenRegistry.Register
            (
                new ScreenOptions("Menu"),
                source
            );
            var transitioner = new ImmediateTransitioner();
            var input = new TestInputDriver();
            var controller = new ScreenController
            (
                screenRegistry,
                presentation,
                transitioner,
                new FocusController(new TestFocusDriver()),
                input
            );
            controller.Activate();

            var response = controller.Open("Menu");
            Assert.IsTrue(response.Accepted);
            Assert.IsTrue(presentation.LayerRegistry.HasConsumers);
            Assert.AreSame(placementRoot, source.ScreenRoot.parent);

            Assert.IsTrue(response.Session.Close());

            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsNull(source.ScreenRoot.parent);
            Assert.IsFalse(presentation.LayerRegistry.HasConsumers);

            controller.Clear();
            input.Dispose();
            transitioner.Dispose();
            registration.Dispose();
            screenRegistry.Dispose();
            presentation.Dispose();
            parentPresentation.Dispose();
        }

    #endregion

    }
}

namespace inonego.Xeri.UI.TEST
{
    // ======================================================================
    /// <summary>
    /// transient PanelSettings의 runtime theme을 보정하는 테스트 헬퍼.
    /// </summary>
    // ======================================================================
    internal static class UITKTestPanelSettings
    {

    #region 필드

        private const string DEFAULT_THEME_GETTER_FIELD_NAME = "GetOrCreateDefaultTheme";

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 임시 PanelSettings에 Unity 기본 runtime theme을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static void ApplyDefaultRuntimeTheme(PanelSettings target)
        {
            if (target == null)
            {
                return;
            }

            var field = typeof(PanelSettings).GetField
            (
                DEFAULT_THEME_GETTER_FIELD_NAME,
                BindingFlags.NonPublic | BindingFlags.Static
            );

            if (field == null)
            {
                return;
            }

            var getter = field.GetValue(null) as Func<ThemeStyleSheet>;
            var theme = getter?.Invoke();

            if (theme != null)
            {
                target.themeStyleSheet = theme;
            }
        }

    #endregion

    }
}
