/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowThemeAsset.cs
수정일 : 2026-11-10

# 설명
Xeri Window USS 테마 ID와 스타일시트를 보유하는 ScriptableObject.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 theme asset.
    /// </summary>
    // ============================================================
    [CreateAssetMenu(menuName = "Xeri/UI/Window Theme", fileName = "XeriWindowTheme")]
    public sealed class XeriWindowThemeAsset : ScriptableObject
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Theme stable ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ID => id?.Trim() ?? string.Empty;

        [SerializeField]
        private string id = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// Window 공통 USS.
        /// </summary>
        // ------------------------------------------------------------
        public StyleSheet WindowStyleSheet => windowStyleSheet;

        [SerializeField]
        private StyleSheet windowStyleSheet = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Window theme USS.
        /// </summary>
        // ------------------------------------------------------------
        public StyleSheet WindowThemeStyleSheet => windowThemeStyleSheet;

        [SerializeField]
        private StyleSheet windowThemeStyleSheet = null;

    #endregion

    }
}
