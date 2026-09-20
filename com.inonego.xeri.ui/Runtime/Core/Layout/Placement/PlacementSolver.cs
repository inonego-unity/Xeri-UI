/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PlacementSolver.cs
수정일 : 2026-09-17
# 설명
동일한 로컬 좌표계의 Rect와 Anchor를 기준으로 UI 요소 Pivot의 정렬 위치와 clamp를 계산한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI 요소 Pivot의 현재 영역 배치 위치를 계산한다.
    /// </summary>
    // ============================================================
    public sealed class PlacementSolver
    {

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 중심 Pivot UI 요소의 배치 위치를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        public PlacementResult Place
        (
            Rect bounds,
            Vector2 anchor,
            Vector2 elementSize,
            PlacementOptions options
        )
        {
            return Place
            (
                bounds,
                anchor,
                elementSize,
                new Vector2(0.5f, 0.5f),
                options
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 지정 로컬 Rect 안에서 UI 요소 Pivot의 배치 위치를 계산한다.
        /// <br/> Pivot은 현재 좌표계 Rect의 최소 모서리에서 최대 모서리 방향으로 정규화한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public PlacementResult Place
        (
            Rect bounds,
            Vector2 anchor,
            Vector2 elementSize,
            Vector2 elementPivot,
            PlacementOptions options
        )
        {
            Validate(elementSize, elementPivot);

            var minExtent = Vector2.Scale(elementSize, elementPivot);
            var maxExtent = elementSize - minExtent;
            var alignmentOffset = GetAlignmentOffset
            (
                options.Alignment,
                options.CoordinateSystem,
                minExtent,
                maxExtent
            );
            var position = anchor + alignmentOffset + options.Offset;
            var original = position;

            if (options.ClampToBounds)
            {
                var min = bounds.min + options.Padding + minExtent;
                var max = bounds.max - options.Padding - maxExtent;
                position.x = min.x <= max.x
                    ? Mathf.Clamp(position.x, min.x, max.x)
                    : bounds.center.x;
                position.y = min.y <= max.y
                    ? Mathf.Clamp(position.y, min.y, max.y)
                    : bounds.center.y;
            }

            return new PlacementResult(position, position != original);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 정렬 방향을 현재 Pivot의 최소·최대 방향 크기 기준 offset으로 변환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static Vector2 GetAlignmentOffset
        (
            PlacementAlignment alignment,
            PlacementCoordinateSystem coordinateSystem,
            Vector2 minExtent,
            Vector2 maxExtent
        )
        {
            var top = coordinateSystem == PlacementCoordinateSystem.YUp
                ? minExtent.y
                : -maxExtent.y;
            var bottom = coordinateSystem == PlacementCoordinateSystem.YUp
                ? -maxExtent.y
                : minExtent.y;

            switch (alignment)
            {
                case PlacementAlignment.Top:
                    return new Vector2(0.0f, top);
                case PlacementAlignment.Bottom:
                    return new Vector2(0.0f, bottom);
                case PlacementAlignment.Left:
                    return new Vector2(-maxExtent.x, 0.0f);
                case PlacementAlignment.Right:
                    return new Vector2(minExtent.x, 0.0f);
                case PlacementAlignment.TopLeft:
                    return new Vector2(-maxExtent.x, top);
                case PlacementAlignment.TopRight:
                    return new Vector2(minExtent.x, top);
                case PlacementAlignment.BottomLeft:
                    return new Vector2(-maxExtent.x, bottom);
                case PlacementAlignment.BottomRight:
                    return new Vector2(minExtent.x, bottom);
                default:
                    return Vector2.zero;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 요소 크기와 Pivot이 유효한 배치 입력인지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Validate
        (
            Vector2 elementSize,
            Vector2 elementPivot
        )
        {
            if (elementSize.x < 0.0f || elementSize.y < 0.0f)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(elementSize),
                    "Placement 요소 크기는 음수일 수 없습니다."
                );
            }

            if
            (
                elementPivot.x < 0.0f || elementPivot.x > 1.0f ||
                elementPivot.y < 0.0f || elementPivot.y > 1.0f
            )
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(elementPivot),
                    "Placement 요소 Pivot은 0과 1 사이여야 합니다."
                );
            }
        }

    #endregion

    }
}
