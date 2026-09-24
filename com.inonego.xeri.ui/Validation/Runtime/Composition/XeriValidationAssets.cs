/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationAssets.cs
수정일 : 2026-10-05

# 설명
통합 Validation Scene이 사용하는 UXML, USS, PanelSettings와 Application child Layout을 한 Asset에서 참조한다.
씬 MonoBehaviour에 개별 UI Asset 참조가 확산되지 않도록 Validation 전용 asset catalog 역할을 맡는다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// Validation의 템플릿과 스타일 참조를 제공한다.
    /// </summary>
    // ============================================================
    [CreateAssetMenu
    (
        fileName = "XeriUIValidationAssets",
        menuName = "Xeri/UI/Validation Assets"
    )]
    public sealed class XeriValidationAssets : ScriptableObject
    {

    #region 필드와 상태

        // ------------------------------------------------------------
        /// <summary>
        /// PanelSettings 참조.
        /// </summary>
        // ------------------------------------------------------------
        public PanelSettings PanelSettings => panelSettings;
        [SerializeField] private PanelSettings panelSettings = null;

        // ------------------------------------------------------------
        /// <summary>
        /// DesktopTemplate 참조.
        /// </summary>
        // ------------------------------------------------------------
        public VisualTreeAsset DesktopTemplate => desktopTemplate;
        [SerializeField] private VisualTreeAsset desktopTemplate = null;

        // ------------------------------------------------------------
        /// <summary>
        /// ChecklistTemplate 참조.
        /// </summary>
        // ------------------------------------------------------------
        public VisualTreeAsset ChecklistTemplate => checklistTemplate;
        [SerializeField] private VisualTreeAsset checklistTemplate = null;

        // ------------------------------------------------------------
        /// <summary>
        /// CoreLabTemplate 참조.
        /// </summary>
        // ------------------------------------------------------------
        public VisualTreeAsset CoreLabTemplate => coreLabTemplate;
        [SerializeField] private VisualTreeAsset coreLabTemplate = null;

        // ------------------------------------------------------------
        /// <summary>
        /// CoreScreenTemplate 참조.
        /// </summary>
        // ------------------------------------------------------------
        public VisualTreeAsset CoreScreenTemplate => coreScreenTemplate;
        [SerializeField] private VisualTreeAsset coreScreenTemplate = null;

        // ------------------------------------------------------------
        /// <summary>
        /// WindowLabTemplate 참조.
        /// </summary>
        // ------------------------------------------------------------
        public VisualTreeAsset WindowLabTemplate => windowLabTemplate;
        [SerializeField] private VisualTreeAsset windowLabTemplate = null;

        // ------------------------------------------------------------
        /// <summary>
        /// ModalLabTemplate 참조.
        /// </summary>
        // ------------------------------------------------------------
        public VisualTreeAsset ModalLabTemplate => modalLabTemplate;
        [SerializeField] private VisualTreeAsset modalLabTemplate = null;

        // ------------------------------------------------------------
        /// <summary>
        /// TaskBoardTemplate 참조.
        /// </summary>
        // ------------------------------------------------------------
        public VisualTreeAsset TaskBoardTemplate => taskBoardTemplate;
        [SerializeField] private VisualTreeAsset taskBoardTemplate = null;

        // ------------------------------------------------------------
        /// <summary>
        /// PreferencesTemplate 참조.
        /// </summary>
        // ------------------------------------------------------------
        public VisualTreeAsset PreferencesTemplate => preferencesTemplate;
        [SerializeField] private VisualTreeAsset preferencesTemplate = null;

        // ------------------------------------------------------------
        /// <summary>
        /// ApplicationTemplate 참조.
        /// </summary>
        // ------------------------------------------------------------
        public VisualTreeAsset ApplicationTemplate => applicationTemplate;
        [SerializeField] private VisualTreeAsset applicationTemplate = null;

        // ------------------------------------------------------------
        /// <summary>
        /// TokensStyle 참조.
        /// </summary>
        // ------------------------------------------------------------
        public StyleSheet TokensStyle => tokensStyle;
        [SerializeField] private StyleSheet tokensStyle = null;

        // ------------------------------------------------------------
        /// <summary>
        /// ComponentsStyle 참조.
        /// </summary>
        // ------------------------------------------------------------
        public StyleSheet ComponentsStyle => componentsStyle;
        [SerializeField] private StyleSheet componentsStyle = null;

        // ------------------------------------------------------------
        /// <summary>
        /// AppsStyle 참조.
        /// </summary>
        // ------------------------------------------------------------
        public StyleSheet AppsStyle => appsStyle;
        [SerializeField] private StyleSheet appsStyle = null;

        // ------------------------------------------------------------
        /// <summary>
        /// LabsStyle 참조.
        /// </summary>
        // ------------------------------------------------------------
        public StyleSheet LabsStyle => labsStyle;
        [SerializeField] private StyleSheet labsStyle = null;

        // ------------------------------------------------------------
        /// <summary>
        /// ApplicationLayout 참조.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationLayout ApplicationLayout => applicationLayout;
        [SerializeField] private PresentationLayout applicationLayout = null;

    #endregion

    #region 스타일 적용

        // ------------------------------------------------------------
        /// <summary>
        /// Root에 공용 컨트롤과 Validation 스타일을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ApplySharedStyles(VisualElement root)
        {
            AddStyle(root, Resources.Load<StyleSheet>("XeriUI/Theme/XeriDesktopTheme"));
            AddStyle(root, tokensStyle);
            AddStyle(root, componentsStyle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Application 콘텐츠에 필요한 스타일을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ApplyAppStyles(VisualElement root)
        {
            ApplySharedStyles(root);
            AddStyle(root, appsStyle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Lab 콘텐츠에 필요한 스타일을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ApplyLabStyles(VisualElement root)
        {
            ApplySharedStyles(root);
            AddStyle(root, labsStyle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 스타일이 중복되지 않도록 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AddStyle(VisualElement root, StyleSheet styleSheet)
        {
            if (root == null || styleSheet == null)
            {
                return;
            }

            if (!root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }
        }

    #endregion

    }
}
