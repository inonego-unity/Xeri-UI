/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIPresentationLayerHost.cs
수정일 : 2026-09-29

# 설명
Scene-authored UGUI Layer의 Canvas Native Output과 explicit ManagedRoot를 Presentation Layer로 제공한다.
LayerRoot의 authored static content는 유지하고 ManagedRoot subtree만 Xeri runtime placement 영역으로 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Scene-authored UGUI Layer materialization Host.
    /// </summary>
    // ============================================================
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UGUIPresentationOutput))]
    public sealed class UGUIPresentationLayerHost : PresentationLayerHost
    {

    #region 필드

        // ----------------------------------------------------------------------
        /// <summary>
        /// Xeri가 Placement Root ordering/lifetime을 관리할 전용 subtree.
        /// </summary>
        // ----------------------------------------------------------------------
        public RectTransform ManagedRoot => managedRoot;

        [SerializeField]
        private RectTransform managedRoot = null;

        internal override PresentationBackend Backend => PresentationBackend.UGUI;

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// Scene UGUI Layer를 borrow하고 ManagedRoot Driver를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal override IPresentationLayerDriver AcquireCore
        (
            PresentationPlanLayer plan,
            out Action release
        )
        {
            var output = GetComponent<UGUIPresentationOutput>() ??
                throw new MissingComponentException
                (
                    $"UGUI Layer Host '{LayerID}'에 UGUIPresentationOutput이 없습니다."
                );
            var layerRoot = output.Root;

            if
            (
                managedRoot == null ||
                layerRoot == null ||
                ReferenceEquals(managedRoot, layerRoot) ||
                !managedRoot.IsChildOf(layerRoot)
            )
            {
                throw new InvalidOperationException
                (
                    $"UGUI Layer Host '{LayerID}'의 ManagedRoot는 " +
                    "Canvas LayerRoot 아래의 별도 subtree여야 합니다."
                );
            }

            var originalSortingOrder = output.Canvas.sortingOrder;
            var originalActive = gameObject.activeSelf;

            try
            {
                output.Initialize(plan.LayerOrder);
                gameObject.SetActive(true);
                var driver = new UGUIPresentationLayer(managedRoot);
                release = () =>
                {
                    if (output != null && output.Canvas != null)
                    {
                        output.Canvas.sortingOrder = originalSortingOrder;
                    }

                    if (gameObject != null)
                    {
                        gameObject.SetActive(originalActive);
                    }
                };
                return driver;
            }
            catch
            {
                if (output.Canvas != null)
                {
                    output.Canvas.sortingOrder = originalSortingOrder;
                }

                gameObject.SetActive(originalActive);
                throw;
            }
        }

    #endregion

    }
}
