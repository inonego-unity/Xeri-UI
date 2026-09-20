/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayReorderRequest.cs
수정일 : 2026-09-20

# 설명
Tray entry reorder 확정 요청 값을 표현한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray entry reorder 확정 요청.
    /// </summary>
    // ============================================================
    public readonly struct XeriTrayReorderRequest
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 이동할 Tray entry.
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
        /// Tray reorder 요청 값을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayReorderRequest
        (
            XeriTrayEntry entry,
            int sourceIndex,
            int targetIndex
        )
        {
            Entry = entry;
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
        }

    #endregion

    }
}
