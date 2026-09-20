/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayReorderAnimator.cs
수정일 : 2026-09-20

# 설명
Tray reorder preview에서 주변 entry를 transform 기반으로 이동시킨다.
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
    /// Tray reorder preview 애니메이션 구현.
    /// </summary>
    // ============================================================
    public sealed class XeriTrayReorderAnimator : IXeriTrayReorderAnimator
    {

    #region 생성자

        public XeriTrayReorderAnimator() : base() {}

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 중인 entry가 들어갈 위치에 맞춰 주변 entry offset을 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Preview(IXeriTrayReorderTarget target, XeriTrayReorderSession session)
        {
            if (target == null || session == null) return;

            var buttons = target.GetEntryButtons();
            var bounds = target.GetEntryBounds();

            if (buttons == null || bounds == null) return;
            if (session.SourceIndex < 0 || session.SourceIndex >= buttons.Count) return;

            for (var i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                if (button == null) continue;
                if (i == session.SourceIndex) continue;

                button.style.translate = CalculateOffset
                (
                    i,
                    session.SourceIndex,
                    session.TargetIndex,
                    bounds,
                    target.ReorderAxis
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 확정 후 preview offset을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Commit(IXeriTrayReorderTarget target, XeriTrayReorderSession session)
        {
            Clear(target);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 취소 후 preview offset을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Cancel(IXeriTrayReorderTarget target, XeriTrayReorderSession session)
        {
            Clear(target);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Target의 모든 button offset을 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Clear(IXeriTrayReorderTarget target)
        {
            if (target == null) return;

            foreach (var button in target.GetEntryButtons())
            {
                if (button == null) continue;

                button.style.translate = new Translate(0f, 0f, 0f);
            }
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Preview 중인 entry index에 적용할 offset을 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Translate CalculateOffset
        (
            int index,
            int sourceIndex,
            int targetIndex,
            IReadOnlyList<Rect> bounds,
            XeriTrayReorderAxis axis
        )
        {
            var offset = 0f;

            if (targetIndex > sourceIndex && index > sourceIndex && index <= targetIndex)
            {
                offset = GetAxisCenter(bounds[index - 1], axis) -
                         GetAxisCenter(bounds[index], axis);
            }
            else if (targetIndex < sourceIndex && index >= targetIndex && index < sourceIndex)
            {
                offset = GetAxisCenter(bounds[index + 1], axis) -
                         GetAxisCenter(bounds[index], axis);
            }

            return axis == XeriTrayReorderAxis.Horizontal
                ? new Translate(offset, 0f, 0f)
                : new Translate(0f, offset, 0f);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 축의 rect 중심 좌표를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static float GetAxisCenter(Rect rect, XeriTrayReorderAxis axis)
        {
            return axis == XeriTrayReorderAxis.Horizontal
                ? rect.center.x
                : rect.center.y;
        }

    #endregion

    }
}
