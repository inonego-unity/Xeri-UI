/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowThemeAsset.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 UXML/USS와 Tray theme USS를 묶는 ScriptableObject.
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
        public string ID => id;

        [SerializeField]
        private string id = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// Window UXML template.
        /// </summary>
        // ------------------------------------------------------------
        public VisualTreeAsset WindowTemplate => windowTemplate;

        [SerializeField]
        private VisualTreeAsset windowTemplate = null;

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

        // ------------------------------------------------------------
        /// <summary>
        /// 공통 Tray에 적용할 theme USS.
        /// </summary>
        // ------------------------------------------------------------
        public StyleSheet TrayThemeStyleSheet => trayThemeStyleSheet;

        [SerializeField]
        private StyleSheet trayThemeStyleSheet = null;

    #endregion

    }
}
