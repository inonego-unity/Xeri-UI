/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowWorkspaceTestSupport.cs
수정일 : 2026-09-28

# 설명
Window Workspace 통합 테스트가 공유하는 test double, runtime fixture와 Application helper를 제공한다.

# 테스트 구성
 D: Window Driver test double
 F: Focus Driver test double
 V: UI Session과 View Source test double
 A: Application Screen과 Focus Scope test double
 X-1: Workspace runtime fixture
 X-2: 공용 static helper
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

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.TEST;
using inonego.Xeri.UI.TEST.Core;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    #region D-1: Window 테스트 대역

    internal sealed class TestWindowDriver : IXeriWindowDriver
    {
        public PresentationAlpha Alpha { get; } = new();
        public PresentationVisibility Visibility { get; } = new();

        public Vector2 Pos { get; set; } = Vector2.zero;
        public Vector2 Size { get; set; } = new Vector2(200f, 120f);
        public XeriWindowState State { get; set; } = XeriWindowState.Normal;
        public bool MaximizedApplied { get; private set; }

        public Rect Bounds
        {
            get => new Rect(Pos, Size);
            set
            {
                Pos = value.position;
                Size = value.size;
            }
        }

        public void CommitState(XeriWindowState state)
        {
            State = state;
        }

        public void ApplyVisualState(XeriWindowState state)
        {
            // NONE
        }

        public void ApplyBounds(Rect bounds)
        {
            Bounds = bounds;
        }

        public void ApplyMaximizedBounds()
        {
            MaximizedApplied = true;
        }
    }

    #endregion

    #region F-1: Focus Driver

    internal sealed class TestFocusDriver : FocusDriverBehaviour
    {
        public override object Current => current;

        private object current = null;

        public object Fallback { get; } = new object();

        public override bool CanSelect(object target)
        {
            return target != null;
        }

        public override bool IsValid(object target)
        {
            return target != null;
        }

        public override void Select(object target)
        {
            current = target;
        }

        public override object FindFallback()
        {
            return Fallback;
        }
    }

    #endregion

    #region V-1: UI Session

    internal sealed class TestSession : IXeriUISession
    {
        public int Version { get; set; }
    }

    #endregion

    #region A-1: Application Screen과 Focus Scope

    internal sealed class TestApplicationScreenSource : IScreenSource
    {
        public VisualElement DefaultFocus { get; private set; }
        public VisualElement SecondaryFocus { get; private set; }
        public int ReleaseCount { get; private set; }

        private VisualElement root = null;

        public ScreenInstance Acquire(ScreenViewScope scope)
        {
            if (scope.Layer is not IPresentationLayerDriver<VisualElement> layer)
            {
                throw new InvalidOperationException("Application Window Screen Layer가 UITK Root를 제공하지 않습니다.");
            }

            root = new VisualElement
            {
                name = "Application Screen",
            };
            DefaultFocus = new Button
            {
                name = "Application Default Focus",
            };
            SecondaryFocus = new Button
            {
                name = "Application Secondary Focus",
            };
            root.Add(DefaultFocus);
            root.Add(SecondaryFocus);
            layer.Root.Add(root);
            return new ScreenInstance(new UITKScreenDriver(root, DefaultFocus));
        }

        public void Release(ScreenInstance instance)
        {
            ReleaseCount++;
            root?.RemoveFromHierarchy();
            root = null;
            DefaultFocus = null;
            SecondaryFocus = null;
        }
    }

    internal sealed class TestVisualFocusScope : IFocusScope
    {
        public object DefaultFocus { get; }

        private readonly VisualElement root = null;

        public TestVisualFocusScope(VisualElement root, object defaultFocus)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            DefaultFocus = defaultFocus;
        }

        public bool ContainsFocus(object target)
        {
            if (target is not VisualElement element)
            {
                return false;
            }

            for (var current = element; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, root))
                {
                    return true;
                }
            }

            return false;
        }
    }

    #endregion

    #region V-2: View Source

    internal sealed class TestViewSource : IXeriUIViewSource
    {
        public string ID { get; }
        public int LoadCount { get; private set; }
        public int AcquireCount { get; private set; }
        public int SaveCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public XeriUIViewScope LastScope { get; private set; }
        public VisualElement LastView { get; private set; }
        public bool ReturnParentedView { get; set; }

        private readonly VisualElement foreignParent = new();

        public TestViewSource(string id)
        {
            ID = id;
        }

        public VisualElement AcquireView(XeriUIViewScope scope)
        {
            AcquireCount++;
            LastScope = scope;
            LastView = new VisualElement();

            if (ReturnParentedView)
            {
                foreignParent.Add(LastView);
            }

            return LastView;
        }

        public void ReleaseView(XeriUIViewScope scope, VisualElement view)
        {
            ReleaseCount++;
            LastScope = scope;
            LastView = view;
            view?.RemoveFromHierarchy();
        }

        public void SaveSession(XeriUIViewScope scope)
        {
            SaveCount++;
            LastScope = scope;

            if (scope.UISession is TestSession session)
            {
                session.Version++;
            }
        }

        public void LoadSession(XeriUIViewScope scope)
        {
            LoadCount++;
            LastScope = scope;
        }
    }

    #endregion

    #region X-1: Workspace Fixture

    internal sealed class WorkspaceFixture : IDisposable
    {
        public IPresentationLayerDriver<VisualElement> LayerDriver { get; }
        public VisualElement WorkspacePlacementRoot { get; }
        public TestFocusDriver FocusDriver { get; }
        public InputActionMap GameplayMap { get; }
        public XeriWindowWorkspace Workspace { get; }

        private readonly List<PresentationLayout> ownedLayouts = new();
        private readonly GameObject runtimeHost = null;
        private readonly GameObject presentationRoot = null;
        private readonly UIRuntime runtime = null;
        private readonly PresentationSession rootPresentation = null;
        private readonly DOTweenPresentationTransitioner transitioner = null;
        private readonly UIFocusDriver focusController = null;
        private readonly InputSystemScreenInputDriver inputDriver = null;
        private readonly InputActionAsset uiActions = null;
        private readonly InputActionAsset gameplayActions = null;
        private readonly UISettingsAsset settings = null;
        private readonly PanelSettings panelSettings = null;
        private readonly UIContext rootContext = null;

        public WorkspaceFixture(IXeriUIViewResolver viewResolver = null)
        {
            UIRuntime.Clear();

            runtimeHost = new GameObject("TEST_XeriWindowWorkspace_Runtime");
            runtimeHost.SetActive(false);
            runtimeHost.AddComponent<EventSystem>();
            var inputModule = runtimeHost.AddComponent<InputSystemUIInputModule>();
            FocusDriver = runtimeHost.AddComponent<TestFocusDriver>();
            focusController = runtimeHost.AddComponent<UIFocusDriver>();
            inputDriver = runtimeHost.AddComponent<InputSystemScreenInputDriver>();
            runtime = runtimeHost.AddComponent<UIRuntime>();

            uiActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var uiMap = new InputActionMap("UI");
            uiMap.AddAction("Cancel", InputActionType.Button);
            uiActions.AddActionMap(uiMap);
            inputModule.actionsAsset = uiActions;

            gameplayActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplayMap = new InputActionMap("Player");
            gameplayMap.AddAction("Move", InputActionType.Value);
            gameplayActions.AddActionMap(gameplayMap);
            GameplayMap = gameplayMap;

            settings = ScriptableObject.CreateInstance<UISettingsAsset>();
            XeriWindowWorkspaceTestSupport.SetField(settings, "gameplayActionsAsset", gameplayActions);
            XeriWindowWorkspaceTestSupport.SetField(settings, "uiActionMap", "UI");
            XeriWindowWorkspaceTestSupport.SetField(settings, "gameplayActionMap", "Player");
            XeriWindowWorkspaceTestSupport.SetField
            (
                settings,
                "releaseActionNames",
                new[]
                {
                    "Cancel",
                }
            );

            focusController.Initialize();
            inputDriver.Initialize(inputModule, settings);
            transitioner = new DOTweenPresentationTransitioner();
            XeriWindowWorkspaceTestSupport.SetField(runtime, "transitioner", transitioner);
            XeriWindowWorkspaceTestSupport.SetField(runtime, "focusDriver", focusController);
            XeriWindowWorkspaceTestSupport.SetField(runtime, "inputDriver", inputDriver);
            XeriWindowWorkspaceTestSupport.SetProperty(runtime, nameof(UIRuntime.IsInitialized), true);

            presentationRoot = new GameObject("TEST Presentation Root");
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            var parentLayer = new PresentationPlanLayer
            (
                "TEST.Window",
                "TEST Window",
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
                        "TEST.Window.Workspace",
                        parentLayer,
                        0
                    ),
                }
            );
            rootPresentation = PresentationSession.CreateTopLevel
            (
                parentPlan,
                presentationRoot.transform,
                panelSettings,
                null,
                focusController.BindLayer
            );

            Assert.IsTrue
            (
                rootPresentation.LayerRegistry.TryGet
                (
                    "TEST.Window",
                    out var layer
                )
            );
            LayerDriver = (IPresentationLayerDriver<VisualElement>)layer;
            Assert.IsTrue
            (
                rootPresentation.TryAcquirePlacement
                (
                    "TEST.Window.Workspace",
                    out var workspacePlacement,
                    out var workspaceUsage
                )
            );
            workspaceUsage.Dispose();
            WorkspacePlacementRoot =
                ((IPresentationLayerDriver<VisualElement>)workspacePlacement).Root;

            rootContext = new UIContext
            (
                runtime,
                null,
                rootPresentation,
                transitioner,
                focusController,
                inputDriver
            );
            XeriWindowWorkspaceTestSupport.SetProperty(runtime, nameof(UIRuntime.Main), rootContext);
            XeriWindowWorkspaceTestSupport.SetProperty(runtime, nameof(UIRuntime.RootPresentation), rootPresentation);
            rootContext.SetBaseAuthority();

            Workspace = new XeriWindowWorkspace
            (
                rootContext,
                "TEST.Window.Workspace",
                viewResolver: viewResolver,
                animationOptions: XeriWindowAnimationOptions.Immediate()
            );
        }

        public XeriWindowApplicationOptions CreateApplicationOptions(string prefix = "Application")
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
                    new PresentationPlacementDefinition
                    (
                        $"{prefix}.First.Screen",
                        screenID,
                        0
                    ),
                    new PresentationPlacementDefinition
                    (
                        $"{prefix}.Second.Screen",
                        screenID,
                        10
                    ),
                    new PresentationPlacementDefinition
                    (
                        $"{prefix}.Overlay",
                        overlayID,
                        0
                    ),
                    new PresentationPlacementDefinition
                    (
                        $"{prefix}.Modal.A",
                        modalID,
                        0
                    ),
                    new PresentationPlacementDefinition
                    (
                        $"{prefix}.Modal.B",
                        modalID,
                        10
                    ),
                }
            );
            return new XeriWindowApplicationOptions(layout);
        }

        public void Dispose()
        {
            Workspace?.Dispose();

            if (rootContext != null && !rootContext.IsDisposed)
            {
                var errors = rootContext.DisposeFromRuntime();

                if (errors.Count > 0)
                {
                    throw new AggregateException
                    (
                        "Workspace 테스트 Root Context 정리가 실패했습니다.",
                        errors
                    );
                }
            }

            rootPresentation?.Dispose();
            inputDriver?.Dispose();
            transitioner?.Dispose();

            if (runtime != null)
            {
                XeriWindowWorkspaceTestSupport.SetProperty(runtime, nameof(UIRuntime.Main), null);
                XeriWindowWorkspaceTestSupport.SetProperty(runtime, nameof(UIRuntime.RootPresentation), null);
                XeriWindowWorkspaceTestSupport.SetProperty(runtime, nameof(UIRuntime.IsInitialized), false);
                XeriWindowWorkspaceTestSupport.SetProperty(runtime, nameof(UIRuntime.IsReleased), true);
            }

            if (settings != null)
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }

            if (panelSettings != null)
            {
                UnityEngine.Object.DestroyImmediate(panelSettings);
            }

            if (uiActions != null)
            {
                UnityEngine.Object.DestroyImmediate(uiActions);
            }

            if (gameplayActions != null)
            {
                UnityEngine.Object.DestroyImmediate(gameplayActions);
            }

            for (var index = ownedLayouts.Count - 1; index >= 0; index--)
            {
                if (ownedLayouts[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedLayouts[index]);
                }
            }

            ownedLayouts.Clear();

            if (presentationRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(presentationRoot);
            }

            if (runtimeHost != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimeHost);
            }

            UIRuntime.Clear();
        }

        private PresentationLayout CreateLayout
        (
            IReadOnlyList<PresentationLayerDefinition> layers,
            IReadOnlyList<PresentationPlacementDefinition> placements
        )
        {
            var layout = PresentationTestScope.CreateLayout(layers, placements);
            ownedLayouts.Add(layout);
            return layout;
        }

        private static PresentationLayerDefinition CreateApplicationLayer
        (
            string id,
            int layerOrder
        )
        {
            return new PresentationLayerDefinition
            (
                id,
                id,
                layerOrder,
                PresentationBackend.UITK
            );
        }
    }

    #endregion

    #region X-2: Static Helpers

    internal static class XeriWindowWorkspaceTestSupport
    {
        internal static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField
            (
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            field.SetValue(target, value);
        }

        internal static void SetProperty(object target, string name, object value)
        {
            var property = target.GetType().GetProperty
            (
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            property.SetValue(target, value);
        }

        internal static XeriWindowSession OpenWindow
        (
            XeriWindowWorkspace workspace,
            string id
        )
        {
            return OpenWindow(workspace, id, new VisualElement());
        }

        internal static XeriWindowSession OpenWindow
        (
            XeriWindowWorkspace workspace,
            string id,
            VisualElement view
        )
        {
            return workspace.OpenWindow
            (
                id,
                id,
                view,
                new Vector2(20f, 30f),
                new Vector2(320f, 220f)
            );
        }

        internal static ModalSession OpenApplicationModal
        (
            UIContext context,
            string presentationID,
            string name,
            out Button secondaryFocus
        )
        {
            var modalRoot = new VisualElement
            {
                name = name,
            };
            var defaultFocus = new Button
            {
                name = $"{name} Default Focus",
            };
            secondaryFocus = new Button
            {
                name = $"{name} Secondary Focus",
            };
            modalRoot.Add(defaultFocus);
            modalRoot.Add(secondaryFocus);
            return UITKModal.OpenWithFocus
            (
                context,
                presentationID,
                modalRoot,
                new TestVisualFocusScope(modalRoot, defaultFocus)
            );
        }
    }

    #endregion
}
