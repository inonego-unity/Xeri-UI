/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PlacementOptions.cs
수정일 : 2026-09-17
# 설명
UI Placement 정렬, 좌표 방향, offset, padding과 영역 clamp 정책을 정의한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Placement 정렬 방향.
    /// </summary>
    // ============================================================
    public enum PlacementAlignment
    {
        Center = 0,
        Top = 1,
        Bottom = 2,
        Left = 3,
        Right = 4,
        TopLeft = 5,
        TopRight = 6,
        BottomLeft = 7,
        BottomRight = 8,
    }

    // ============================================================
    /// <summary>
    /// Placement 로컬 좌표계의 세로 방향.
    /// </summary>
    // ============================================================
    public enum PlacementCoordinateSystem
    {
        YUp = 0,
        YDown = 1,
    }

    // ============================================================
    /// <summary>
    /// UI Placement 계산 옵션.
    /// </summary>
    // ============================================================
    public readonly struct PlacementOptions
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Anchor 기준 정렬 방향.
        /// </summary>
        // ------------------------------------------------------------
        public PlacementAlignment Alignment { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 정렬 결과에 더할 로컬 offset.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 Offset { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 배치 영역 안쪽 여백.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 Padding { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 배치 결과를 현재 영역 안으로 제한할지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool ClampToBounds { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Top과 Bottom을 해석할 현재 로컬 좌표 방향.
        /// </summary>
        // ------------------------------------------------------------
        public PlacementCoordinateSystem CoordinateSystem { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// UI Placement 옵션을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public PlacementOptions
        (
            PlacementAlignment alignment,
            Vector2 offset,
            Vector2 padding,
            bool clampToBounds = true,
            PlacementCoordinateSystem coordinateSystem = PlacementCoordinateSystem.YUp
        ) : this()
        {
            Alignment = alignment;
            Offset = offset;
            Padding = padding;
            ClampToBounds = clampToBounds;
            CoordinateSystem = coordinateSystem;
        }

    #endregion

    }
}
