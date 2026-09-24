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
        private bool referenceCallbacksRegistered = false;

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

            RegisterReferenceCallbacks();

            try
            {
                var resolvedRoot = RequireRoot(uitk.Root);
                var originalZIndex = resolvedRoot.style.zIndex;
                resolvedRoot.style.zIndex = plan.LocalOrder;
                release = () =>
                {
                    try
                    {
                        if (resolvedRoot != null)
                        {
                            resolvedRoot.style.zIndex = originalZIndex;
                        }
                    }
                    finally
                    {
                        UnregisterReferenceCallbacks();
                    }
                };
                return new UITKPresentationLayer(resolvedRoot);
            }
            catch
            {
                UnregisterReferenceCallbacks();
                throw;
            }
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
            if (referenceCallbacksRegistered) return;

            rootReference.RegisterReferenceResolvedCallback(HandleRootResolved);
            rootReference.RegisterReferenceUnloadedCallback(HandleRootUnloaded);
            referenceCallbacksRegistered = true;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Placement Root reference 구독과 cached element를 대칭 해제한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void UnregisterReferenceCallbacks()
        {
            if (!referenceCallbacksRegistered) return;

            rootReference.UnregisterReferenceResolvedCallback(HandleRootResolved);
            rootReference.UnregisterReferenceUnloadedCallback(HandleRootUnloaded);
            referenceCallbacksRegistered = false;
            root = null;
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
