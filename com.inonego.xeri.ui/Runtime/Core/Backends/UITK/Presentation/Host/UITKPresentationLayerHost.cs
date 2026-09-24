/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationLayerHost.cs
수정일 : 2026-09-30

# 설명
Scene-authored UITK Layer의 PanelRenderer output과 VisualElementReference ManagedRoot를 Presentation Layer로 제공한다.
PanelRenderer visual tree의 authored static content는 유지하고 ManagedRoot subtree만 Xeri runtime placement 영역으로 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Scene-authored UITK Layer materialization Host.
    /// </summary>
    // ============================================================
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    [RequireComponent(typeof(UITKPresentationOutput))]
    public sealed class UITKPresentationLayerHost : PresentationLayerHost
    {

    #region 필드

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Xeri가 Placement Root ordering/lifetime을 관리할 authored subtree reference.
        /// </summary>
        // --------------------------------------------------------------------------------
        public VisualElementReference ManagedRootReference => managedRootReference;

        [SerializeField]
        private VisualElementReference managedRootReference = new();

        internal override PresentationBackend Backend => PresentationBackend.UITK;

        private VisualElement managedRoot = null;
        private bool referenceCallbacksRegistered = false;

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// Scene UITK Layer를 borrow하고 ManagedRoot Driver를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal override IPresentationLayerDriver AcquireCore
        (
            PresentationPlanLayer plan,
            out Action release
        )
        {
            var output = GetComponent<UITKPresentationOutput>() ??
                throw new MissingComponentException
                (
                    $"UITK Layer Host '{LayerID}'에 UITKPresentationOutput이 없습니다."
                );
            var panelRenderer = output.PanelRenderer;

            if
            (
                panelRenderer == null ||
                managedRootReference.panelRenderer != panelRenderer
            )
            {
                throw new InvalidOperationException
                (
                    $"UITK Layer Host '{LayerID}' ManagedRoot reference는 " +
                    "같은 GameObject의 PanelRenderer를 가리켜야 합니다."
                );
            }

            var originalActive = gameObject.activeSelf;

            try
            {
                output.InitializeBorrowed(plan.LayerOrder);
                gameObject.SetActive(true);
                var layerRoot = output.RequireRoot();

                RegisterReferenceCallbacks();
                var resolvedManagedRoot = RequireManagedRoot(layerRoot);
                var driver = new UITKPresentationLayer(resolvedManagedRoot);
                release = () =>
                {
                    try
                    {
                        output.ReleaseRuntimeSettings();
                    }
                    finally
                    {
                        UnregisterReferenceCallbacks();

                        if (gameObject != null)
                        {
                            gameObject.SetActive(originalActive);
                        }
                    }
                };
                return driver;
            }
            catch
            {
                try
                {
                    output.ReleaseRuntimeSettings();
                }
                finally
                {
                    UnregisterReferenceCallbacks();
                    gameObject.SetActive(originalActive);
                }

                throw;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// resolved ManagedRoot가 현재 LayerRoot 아래의 별도 subtree인지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private VisualElement RequireManagedRoot(VisualElement layerRoot)
        {
            if
            (
                managedRoot == null ||
                ReferenceEquals(managedRoot, layerRoot) ||
                !layerRoot.Contains(managedRoot)
            )
            {
                throw new InvalidOperationException
                (
                    $"UITK Layer Host '{LayerID}'의 ManagedRoot는 " +
                    "PanelRenderer LayerRoot 아래의 별도 subtree여야 합니다."
                );
            }

            return managedRoot;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// ManagedRoot reference의 resolve/unload lifecycle을 구독한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void RegisterReferenceCallbacks()
        {
            if (referenceCallbacksRegistered) return;

            managedRootReference.RegisterReferenceResolvedCallback
            (
                HandleManagedRootResolved
            );
            managedRootReference.RegisterReferenceUnloadedCallback
            (
                HandleManagedRootUnloaded
            );
            referenceCallbacksRegistered = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// ManagedRoot reference 구독과 cached element를 대칭 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnregisterReferenceCallbacks()
        {
            if (!referenceCallbacksRegistered) return;

            managedRootReference.UnregisterReferenceResolvedCallback
            (
                HandleManagedRootResolved
            );
            managedRootReference.UnregisterReferenceUnloadedCallback
            (
                HandleManagedRootUnloaded
            );
            referenceCallbacksRegistered = false;
            managedRoot = null;
        }

    #endregion

    #region 이벤트 핸들러

        // ----------------------------------------------------------------------
        /// <summary>
        /// authoring reference가 resolve되면 현재 ManagedRoot를 갱신한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleManagedRootResolved(VisualElement element)
        {
            managedRoot = element;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// authoring reference가 unload되면 cached ManagedRoot를 무효화한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleManagedRootUnloaded(VisualElement element)
        {
            if (ReferenceEquals(managedRoot, element))
            {
                managedRoot = null;
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
