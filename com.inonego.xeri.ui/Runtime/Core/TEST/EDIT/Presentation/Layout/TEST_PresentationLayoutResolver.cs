/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_PresentationLayoutResolver.cs
수정일 : 2026-09-24

# 설명
PresentationLayoutResolver의 순수 validation, deterministic order와 immutable PresentationPlan 계약을 검증한다.

# 테스트 구성
 R: Layer/Placement resolve와 ordering
 V: ID·order·output topology validation
 E: Embedded shared-output 제약
 I: Layout과 분리된 immutable Plan snapshot
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ================================================================================
    /// <summary>
    /// PresentationLayout을 runtime mutation 없이 Plan으로 resolve하는 계약 테스트.
    /// </summary>
    // ================================================================================
    public sealed class TEST_PresentationLayoutResolver
    {

    #region 헬퍼

        private readonly List<UnityEngine.Object> ownedObjects = new();

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
            PresentationBackend backend = PresentationBackend.UITK
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

    #region R-1: Resolve 결과와 조회

        [Test]
        public void TEST_PresentationLayoutResolver_Layer와Placement_Order기준정렬()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("Overlay", 300),
                    Layer("HUD", 100),
                    Layer("Modal", 200),
                },
                new[]
                {
                    Placement("HUD.B", "HUD", 20),
                    Placement("Modal.A", "Modal", 0),
                    Placement("HUD.A", "HUD", 10),
                }
            );

            var plan = PresentationLayoutResolver.Resolve(layout);

            Assert.AreEqual("HUD", plan.Layers[0].LayerID);
            Assert.AreEqual("Modal", plan.Layers[1].LayerID);
            Assert.AreEqual("Overlay", plan.Layers[2].LayerID);
            Assert.AreEqual("HUD.A", plan.Placements[0].PresentationID);
            Assert.AreEqual("HUD.B", plan.Placements[1].PresentationID);
            Assert.AreEqual("Modal.A", plan.Placements[2].PresentationID);
        }

        [Test]
        public void TEST_PresentationLayoutResolver_PresentationID조회_Layer와LocalOrder보존()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("HUD", 100),
                },
                new[]
                {
                    Placement("Gameplay.HUD", "HUD", 30),
                }
            );

            var plan = PresentationLayoutResolver.Resolve(layout);

            Assert.IsTrue(plan.TryGetPlacement("Gameplay.HUD", out var placement));
            Assert.AreEqual("HUD", placement.Layer.LayerID);
            Assert.AreEqual(100, placement.Layer.LayerOrder);
            Assert.AreEqual(30, placement.LocalOrder);
        }

    #endregion

    #region R-2: LocalOrder Layer boundary

        [Test]
        public void TEST_PresentationLayoutResolver_다른Layer동일LocalOrder_LayerOrder우선정렬()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("Modal", 200),
                    Layer("Screen", 100),
                },
                new[]
                {
                    Placement("Modal.View", "Modal", 0),
                    Placement("Screen.View", "Screen", 0),
                }
            );

            var plan = PresentationLayoutResolver.Resolve(layout);

            Assert.AreEqual("Screen.View", plan.Placements[0].PresentationID);
            Assert.AreEqual("Modal.View", plan.Placements[1].PresentationID);
            Assert.AreEqual(0, plan.Placements[0].LocalOrder);
            Assert.AreEqual(0, plan.Placements[1].LocalOrder);
        }

    #endregion

    #region V-1: ID와 참조 validation

        [Test]
        public void TEST_PresentationLayoutResolver_중복LayerID_거부()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("HUD", 100),
                    Layer("HUD", 200),
                },
                Array.Empty<PresentationPlacementDefinition>()
            );

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationLayoutResolver.Resolve(layout)
            );
        }

        [Test]
        public void TEST_PresentationLayoutResolver_중복PresentationID_거부()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("HUD", 100),
                    Layer("Modal", 200),
                },
                new[]
                {
                    Placement("Shared", "HUD", 0),
                    Placement("Shared", "Modal", 0),
                }
            );

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationLayoutResolver.Resolve(layout)
            );
        }

        [Test]
        public void TEST_PresentationLayoutResolver_정의되지않은LayerPlacement_거부()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("HUD", 100),
                },
                new[]
                {
                    Placement("A", "Missing", 0),
                }
            );

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationLayoutResolver.Resolve(layout)
            );
        }

    #endregion

    #region V-2: Order conflict와 native 범위

        [Test]
        public void TEST_PresentationLayoutResolver_중복LayerOrder_거부()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("HUD", 100),
                    Layer("Modal", 100),
                },
                Array.Empty<PresentationPlacementDefinition>()
            );

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationLayoutResolver.Resolve(layout)
            );
        }

        [Test]
        public void TEST_PresentationLayoutResolver_동일Layer중복LocalOrder_거부()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("HUD", 100),
                },
                new[]
                {
                    Placement("A", "HUD", 0),
                    Placement("B", "HUD", 0),
                }
            );

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationLayoutResolver.Resolve(layout)
            );
        }

        [Test]
        public void TEST_PresentationLayoutResolver_ScreenOverlayLayerOrder_Native범위초과거부()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("Overlay", short.MaxValue + 1),
                },
                Array.Empty<PresentationPlacementDefinition>()
            );

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationLayoutResolver.Resolve(layout)
            );
        }

    #endregion

    #region I-1: immutable snapshot

        [Test]
        public void TEST_PresentationPlan_ReadOnlyCollection_외부변경거부()
        {
            var layout = CreateLayout
            (
                new[]
                {
                    Layer("HUD", 100),
                },
                new[]
                {
                    Placement("HUD.View", "HUD", 0),
                }
            );
            var plan = PresentationLayoutResolver.Resolve(layout);
            var layers = (IList)plan.Layers;
            var placements = (IList)plan.Placements;

            Assert.Throws<NotSupportedException>
            (
                () => layers.Add(PresentationTestScope.CreateLayer("Other", 200))
            );
            Assert.Throws<NotSupportedException>
            (
                () => placements.Clear()
            );
        }

    #endregion

    }
}
