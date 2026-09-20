/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowDragFactory.cs
수정일 : 2026-09-20

# 설명
기존 Drag_Drop UITK manipulator를 사용하는 기본 Window titlebar drag factory.
========================================================================= BLOCK_HEADER_END */

using inonego.Xeri.UI.DragDrop;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// 기본 Window titlebar drag factory.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowDragFactory : IXeriWindowDragFactory
    {

    #region 필드

        private readonly DragDropCoordinator coordinator = null;

    #endregion

    #region 생성자

        public XeriWindowDragFactory() : this(null) {}

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 Window titlebar drag factory를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowDragFactory(DragDropCoordinator coordinator) : base()
        {
            this.coordinator = coordinator;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Window titlebar drag binding을 생성하고 panel에 부착한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowTitleBarManipulator CreateTitleBarDrag
        (
            XeriWindowPanel panel,
            XeriWindowController controller
        )
        {
            var manipulator = new XeriWindowTitleBarManipulator(panel, controller, coordinator);
            manipulator.Attach();

            return manipulator;
        }

    #endregion

    }
}
