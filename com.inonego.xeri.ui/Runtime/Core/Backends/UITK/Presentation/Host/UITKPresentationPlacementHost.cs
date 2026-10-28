/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationPlacementHost.cs
수정일 : 2026-10-21

# 설명
VisualElementReference로 지정한 authored element를 특정 Presentation의 optional Placement Root로 제공한다.
Root는 Layer ManagedRoot의 direct child여야 하며 LocalOrder z-index 적용 뒤 authored style을 복원한다.
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
        private IPresentationLayerDriver<VisualElement> parentLayer;
        private UITKPresentationLayer driver;
        private VisualElement borrowedRoot;
        private StyleInt originalZIndex;
        private int localOrder;
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

            try
            {
                parentLayer = uitk;
                localOrder = plan.LocalOrder;
                driver = new UITKPresentationLayer(null);
                if (parentLayer is UITKPresentationLayer observed)
                {
                    observed.RootChanged += HandleParentRootChanged;
                }

                RegisterReferenceCallbacks();
                RequireRoot(uitk.Root);
                RefreshRoot();
                release = ReleaseBorrowedState;
                return driver;
            }
            catch (Exception exception)
            {
                var errors = new System.Collections.Generic.List<Exception>
                {
                    exception,
                };

                try
                {
                    ReleaseBorrowedState();
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
        private void ReleaseBorrowedState()
        {
            var errors = new List<Exception>();

            if (parentLayer is UITKPresentationLayer observed)
            {
                observed.RootChanged -= HandleParentRootChanged;
            }

            parentLayer = null;
            try
            {
                BindRoot(null);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            driver = null;
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
        /// Layer와 Placement 참조의 resolve 순서에 관계없이 새 요소를 연결한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void RefreshRoot()
        {
            if (driver == null) return;
            var managedRoot = parentLayer?.Root;
            if (root == null || managedRoot == null)
            {
                BindRoot(null);
                return;
            }

            if (ReferenceEquals(root.parent, managedRoot))
            {
                BindRoot(root);
                return;
            }

            // 서로 다른 Native 트리는 상대 참조의 resolve를 기다린다.
            var tree = managedRoot;
            while (tree.parent != null)
            {
                tree = tree.parent;
            }

            BindRoot(null);
            if (tree.Contains(root))
            {
                RequireRoot(managedRoot);
            }
        }

        private void BindRoot(VisualElement value)
        {
            if (ReferenceEquals(borrowedRoot, value)) return;
            if (borrowedRoot != null)
            {
                borrowedRoot.style.zIndex = originalZIndex;
            }

            borrowedRoot = value;
            if (value != null)
            {
                originalZIndex = value.style.zIndex;
                value.style.zIndex = localOrder;
            }

            driver?.RebindRoot(value);
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
            RefreshRoot();
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
                RefreshRoot();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Host 파괴 경계에서 VisualElementReference callback을 해제한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void OnDestroy()
        {
            ReleaseBorrowedState();
        }

        private void HandleParentRootChanged(UITKPresentationLayer layer)
        {
            RefreshRoot();
        }

    #endregion

    }
}
