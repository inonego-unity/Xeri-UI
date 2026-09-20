/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriTrayContent.cs
수정일 : 2026-09-20

# 설명
Tray entry에서 표시할 구성 요소 조합.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray entry 표시 구성 요소.
    /// </summary>
    // ============================================================
    [Flags]
    public enum XeriTrayContent
    {
        None        = 0,
        Icon        = 1 << 0,
        Title       = 1 << 1,
        Badge       = 1 << 2,
        StateMarker = 1 << 3,
        CloseButton = 1 << 4,
        All         = Icon | Title | Badge | StateMarker | CloseButton,
    }
}
