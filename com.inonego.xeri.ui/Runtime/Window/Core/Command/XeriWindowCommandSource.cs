/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowCommandSource.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 명령 발생 원인.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 명령 발생 원인.
    /// </summary>
    // ============================================================
    [Serializable]
    public enum XeriWindowCommandSource
    {
        API,
        ControlButton,
        TitleBar,
        Tray,
    }
}
