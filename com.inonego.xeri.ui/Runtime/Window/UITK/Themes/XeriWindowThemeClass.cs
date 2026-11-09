/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowThemeClass.cs
수정일 : 2026-11-09

# 설명
내장 Theme ID와 Window USS class 상수를 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Window theme ID와 USS class 이름.
    /// </summary>
    // ============================================================
    public static class XeriWindowThemeClass
    {

    #region 상수

        // ------------------------------------------------------------
        /// <summary>
        /// Windows theme을 식별하는 stable ID.
        /// </summary>
        // ------------------------------------------------------------
        public const string WindowsID = "windows";

        // ------------------------------------------------------------
        /// <summary>
        /// Mac theme을 식별하는 stable ID.
        /// </summary>
        // ------------------------------------------------------------
        public const string MacID     = "mac";

        // ------------------------------------------------------------
        /// <summary>
        /// Minimal theme을 식별하는 stable ID.
        /// </summary>
        // ------------------------------------------------------------
        public const string MinimalID = "minimal";

        // ------------------------------------------------------------
        /// <summary>
        /// Windows theme에 적용할 USS class.
        /// </summary>
        // ------------------------------------------------------------
        public const string Windows = "xeri-window--theme-windows";

        // ------------------------------------------------------------
        /// <summary>
        /// Mac theme에 적용할 USS class.
        /// </summary>
        // ------------------------------------------------------------
        public const string Mac     = "xeri-window--theme-mac";

        // ------------------------------------------------------------
        /// <summary>
        /// Minimal theme에 적용할 USS class.
        /// </summary>
        // ------------------------------------------------------------
        public const string Minimal = "xeri-window--theme-minimal";

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 사용자 정의 theme의 공통 USS class.
        /// </summary>
        // ------------------------------------------------------------
        public const string Custom = "xeri-window--theme-custom";

    #endregion

    }
}
