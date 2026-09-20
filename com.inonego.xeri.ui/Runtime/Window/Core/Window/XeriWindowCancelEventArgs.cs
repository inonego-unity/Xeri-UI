/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowCancelEventArgs.cs
수정일 : 2026-09-20

# 설명
취소 가능한 Xeri 커스텀 윈도우 이벤트 인자.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// 취소 가능한 Xeri 커스텀 윈도우 이벤트 인자.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class XeriWindowCancelEventArgs : XeriWindowEventArgs
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 요청을 취소할지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool Cancel;

    #endregion

    }
}
