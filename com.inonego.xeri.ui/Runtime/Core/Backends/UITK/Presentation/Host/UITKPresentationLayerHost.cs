/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationLayerHost.cs
수정일 : 2026-10-05

# 설명
Scene-authored UITK Layer의 PanelRenderer output과 VisualElementReference ManagedRoot를 Presentation Layer로 제공한다.
PanelRenderer visual tree의 authored static content는 유지하고 ManagedRoot subtree만 Xeri runtime placement 영역으로 사용한다.
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
        private bool resolvedCallbackRegistered = false;
        private bool unloadedCallbackRegistered = false;

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

            var hostObject = gameObject;
            var originalActive = hostObject.activeSelf;

            try
            {
                output.InitializeBorrowed(plan.LayerOrder);
                hostObject.SetActive(true);
                var layerRoot = output.RequireRoot();

                RegisterReferenceCallbacks();
                var resolvedManagedRoot = RequireManagedRoot(layerRoot);
                var driver = new UITKPresentationLayer(resolvedManagedRoot);
                release = () => ReleaseBorrowedState(output, hostObject, originalActive);
                return driver;
            }
            catch (Exception exception)
            {
                try
                {
                    ReleaseBorrowedState(output, hostObject, originalActive);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        $"UITK Layer Host '{LayerID}' materialization과 rollback이 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Borrowed output state, reference callback과 GameObject active 상태를
        /// <br/> 모두 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ReleaseBorrowedState
        (
            UITKPresentationOutput output,
            GameObject hostObject,
            bool originalActive
        )
        {
            var errors = new List<Exception>();

            try
            {
                if (output != null)
                {
                    output.ReleaseOutputState();
                }
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                UnregisterReferenceCallbacks();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
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
                $"UITK Layer Host '{LayerID}' borrowed state 복원이 실패했습니다.",
                errors
            );
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
            if (resolvedCallbackRegistered && unloadedCallbackRegistered) return;

            var errors = new List<Exception>();

            try
            {
                if (!resolvedCallbackRegistered)
                {
                    // accessor가 구독 후 throw해도 rollback 대상임을 먼저 기록한다.
                    resolvedCallbackRegistered = true;
                    managedRootReference.RegisterReferenceResolvedCallback
                    (
                        HandleManagedRootResolved
                    );
                }

                if (!unloadedCallbackRegistered)
                {
                    unloadedCallbackRegistered = true;
                    managedRootReference.RegisterReferenceUnloadedCallback
                    (
                        HandleManagedRootUnloaded
                    );
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
                "UITK Layer Host reference callback 등록과 롤백이 모두 실패했습니다.",
                errors
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// ManagedRoot reference 구독과 cached element를 역순 attempt-all로 해제한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void UnregisterReferenceCallbacks()
        {
            var errors = new List<Exception>();

            if (unloadedCallbackRegistered)
            {
                unloadedCallbackRegistered = false;

                try
                {
                    managedRootReference.UnregisterReferenceUnloadedCallback
                    (
                        HandleManagedRootUnloaded
                    );
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
                    managedRootReference.UnregisterReferenceResolvedCallback
                    (
                        HandleManagedRootResolved
                    );
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            managedRoot = null;

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "UITK Layer Host reference callback 해제가 실패했습니다.",
                errors
            );
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
