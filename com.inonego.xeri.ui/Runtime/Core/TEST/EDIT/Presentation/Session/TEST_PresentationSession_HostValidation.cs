/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_PresentationSession_HostValidation.cs
수정일 : 2026-09-29

# 설명
PresentationSession의 explicit Scene Host collection 사전 검증과 activation rollback 계약을 검증한다.

# 테스트 구성
 V: LayerHost / PlacementHost topology 사전 검증
 R: top-level materialization rollback
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.UI;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ======================================================================
    /// <summary>
    /// Scene Host collection validation과 materialization rollback 테스트.
    /// </summary>
    // ======================================================================
    public sealed class TEST_PresentationSession_HostValidation
    {

    #region 필드

        private readonly List<UnityEngine.Object> ownedObjects = new();

    #endregion

    #region 헬퍼

        private GameObject CreateOwnedRoot(string name)
        {
            var root = new GameObject(name);
            ownedObjects.Add(root);
            return root;
        }

        private static PresentationPlanLayer Layer
        (
            string id,
            PresentationBackend backend
        )
        {
            return new PresentationPlanLayer(id, id, 0, backend);
        }

        private static PresentationPlan CreatePlan
        (
            IReadOnlyList<PresentationPlanLayer> layers,
            IReadOnlyList<PresentationPlanPlacement> placements
        )
        {
            return new PresentationPlan
            (
                new List<PresentationPlanLayer>(layers),
                new List<PresentationPlanPlacement>(placements)
            );
        }

        private static UGUIPresentationLayerHost CreateLayerHost
        (
            Transform parent,
            string layerID,
            string name
        )
        {
            var hostObject = new GameObject
            (
                name,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UGUIPresentationOutput),
                typeof(UGUIPresentationLayerHost)
            );
            hostObject.transform.SetParent(parent, false);
            hostObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var host = hostObject.GetComponent<UGUIPresentationLayerHost>();
            SetField(host, "layerID", layerID);
            return host;
        }

        private static UGUIPresentationPlacementHost CreateUGUIPlacementHost
        (
            Transform parent,
            string presentationID,
            string name
        )
        {
            var hostObject = new GameObject
            (
                name,
                typeof(RectTransform),
                typeof(UGUIPresentationPlacementHost)
            );
            hostObject.transform.SetParent(parent, false);
            var host = hostObject.GetComponent<UGUIPresentationPlacementHost>();
            SetField(host, "presentationID", presentationID);
            return host;
        }

        private static UITKPresentationPlacementHost CreateUITKPlacementHost
        (
            Transform parent,
            string presentationID,
            string name
        )
        {
            var hostObject = new GameObject
            (
                name,
                typeof(UITKPresentationPlacementHost)
            );
            hostObject.transform.SetParent(parent, false);
            var host = hostObject.GetComponent<UITKPresentationPlacementHost>();
            SetField(host, "presentationID", presentationID);
            return host;
        }

        private UGUIPresentationOutput CreateOutputTemplate()
        {
            var templateObject = new GameObject
            (
                "UGUI Output Template",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UGUIPresentationOutput)
            );
            templateObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            templateObject.SetActive(false);
            ownedObjects.Add(templateObject);
            return templateObject.GetComponent<UGUIPresentationOutput>();
        }

        private static void SetPlacementHosts
        (
            PresentationLayerHost layerHost,
            params PresentationPlacementHost[] placementHosts
        )
        {
            SetField(layerHost, "placementHosts", placementHosts);
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

    #region V-1: LayerHost identity와 backend

        // ----------------------------------------------------------------------
        /// <summary>
        /// unknown 또는 duplicate LayerID를 materialization 전에 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationSession_LayerHostIdentity_InvalidCollection_Activation전거부()
        {
            var layer = Layer("A", PresentationBackend.UGUI);
            var plan = CreatePlan
            (
                new[]
                {
                    layer,
                },
                Array.Empty<PresentationPlanPlacement>()
            );
            var runtimeRoot = CreateOwnedRoot("Runtime Root");
            var first = CreateLayerHost(runtimeRoot.transform, "Unknown", "First Host");

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationSession.CreateTopLevel
                (
                    plan,
                    runtimeRoot.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        first,
                    }
                )
            );
            Assert.AreEqual(1, runtimeRoot.transform.childCount);

            SetField(first, "layerID", "A");
            var second = CreateLayerHost(runtimeRoot.transform, "A", "Second Host");

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationSession.CreateTopLevel
                (
                    plan,
                    runtimeRoot.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        first,
                        second,
                    }
                )
            );
            Assert.AreEqual(2, runtimeRoot.transform.childCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// concrete Host backend가 Plan backend와 다르면 activation 전에 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationSession_LayerHostBackend_Plan과불일치_Activation전거부()
        {
            var layer = Layer("A", PresentationBackend.UITK);
            var plan = CreatePlan
            (
                new[]
                {
                    layer,
                },
                Array.Empty<PresentationPlanPlacement>()
            );
            var runtimeRoot = CreateOwnedRoot("Runtime Root");
            var host = CreateLayerHost(runtimeRoot.transform, "A", "UGUI Host");

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationSession.CreateTopLevel
                (
                    plan,
                    runtimeRoot.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        host,
                    }
                )
            );
            Assert.AreEqual(1, runtimeRoot.transform.childCount);
        }

    #endregion

    #region V-2: PlacementHost identity와 backend

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> null, unknown, duplicate PresentationID와 backend mismatch를
        /// <br/> Layer materialization 전에 모두 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationSession_PlacementHostCollection_InvalidTopology_Activation전거부()
        {
            var layer = Layer("A", PresentationBackend.UGUI);
            var placement = new PresentationPlanPlacement("A.View", layer, 10);
            var plan = CreatePlan
            (
                new[]
                {
                    layer,
                },
                new[]
                {
                    placement,
                }
            );
            var runtimeRoot = CreateOwnedRoot("Runtime Root");
            var layerHost = CreateLayerHost(runtimeRoot.transform, "A", "Layer Host");

            SetPlacementHosts(layerHost, null);
            Assert.Throws<InvalidOperationException>
            (
                () => PresentationSession.CreateTopLevel
                (
                    plan,
                    runtimeRoot.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        layerHost,
                    }
                )
            );

            var first = CreateUGUIPlacementHost
            (
                layerHost.transform,
                "Unknown",
                "Unknown Placement"
            );
            SetPlacementHosts(layerHost, first);
            Assert.Throws<InvalidOperationException>
            (
                () => PresentationSession.CreateTopLevel
                (
                    plan,
                    runtimeRoot.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        layerHost,
                    }
                )
            );

            SetField(first, "presentationID", "A.View");
            var second = CreateUGUIPlacementHost
            (
                layerHost.transform,
                "A.View",
                "Duplicate Placement"
            );
            SetPlacementHosts(layerHost, first, second);
            Assert.Throws<InvalidOperationException>
            (
                () => PresentationSession.CreateTopLevel
                (
                    plan,
                    runtimeRoot.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        layerHost,
                    }
                )
            );

            var wrongBackend = CreateUITKPlacementHost
            (
                layerHost.transform,
                "A.View",
                "UITK Placement"
            );
            SetPlacementHosts(layerHost, wrongBackend);
            Assert.Throws<InvalidOperationException>
            (
                () => PresentationSession.CreateTopLevel
                (
                    plan,
                    runtimeRoot.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        layerHost,
                    }
                )
            );
            Assert.AreEqual(1, runtimeRoot.transform.childCount);
        }

    #endregion

    #region R-1: top-level materialization rollback

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Generated Layer를 먼저 만든 뒤 Scene Host materialization이 실패하면
        /// <br/> 이전 generated Layer를 rollback으로 제거한다.
        /// <br/> 실패한 Scene Host의 외부 active/order 상태도 보존한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_PresentationSession_LayerHostMaterialization실패_이전GeneratedLayerRollback()
        {
            var firstLayer = new PresentationPlanLayer
            (
                "Generated",
                "Generated",
                10,
                PresentationBackend.UGUI
            );
            var secondLayer = new PresentationPlanLayer
            (
                "Scene",
                "Scene",
                20,
                PresentationBackend.UGUI
            );
            var plan = CreatePlan
            (
                new[]
                {
                    firstLayer,
                    secondLayer,
                },
                Array.Empty<PresentationPlanPlacement>()
            );
            var runtimeRoot = CreateOwnedRoot("Runtime Root");
            var sceneHost = CreateLayerHost
            (
                runtimeRoot.transform,
                "Scene",
                "Invalid Scene Host"
            );
            var sceneCanvas = sceneHost.GetComponent<Canvas>();
            sceneCanvas.sortingOrder = 37;
            sceneHost.gameObject.SetActive(false);
            var outputTemplate = CreateOutputTemplate();

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => PresentationSession.CreateTopLevel
                (
                    plan,
                    runtimeRoot.transform,
                    null,
                    outputTemplate,
                    layerHosts: new PresentationLayerHost[]
                    {
                        sceneHost,
                    }
                )
            );

            Assert.That(exception.Message, Does.Contain("ManagedRoot"));
            Assert.AreEqual(1, runtimeRoot.transform.childCount);
            Assert.AreSame(sceneHost.transform, runtimeRoot.transform.GetChild(0));
            Assert.IsFalse(sceneHost.gameObject.activeSelf);
            Assert.AreEqual(37, sceneCanvas.sortingOrder);
        }

    #endregion

    #region R-2: Placement materialization rollback

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> UGUI PlacementHost는 ManagedRoot의 direct child여야 한다.
        /// <br/> 조건이 어긋나면 activation을 거부한다.
        /// <br/> 이미 borrow한 LayerHost의 active/order도 rollback으로 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationSession_UGUIPlacementHost_ManagedRootDirectChild아니면LayerHostRollback()
        {
            var layer = Layer("A", PresentationBackend.UGUI);
            var placement = new PresentationPlanPlacement("A.View", layer, 10);
            var plan = CreatePlan
            (
                new[]
                {
                    layer,
                },
                new[]
                {
                    placement,
                }
            );
            var runtimeRoot = CreateOwnedRoot("Runtime Root");
            var layerHost = CreateLayerHost
            (
                runtimeRoot.transform,
                "A",
                "Scene Layer Host"
            );
            var canvas = layerHost.GetComponent<Canvas>();
            canvas.sortingOrder = 37;
            var layerRoot = layerHost.GetComponent<RectTransform>();
            var managedRoot = UGUIPresentationSurface.CreateRect
            (
                "Managed Root",
                layerRoot
            );
            var nestedRoot = UGUIPresentationSurface.CreateRect
            (
                "Nested Root",
                managedRoot
            );
            var placementHost = CreateUGUIPlacementHost
            (
                nestedRoot,
                "A.View",
                "Nested Placement"
            );
            SetField(layerHost, "managedRoot", managedRoot);
            SetPlacementHosts(layerHost, placementHost);
            layerHost.gameObject.SetActive(false);

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => PresentationSession.CreateTopLevel
                (
                    plan,
                    runtimeRoot.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        layerHost,
                    }
                )
            );

            Assert.That(exception.Message, Does.Contain("direct child"));
            Assert.IsFalse(layerHost.gameObject.activeSelf);
            Assert.AreEqual(37, canvas.sortingOrder);
            Assert.AreSame(nestedRoot, placementHost.transform.parent);
            Assert.AreEqual(1, runtimeRoot.transform.childCount);
        }

    #endregion

    }
}