/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriWindowDragFactory.cs
수정일 : 2026-09-20

# 설명
XeriWindowPanel titlebar drag binding을 생성하는 factory 계약.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Window titlebar drag binding factory 계약.
    /// </summary>
    // ============================================================
    public interface IXeriWindowDragFactory
    {

    #region 메서드

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Session이 Attach/Detach할 미부착 Window titlebar drag binding을 생성한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        XeriWindowTitleBarManipulator CreateTitleBarDrag
        (
            XeriWindowPanel panel,
            XeriWindowController controller
        );

    #endregion

    }
}
