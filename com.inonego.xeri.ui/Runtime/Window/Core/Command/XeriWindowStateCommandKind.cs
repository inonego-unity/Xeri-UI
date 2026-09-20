/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowStateCommandKind.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 상태 전환 명령 종류.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 상태 전환 명령 종류.
    /// </summary>
    // ============================================================
    [Serializable]
    public enum XeriWindowStateCommandKind
    {
        Minimize,
        Maximize,
        ShowNormal,
        Restore,
        Close,
    }
}
