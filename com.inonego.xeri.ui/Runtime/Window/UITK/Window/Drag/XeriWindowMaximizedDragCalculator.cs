/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowMaximizedDragCalculator.cs
수정일 : 2026-09-20

# 설명
Maximized 상태의 XeriWindow를 titlebar drag 위치에 맞춰 normal 위치로 변환하는 계산 유틸리티.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Maximized window drag 복원 위치 계산기.
    /// </summary>
    // ============================================================
    internal static class XeriWindowMaximizedDragCalculator
    {

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// Maximized titlebar drag 시작 위치를 normal window 위치로 변환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static Vector2 CalculateRestoredPos
        (
            Vector2 pointerPos,
            Vector2 normalSize,
            Vector2 maximizedSize,
            float titleBarHeight
        )
        {
            var safeMaximizedWidth = Mathf.Max(1f, maximizedSize.x);
            var safeNormalWidth = Mathf.Max(1f, normalSize.x);
            var safeTitleBarHeight = Mathf.Max(1f, titleBarHeight);
            var pointerRatioX = Mathf.Clamp01(pointerPos.x / safeMaximizedWidth);
            var grabX = safeNormalWidth * pointerRatioX;
            var grabY = Mathf.Clamp(pointerPos.y, 0f, safeTitleBarHeight);

            return new Vector2
            (
                pointerPos.x - grabX,
                pointerPos.y - grabY
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 유효한 양수 값이 아니면 fallback을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static float ResolvePositive(float value, float fallback)
        {
            return float.IsNaN(value) || value <= 0f
                ? fallback
                : value;
        }

    #endregion

    }
}
