/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayReorderManipulator.cs
수정일 : 2026-10-03

# 설명
UITK pointer 입력을 Tray entry reorder drag로 변환한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray entry reorder pointer manipulator.
    /// </summary>
    // ============================================================
    internal sealed class XeriTrayReorderManipulator : Manipulator
    {

    #region 필드

        private const float DRAG_THRESHOLD = 4f;

        private readonly IXeriTrayReorderTarget reorderTarget = null;
        private readonly XeriTrayReorderCalculator calculator = new();
        private readonly XeriTrayReorderVisual visual = new();

        private XeriTrayReorderSession session = null;
        private XeriTrayButton activeButton = null;
        private bool isDragging = false;
        private int pointerID = -1;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Tray reorder pointer manipulator를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayReorderManipulator(IXeriTrayReorderTarget reorderTarget) : base()
        {
            this.reorderTarget = reorderTarget ??
                throw new ArgumentNullException(nameof(reorderTarget));
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Pointer 입력 callback을 target에 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Pointer 입력 callback을 target에서 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void UnregisterCallbacksFromTarget()
        {
            var errors = new List<Exception>();

            TryCleanup(CancelActive, errors);
            TryCleanup
            (
                () => target.UnregisterCallback<PointerDownEvent>
                (
                    OnPointerDown,
                    TrickleDown.TrickleDown
                ),
                errors
            );
            TryCleanup
            (
                () => target.UnregisterCallback<PointerMoveEvent>
                (
                    OnPointerMove,
                    TrickleDown.TrickleDown
                ),
                errors
            );
            TryCleanup
            (
                () => target.UnregisterCallback<PointerUpEvent>
                (
                    OnPointerUp,
                    TrickleDown.TrickleDown
                ),
                errors
            );
            TryCleanup
            (
                () => target.UnregisterCallback<PointerCancelEvent>
                (
                    OnPointerCancel,
                    TrickleDown.TrickleDown
                ),
                errors
            );

            ThrowCleanupErrors("Tray reorder callback 해제", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 reorder session과 preview를 즉시 취소한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void CancelActive()
        {
            if (session == null) return;

            var currentSession = session;
            var currentButton = activeButton;
            var currentPointerID = pointerID;
            var errors = new List<Exception>();

            // 외부 animator/callback 재진입이 같은 drag 종료를 다시 시작하지 않게 먼저 terminalize한다.
            ResetActiveState();

            TryCleanup
            (
                () =>
                {
                    if
                    (
                        currentPointerID >= 0 &&
                        target != null &&
                        target.HasPointerCapture(currentPointerID)
                    )
                    {
                        target.ReleasePointer(currentPointerID);
                    }
                },
                errors
            );
            TryCleanup
            (
                () => reorderTarget.ReorderAnimator?.Cancel
                (
                    reorderTarget,
                    currentSession
                ),
                errors
            );
            TryCleanup
            (
                () => visual.Clear(currentButton, currentSession),
                errors
            );

            ThrowCleanupErrors("Tray reorder 취소", errors);
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 입력 상태에서 reorder drag를 시작할 수 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool CanStartDrag()
        {
            return
                reorderTarget.Reorderable &&
                reorderTarget.GetEntryButtons().Count >= 2;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Pointer 좌표를 entry container local 좌표로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private Vector2 ToEntryContainerPos(Vector2 panelPos)
        {
            return reorderTarget.EntryContainer.WorldToLocal(panelPos);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 event target에서 Tray button을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriTrayButton FindButton(VisualElement element)
        {
            while (element != null)
            {
                if (element is XeriTrayButton button)
                {
                    return button;
                }

                element = element.parent;
            }

            return null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Button 목록에서 지정한 button index를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static int IndexOf(IReadOnlyList<XeriTrayButton> buttons, XeriTrayButton button)
        {
            if (buttons == null) return -1;

            for (var i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == button)
                {
                    return i;
                }
            }

            return -1;
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder drag 시작 후보를 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (session != null) return;
            if (evt.button != 0) return;
            if (!CanStartDrag()) return;

            var button = FindButton(evt.target as VisualElement);
            if (button == null) return;

            var buttons = reorderTarget.GetEntryButtons();
            var sourceIndex = IndexOf(buttons, button);
            if (sourceIndex < 0) return;

            activeButton = button;
            session = new XeriTrayReorderSession
            (
                button.Entry,
                sourceIndex,
                ToEntryContainerPos(evt.position)
            );
            isDragging = false;
            pointerID = evt.pointerId;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Pointer 이동량이 threshold를 넘으면 reorder preview를 갱신한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (session == null) return;
            if (evt.pointerId != pointerID) return;

            var currentPos = ToEntryContainerPos(evt.position);
            var delta = currentPos - session.StartPointerPos;

            if (!isDragging && delta.magnitude < DRAG_THRESHOLD) return;

            if (!target.HasPointerCapture(evt.pointerId))
            {
                target.CapturePointer(evt.pointerId);
            }

            isDragging = true;
            visual.Move(activeButton, session, currentPos, reorderTarget);

            var bounds = reorderTarget.GetEntryBounds();
            var targetIndex = calculator.CalculateTargetIndex
            (
                bounds,
                session.SourceIndex,
                currentPos,
                reorderTarget.ReorderAxis
            );

            if (targetIndex >= 0 && session.TargetIndex != targetIndex)
            {
                session.TargetIndex = targetIndex;
                reorderTarget.ReorderAnimator?.Preview(reorderTarget, session);
            }

            evt.StopPropagation();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Pointer release 시 reorder 요청을 확정하거나 preview를 취소한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void OnPointerUp(PointerUpEvent evt)
        {
            if (session == null) return;
            if (evt.pointerId != pointerID) return;

            var currentSession = session;
            var currentButton = activeButton;
            var wasDragging = isDragging;
            var shouldCommit =
                wasDragging &&
                currentSession.TargetIndex != currentSession.SourceIndex;
            var errors = new List<Exception>();

            // 확정 observer가 재진입해 Panel을 reload해도 같은 drag 수명을 다시 정리하지 않게 먼저 종료한다.
            ResetActiveState();

            TryCleanup
            (
                () =>
                {
                    if (target.HasPointerCapture(evt.pointerId))
                    {
                        target.ReleasePointer(evt.pointerId);
                    }
                },
                errors
            );

            if (shouldCommit)
            {
                TryCleanup
                (
                    () => reorderTarget.InvokeEntryReorder
                    (
                        new XeriTrayReorderRequest
                        (
                            currentSession.Entry,
                            currentSession.SourceIndex,
                            currentSession.TargetIndex
                        )
                    ),
                    errors
                );
                TryCleanup
                (
                    () => reorderTarget.ReorderAnimator?.Commit
                    (
                        reorderTarget,
                        currentSession
                    ),
                    errors
                );
            }
            else
            {
                TryCleanup
                (
                    () => reorderTarget.ReorderAnimator?.Cancel
                    (
                        reorderTarget,
                        currentSession
                    ),
                    errors
                );
            }

            TryCleanup
            (
                () => visual.Clear(currentButton, currentSession),
                errors
            );

            if (wasDragging)
            {
                evt.StopImmediatePropagation();
            }

            ThrowCleanupErrors("Tray reorder 확정", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Pointer cancel 시 preview를 취소하고 drag 상태를 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (session == null) return;
            if (evt.pointerId != pointerID) return;

            var currentSession = session;
            var currentButton = activeButton;
            var errors = new List<Exception>();

            ResetActiveState();

            TryCleanup
            (
                () =>
                {
                    if (target.HasPointerCapture(evt.pointerId))
                    {
                        target.ReleasePointer(evt.pointerId);
                    }
                },
                errors
            );
            TryCleanup
            (
                () => reorderTarget.ReorderAnimator?.Cancel
                (
                    reorderTarget,
                    currentSession
                ),
                errors
            );
            TryCleanup
            (
                () => visual.Clear(currentButton, currentSession),
                errors
            );

            ThrowCleanupErrors("Tray reorder pointer 취소", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 reorder 입력 소유권을 terminal 상태로 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ResetActiveState()
        {
            activeButton = null;
            session = null;
            isDragging = false;
            pointerID = -1;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 cleanup을 끝까지 시도하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void TryCleanup
        (
            Action cleanup,
            List<Exception> errors
        )
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 수집한 reorder cleanup 실패를 원래 형태로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ThrowCleanupErrors
        (
            string message,
            List<Exception> errors
        )
        {
            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                $"{message} 중 하나 이상의 작업이 실패했습니다.",
                errors
            );
        }

    #endregion

    }
}
