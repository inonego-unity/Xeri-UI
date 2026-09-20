/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriTrayCancelEventArgs.cs
수정일 : 2026-09-20

# 설명
취소 가능한 Tray entry 이벤트 인자.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// 취소 가능한 Tray entry 이벤트 인자.
    /// </summary>
    // ============================================================
    public sealed class XeriTrayCancelEventArgs : XeriTrayEventArgs
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 요청을 취소할지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool Cancel
        {
            get => cancel;
            set => cancel = value;
        }

        private bool cancel = false;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 취소 가능한 Tray entry 이벤트 인자를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayCancelEventArgs(XeriTrayEntry entry) : base(entry) {}

    #endregion

    }
}
