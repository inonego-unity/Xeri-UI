/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationSurface.cs
수정일 : 2026-10-05

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
    // ============================================================
    /// <summary>
    /// UITK 하위 Session의 배치와 표시 순서 경계를 소유한다.
    /// </summary>
    // ============================================================
    internal sealed class UITKPresentationSurface : PresentationSurface
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 하위 Session의 표시 순서를 격리하는 Root.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Root { get; private set; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 부모 안에 독립된 Surface와 Layer들을 구성한다.
        /// </summary>
        // ------------------------------------------------------------
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
            // Surface 밖의 형제 Window와 내부 Placement의 z-index를 격리한다.
            Root.style.zIndex = 0;
            parent.Add(Root);

            try
            {
                for (var index = 0; index < layers.Count; index++)
                {
                    AddLayer(layers[index]);
                }
            }
            catch (Exception exception)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        "UITK Presentation Surface 생성과 롤백이 모두 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

    #endregion

    #region 레이어 구성

        // ------------------------------------------------------------
        /// <summary>
        /// 계획에 지정된 Layer의 Root와 수명을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AddLayer(PresentationPlanLayer plan)
        {
            var root = CreateRoot($"xeri-layer-{plan.LayerID}");
            Root.Add(root);
            var driver = new UITKPresentationLayer(root);
            AddLayer(plan, driver, root.RemoveFromHierarchy);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 계획 순서를 계층과 Layer 표시 경계에 함께 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void ApplyLayerOrder(IReadOnlyList<IPresentationLayerDriver> orderedDrivers)
        {
            for (var index = 0; index < orderedDrivers.Count; index++)
            {
                if (orderedDrivers[index] is not IPresentationLayerDriver<VisualElement> layer)
                {
                    throw new InvalidOperationException("UITK Surface에 다른 backend Layer가 등록되었습니다.");
                }

                // 명시적 z-index가 Layer의 stacking context를 만들어 LocalOrder 유출을 막는다.
                layer.Root.style.zIndex = index;
                layer.Root.BringToFront();
            }
        }

    #endregion

    #region 루트 수명

        // ------------------------------------------------------------
        /// <summary>
        /// Surface를 부모 계층에서 분리한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void ReleaseSurfaceRoot()
        {
            var root = Root;
            Root = null;
            root?.RemoveFromHierarchy();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부모 영역을 채우며 자체 입력은 받지 않는 Root를 만든다.
        /// </summary>
        // ------------------------------------------------------------
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
