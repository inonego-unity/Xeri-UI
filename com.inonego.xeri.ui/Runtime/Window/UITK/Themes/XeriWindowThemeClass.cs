/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowThemeClass.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 theme ID와 USS class 이름을 관리한다.
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

        public const string WindowsID = "windows";
        public const string MacID     = "mac";
        public const string MinimalID = "minimal";

        public const string Windows = "xeri-window--theme-windows";
        public const string Mac     = "xeri-window--theme-mac";
        public const string Minimal = "xeri-window--theme-minimal";

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// Theme ID 또는 USS class 이름을 실제 적용할 class 이름으로 변환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static string Normalize(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme)) return Windows;

            return theme.Trim() switch
            {
                WindowsID or Windows => Windows,
                MacID or Mac         => Mac,
                MinimalID or Minimal => Minimal,
                _                    => Windows,
            };
        }

    #endregion

    }
}
