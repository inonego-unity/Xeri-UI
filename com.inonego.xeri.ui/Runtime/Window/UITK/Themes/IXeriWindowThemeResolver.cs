/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriWindowThemeResolver.cs
수정일 : 2026-09-20

# 설명
stable theme ID로 Xeri window theme asset을 조회하는 resolver 계약.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Stable theme ID 기반 theme resolver 계약.
    /// </summary>
    // ============================================================
    public interface IXeriWindowThemeResolver
    {

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Theme ID에 대응하는 theme asset을 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        bool TryGetTheme(string id, out XeriWindowThemeAsset theme);

    #endregion

    }
}
