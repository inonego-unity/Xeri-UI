/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayReorderManipulator.cs
수정일 : 2026-11-10

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

    #region 입력 콜백 연결

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
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Pointer 입력 callback을 target에서 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void UnregisterCallbacksFromTarget()
        {
            var errors = new ExceptionBuffer();

            TryCleanup(CancelActive, ref errors);
            TryCleanup
            (
                () => target.UnregisterCallback<PointerDownEvent>
                (
                    OnPointerDown,
                    TrickleDown.TrickleDown
                ),
                ref errors
            );
            TryCleanup
            (
                () => target.UnregisterCallback<PointerMoveEvent>
                (
                    OnPointerMove,
                    TrickleDown.TrickleDown
                ),
                ref errors
            );
            TryCleanup
            (
                () => target.UnregisterCallback<PointerUpEvent>
                (
                    OnPointerUp,
                    TrickleDown.TrickleDown
                ),
                ref errors
            );
            TryCleanup
            (
                () => target.UnregisterCallback<PointerCancelEvent>
                (
                    OnPointerCancel,
                    TrickleDown.TrickleDown
                ),
                ref errors
            );
            TryCleanup
            (
                () => target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave),
                ref errors
            );
            TryCleanup
            (
                () => target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut),
                ref errors
            );

            ThrowCleanupErrors("Tray reorder callback 해제", errors);
        }

    #endregion

    #region 포인터 입력

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

            if (!isDragging)
            {
                if (delta.magnitude < DRAG_THRESHOLD) return;

                target.CapturePointer(evt.pointerId);
                if (!target.HasPointerCapture(evt.pointerId))
                {
                    CancelActive();
                    return;
                }

                isDragging = true;
            }
            else if (!target.HasPointerCapture(evt.pointerId))
            {
                // 외부에서 capture를 빼앗으면 기존 drag를 다시 획득하지 않는다.
                CancelActive();
                evt.StopPropagation();
                return;
            }

            var currentSession = session;

            try
            {
                visual.Move(activeButton, currentSession, currentPos, reorderTarget);

                var bounds = reorderTarget.GetEntryBounds();
                var targetIndex = calculator.CalculateTargetIndex
                (
                    bounds,
                    currentSession.SourceIndex,
                    currentPos,
                    reorderTarget.ReorderAxis
                );

                if (targetIndex >= 0 && currentSession.TargetIndex != targetIndex)
                {
                    currentSession.TargetIndex = targetIndex;
                    reorderTarget.ReorderAnimator?.Preview(reorderTarget, currentSession);
                }

                evt.StopPropagation();
            }
            catch (Exception exception)
            {
                // Preview 실패는 실행 중이던 해당 drag의 소유 자원만 되돌린다.
                if (ReferenceEquals(session, currentSession))
                {
                    try
                    {
                        CancelActive();
                    }
                    catch (Exception cleanupException)
                    {
                        throw new AggregateException
                        (
                            "Tray reorder preview와 취소가 모두 실패했습니다.",
                            exception,
                            cleanupException
                        );
                    }
                }

                throw;
            }
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
            var shouldCommit = currentSession.TargetIndex != currentSession.SourceIndex;

            // 확정 observer가 재진입해도 이 입력 세션은 이미 종료된 상태다.
            ResetActiveState();
            if (!wasDragging) return;

            var errors = new ExceptionBuffer();
            TryCleanup
            (
                () =>
                {
                    if (target.HasPointerCapture(evt.pointerId))
                    {
                        target.ReleasePointer(evt.pointerId);
                    }
                },
                ref errors
            );

            if (shouldCommit)
            {
                TryCleanup
                (
                    () => reorderTarget.ReorderAnimator?.Commit
                    (
                        reorderTarget,
                        currentSession
                    ),
                    ref errors
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
                    ref errors
                );
            }

            // 새로운 입력이나 Source Reload 전에 이전 drag의 표시 소유권을 정리한다.
            TryCleanup
            (
                () => visual.Clear(currentButton),
                ref errors
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
                    ref errors
                );
            }

            evt.StopImmediatePropagation();
            ThrowCleanupErrors("Tray reorder 확정", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Pointer cancel 시 preview를 취소하고 drag 상태를 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (session == null || evt.pointerId != pointerID) return;

            CancelActive();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 이전에 Tray를 떠난 포인터의 입력 후보를 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (session == null || isDragging || evt.pointerId != pointerID) return;
            if (evt.currentTarget != target) return;

            CancelActive();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 중 포인터 capture를 상실하면 현재 세션과 preview를 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (session == null || !isDragging || evt.pointerId != pointerID) return;
            if (evt.currentTarget != target || target.HasPointerCapture(evt.pointerId)) return;

            CancelActive();
        }

    #endregion

    #region 입력 세션 종료

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
            var wasDragging = isDragging;

            // Drag 이전의 후보 세션은 시각 자원을 획득하지 않았다.
            ResetActiveState();
            if (!wasDragging) return;

            var errors = new ExceptionBuffer();
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
                ref errors
            );
            TryCleanup
            (
                () => reorderTarget.ReorderAnimator?.Cancel
                (
                    reorderTarget,
                    currentSession
                ),
                ref errors
            );
            TryCleanup
            (
                () => visual.Clear(currentButton),
                ref errors
            );

            ThrowCleanupErrors("Tray reorder 취소", errors);
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

    #endregion

    #region 입력 대상과 좌표

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

    #region 오류 수집과 전달

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 cleanup을 끝까지 시도하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void TryCleanup
        (
            Action cleanup,
            ref ExceptionBuffer errors
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
            ExceptionBuffer errors
        )
        {
            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException
            (
                $"{message} 중 하나 이상의 작업이 실패했습니다."
            );
        }

    #endregion

    }
}
