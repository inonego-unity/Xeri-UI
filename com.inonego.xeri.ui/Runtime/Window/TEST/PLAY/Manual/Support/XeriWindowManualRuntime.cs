/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowManualRuntime.cs
수정일 : 2026-09-24

# 설명
Simple/Application Window 수동 테스트가 공유하는 Runtime, Presentation, Context와 Workspace를 구성한다.

# 테스트 구성
 R: 공용 Runtime과 Workspace 수명
 A: Application Window용 PresentationLayout 생성
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

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.TEST;
using inonego.Xeri.UI.Tray;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// Window 수동 테스트 공용 Runtime 환경.
    /// </summary>
    // ============================================================
    internal sealed class XeriWindowManualRuntime : IDisposable
    {

    #region 상수

        private const string MANUAL_TEST_USS_PATH = "XeriUI/TEST/Window/XeriWindowManualTest";

    #endregion

    #region 필드

        public XeriWindowManualTestHUD HUD { get; private set; }
        public VisualElement RootElement { get; private set; }
        public XeriWindowWorkspace Workspace => workspace;
        private XeriWindowWorkspace workspace = null;
        public XeriWindowManualFocusDriver FocusDriver => focusDriver;
        private XeriWindowManualFocusDriver focusDriver = null;

        private GameObject cameraGO = null;
        private GameObject runtimeGO = null;
        private PanelSettings panelSettings = null;
        private UIRuntime runtime = null;
        private GameObject presentationRootGO = null;
        private PresentationSession rootPresentation = null;
        private PresentationSession workspacePresentation = null;
        private PresentationLayout workspaceLayout = null;
        private DOTweenPresentationTransitioner screenTransitioner = null;
        private UIFocusDriver focusController = null;
        private InputSystemScreenInputDriver inputDriver = null;
        private InputActionAsset uiActions = null;
        private InputActionAsset gameplayActions = null;
        private UISettingsAsset runtimeSettings = null;
        private UIContext rootContext = null;
        private UIContext workspaceContext = null;
        private readonly List<PresentationLayout> ownedApplicationLayouts = new();

    #endregion

    #region 생성자

        public XeriWindowManualRuntime(int stepCount)
        {
            CreateTestCamera();
            CreateManualRuntime(stepCount);
        }

    #endregion

    #region R-1: Runtime과 Workspace

        // ------------------------------------------------------------
        /// <summary>
        /// Game View 경고가 나오지 않도록 테스트용 카메라를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CreateTestCamera()
        {
            cameraGO = new GameObject("TEST_XeriWindow_Camera");

            var testCamera = cameraGO.AddComponent<Camera>();
            testCamera.clearFlags      = CameraClearFlags.SolidColor;
            testCamera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            testCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Manual 화면의 UITK Root를 Core Layer로 등록하고
        /// <br/> production Window Workspace를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CreateManualRuntime(int stepCount)
        {
            UIRuntime.Clear();

            runtimeGO = new GameObject("TEST_XeriWindow_Runtime");
            runtimeGO.SetActive(false);
            runtimeGO.AddComponent<EventSystem>();
            var inputModule = runtimeGO.AddComponent<InputSystemUIInputModule>();
            focusDriver = runtimeGO.AddComponent<XeriWindowManualFocusDriver>();
            focusController = runtimeGO.AddComponent<UIFocusDriver>();
            inputDriver = runtimeGO.AddComponent<InputSystemScreenInputDriver>();
            runtime = runtimeGO.AddComponent<UIRuntime>();

            uiActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var uiMap = new InputActionMap("UI");
            uiMap.AddAction("Cancel", InputActionType.Button);
            uiActions.AddActionMap(uiMap);
            inputModule.actionsAsset = uiActions;

            gameplayActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplayMap = new InputActionMap("Player");
            gameplayMap.AddAction("Move", InputActionType.Value);
            gameplayActions.AddActionMap(gameplayMap);

            runtimeSettings = ScriptableObject.CreateInstance<UISettingsAsset>();
            SetField(runtimeSettings, "gameplayActionsAsset", gameplayActions);
            SetField(runtimeSettings, "uiActionMap", "UI");
            SetField(runtimeSettings, "gameplayActionMap", "Player");
            SetField
            (
                runtimeSettings,
                "releaseActionNames",
                new[]
                {
                    "Cancel",
                }
            );

            focusController.Initialize();
            inputDriver.Initialize(inputModule, runtimeSettings);
            screenTransitioner = new DOTweenPresentationTransitioner();
            SetField(runtime, "transitioner", screenTransitioner);
            SetField(runtime, "focusDriver", focusController);
            SetField(runtime, "inputDriver", inputDriver);
            SetProperty(runtime, nameof(UIRuntime.IsInitialized), true);

            runtimeGO.SetActive(true);
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            panelSettings.name = "TEST_XeriWindow_PanelSettings";
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;

            presentationRootGO = new GameObject("TEST_XeriWindow_PresentationRoot");
            presentationRootGO.transform.SetParent(runtimeGO.transform, false);
            var parentLayer = new PresentationPlanLayer
            (
                "TEST.Parent",
                "TEST Parent",
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
                    new PresentationPlanPlacement
                    (
                        "TEST.Manual.Host",
                        parentLayer,
                        0
                    ),
                }
            );
            rootPresentation = PresentationSession.CreateTopLevel
            (
                parentPlan,
                presentationRootGO.transform,
                panelSettings,
                null,
                focusController.BindLayer
            );

            if
            (
                !rootPresentation.TryAcquirePlacement
                (
                    "TEST.Manual.Host",
                    out var manualHost,
                    out var manualHostUsage
                )
            )
            {
                throw new InvalidOperationException("Manual HUD Presentation placement를 획득할 수 없습니다.");
            }

            manualHostUsage.Dispose();
            RootElement = ((IPresentationLayerDriver<VisualElement>)manualHost).Root;
            LoadManualTestStyleSheet(RootElement);
            RootElement.style.flexGrow = 1f;
            RootElement.style.flexDirection = FlexDirection.Column;
            RootElement.style.backgroundColor = new Color(0.06f, 0.06f, 0.07f, 1f);
            HUD = new XeriWindowManualTestHUD(RootElement, stepCount);

            workspaceLayout = CreateLayout
            (
                new[]
                {
                    new PresentationLayerDefinition
                    (
                        "TEST.Window",
                        "TEST Window",
                        0,
                        PresentationBackend.UITK
                    ),
                },
                new[]
                {
                    new PresentationPlacementDefinition
                    (
                        "TEST.Window.Workspace",
                        "TEST.Window",
                        0
                    ),
                }
            );
            workspacePresentation = rootPresentation.CreateChild
            (
                workspaceLayout,
                HUD.WindowArea
            );

            rootContext = new UIContext
            (
                runtime,
                null,
                rootPresentation,
                screenTransitioner,
                focusController,
                inputDriver
            );
            workspaceContext = rootContext.CreateChild(workspacePresentation);
            SetProperty(runtime, nameof(UIRuntime.Main), rootContext);
            SetProperty(runtime, nameof(UIRuntime.RootPresentation), rootPresentation);
            workspaceContext.SetBaseAuthority();

            workspace = new XeriWindowWorkspace
            (
                workspaceContext,
                "TEST.Window.Workspace",
                animationOptions: CreateManualAnimationOptions()
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Runtime 생성 역순으로 Workspace, Context, Presentation과 backend 자원을 정리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void DisposeManualRuntime()
        {
            workspace?.Dispose();
            workspace = null;

            if (rootContext != null && !rootContext.IsDisposed)
            {
                var errors = rootContext.DisposeFromRuntime();

                if (errors.Count > 0)
                {
                    throw new AggregateException
                    (
                        "Manual Window Root Context 정리가 실패했습니다.",
                        errors
                    );
                }
            }

            workspaceContext = null;
            rootContext = null;

            if (workspacePresentation != null && !workspacePresentation.IsDisposed)
            {
                workspacePresentation.Dispose();
            }

            workspacePresentation = null;

            if (rootPresentation != null && !rootPresentation.IsDisposed)
            {
                rootPresentation.Dispose();
            }

            rootPresentation = null;

            inputDriver?.Dispose();
            inputDriver = null;

            screenTransitioner?.Dispose();
            screenTransitioner = null;

            if (runtime != null)
            {
                SetProperty(runtime, nameof(UIRuntime.Main), null);
                SetProperty(runtime, nameof(UIRuntime.RootPresentation), null);
                SetProperty(runtime, nameof(UIRuntime.IsInitialized), false);
                SetProperty(runtime, nameof(UIRuntime.IsReleased), true);
            }

            if (runtimeSettings != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimeSettings);
                runtimeSettings = null;
            }

            if (uiActions != null)
            {
                UnityEngine.Object.DestroyImmediate(uiActions);
                uiActions = null;
            }

            if (gameplayActions != null)
            {
                UnityEngine.Object.DestroyImmediate(gameplayActions);
                gameplayActions = null;
            }

            if (workspaceLayout != null)
            {
                UnityEngine.Object.DestroyImmediate(workspaceLayout);
                workspaceLayout = null;
            }

            if (presentationRootGO != null)
            {
                UnityEngine.Object.DestroyImmediate(presentationRootGO);
                presentationRootGO = null;
            }

            if (runtimeGO != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimeGO);
                runtimeGO = null;
            }

            focusController = null;
            focusDriver = null;
            runtime = null;
            UIRuntime.Clear();
        }

    #endregion

    #region A-1: Application Layout

        internal XeriWindowApplicationOptions CreateApplicationOptions(string prefix)
        {
            var screenID = $"{prefix}.Screen";
            var overlayID = $"{prefix}.Overlay";
            var modalID = $"{prefix}.Modal";
            var layout = CreateLayout
            (
                new[]
                {
                    CreateApplicationLayer(screenID, 0),
                    CreateApplicationLayer(overlayID, 100),
                    CreateApplicationLayer(modalID, 200),
                },
                new[]
                {
                    new PresentationPlacementDefinition($"{prefix}.Home", screenID, 0),
                    new PresentationPlacementDefinition($"{prefix}.Detail", screenID, 10),
                    new PresentationPlacementDefinition($"{prefix}.Overlay", overlayID, 0),
                    new PresentationPlacementDefinition($"{prefix}.Modal.A", modalID, 0),
                    new PresentationPlacementDefinition($"{prefix}.Modal.B", modalID, 10),
                }
            );
            ownedApplicationLayouts.Add(layout);
            return new XeriWindowApplicationOptions(layout);
        }

        private static PresentationLayerDefinition CreateApplicationLayer
        (
            string id,
            int order
        )
        {
            return new PresentationLayerDefinition
            (
                id,
                id,
                order,
                PresentationBackend.UITK
            );
        }

    #endregion

    #region 조회

        internal XeriWindowPanel FindWindowPanel(string title)
        {
            var panels = HUD.WindowArea.Query<XeriWindowPanel>().ToList();

            for (var index = 0; index < panels.Count; index++)
            {
                if (panels[index].TitleLabel.text == title)
                {
                    return panels[index];
                }
            }

            throw new InvalidOperationException
            (
                $"Manual Window Panel을 찾을 수 없습니다. Title: {title}"
            );
        }

    #endregion

    #region 내부 처리

        private static PresentationLayout CreateLayout
        (
            IReadOnlyList<PresentationLayerDefinition> layers,
            IReadOnlyList<PresentationPlacementDefinition> placements
        )
        {
            var layout = ScriptableObject.CreateInstance<PresentationLayout>();
            SetField(layout, "layers", new List<PresentationLayerDefinition>(layers));
            SetField
            (
                layout,
                "placements",
                new List<PresentationPlacementDefinition>(placements)
            );
            return layout;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField
            (
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            field.SetValue(target, value);
        }

        private static void SetProperty(object target, string name, object value)
        {
            var property = target.GetType().GetProperty
            (
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            property.SetValue(target, value);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Manual 테스트에서 눈으로 확인 가능한 window animation 옵션을 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static XeriWindowAnimationOptions CreateManualAnimationOptions()
        {
            return new XeriWindowAnimationOptions
            {
                Enabled = true,
                Duration = 0.18f,
                HiddenOpacity = 0f,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Manual test USS를 Resources에서 로드해 target에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void LoadManualTestStyleSheet(VisualElement target)
        {
            var styleSheet = Resources.Load<StyleSheet>(MANUAL_TEST_USS_PATH);

            if (styleSheet == null)
            {
                throw new InvalidOperationException($"Manual test USS load failed. Path: {MANUAL_TEST_USS_PATH}");
            }

            target.styleSheets.Add(styleSheet);
        }

    #endregion

    #region IDisposable

        public void Dispose()
        {
            DisposeManualRuntime();

            for (var index = ownedApplicationLayouts.Count - 1; index >= 0; index--)
            {
                if (ownedApplicationLayouts[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedApplicationLayouts[index]);
                }
            }

            ownedApplicationLayouts.Clear();

            if (cameraGO != null)
            {
                UnityEngine.Object.DestroyImmediate(cameraGO);
                cameraGO = null;
            }

            if (panelSettings != null)
            {
                UnityEngine.Object.DestroyImmediate(panelSettings);
                panelSettings = null;
            }

            RootElement = null;
            HUD = null;
        }

    #endregion

    }
}
