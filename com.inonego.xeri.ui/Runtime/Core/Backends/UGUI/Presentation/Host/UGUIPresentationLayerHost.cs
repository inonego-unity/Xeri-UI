/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIPresentationLayerHost.cs
수정일 : 2026-10-05

# 설명
Scene-authored UGUI Layer의 Canvas Native Output과 explicit ManagedRoot를 Presentation Layer로 제공한다.
LayerRoot의 authored static content는 유지하고 ManagedRoot subtree만 Xeri runtime placement 영역으로 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

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
            var hostObject = gameObject;
            var originalActive = hostObject.activeSelf;

            try
            {
                output.Initialize(plan.LayerOrder);
                hostObject.SetActive(true);
                var driver = new UGUIPresentationLayer(managedRoot);
                release = () => ReleaseBorrowedState
                (
                    output,
                    hostObject,
                    originalSortingOrder,
                    originalActive
                );
                return driver;
            }
            catch (Exception exception)
            {
                try
                {
                    ReleaseBorrowedState
                    (
                        output,
                        hostObject,
                        originalSortingOrder,
                        originalActive
                    );
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        $"UGUI Layer Host '{LayerID}' materialization과 rollback이 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// borrowed Canvas order와 GameObject active 상태를 독립적으로 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ReleaseBorrowedState
        (
            UGUIPresentationOutput output,
            GameObject hostObject,
            int originalSortingOrder,
            bool originalActive
        )
        {
            var errors = new List<Exception>();

            if (output != null && output.Canvas != null)
            {
                try
                {
                    output.Canvas.sortingOrder = originalSortingOrder;
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (hostObject != null)
            {
                try
                {
                    hostObject.SetActive(originalActive);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                $"UGUI Layer Host '{LayerID}' borrowed state 복원이 실패했습니다.",
                errors
            );
        }

    #endregion

    }
}
