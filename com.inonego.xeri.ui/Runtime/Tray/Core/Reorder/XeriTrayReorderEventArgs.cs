/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayReorderEventArgs.cs
수정일 : 2026-09-20

# 설명
Tray entry reorder 이벤트 인자를 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray entry reorder 이벤트 인자.
    /// </summary>
    // ============================================================
    public sealed class XeriTrayReorderEventArgs : EventArgs
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 이동한 Tray entry.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayEntry Entry { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 이동 전 entry index.
        /// </summary>
        // ------------------------------------------------------------
        public int SourceIndex { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 이동 후 entry index.
        /// </summary>
        // ------------------------------------------------------------
        public int TargetIndex { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 요청 값을 이벤트 인자로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayReorderEventArgs(XeriTrayReorderRequest request) : base()
        {
            Entry = request.Entry;
            SourceIndex = request.SourceIndex;
            TargetIndex = request.TargetIndex;
        }

    #endregion

    }
}
