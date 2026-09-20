/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowResizeMode.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 resize 입력 방향.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Window resize 입력 방향.
    /// </summary>
    // ============================================================
    public enum XeriWindowResizeMode
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 3,
        Bottom = 4,
        TopLeft = 5,
        TopRight = 6,
        BottomLeft = 7,
        BottomRight = 8,
    }
}
