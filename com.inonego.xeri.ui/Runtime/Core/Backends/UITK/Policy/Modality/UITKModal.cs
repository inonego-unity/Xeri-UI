/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKModal.cs
수정일 : 2026-09-30

# 설명
일반 UITK Modal 사용에서 Presentation placement 획득, hierarchy attach/detach,
Presentation/Interaction adapter 조립을 기존 ModalController primitive 위에 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Modal의 Common API 조립 helper.
    /// </summary>
    // ============================================================
    public static class UITKModal
    {

    #region Golden Path

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 현재 Context의 Presentation placement에 Root를 붙여 Modal Session을 연다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public static ModalSession Open
        (
            UIContext context,
            string presentationID,
            VisualElement root
        )
        {
            return OpenPlacement(context, presentationID, root, null);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 현재 Context의 Presentation placement에 Root와 Focus Scope를 함께 연다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public static ModalSession OpenWithFocus
        (
            UIContext context,
            string presentationID,
            VisualElement root,
            IFocusScope focusScope
        )
        {
            if (focusScope == null)
            {
                throw new ArgumentNullException(nameof(focusScope));
            }

            return OpenPlacement(context, presentationID, root, focusScope);
        }

    #endregion

    #region Composition API

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
            catch
            {
                restoreInteraction.Dispose();
                throw;
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
            catch
            {
                restoreInteraction.Dispose();
                throw;
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
                    root.SetEnabled(enabled);
                    root.pickingMode = pickingMode;
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Placement usage와 hierarchy lifetime을 Common Modal 조립으로 이전한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static ModalSession OpenPlacement
        (
            UIContext context,
            string presentationID,
            VisualElement root,
            IFocusScope focusScope
        )
        {
            ValidateComposition(context, root);

            if (string.IsNullOrWhiteSpace(presentationID))
            {
                throw new ArgumentException
                (
                    "Modal Presentation ID가 비어 있습니다.",
                    nameof(presentationID)
                );
            }

            if (root.parent != null)
            {
                throw new InvalidOperationException
                (
                    "UITK Modal Root는 다른 hierarchy에 연결되지 않은 상태여야 합니다."
                );
            }

            if
            (
                !context.Presentation.TryAcquirePlacement
                (
                    presentationID,
                    out var placement,
                    out var usage
                )
            )
            {
                throw new InvalidOperationException
                (
                    $"Modal Presentation '{presentationID}'을 획득할 수 없습니다."
                );
            }

            Lease hierarchyLifetime = null;

            try
            {
                if (placement is not IPresentationLayerDriver<VisualElement> layer)
                {
                    throw new InvalidOperationException
                    (
                        $"Modal Presentation '{presentationID}'이 UI Toolkit placement가 아닙니다."
                    );
                }

                layer.Root.Add(root);
                hierarchyLifetime = new Lease(root.RemoveFromHierarchy);

                return focusScope == null
                    ? Open(context, root, usage, hierarchyLifetime)
                    : OpenWithFocus
                    (
                        context,
                        root,
                        focusScope,
                        usage,
                        hierarchyLifetime
                    );
            }
            catch
            {
                hierarchyLifetime?.Dispose();
                usage.Dispose();
                throw;
            }
        }

    #endregion

    }
}
