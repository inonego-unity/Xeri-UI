/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowTrayStateMask.cs
수정일 : 2026-10-02

# 설명
Window Tray source가 entry로 포함할 live Window 상태 집합.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Window Tray source가 포함할 live Window 상태 집합.
    /// </summary>
    // ============================================================
    [Flags]
    public enum XeriWindowTrayStateMask
    {
        None      = 0,
        Normal    = 1 << 0,
        Minimized = 1 << 1,
        Maximized = 1 << 2,
        All       = Normal | Minimized | Maximized,
    }
}
