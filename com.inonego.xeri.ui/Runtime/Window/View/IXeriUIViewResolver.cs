/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriUIViewResolver.cs
수정일 : 2026-09-20

# 설명
stable ID로 UITK view source를 조회하는 공통 resolver 계약.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Stable ID로 UI view source를 조회하는 resolver 계약.
    /// </summary>
    // ============================================================
    public interface IXeriUIViewResolver
    {

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Stable ID에 대응하는 view source를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        bool TryGetViewSource(string id, out IXeriUIViewSource viewSource);

    #endregion

    }
}
