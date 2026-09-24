/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowTitleBarCoordinateProvider.cs
수정일 : 2026-09-20

# 설명
Window titlebar drag가 움직이는 window 내부 좌표계에 흔들리지 않도록 panel 좌표를 그대로 사용한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.DragDrop;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Titlebar drag 전용 좌표 provider.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowTitleBarCoordinateProvider : IDragCoordinateProvider
    {

    #region 프로퍼티

        // ----------------------------------------------------------------------
        /// <summary>
        /// Window 이동 계산은 입력 delta만 사용하므로 기준 위치는 0으로 고정한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public Vector2 Pos
        {
            get => Vector2.zero;
            set
            {
                // NONE
            }
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// PointerEvent의 panel 좌표를 그대로 drag 좌표로 사용한다.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 ToLocalPos(Vector2 inputPos)
        {
            return inputPos;
        }

    #endregion

    }
}
