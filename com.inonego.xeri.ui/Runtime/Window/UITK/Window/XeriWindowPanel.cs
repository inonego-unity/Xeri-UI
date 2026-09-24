/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowPanel.cs
수정일 : 2026-09-29

# 설명
Xeri 커스텀 윈도우 하나를 표시하는 UITK VisualElement.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 하나를 표시하는 UITK view.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowPanel : VisualElement
    {

    #region 필드

        private const string WINDOW_UXML_PATH = "XeriUI/Window/XeriWindow";
        private const string WINDOW_USS_PATH  = "XeriUI/Window/XeriWindow";
        private const string WINDOWS_THEME_USS_PATH = "XeriUI/Window/Themes/XeriWindow.Windows";
        private const string MAC_THEME_USS_PATH     = "XeriUI/Window/Themes/XeriWindow.Mac";
        private const string MINIMAL_THEME_USS_PATH = "XeriUI/Window/Themes/XeriWindow.Minimal";
        private const string DEFAULT_THEME_CLASS    = XeriWindowThemeClass.Windows;
        private const float WINDOWS_ICON_SIZE       = 8f;
        private const float WINDOWS_STROKE_WIDTH    = 1.25f;
        private const float MAC_ICON_SIZE           = 5f;
        private const float MAC_STROKE_WIDTH        = 0.8f;
        private const float MINIMAL_ICON_SIZE       = 8f;
        private const float MINIMAL_STROKE_WIDTH    = 1.1f;

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar 영역.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement TitleBar => titleBar;

        private readonly VisualElement titleBar = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar icon 영역.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement TitleIcon => titleIcon;

        private readonly VisualElement titleIcon = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar text label.
        /// </summary>
        // ------------------------------------------------------------
        public Label TitleLabel => titleLabel;

        private readonly Label titleLabel = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Window application content hierarchy가 시작되는 Root.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement ContentRoot => contentRoot;

        private readonly VisualElement contentRoot = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Title action 영역.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement TitleActions => titleActions;

        private readonly VisualElement titleActions = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Minimize button.
        /// </summary>
        // ------------------------------------------------------------
        public Button MinimizeButton => minimizeButton;

        private readonly Button minimizeButton = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Maximize button.
        /// </summary>
        // ------------------------------------------------------------
        public Button MaximizeButton => maximizeButton;

        private readonly Button maximizeButton = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Close button.
        /// </summary>
        // ------------------------------------------------------------
        public Button CloseButton => closeButton;

        private readonly Button closeButton = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Minimize button icon.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowButtonIcon MinimizeButtonIcon => minimizeButtonIcon;

        private readonly XeriWindowButtonIcon minimizeButtonIcon = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Maximize button icon.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowButtonIcon MaximizeButtonIcon => maximizeButtonIcon;

        private readonly XeriWindowButtonIcon maximizeButtonIcon = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Close button icon.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowButtonIcon CloseButtonIcon => closeButtonIcon;

        private readonly XeriWindowButtonIcon closeButtonIcon = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Left resize input area.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement ResizeLeft => resizeLeft;

        private readonly VisualElement resizeLeft = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Top resize input area.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement ResizeTop => resizeTop;

        private readonly VisualElement resizeTop = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Right resize input area.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement ResizeRight => resizeRight;

        private readonly VisualElement resizeRight = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Bottom resize input area.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement ResizeBottom => resizeBottom;

        private readonly VisualElement resizeBottom = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Top-left resize input area.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement ResizeTopLeft => resizeTopLeft;

        private readonly VisualElement resizeTopLeft = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Top-right resize input area.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement ResizeTopRight => resizeTopRight;

        private readonly VisualElement resizeTopRight = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Bottom-left resize input area.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement ResizeBottomLeft => resizeBottomLeft;

        private readonly VisualElement resizeBottomLeft = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Bottom-right resize input area.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement ResizeBottomRight => resizeBottomRight;

        private readonly VisualElement resizeBottomRight = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 적용된 theme class.
        /// </summary>
        // ------------------------------------------------------------
        public string ThemeClass => themeClass;

        private string themeClass = DEFAULT_THEME_CLASS;
        private XeriWindowState currentState = XeriWindowState.Normal;

    #endregion

    #region 생성자

        public XeriWindowPanel() : base()
        {
            name = "xeri-window";
            AddToClassList("xeri-window");
            LoadStyleSheet();

            CreateElements
            (
                out var createdTitleBar,
                out var createdTitleIcon,
                out var createdTitleLabel,
                out var createdContentRoot,
                out var createdTitleActions,
                out var createdMinimizeButton,
                out var createdMaximizeButton,
                out var createdCloseButton,
                out var createdResizeLeft,
                out var createdResizeTop,
                out var createdResizeRight,
                out var createdResizeBottom,
                out var createdResizeTopLeft,
                out var createdResizeTopRight,
                out var createdResizeBottomLeft,
                out var createdResizeBottomRight
            );

            titleBar       = createdTitleBar;
            titleIcon      = createdTitleIcon;
            titleLabel     = createdTitleLabel;
            contentRoot    = createdContentRoot;
            titleActions   = createdTitleActions;
            minimizeButton = createdMinimizeButton;
            maximizeButton = createdMaximizeButton;
            closeButton    = createdCloseButton;
            resizeLeft     = createdResizeLeft;
            resizeTop      = createdResizeTop;
            resizeRight    = createdResizeRight;
            resizeBottom   = createdResizeBottom;
            resizeTopLeft  = createdResizeTopLeft;
            resizeTopRight = createdResizeTopRight;
            resizeBottomLeft  = createdResizeBottomLeft;
            resizeBottomRight = createdResizeBottomRight;
            minimizeButtonIcon = CreateControlButtonIcon
            (
                minimizeButton,
                XeriWindowButtonIconType.Minimize,
                "Minimize"
            );
            maximizeButtonIcon = CreateControlButtonIcon
            (
                maximizeButton,
                XeriWindowButtonIconType.Maximize,
                "Maximize"
            );
            closeButtonIcon = CreateControlButtonIcon
            (
                closeButton,
                XeriWindowButtonIconType.Close,
                "Close"
            );

            hierarchy.Add(titleBar);
            hierarchy.Add(contentRoot);
            hierarchy.Add(resizeLeft);
            hierarchy.Add(resizeTop);
            hierarchy.Add(resizeRight);
            hierarchy.Add(resizeBottom);
            hierarchy.Add(resizeTopLeft);
            hierarchy.Add(resizeTopRight);
            hierarchy.Add(resizeBottomLeft);
            hierarchy.Add(resizeBottomRight);

            ApplyTheme(DEFAULT_THEME_CLASS);
            ApplyStructuralLayout();
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Content Root에 view를 부착한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AttachView(VisualElement view)
        {
            contentRoot.Clear();

            if (view != null)
            {
                contentRoot.Add(view);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window titlebar 텍스트를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ApplyTitle(string title)
        {
            if (titleLabel == null) return;

            titleLabel.text = title ?? string.Empty;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window titlebar icon을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ApplyTitleIcon(Texture2D icon)
        {
            if (titleIcon == null) return;

            if (icon == null)
            {
                titleIcon.style.display = DisplayStyle.None;
                titleIcon.style.backgroundImage = StyleKeyword.Null;
                return;
            }

            titleIcon.style.display = DisplayStyle.Flex;
            titleIcon.style.backgroundImage = new StyleBackground(icon);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window 옵션을 button과 resize handle에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ApplyOptions(XeriWindowOptions options)
        {
            ApplyButtonOption(minimizeButton, options.CanMinimize, options.HideDisabledButtons);
            ApplyButtonOption(maximizeButton, options.CanMaximize, options.HideDisabledButtons);
            ApplyButtonOption(closeButton, options.CanClose, options.HideDisabledButtons);

            var resizeDisplay = options.CanResize ? DisplayStyle.Flex : DisplayStyle.None;

            resizeLeft.style.display = resizeDisplay;
            resizeTop.style.display = resizeDisplay;
            resizeRight.style.display = resizeDisplay;
            resizeBottom.style.display = resizeDisplay;
            resizeTopLeft.style.display = resizeDisplay;
            resizeTopRight.style.display = resizeDisplay;
            resizeBottomLeft.style.display = resizeDisplay;
            resizeBottomRight.style.display = resizeDisplay;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window 상태 class를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ApplyState(XeriWindowState state)
        {
            RemoveFromClassList(GetStateClass(currentState));

            currentState = state;

            AddToClassList(GetStateClass(currentState));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window theme class를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ApplyTheme(string theme)
        {
            RemoveFromClassList(XeriWindowThemeClass.Windows);
            RemoveFromClassList(XeriWindowThemeClass.Mac);
            RemoveFromClassList(XeriWindowThemeClass.Minimal);

            themeClass = XeriWindowThemeClass.Normalize(theme);
            AddToClassList(themeClass);
            ApplyControlButtonIconStyle();
            ApplyControlButtonOrder();
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Window USS를 Resources에서 로드해 현재 element에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void LoadStyleSheet()
        {
            AddStyleSheet(WINDOW_USS_PATH);
            AddStyleSheet(WINDOWS_THEME_USS_PATH);
            AddStyleSheet(MAC_THEME_USS_PATH);
            AddStyleSheet(MINIMAL_THEME_USS_PATH);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 Resources 경로의 USS를 현재 element에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AddStyleSheet(string path)
        {
            var styleSheet = Resources.Load<StyleSheet>(path);

            if (styleSheet == null)
            {
                throw new InvalidOperationException($"XeriWindow USS를 로드할 수 없습니다. Path: {path}");
            }

            styleSheets.Add(styleSheet);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window UXML에서 주요 element를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CreateElements
        (
            out VisualElement titleBar,
            out VisualElement titleIcon,
            out Label titleLabel,
            out VisualElement contentRoot,
            out VisualElement titleActions,
            out Button minimizeButton,
            out Button maximizeButton,
            out Button closeButton,
            out VisualElement resizeLeft,
            out VisualElement resizeTop,
            out VisualElement resizeRight,
            out VisualElement resizeBottom,
            out VisualElement resizeTopLeft,
            out VisualElement resizeTopRight,
            out VisualElement resizeBottomLeft,
            out VisualElement resizeBottomRight
        )
        {
            var template = Resources.Load<VisualTreeAsset>(WINDOW_UXML_PATH);

            if (template == null)
            {
                throw new InvalidOperationException($"XeriWindow UXML을 로드할 수 없습니다. Path: {WINDOW_UXML_PATH}");
            }

            var tree = template.CloneTree();

            titleBar       = tree.Q<VisualElement>("title-bar");
            titleIcon      = tree.Q<VisualElement>("title-icon");
            titleLabel     = tree.Q<Label>("title-label");
            contentRoot    = tree.Q<VisualElement>("content-root");
            titleActions   = tree.Q<VisualElement>("title-actions");
            minimizeButton = tree.Q<Button>("minimize-button");
            maximizeButton = tree.Q<Button>("maximize-button");
            closeButton    = tree.Q<Button>("close-button");
            resizeLeft     = tree.Q<VisualElement>("resize-left");
            resizeTop      = tree.Q<VisualElement>("resize-top");
            resizeRight    = tree.Q<VisualElement>("resize-right");
            resizeBottom   = tree.Q<VisualElement>("resize-bottom");
            resizeTopLeft  = tree.Q<VisualElement>("resize-top-left");
            resizeTopRight = tree.Q<VisualElement>("resize-top-right");
            resizeBottomLeft  = tree.Q<VisualElement>("resize-bottom-left");
            resizeBottomRight = tree.Q<VisualElement>("resize-bottom-right");

            if
            (
                titleBar == null || titleIcon == null || titleLabel == null ||
                contentRoot == null || titleActions == null ||
                minimizeButton == null || maximizeButton == null || closeButton == null ||
                resizeLeft == null || resizeTop == null || resizeRight == null ||
                resizeBottom == null || resizeTopLeft == null || resizeTopRight == null ||
                resizeBottomLeft == null || resizeBottomRight == null
            )
            {
                throw new InvalidOperationException("XeriWindow UXML에 필수 element가 없습니다.");
            }

            titleBar.RemoveFromHierarchy();
            contentRoot.RemoveFromHierarchy();
            resizeLeft.RemoveFromHierarchy();
            resizeTop.RemoveFromHierarchy();
            resizeRight.RemoveFromHierarchy();
            resizeBottom.RemoveFromHierarchy();
            resizeTopLeft.RemoveFromHierarchy();
            resizeTopRight.RemoveFromHierarchy();
            resizeBottomLeft.RemoveFromHierarchy();
            resizeBottomRight.RemoveFromHierarchy();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// UXML clone 이후 window 내부 element가 flex 흐름으로 배치되도록 구조 layout을 확정한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ApplyStructuralLayout()
        {
            titleBar.style.position       = Position.Relative;
            titleLabel.style.overflow     = Overflow.Hidden;
            titleLabel.style.whiteSpace   = WhiteSpace.NoWrap;
            minimizeButton.style.position = Position.Relative;
            maximizeButton.style.position = Position.Relative;
            closeButton.style.position    = Position.Relative;
            contentRoot.style.position    = Position.Relative;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Theme에 맞는 control button 표시 순서를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyControlButtonOrder()
        {
            if (themeClass == XeriWindowThemeClass.Mac)
            {
                titleActions.Insert(0, closeButton);
                titleActions.Insert(1, minimizeButton);
                titleActions.Insert(2, maximizeButton);
                return;
            }

            titleActions.Insert(0, minimizeButton);
            titleActions.Insert(1, maximizeButton);
            titleActions.Insert(2, closeButton);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Theme별 control button icon 크기와 두께를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyControlButtonIconStyle()
        {
            var iconSize = WINDOWS_ICON_SIZE;
            var strokeWidth = WINDOWS_STROKE_WIDTH;
            var iconDisplay = DisplayStyle.Flex;

            if (themeClass == XeriWindowThemeClass.Mac)
            {
                iconSize = MAC_ICON_SIZE;
                strokeWidth = MAC_STROKE_WIDTH;
                iconDisplay = DisplayStyle.None;
            }
            else if (themeClass == XeriWindowThemeClass.Minimal)
            {
                iconSize = MINIMAL_ICON_SIZE;
                strokeWidth = MINIMAL_STROKE_WIDTH;
            }

            ApplyIconStyle(minimizeButtonIcon, iconSize, strokeWidth);
            ApplyIconStyle(maximizeButtonIcon, iconSize, strokeWidth);
            ApplyIconStyle(closeButtonIcon, iconSize, strokeWidth);
            ApplyIconDisplay(minimizeButtonIcon, iconDisplay);
            ApplyIconDisplay(maximizeButtonIcon, iconDisplay);
            ApplyIconDisplay(closeButtonIcon, iconDisplay);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Panel 연결 뒤 UXML template style이 다시 계산되어도 window 내부 layout을 유지한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            ApplyStructuralLayout();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Button 표시와 활성 상태를 옵션에 맞춘다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ApplyButtonOption(Button button, bool enabled, bool hideDisabled)
        {
            button.SetEnabled(enabled);
            button.style.display = !enabled && hideDisabled
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Control button text를 비우고 vector icon을 부착한다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriWindowButtonIcon CreateControlButtonIcon
        (
            Button button,
            XeriWindowButtonIconType iconType,
            string tooltip
        )
        {
            button.text = string.Empty;
            button.tooltip = tooltip;

            var icon = new XeriWindowButtonIcon(iconType);
            button.Add(icon);

            return icon;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Control button icon 크기와 두께를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ApplyIconStyle
        (
            XeriWindowButtonIcon icon,
            float iconSize,
            float strokeWidth
        )
        {
            if (icon == null) return;

            icon.IconSize = iconSize;
            icon.StrokeWidth = strokeWidth;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Control button icon의 표시 여부를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ApplyIconDisplay(XeriWindowButtonIcon icon, DisplayStyle display)
        {
            if (icon == null) return;

            icon.style.display = display;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태를 표현하는 USS class 이름을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetStateClass(XeriWindowState state)
        {
            return state switch
            {
                XeriWindowState.Minimized => "xeri-window--minimized",
                XeriWindowState.Maximized => "xeri-window--maximized",
                XeriWindowState.Closed    => "xeri-window--closed",
                _                         => "xeri-window--normal",
            };
        }

    #endregion

    }
}
