/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKModal.cs
수정일 : 2026-10-07

# 설명
일반 UITK Modal 사용에서 PresentationTarget Layer Lease 획득, hierarchy attach/detach,
Presentation/Interaction adapter 조립을 기존 ModalController primitive 위에 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Modal의 Tier 1 조립 helper.
    /// </summary>
    // ============================================================
    public static class UITKModal
    {

    #region 기본 경로

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Presentation Target에 Root를 붙여 Modal Session을 연다.
        /// </summary>
        // ------------------------------------------------------------
        public static ModalSession Open
        (
            UIContext context,
            PresentationTarget target,
            VisualElement root
        )
        {
            return OpenTarget(context, target, root, null);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Presentation Target에 Root와 Focus Scope를 함께 연다.
        /// </summary>
        // ------------------------------------------------------------
        public static ModalSession OpenWithFocus
        (
            UIContext context,
            PresentationTarget target,
            VisualElement root,
            IFocusScope focusScope
        )
        {
            if (focusScope == null)
            {
                throw new ArgumentNullException(nameof(focusScope));
            }

            return OpenTarget(context, target, root, focusScope);
        }

    #endregion

    #region 합성 API

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 이미 조립된 UITK Root를 Modal Stack에 열고 전달 lifetime을 Session에 이전한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public static ModalSession Open
        (
            UIContext context,
            VisualElement root,
            params IDisposable[] ownedLifetimes
        )
        {
            ValidateComposition(context, root);
            var restoreInteraction = CreateInteractionRestore(root);

            try
            {
                return context.Modals.Open
                (
                    new UITKPresentation(root),
                    new UITKModalInteractionDriver(root),
                    PrependOwnedLifetime(ownedLifetimes, restoreInteraction)
                );
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };
                TryDispose(restoreInteraction, errors);

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "UITK Modal 조립과 interaction 상태 rollback이 모두 실패했습니다.",
                    errors
                );
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이미 조립된 UITK Root와 Focus Scope를 Modal Stack에 함께 연다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static ModalSession OpenWithFocus
        (
            UIContext context,
            VisualElement root,
            IFocusScope focusScope,
            params IDisposable[] ownedLifetimes
        )
        {
            ValidateComposition(context, root);

            if (focusScope == null)
            {
                throw new ArgumentNullException(nameof(focusScope));
            }

            var restoreInteraction = CreateInteractionRestore(root);

            try
            {
                return context.Modals.Open
                (
                    new UITKPresentation(root),
                    new UITKModalInteractionDriver(root),
                    focusScope,
                    PrependOwnedLifetime(ownedLifetimes, restoreInteraction)
                );
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };
                TryDispose(restoreInteraction, errors);

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "UITK Modal 조립과 interaction 상태 rollback이 모두 실패했습니다.",
                    errors
                );
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Modal adapter가 변경하는 UITK interaction 상태의 복원 lifetime을 만든다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static Lease CreateInteractionRestore(VisualElement root)
        {
            var enabled = root.enabledSelf;
            var pickingMode = root.pickingMode;
            return new Lease
            (
                () =>
                {
                    var errors = new List<Exception>();

                    try
                    {
                        root.SetEnabled(enabled);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }

                    try
                    {
                        root.pickingMode = pickingMode;
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
                        "UITK Modal interaction 상태 복원이 실패했습니다.",
                        errors
                    );
                }
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 역순 해제에서 helper 복원이 마지막에 실행되도록 소유 lifetime 앞에 추가한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static IDisposable[] PrependOwnedLifetime
        (
            IDisposable[] ownedLifetimes,
            IDisposable lifetime
        )
        {
            var count = ownedLifetimes?.Length ?? 0;
            var result = new IDisposable[count + 1];
            result[0] = lifetime;

            if (count > 0)
            {
                Array.Copy(ownedLifetimes, 0, result, 1, count);
            }

            return result;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Composition API가 요구하는 Context와 Root 참조를 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ValidateComposition
        (
            UIContext context,
            VisualElement root
        )
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.IsDisposing || context.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(UIContext));
            }

            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
        }

    #endregion

    #region 내부 처리

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Lease와 hierarchy lifetime을 Modal 조립에 이전한다.
        /// </summary>
        // ------------------------------------------------------------
        private static ModalSession OpenTarget
        (
            UIContext context,
            PresentationTarget target,
            VisualElement root,
            IFocusScope focusScope
        )
        {
            ValidateComposition(context, root);

            if (!target.IsValid)
            {
                throw new ArgumentException
                (
                    "Modal Presentation Target이 유효하지 않습니다.",
                    nameof(target)
                );
            }

            if (root.parent != null)
            {
                throw new InvalidOperationException
                (
                    "UITK Modal Root는 다른 hierarchy에 연결되지 않은 상태여야 합니다."
                );
            }

            PresentationLayerLease layerLease = null;
            Lease hierarchyLifetime = null;

            try
            {
                layerLease = context.AcquireLayer(target);

                if (layerLease.Layer is not IPresentationLayerDriver<VisualElement> layer)
                {
                    throw new InvalidOperationException
                    (
                        $"Modal Presentation Target '{target}'이 UI Toolkit Layer가 아닙니다."
                    );
                }

                layer.Root.Add(root);
                hierarchyLifetime = new Lease(root.RemoveFromHierarchy);

                return focusScope == null
                    ? Open(context, root, layerLease, hierarchyLifetime)
                    : OpenWithFocus
                    (
                        context,
                        root,
                        focusScope,
                        layerLease,
                        hierarchyLifetime
                    );
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };
                TryDispose(hierarchyLifetime, errors);
                TryDispose(layerLease, errors);

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "UITK Modal Layer Lease 조립과 소유권 rollback이 실패했습니다.",
                    errors
                );
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Modal helper가 획득한 lifetime을 독립적으로 반환하고 실패를 수집한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void TryDispose
        (
            IDisposable lifetime,
            List<Exception> errors
        )
        {
            if (lifetime == null) return;

            try
            {
                lifetime.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

    #endregion

    }
}
