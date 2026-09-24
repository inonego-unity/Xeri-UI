/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIPresentationPlacementHost.cs
수정일 : 2026-09-29

# 설명
Scene-authored UGUI RectTransform 하나를 특정 Presentation의 optional Placement Root로 제공한다.
Root는 Layer ManagedRoot의 direct child여야 하며 LocalOrder 적용 뒤 Session 종료 시 authored sibling index를 복원한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Scene-authored UGUI Placement Root Host.
    /// </summary>
    // ============================================================
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UGUIPresentationPlacementHost : PresentationPlacementHost
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Scene에 authoring된 Placement Root.
        /// </summary>
        // ------------------------------------------------------------
        public RectTransform Root => transform as RectTransform;

        internal override PresentationBackend Backend => PresentationBackend.UGUI;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// authored RectTransform을 Placement Driver로 borrow한다.
        /// </summary>
        // ------------------------------------------------------------
        internal override IPresentationLayerDriver AcquireCore
        (
            PresentationPlanPlacement plan,
            IPresentationLayerDriver layerDriver,
            out Action release
        )
        {
            if
            (
                layerDriver is not IPresentationLayerDriver<RectTransform> ugui ||
                ugui.Root == null
            )
            {
                throw new InvalidOperationException
                (
                    $"UGUI Placement Host '{PresentationID}'에는 UGUI Layer가 필요합니다."
                );
            }

            var root = Root;

            if (root == null || !ReferenceEquals(root.parent, ugui.Root))
            {
                throw new InvalidOperationException
                (
                    $"UGUI Placement Host '{PresentationID}'는 " +
                    "Layer ManagedRoot의 direct child여야 합니다."
                );
            }

            var originalSiblingIndex = root.GetSiblingIndex();
            root.SetAsLastSibling();

            release = () =>
            {
                if (root == null || root.parent == null) return;

                var siblingIndex = Mathf.Clamp
                (
                    originalSiblingIndex,
                    0,
                    Mathf.Max(0, root.parent.childCount - 1)
                );
                root.SetSiblingIndex(siblingIndex);
            };
            return new UGUIPresentationLayer(root);
        }

    #endregion

    }
}
