/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayNoReorderAnimator.cs
수정일 : 2026-11-09

# 설명
Tray reorder preview 애니메이션을 사용하지 않는 구현이다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray reorder 애니메이션 비활성 구현.
    /// </summary>
    // ============================================================
    public sealed class XeriTrayNoReorderAnimator : IXeriTrayReorderAnimator
    {

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Preview offset을 적용하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        public void Preview(IXeriTrayReorderTarget target, XeriTrayReorderSession session)
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 확정 후 남은 offset을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Commit(IXeriTrayReorderTarget target, XeriTrayReorderSession session)
        {
            Clear(target);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 취소 후 남은 offset을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Cancel(IXeriTrayReorderTarget target, XeriTrayReorderSession session)
        {
            Clear(target);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Target의 모든 button offset을 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Clear(IXeriTrayReorderTarget target)
        {
            XeriTrayReorderVisual.ClearOffsets(target);
        }

    #endregion

    }
}
