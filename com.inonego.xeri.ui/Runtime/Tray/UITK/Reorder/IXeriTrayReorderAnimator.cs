/* BLOCK_HEADER_BEGIN =======================================================================
파일명: IXeriTrayReorderAnimator.cs
수정일 : 2026-09-20

# 설명
Tray reorder preview와 정리 애니메이션 계약을 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray reorder 애니메이션 계약.
    /// </summary>
    // ============================================================
    public interface IXeriTrayReorderAnimator
    {
        // ------------------------------------------------------------
        /// <summary>
        /// Drag 중인 entry를 제외한 entry들의 preview offset을 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        void Preview(IXeriTrayReorderTarget target, XeriTrayReorderSession session);

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 확정 후 preview offset을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        void Commit(IXeriTrayReorderTarget target, XeriTrayReorderSession session);

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 취소 후 preview offset을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        void Cancel(IXeriTrayReorderTarget target, XeriTrayReorderSession session);

        // ------------------------------------------------------------
        /// <summary>
        /// Target에 남은 모든 preview offset을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        void Clear(IXeriTrayReorderTarget target);
    }
}
