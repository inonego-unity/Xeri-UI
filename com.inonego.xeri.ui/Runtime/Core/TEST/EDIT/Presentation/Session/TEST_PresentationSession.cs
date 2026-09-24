/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_PresentationSession.cs
수정일 : 2026-10-03

# 설명
PresentationSession의 top-level materialization, placement ordering, Child Session 격리와 release 계약을 검증한다.

# 테스트 구성
 T: Top-level Native Output materialization
 P: backend LocalOrder materialization
 C: Child Session attach/detach
 I: sibling Child Session runtime isolation
 L: active consumer lifetime
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ================================================================================
    /// <summary>
    /// immutable Plan을 실제 output과 nested Session tree로 materialize하는 계약 테스트.
    /// </summary>
    // ================================================================================
    public sealed class TEST_PresentationSession
    {

    #region 필드

        private readonly List<UnityEngine.Object> ownedObjects = new();

    #endregion

    #region 헬퍼

        private PresentationLayout CreateLayout
        (
            IReadOnlyList<PresentationLayerDefinition> layers,
            IReadOnlyList<PresentationPlacementDefinition> placements
        )
        {
            var layout = PresentationTestScope.CreateLayout(layers, placements);
            ownedObjects.Add(layout);
            return layout;
        }

        private static PresentationLayerDefinition Layer
        (
            string id,
            int order,
            PresentationBackend backend = PresentationBackend.UGUI
        )
        {
            return new PresentationLayerDefinition(id, id, order, backend);
        }

        private static PresentationPlacementDefinition Placement
        (
            string id,
            string layerID,
            int localOrder
        )
        {
            return new PresentationPlacementDefinition(id, layerID, localOrder);
        }

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

    #endregion

    #region 픽스처

        [TearDown]
        public void TearDown()
        {
            for (var index = ownedObjects.Count - 1; index >= 0; index--)
            {
                if (ownedObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[index]);
                }
            }

            ownedObjects.Clear();
        }

    #endregion

    #region T-1: UGUI 최상위 네이티브 출력

        [Test]
        public void TEST_PresentationSession_TopLevelUGUI_LayerOrder_CanvasSortingOrder반영()
        {
            using var scope = PresentationTestScope.CreateUGUI
            (
                "Screen",
                ("Screen.View", 0)
            );
            Assert.IsTrue(scope.Session.LayerRegistry.TryGet("Screen", out var driver));
            var managedRoot = ((IPresentationLayerDriver<RectTransform>)driver).Root;
            var canvas = managedRoot.GetComponentInParent<Canvas>();
            var layerRoot = canvas.transform as RectTransform;

            Assert.IsNotNull(canvas);
            Assert.IsNotNull(layerRoot);
            Assert.AreNotSame(layerRoot, managedRoot);
            Assert.AreSame(layerRoot, managedRoot.parent);
            Assert.AreEqual(0, canvas.sortingOrder);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
        }

    #endregion

    #region T-2: UITK 레이어별 네이티브 출력

        [Test]
        public void TEST_PresentationSession_TopLevelUITK_Layer별PanelRenderer와Order생성()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("Modal", 200, PresentationBackend.UITK),
                    Layer("Screen", 100, PresentationBackend.UITK),
                },
                Array.Empty<PresentationPlacementDefinition>()
            );
            var plan = PresentationLayoutResolver.Resolve(layout);
            var host = new GameObject("UITK Session Host");
            ownedObjects.Add(host);
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            ownedObjects.Add(panelSettings);
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            var session = PresentationSession.CreateTopLevel
            (
                plan,
                host.transform,
                panelSettings,
                null
            );

            try
            {
                Assert.AreEqual(2, host.transform.childCount);
                var screenOutput = host.transform.GetChild(0).GetComponent<UITKPresentationOutput>();
                var modalOutput = host.transform.GetChild(1).GetComponent<UITKPresentationOutput>();

                Assert.IsNotNull(screenOutput);
                Assert.IsNotNull(modalOutput);
                Assert.IsNotNull(screenOutput.PanelRenderer);
                Assert.IsNotNull(modalOutput.PanelRenderer);
                Assert.AreNotSame(screenOutput.PanelRenderer, modalOutput.PanelRenderer);
                Assert.AreSame(screenOutput.RuntimePanelSettings, screenOutput.PanelRenderer.panelSettings);
                Assert.AreSame(modalOutput.RuntimePanelSettings, modalOutput.PanelRenderer.panelSettings);
                Assert.AreEqual(100.0f, screenOutput.RuntimePanelSettings.sortingOrder);
                Assert.AreEqual(200.0f, modalOutput.RuntimePanelSettings.sortingOrder);
                Assert.IsTrue
                (
                    session.LayerRegistry.TryGet("Screen", out var screenDriver)
                );
                var screenManagedRoot =
                    ((IPresentationLayerDriver<VisualElement>)screenDriver).Root;
                Assert.AreEqual("xeri-managed-Screen", screenManagedRoot.name);
                Assert.AreEqual("xeri-layer-Screen", screenManagedRoot.parent.name);
            }
            finally
            {
                session.Dispose();
            }
        }

    #endregion

    #region T-3: 씬 작성 레이어 호스트 / 배치 호스트

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> UGUI LayerHost는 authored static content를 유지한다.
        /// <br/> ManagedRoot만 runtime Placement 영역으로 사용한다.
        /// <br/> authored/generated Placement는 같은 LocalOrder 계약을 따른다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationSession_UGUILayerHost_ManagedRoot와PlacementHost계약유지()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("Screen", 100, PresentationBackend.UGUI),
                },
                new[]
                {
                    Placement("Screen.Early", "Screen", 10),
                    Placement("Screen.Generated", "Screen", 20),
                    Placement("Screen.Late", "Screen", 30),
                }
            );
            var plan = PresentationLayoutResolver.Resolve(layout);
            var runtimeHost = new GameObject("Runtime Host");
            ownedObjects.Add(runtimeHost);
            var outputObject = new GameObject
            (
                "Scene Layer Host",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UGUIPresentationOutput),
                typeof(UGUIPresentationLayerHost)
            );
            outputObject.transform.SetParent(runtimeHost.transform, false);
            ownedObjects.Add(outputObject);
            var canvas = outputObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 37;
            var layerRoot = outputObject.GetComponent<RectTransform>();
            var staticContent = UGUIPresentationSurface.CreateRect
            (
                "Static Content",
                layerRoot
            );
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
            var latePlacementObject = new GameObject
            (
                "Late Placement",
                typeof(RectTransform),
                typeof(UGUIPresentationPlacementHost)
            );
            latePlacementObject.transform.SetParent(managedRoot, false);
            var earlyPlacementObject = new GameObject
            (
                "Early Placement",
                typeof(RectTransform),
                typeof(UGUIPresentationPlacementHost)
            );
            earlyPlacementObject.transform.SetParent(managedRoot, false);
            var lateRoot = latePlacementObject.GetComponent<RectTransform>();
            var earlyRoot = earlyPlacementObject.GetComponent<RectTransform>();
            var layerHost = outputObject.GetComponent<UGUIPresentationLayerHost>();
            var lateHost =
                latePlacementObject.GetComponent<UGUIPresentationPlacementHost>();
            var earlyHost =
                earlyPlacementObject.GetComponent<UGUIPresentationPlacementHost>();
            SetField(layerHost, "layerID", "Screen");
            SetField(layerHost, "managedRoot", managedRoot);
            SetField(lateHost, "presentationID", "Screen.Late");
            SetField(earlyHost, "presentationID", "Screen.Early");
            SetField
            (
                layerHost,
                "placementHosts",
                new PresentationPlacementHost[]
                {
                    lateHost,
                    earlyHost,
                }
            );
            outputObject.SetActive(false);

            var session = PresentationSession.CreateTopLevel
            (
                plan,
                runtimeHost.transform,
                null,
                null,
                layerHosts: new PresentationLayerHost[]
                {
                    layerHost,
                }
            );
            Lease earlyUsage = null;
            Lease generatedUsage = null;
            Lease lateUsage = null;

            try
            {
                Assert.IsTrue(outputObject.activeSelf);
                Assert.AreEqual(100, canvas.sortingOrder);
                Assert.AreSame(layerRoot, staticContent.parent);
                Assert.AreSame(layerRoot, authoredContainer.parent);
                Assert.AreSame(authoredContainer, managedRoot.parent);
                Assert.IsTrue
                (
                    session.LayerRegistry.TryGet("Screen", out var layerDriver)
                );
                Assert.AreSame
                (
                    managedRoot,
                    ((IPresentationLayerDriver<RectTransform>)layerDriver).Root
                );
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "Screen.Early",
                        out var earlyDriver,
                        out earlyUsage
                    )
                );
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "Screen.Generated",
                        out var generatedDriver,
                        out generatedUsage
                    )
                );
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "Screen.Late",
                        out var lateDriver,
                        out lateUsage
                    )
                );
                Assert.AreSame
                (
                    earlyRoot,
                    ((IPresentationLayerDriver<RectTransform>)earlyDriver).Root
                );
                Assert.AreSame
                (
                    lateRoot,
                    ((IPresentationLayerDriver<RectTransform>)lateDriver).Root
                );
                var generatedRoot =
                    ((IPresentationLayerDriver<RectTransform>)generatedDriver).Root;
                Assert.AreSame(managedRoot, generatedRoot.parent);
                Assert.AreEqual(0, earlyRoot.GetSiblingIndex());
                Assert.AreEqual(1, generatedRoot.GetSiblingIndex());
                Assert.AreEqual(2, lateRoot.GetSiblingIndex());

                earlyUsage.Dispose();
                earlyUsage = null;
                generatedUsage.Dispose();
                generatedUsage = null;
                lateUsage.Dispose();
                lateUsage = null;
                session.Dispose();

                Assert.IsFalse(outputObject.activeSelf);
                Assert.AreEqual(37, canvas.sortingOrder);
                Assert.AreSame(layerRoot, staticContent.parent);
                Assert.AreSame(layerRoot, authoredContainer.parent);
                Assert.AreSame(authoredContainer, managedRoot.parent);
                Assert.AreSame(managedRoot, lateRoot.parent);
                Assert.AreSame(managedRoot, earlyRoot.parent);
                Assert.AreEqual(2, managedRoot.childCount);
                Assert.AreEqual(0, lateRoot.GetSiblingIndex());
                Assert.AreEqual(1, earlyRoot.GetSiblingIndex());
            }
            finally
            {
                earlyUsage?.Dispose();
                generatedUsage?.Dispose();
                lateUsage?.Dispose();

                if (!session.IsDisposed)
                {
                    session.Dispose();
                }
            }
        }

    #endregion

    #region T-4: 배치 호스트 토폴로지 검증

        [Test]
        public void TEST_PresentationSession_PlacementHost가다른Layer에연결_Activation전거부()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("A", 100, PresentationBackend.UGUI),
                    Layer("B", 200, PresentationBackend.UGUI),
                },
                new[]
                {
                    Placement("A.View", "A", 10),
                }
            );
            var plan = PresentationLayoutResolver.Resolve(layout);
            var runtimeHost = new GameObject("Runtime Host");
            ownedObjects.Add(runtimeHost);
            var layerHostObject = new GameObject
            (
                "Layer B Host",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UGUIPresentationOutput),
                typeof(UGUIPresentationLayerHost)
            );
            layerHostObject.transform.SetParent(runtimeHost.transform, false);
            ownedObjects.Add(layerHostObject);
            var placementObject = new GameObject
            (
                "Wrong Placement",
                typeof(RectTransform),
                typeof(UGUIPresentationPlacementHost)
            );
            placementObject.transform.SetParent(layerHostObject.transform, false);
            var layerHost =
                layerHostObject.GetComponent<UGUIPresentationLayerHost>();
            var placementHost =
                placementObject.GetComponent<UGUIPresentationPlacementHost>();
            SetField(layerHost, "layerID", "B");
            SetField(placementHost, "presentationID", "A.View");
            SetField
            (
                layerHost,
                "placementHosts",
                new PresentationPlacementHost[]
                {
                    placementHost,
                }
            );

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationSession.CreateTopLevel
                (
                    plan,
                    runtimeHost.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        layerHost,
                    }
                )
            );
            Assert.AreEqual(1, runtimeHost.transform.childCount);
        }

    #endregion

    #region P-1: UGUI LocalOrder 정렬

        [Test]
        public void TEST_PresentationSession_UGUIPlacement_LocalOrder를SiblingOrder로Materialize()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("Screen", 0),
                },
                new[]
                {
                    Placement("Screen.Late", "Screen", 20),
                    Placement("Screen.Early", "Screen", 10),
                }
            );
            var plan = PresentationLayoutResolver.Resolve(layout);
            var host = new GameObject("UGUI Session Host");
            ownedObjects.Add(host);
            var session = PresentationSession.CreateTopLevel(plan, host.transform, null, null);
            Lease earlyUsage = null;
            Lease lateUsage = null;

            try
            {
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "Screen.Early",
                        out var early,
                        out earlyUsage
                    )
                );
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "Screen.Late",
                        out var late,
                        out lateUsage
                    )
                );
                var earlyRoot = ((IPresentationLayerDriver<RectTransform>)early).Root;
                var lateRoot = ((IPresentationLayerDriver<RectTransform>)late).Root;

                Assert.AreEqual(0, earlyRoot.GetSiblingIndex());
                Assert.AreEqual(1, lateRoot.GetSiblingIndex());
            }
            finally
            {
                lateUsage?.Dispose();
                earlyUsage?.Dispose();
                session.Dispose();
            }
        }

    #endregion

    #region P-2: UITK LocalOrder 정렬

        [Test]
        public void TEST_PresentationSession_UITKPlacement_LocalOrder를ZIndex로Materialize()
        {
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
                    new PresentationPlanPlacement("Parent.Host", parentLayer, 0),
                }
            );
            var parentObject = new GameObject("UITK Parent Session");
            ownedObjects.Add(parentObject);
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            ownedObjects.Add(panelSettings);
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            var parent = PresentationSession.CreateTopLevel
            (
                parentPlan,
                parentObject.transform,
                panelSettings,
                null
            );
            Assert.IsTrue
            (
                parent.TryAcquirePlacement
                (
                    "Parent.Host",
                    out var parentPlacement,
                    out var parentUsage
                )
            );
            parentUsage.Dispose();
            var parentRoot =
                ((IPresentationLayerDriver<VisualElement>)parentPlacement).Root;
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("Screen", 0, PresentationBackend.UITK),
                },
                new[]
                {
                    Placement("Screen.Late", "Screen", 20),
                    Placement("Screen.Early", "Screen", 10),
                }
            );
            var child = parent.CreateChild(layout, parentRoot);
            Lease earlyUsage = null;
            Lease lateUsage = null;

            try
            {
                Assert.IsTrue
                (
                    child.TryAcquirePlacement
                    (
                        "Screen.Early",
                        out var early,
                        out earlyUsage
                    )
                );
                Assert.IsTrue
                (
                    child.TryAcquirePlacement
                    (
                        "Screen.Late",
                        out var late,
                        out lateUsage
                    )
                );
                var earlyRoot = ((IPresentationLayerDriver<VisualElement>)early).Root;
                var lateRoot = ((IPresentationLayerDriver<VisualElement>)late).Root;

                Assert.AreEqual(10, earlyRoot.style.zIndex.value);
                Assert.AreEqual(20, lateRoot.style.zIndex.value);
            }
            finally
            {
                lateUsage?.Dispose();
                earlyUsage?.Dispose();
                child.Dispose();
                parent.Dispose();
            }
        }

    #endregion

    #region C-1: 자식 세션 연결와 분리

        [Test]
        public void TEST_PresentationSession_Child생성해제_ParentPlan불변과Tree수명유지()
        {
            using var parent = PresentationTestScope.CreateUGUI
            (
                "Parent",
                ("Parent.Host", 0)
            );
            Assert.IsTrue
            (
                parent.Session.TryAcquirePlacement
                (
                    "Parent.Host",
                    out var host,
                    out var hostUsage
                )
            );
            hostUsage.Dispose();
            var hostRoot = ((IPresentationLayerDriver<RectTransform>)host).Root;
            var parentPlan = parent.Session.Plan;
            var layerCount = parentPlan.Layers.Count;
            var placementCount = parentPlan.Placements.Count;
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("Screen", 0),
                },
                new[]
                {
                    Placement("Screen.View", "Screen", 0),
                }
            );
            var child = parent.Session.CreateChild(layout, hostRoot);

            Assert.AreEqual(1, parent.Session.ChildCount);
            Assert.AreSame(parentPlan, parent.Session.Plan);
            Assert.AreEqual(layerCount, parentPlan.Layers.Count);
            Assert.AreEqual(placementCount, parentPlan.Placements.Count);
            Assert.Greater(hostRoot.childCount, 0);

            child.Dispose();

            Assert.AreEqual(0, parent.Session.ChildCount);
            Assert.AreSame(parentPlan, parent.Session.Plan);
            Assert.AreEqual(0, hostRoot.childCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// owner-controlled Child도 구조적 Parent 종료에서 함께 해제되는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationSession_OwnerControlledChild_Parent종료가해제()
        {
            using var parent = PresentationTestScope.CreateUGUI
            (
                "Parent",
                ("Parent.Host", 0)
            );
            Assert.IsTrue
            (
                parent.Session.TryAcquirePlacement
                (
                    "Parent.Host",
                    out var host,
                    out var hostUsage
                )
            );
            hostUsage.Dispose();
            var hostRoot = ((IPresentationLayerDriver<RectTransform>)host).Root;
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("Child", 0),
                },
                Array.Empty<PresentationPlacementDefinition>()
            );
            var child = parent.Session.CreateChild(layout, hostRoot);
            child.SetOwnerControlledLifetime();

            Assert.Throws<InvalidOperationException>(child.Dispose);
            Assert.IsFalse(child.IsDisposed);
            Assert.AreEqual(1, parent.Session.ChildCount);

            Assert.DoesNotThrow(parent.Session.Dispose);

            Assert.IsTrue(child.IsDisposed);
            Assert.IsTrue(parent.Session.IsDisposed);
            Assert.AreEqual(0, parent.Session.ChildCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Child layer Focus binding 중 Parent가 종료되면 provisional Child를
        /// <br/> 반환하지 않고 Child materialization과 registration을 모두 rollback한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_PresentationSession_Child생성중ParentDispose_ProvisionalChildRollback()
        {
            var parentLayer = PresentationTestScope.CreateLayer
            (
                "Parent",
                backend: PresentationBackend.UGUI
            );
            var parentPlan = new PresentationPlan
            (
                new List<PresentationPlanLayer>
                {
                    parentLayer,
                },
                new List<PresentationPlanPlacement>()
            );
            var root = new GameObject("Parent Presentation Root");
            ownedObjects.Add(root);
            PresentationSession parent = null;
            var disposeParentOnBind = false;

            parent = PresentationSession.CreateTopLevel
            (
                parentPlan,
                root.transform,
                null,
                null,
                driver =>
                {
                    if (disposeParentOnBind)
                    {
                        parent.Dispose();
                    }

                    return new Lease(() => { });
                }
            );
            Assert.IsTrue(parent.LayerRegistry.TryGet("Parent", out var parentDriver));
            var parentRoot =
                ((IPresentationLayerDriver<RectTransform>)parentDriver).Root;
            var childLayout = CreateLayout
            (
                new[]
                {
                    Layer("Child", 0, PresentationBackend.UGUI),
                },
                Array.Empty<PresentationPlacementDefinition>()
            );
            disposeParentOnBind = true;

            Assert.Throws<ObjectDisposedException>
            (
                () => parent.CreateChild(childLayout, parentRoot)
            );

            Assert.IsTrue(parent.IsDisposed);
            Assert.AreEqual(0, parent.ChildCount);
            Assert.AreEqual(0, root.transform.childCount);
        }

    #endregion

    #region C-2: 자식 세션 분기 불변식

        [Test]
        public void TEST_PresentationSession_ChildRoot_외부Root와Backend불일치거부()
        {
            using var parent = PresentationTestScope.CreateUGUI
            (
                "Parent",
                ("Parent.Host", 0)
            );
            var externalObject = new GameObject("External Root", typeof(RectTransform));
            ownedObjects.Add(externalObject);
            var uguiLayout = CreateLayout
            (
                new[]
                {
                    Layer("Child", 0, PresentationBackend.UGUI),
                },
                Array.Empty<PresentationPlacementDefinition>()
            );
            var uitkLayout = CreateLayout
            (
                new[]
                {
                    Layer("Child", 0, PresentationBackend.UITK),
                },
                Array.Empty<PresentationPlacementDefinition>()
            );

            Assert.Throws<InvalidOperationException>
            (
                () => parent.Session.CreateChild
                (
                    uguiLayout,
                    externalObject.GetComponent<RectTransform>()
                )
            );

            Assert.IsTrue
            (
                parent.Session.TryAcquirePlacement
                (
                    "Parent.Host",
                    out var host,
                    out var hostUsage
                )
            );
            hostUsage.Dispose();
            var hostRoot = ((IPresentationLayerDriver<RectTransform>)host).Root;

            Assert.Throws<InvalidOperationException>
            (
                () => parent.Session.CreateChild(uitkLayout, hostRoot)
            );
        }

        [Test]
        public void TEST_PresentationSession_Parent가ExistingChildBranch에SiblingRoot거부()
        {
            using var parent = PresentationTestScope.CreateUGUI
            (
                "Parent",
                ("Parent.Host", 0)
            );
            Assert.IsTrue
            (
                parent.Session.TryAcquirePlacement
                (
                    "Parent.Host",
                    out var parentHost,
                    out var parentUsage
                )
            );
            parentUsage.Dispose();
            var parentRoot = ((IPresentationLayerDriver<RectTransform>)parentHost).Root;
            var childLayout = CreateLayout
            (
                new[]
                {
                    Layer("Child", 0, PresentationBackend.UGUI),
                },
                new[]
                {
                    Placement("Child.Host", "Child", 0),
                }
            );
            var first = parent.Session.CreateChild(childLayout, parentRoot);

            try
            {
                Assert.IsTrue
                (
                    first.TryAcquirePlacement
                    (
                        "Child.Host",
                        out var childHost,
                        out var childUsage
                    )
                );
                childUsage.Dispose();
                var childRoot = ((IPresentationLayerDriver<RectTransform>)childHost).Root;

                Assert.Throws<InvalidOperationException>
                (
                    () => parent.Session.CreateChild(childLayout, childRoot)
                );
            }
            finally
            {
                first.Dispose();
            }
        }

    #endregion

    #region I-1: 동일 레이아웃 형제 세션 격리

        [Test]
        public void TEST_PresentationSession_동일LayoutSibling_Registry와PlacementState독립()
        {
            using var parent = PresentationTestScope.CreateUGUI
            (
                "Parent",
                ("Parent.Host", 0)
            );
            Assert.IsTrue
            (
                parent.Session.TryAcquirePlacement
                (
                    "Parent.Host",
                    out var parentPlacement,
                    out var parentUsage
                )
            );
            parentUsage.Dispose();
            var parentRoot =
                ((IPresentationLayerDriver<RectTransform>)parentPlacement).Root;
            var firstHostObject = new GameObject("First Child Host", typeof(RectTransform));
            var secondHostObject = new GameObject("Second Child Host", typeof(RectTransform));
            firstHostObject.transform.SetParent(parentRoot, false);
            secondHostObject.transform.SetParent(parentRoot, false);
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("Screen", 0),
                },
                new[]
                {
                    Placement("Screen.View", "Screen", 0),
                }
            );
            var first = parent.Session.CreateChild
            (
                layout,
                firstHostObject.GetComponent<RectTransform>()
            );
            var second = parent.Session.CreateChild
            (
                layout,
                secondHostObject.GetComponent<RectTransform>()
            );
            Lease firstUsage = null;
            Lease secondUsage = null;

            try
            {
                Assert.AreNotSame(first.LayerRegistry, second.LayerRegistry);
                Assert.IsTrue(first.LayerRegistry.Contains("Screen"));
                Assert.IsTrue(second.LayerRegistry.Contains("Screen"));
                Assert.IsTrue
                (
                    first.TryAcquirePlacement
                    (
                        "Screen.View",
                        out var firstPlacement,
                        out firstUsage
                    )
                );
                Assert.IsTrue
                (
                    second.TryAcquirePlacement
                    (
                        "Screen.View",
                        out var secondPlacement,
                        out secondUsage
                    )
                );
                Assert.AreNotSame
                (
                    ((IPresentationLayerDriver<RectTransform>)firstPlacement).Root,
                    ((IPresentationLayerDriver<RectTransform>)secondPlacement).Root
                );

                firstUsage.Dispose();
                firstUsage = null;
                first.Dispose();

                Assert.IsTrue(first.IsDisposed);
                Assert.IsFalse(second.IsDisposed);
                Assert.IsTrue(second.LayerRegistry.Contains("Screen"));
            }
            finally
            {
                secondUsage?.Dispose();
                firstUsage?.Dispose();

                if (!second.IsDisposed)
                {
                    second.Dispose();
                }

                if (!first.IsDisposed)
                {
                    first.Dispose();
                }
            }
        }

    #endregion

    #region L-1: 활성 소비자 수명

        [Test]
        public void TEST_PresentationSession_활성PlacementUsage_Dispose거부후반환뒤종료()
        {
            var scope = PresentationTestScope.CreateUGUI
            (
                "Screen",
                ("Screen.View", 0)
            );
            Lease usage = null;

            try
            {
                Assert.IsTrue
                (
                    scope.Session.TryAcquirePlacement
                    (
                        "Screen.View",
                        out _,
                        out usage
                    )
                );

                Assert.Throws<InvalidOperationException>(scope.Session.Dispose);
                Assert.IsFalse(scope.Session.IsDisposed);

                usage.Dispose();
                usage = null;

                Assert.DoesNotThrow(scope.Session.Dispose);
                Assert.IsTrue(scope.Session.IsDisposed);
            }
            finally
            {
                usage?.Dispose();
                scope.Dispose();
            }
        }

    #endregion

    }
}
