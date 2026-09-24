/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationPlacementHost.cs
수정일 : 2026-09-29

# 설명
VisualElementReference로 지정한 authored element를 특정 Presentation의 optional Placement Root로 제공한다.
Root는 Layer ManagedRoot의 direct child여야 하며 LocalOrder z-index 적용 뒤 authored style을 복원한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Scene-authored UITK Placement Root Host.
    /// </summary>
    // ============================================================
    [DisallowMultipleComponent]
    public sealed class UITKPresentationPlacementHost : PresentationPlacementHost
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// authored Placement Root VisualElement reference.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElementReference RootReference => rootReference;

        [SerializeField]
        private VisualElementReference rootReference = new();

        internal override PresentationBackend Backend => PresentationBackend.UITK;

        private VisualElement root = null;
        private bool resolvedCallbackRegistered = false;
        private bool unloadedCallbackRegistered = false;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// authored VisualElement를 Placement Driver로 borrow한다.
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
                layerDriver is not IPresentationLayerDriver<VisualElement> uitk ||
                uitk.Root == null
            )
            {
                throw new InvalidOperationException
                (
                    $"UITK Placement Host '{PresentationID}'에는 UITK Layer가 필요합니다."
                );
            }

            VisualElement resolvedRoot = null;
            var originalZIndex = default(StyleInt);
            var hasOriginalZIndex = false;

            try
            {
                RegisterReferenceCallbacks();
                resolvedRoot = RequireRoot(uitk.Root);
                originalZIndex = resolvedRoot.style.zIndex;
                hasOriginalZIndex = true;
                resolvedRoot.style.zIndex = plan.LocalOrder;
                release = () => ReleaseBorrowedState
                (
                    resolvedRoot,
                    originalZIndex,
                    restoreZIndex: true
                );
                return new UITKPresentationLayer(resolvedRoot);
            }
            catch (Exception exception)
            {
                var errors = new System.Collections.Generic.List<Exception>
                {
                    exception,
                };

                try
                {
                    ReleaseBorrowedState
                    (
                        resolvedRoot,
                        originalZIndex,
                        hasOriginalZIndex
                    );
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    $"UITK Placement Host '{PresentationID}' 획득과 rollback이 실패했습니다.",
                    errors
                );
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// borrowed z-index와 reference callback lifetime을 서로 독립적으로 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ReleaseBorrowedState
        (
            VisualElement resolvedRoot,
            StyleInt originalZIndex,
            bool restoreZIndex
        )
        {
            var errors = new System.Collections.Generic.List<Exception>();

            if (restoreZIndex && resolvedRoot != null)
            {
                try
                {
                    resolvedRoot.style.zIndex = originalZIndex;
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            try
            {
                UnregisterReferenceCallbacks();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "UITK Placement Host borrowed state 복원이 실패했습니다.",
                errors
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// resolved Placement Root가 ManagedRoot의 direct child인지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private VisualElement RequireRoot(VisualElement managedRoot)
        {
            if
            (
                root == null ||
                !ReferenceEquals(root.parent, managedRoot)
            )
            {
                throw new InvalidOperationException
                (
                    $"UITK Placement Host '{PresentationID}'는 " +
                    "Layer ManagedRoot의 direct child여야 합니다."
                );
            }

            return root;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Placement Root reference의 resolve/unload lifecycle을 구독한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void RegisterReferenceCallbacks()
        {
            if (resolvedCallbackRegistered && unloadedCallbackRegistered) return;

            var errors = new System.Collections.Generic.List<Exception>();

            try
            {
                if (!resolvedCallbackRegistered)
                {
                    resolvedCallbackRegistered = true;
                    rootReference.RegisterReferenceResolvedCallback(HandleRootResolved);
                }

                if (!unloadedCallbackRegistered)
                {
                    unloadedCallbackRegistered = true;
                    rootReference.RegisterReferenceUnloadedCallback(HandleRootUnloaded);
                }

                return;
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                UnregisterReferenceCallbacks();
            }
            catch (Exception cleanupException)
            {
                errors.Add(cleanupException);
            }

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "UITK Placement Host reference callback 등록과 롤백이 모두 실패했습니다.",
                errors
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Placement Root reference 구독과 cached element를 역순 attempt-all로 해제한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void UnregisterReferenceCallbacks()
        {
            var errors = new System.Collections.Generic.List<Exception>();

            if (unloadedCallbackRegistered)
            {
                unloadedCallbackRegistered = false;

                try
                {
                    rootReference.UnregisterReferenceUnloadedCallback(HandleRootUnloaded);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (resolvedCallbackRegistered)
            {
                resolvedCallbackRegistered = false;

                try
                {
                    rootReference.UnregisterReferenceResolvedCallback(HandleRootResolved);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            root = null;

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "UITK Placement Host reference callback 해제가 실패했습니다.",
                errors
            );
        }

    #endregion

    #region 이벤트 핸들러

        // ----------------------------------------------------------------------
        /// <summary>
        /// authoring reference가 resolve되면 현재 Placement Root를 갱신한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleRootResolved(VisualElement element)
        {
            root = element;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// authoring reference가 unload되면 cached Placement Root를 무효화한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleRootUnloaded(VisualElement element)
        {
            if (ReferenceEquals(root, element))
            {
                root = null;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Host 파괴 경계에서 VisualElementReference callback을 해제한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void OnDestroy()
        {
            UnregisterReferenceCallbacks();
        }

    #endregion

    }
}
