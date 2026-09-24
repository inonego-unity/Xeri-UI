/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationSurface.cs
수정일 : 2026-09-29

# 설명
Embedded UITK Session이 부모 Panel Native Output을 공유하면서 Plan Layer roots를 containment한다.
Surface, Layer와 Placement containment root는 child hit-test를 막지 않도록 자체 picking을 받지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    internal sealed class UITKPresentationSurface : PresentationSurface
    {

    #region 필드

        public VisualElement Root { get; private set; }

    #endregion

    #region 생성자

        public UITKPresentationSurface
        (
            VisualElement parent,
            IReadOnlyList<PresentationPlanLayer> layers
        )
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (layers == null)
            {
                throw new ArgumentNullException(nameof(layers));
            }

            Root = CreateRoot("xeri-presentation-surface");
            parent.Add(Root);

            try
            {
                for (var index = 0; index < layers.Count; index++)
                {
                    AddLayer(layers[index]);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

    #endregion

    #region Layer 구성

        private void AddLayer(PresentationPlanLayer plan)
        {
            var root = CreateRoot($"xeri-layer-{plan.LayerID}");
            Root.Add(root);
            var driver = new UITKPresentationLayer(root);
            AddLayer(plan, driver, root.RemoveFromHierarchy);
        }

        protected override void ApplyLayerOrder(IReadOnlyList<IPresentationLayerDriver> orderedDrivers)
        {
            for (var index = 0; index < orderedDrivers.Count; index++)
            {
                if (orderedDrivers[index] is not IPresentationLayerDriver<VisualElement> layer)
                {
                    throw new InvalidOperationException("UITK Surface에 다른 backend Layer가 등록되었습니다.");
                }

                layer.Root.BringToFront();
            }
        }

    #endregion

    #region Root 수명

        protected override void ReleaseSurfaceRoot()
        {
            var root = Root;
            Root = null;
            root?.RemoveFromHierarchy();
        }

        internal static VisualElement CreateRoot(string name)
        {
            var root = new VisualElement
            {
                name = name,
                pickingMode = PickingMode.Ignore,
            };
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;
            return root;
        }

    #endregion

    }
}
