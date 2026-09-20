/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriTrayEventArgs.cs
수정일 : 2026-09-20

# 설명
Tray entry 이벤트 인자.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray entry 이벤트 인자.
    /// </summary>
    // ============================================================
    public class XeriTrayEventArgs : EventArgs
    {

    #region 프로퍼티

        // ------------------------------------------------------------
        /// <summary>
        /// 이벤트 대상 entry.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayEntry Entry => entry;

        private readonly XeriTrayEntry entry = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 이벤트 인자를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayEventArgs(XeriTrayEntry entry) : base()
        {
            this.entry = entry;
        }

    #endregion

    }
}
