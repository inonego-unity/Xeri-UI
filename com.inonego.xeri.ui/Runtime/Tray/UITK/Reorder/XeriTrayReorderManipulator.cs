/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayReorderManipulator.cs
수정일 : 2026-09-20

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
            CancelActive();

            target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 reorder session과 preview를 즉시 취소한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void CancelActive()
        {
            if (session == null) return;

            if
            (
                pointerID >= 0 &&
                target != null &&
                target.HasPointerCapture(pointerID)
            )
            {
                target.ReleasePointer(pointerID);
            }

            reorderTarget.ReorderAnimator?.Cancel(reorderTarget, session);
            visual.Clear(activeButton, session);
            activeButton = null;
            session = null;
            isDragging = false;
            pointerID = -1;
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

            var wasDragging = isDragging;

            if (target.HasPointerCapture(evt.pointerId))
            {
                target.ReleasePointer(evt.pointerId);
            }

            if (isDragging && session.TargetIndex != session.SourceIndex)
            {
                reorderTarget.InvokeEntryReorder
                (
                    new XeriTrayReorderRequest
                    (
                        session.Entry,
                        session.SourceIndex,
                        session.TargetIndex
                    )
                );

                reorderTarget.ReorderAnimator?.Commit(reorderTarget, session);
            }
            else
            {
                reorderTarget.ReorderAnimator?.Cancel(reorderTarget, session);
            }

            visual.Clear(activeButton, session);
            activeButton = null;
            session = null;
            isDragging = false;
            pointerID = -1;

            if (wasDragging)
            {
                evt.StopImmediatePropagation();
            }
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

            if (target.HasPointerCapture(evt.pointerId))
            {
                target.ReleasePointer(evt.pointerId);
            }

            reorderTarget.ReorderAnimator?.Cancel(reorderTarget, session);
            visual.Clear(activeButton, session);
            activeButton = null;
            session = null;
            isDragging = false;
            pointerID = -1;
        }

    #endregion

    }
}
