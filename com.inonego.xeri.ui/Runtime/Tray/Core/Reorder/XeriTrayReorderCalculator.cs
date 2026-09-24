/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayReorderCalculator.cs
수정일 : 2026-09-20

# 설명
Tray entry bounds와 drag 좌표를 기준으로 reorder target index를 계산한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray entry reorder index 계산기.
    /// </summary>
    // ============================================================
    internal sealed class XeriTrayReorderCalculator
    {

    #region 생성자

        public XeriTrayReorderCalculator() : base()
        {
            // NONE
        }

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// Pointer가 지나간 insertion boundary를 기준으로 target index를 계산한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public int CalculateTargetIndex
        (
            IReadOnlyList<Rect> entryBounds,
            int sourceIndex,
            Vector2 pointerPos,
            XeriTrayReorderAxis axis
        )
        {
            if (entryBounds == null || entryBounds.Count <= 0) return -1;
            if (sourceIndex < 0 || sourceIndex >= entryBounds.Count) return -1;

            var pointerAxisPos = GetAxisPos(pointerPos, axis);
            var sourceCenter = GetAxisPos(entryBounds[sourceIndex].center, axis);

            if (pointerAxisPos > sourceCenter)
            {
                return CalculateForwardIndex(entryBounds, sourceIndex, pointerAxisPos, axis);
            }

            if (pointerAxisPos < sourceCenter)
            {
                return CalculateBackwardIndex(entryBounds, sourceIndex, pointerAxisPos, axis);
            }

            return sourceIndex;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 축에 해당하는 좌표 값을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static float GetAxisPos(Vector2 pos, XeriTrayReorderAxis axis)
        {
            return axis == XeriTrayReorderAxis.Horizontal ? pos.x : pos.y;
        }

    #endregion

    #region 내부 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// Source 뒤쪽 insertion boundary를 기준으로 target index를 계산한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static int CalculateForwardIndex
        (
            IReadOnlyList<Rect> entryBounds,
            int sourceIndex,
            float pointerAxisPos,
            XeriTrayReorderAxis axis
        )
        {
            var targetIndex = sourceIndex;

            for (var i = sourceIndex + 1; i < entryBounds.Count; i++)
            {
                if (pointerAxisPos > GetBoundary(entryBounds[i - 1], entryBounds[i], axis))
                {
                    targetIndex = i;
                }
            }

            return Mathf.Clamp(targetIndex, 0, entryBounds.Count - 1);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Source 앞쪽 insertion boundary를 기준으로 target index를 계산한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static int CalculateBackwardIndex
        (
            IReadOnlyList<Rect> entryBounds,
            int sourceIndex,
            float pointerAxisPos,
            XeriTrayReorderAxis axis
        )
        {
            var targetIndex = sourceIndex;

            for (var i = sourceIndex - 1; i >= 0; i--)
            {
                if (pointerAxisPos < GetBoundary(entryBounds[i], entryBounds[i + 1], axis))
                {
                    targetIndex = i;
                }
            }

            return Mathf.Clamp(targetIndex, 0, entryBounds.Count - 1);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 두 entry 사이의 insertion boundary 좌표를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static float GetBoundary(Rect previous, Rect next, XeriTrayReorderAxis axis)
        {
            var previousCenter = GetAxisPos(previous.center, axis);
            var nextCenter = GetAxisPos(next.center, axis);

            return (previousCenter + nextCenter) * 0.5f;
        }

    #endregion

    }
}
