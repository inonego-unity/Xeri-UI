/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowPanel.cs
수정일 : 2026-09-20

# 설명
XeriWindowPanel UITK view 테스트.

# 테스트 구성
 Q: Element query
 O: Option 반영
 S: State class 반영
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// XeriWindowPanel 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowPanel
    {

    #region Q-1: 주요 Element

        // ------------------------------------------------------------
        /// <summary>
        /// 생성된 panel은 titlebar, content, button, resize handle 참조를 제공한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_Construct_주요_Element_참조_제공()
        {
            var panel = new XeriWindowPanel();

            Assert.IsNotNull(panel.TitleBar);
            Assert.IsNotNull(panel.TitleIcon);
            Assert.IsNotNull(panel.TitleLabel);
            Assert.IsNotNull(panel.ContentSlot);
            Assert.IsNotNull(panel.TitleActions);
            Assert.IsNotNull(panel.MinimizeButton);
            Assert.IsNotNull(panel.MaximizeButton);
            Assert.IsNotNull(panel.CloseButton);
            Assert.IsNotNull(panel.ResizeLeft);
            Assert.IsNotNull(panel.ResizeTop);
            Assert.IsNotNull(panel.ResizeRight);
            Assert.IsNotNull(panel.ResizeBottom);
            Assert.IsNotNull(panel.ResizeTopLeft);
            Assert.IsNotNull(panel.ResizeTopRight);
            Assert.IsNotNull(panel.ResizeBottomLeft);
            Assert.IsNotNull(panel.ResizeBottomRight);
        }

    #endregion

    #region Q-2: Title 표시

        // ------------------------------------------------------------
        /// <summary>
        /// ApplyTitle은 title label text를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_ApplyTitle_TitleLabel_Text_갱신()
        {
            var panel = new XeriWindowPanel();

            panel.ApplyTitle("Inventory");

            Assert.AreEqual("Inventory", panel.TitleLabel.text);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// ApplyTitleIcon은 icon 유무에 따라 title icon 표시를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_ApplyTitleIcon_Icon_표시_갱신()
        {
            var panel = new XeriWindowPanel();
            var icon = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            panel.ApplyTitleIcon(icon);

            Assert.AreEqual(DisplayStyle.Flex, panel.TitleIcon.style.display.value);

            panel.ApplyTitleIcon(null);

            Assert.AreEqual(DisplayStyle.None, panel.TitleIcon.style.display.value);

            Object.DestroyImmediate(icon);
        }

    #endregion

    #region Q-3: Control Button Icon

        // ------------------------------------------------------------
        /// <summary>
        /// Window control button은 UXML text 속성으로 기본 표시 문자를 제공한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_Construct_ControlButton_Icon_제공()
        {
            var panel = new XeriWindowPanel();

            Assert.AreEqual(string.Empty, panel.MinimizeButton.text);
            Assert.AreEqual(string.Empty, panel.MaximizeButton.text);
            Assert.AreEqual(string.Empty, panel.CloseButton.text);

            Assert.AreEqual(XeriWindowButtonIconType.Minimize, panel.MinimizeButtonIcon.IconType);
            Assert.AreEqual(XeriWindowButtonIconType.Maximize, panel.MaximizeButtonIcon.IconType);
            Assert.AreEqual(XeriWindowButtonIconType.Close, panel.CloseButtonIcon.IconType);
            Assert.AreEqual(8f, panel.MinimizeButtonIcon.IconSize);
            Assert.AreEqual(8f, panel.MaximizeButtonIcon.IconSize);
            Assert.AreEqual(8f, panel.CloseButtonIcon.IconSize);
            Assert.AreSame(panel.MinimizeButtonIcon, panel.MinimizeButton.Q<XeriWindowButtonIcon>());
            Assert.AreSame(panel.MaximizeButtonIcon, panel.MaximizeButton.Q<XeriWindowButtonIcon>());
            Assert.AreSame(panel.CloseButtonIcon, panel.CloseButton.Q<XeriWindowButtonIcon>());

            panel.ApplyTheme(XeriWindowThemeClass.MacID);
            Assert.AreEqual(5f, panel.MinimizeButtonIcon.IconSize);
            Assert.AreEqual(5f, panel.MaximizeButtonIcon.IconSize);
            Assert.AreEqual(5f, panel.CloseButtonIcon.IconSize);
            Assert.AreEqual(DisplayStyle.None, panel.MinimizeButtonIcon.style.display.value);
            Assert.AreEqual(DisplayStyle.None, panel.MaximizeButtonIcon.style.display.value);
            Assert.AreEqual(DisplayStyle.None, panel.CloseButtonIcon.style.display.value);

            panel.ApplyTheme(XeriWindowThemeClass.WindowsID);

            Assert.AreEqual(string.Empty, panel.MinimizeButton.text);
            Assert.AreEqual(string.Empty, panel.MaximizeButton.text);
            Assert.AreEqual(string.Empty, panel.CloseButton.text);
            Assert.AreEqual(8f, panel.MinimizeButtonIcon.IconSize);
            Assert.AreEqual(8f, panel.MaximizeButtonIcon.IconSize);
            Assert.AreEqual(8f, panel.CloseButtonIcon.IconSize);
            Assert.AreEqual(DisplayStyle.Flex, panel.MinimizeButtonIcon.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, panel.MaximizeButtonIcon.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, panel.CloseButtonIcon.style.display.value);
            Assert.AreSame(panel.MinimizeButtonIcon, panel.MinimizeButton.Q<XeriWindowButtonIcon>());
            Assert.AreSame(panel.MaximizeButtonIcon, panel.MaximizeButton.Q<XeriWindowButtonIcon>());
            Assert.AreSame(panel.CloseButtonIcon, panel.CloseButton.Q<XeriWindowButtonIcon>());
        }

    #endregion

    #region Q-4: Attach View

        // ------------------------------------------------------------
        /// <summary>
        /// AttachView는 content slot에 view를 부착한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_AttachView_ContentSlot_View_부착()
        {
            var panel = new XeriWindowPanel();
            var view = new Label("Content");

            panel.AttachView(view);

            Assert.AreEqual(1, panel.ContentSlot.childCount);
            Assert.AreSame(view, panel.ContentSlot[0]);
        }

    #endregion

    #region O-1: Option

        // ------------------------------------------------------------
        /// <summary>
        /// HideDisabledButtons 옵션은 비활성화된 button을 숨긴다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_ApplyOptions_HideDisabledButtons_Button_숨김()
        {
            var panel = new XeriWindowPanel();
            var options = XeriWindowOptions.Default();
            options.CanClose = false;
            options.HideDisabledButtons = true;

            panel.ApplyOptions(options);

            Assert.AreEqual(DisplayStyle.None, panel.CloseButton.style.display.value);
        }

    #endregion

    #region S-1: State

        // ------------------------------------------------------------
        /// <summary>
        /// ApplyState는 상태 class를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_ApplyState_State_Class_갱신()
        {
            var panel = new XeriWindowPanel();

            panel.ApplyState(XeriWindowState.Minimized);

            Assert.IsTrue(panel.ClassListContains("xeri-window--minimized"));
            Assert.IsFalse(panel.ClassListContains("xeri-window--normal"));
        }

    #endregion

    #region T-1: Theme

        // ------------------------------------------------------------
        /// <summary>
        /// ApplyTheme은 theme ID를 USS class로 변환해 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_ApplyTheme_ID_Class_적용()
        {
            var panel = new XeriWindowPanel();

            panel.ApplyTheme(XeriWindowThemeClass.MacID);

            Assert.AreEqual(XeriWindowThemeClass.Mac, panel.ThemeClass);
            Assert.IsTrue(panel.ClassListContains(XeriWindowThemeClass.Mac));
            Assert.IsFalse(panel.ClassListContains(XeriWindowThemeClass.Windows));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 알 수 없는 theme 값은 기본 Windows theme으로 정규화한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_ApplyTheme_Unknown_Windows_적용()
        {
            var panel = new XeriWindowPanel();

            panel.ApplyTheme("unknown");

            Assert.AreEqual(XeriWindowThemeClass.Windows, panel.ThemeClass);
            Assert.IsTrue(panel.ClassListContains(XeriWindowThemeClass.Windows));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Mac theme은 control button을 close, minimize, maximize 순서로 배치한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_ApplyTheme_Mac_Button_Order_적용()
        {
            var panel = new XeriWindowPanel();

            panel.ApplyTheme(XeriWindowThemeClass.MacID);

            Assert.AreSame(panel.CloseButton, panel.TitleActions[0]);
            Assert.AreSame(panel.MinimizeButton, panel.TitleActions[1]);
            Assert.AreSame(panel.MaximizeButton, panel.TitleActions[2]);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Windows theme은 control button을 minimize, maximize, close 순서로 배치한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowPanel_ApplyTheme_Windows_Button_Order_적용()
        {
            var panel = new XeriWindowPanel();

            panel.ApplyTheme(XeriWindowThemeClass.WindowsID);

            Assert.AreSame(panel.MinimizeButton, panel.TitleActions[0]);
            Assert.AreSame(panel.MaximizeButton, panel.TitleActions[1]);
            Assert.AreSame(panel.CloseButton, panel.TitleActions[2]);
        }

    #endregion

    }
}
