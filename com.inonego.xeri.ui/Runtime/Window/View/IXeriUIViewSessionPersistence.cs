/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriUIViewSessionPersistence.cs
수정일 : 2026-11-09

# 설명
View Source가 선택적으로 구현하는 UI session 저장·복원 계약.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// View Source가 UI session 상태를 저장·복원할 때만 구현하는 계약.
    /// </summary>
    // ============================================================
    public interface IXeriUIViewSessionPersistence
    {

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 View의 UI session 상태를 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        void SaveSession(XeriUIViewScope scope);

        // ------------------------------------------------------------
        /// <summary>
        /// View 획득 전에 UI session 상태를 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        void LoadSession(XeriUIViewScope scope);

    #endregion

    }
}
