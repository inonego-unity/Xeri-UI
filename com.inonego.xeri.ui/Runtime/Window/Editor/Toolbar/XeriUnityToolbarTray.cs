/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriUnityToolbarTray.cs
수정일 : 2026-09-20

# 설명
Unity Editor 상단 toolbar에 공통 XeriTrayPanel을 주입하는 Editor 전용 host.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Linq;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

using inonego.Xeri.UI.Tray;

namespace inonego.Xeri.UI.Window.Editor
{
    // ============================================================
    /// <summary>
    /// Unity Editor toolbar에 Xeri window tray를 붙이는 host.
    /// </summary>
    // ============================================================
    public sealed class XeriUnityToolbarTray
    {

    #region 필드

        private const string TrayRootName = "xeri-unity-toolbar-tray";
        private const string TrayUssClass = "xeri-tray--unity-toolbar";
        private const string TrayUssPath  = "XeriUI/Tray/XeriTrayUnityToolbar";

        // ------------------------------------------------------------
        /// <summary>
        /// 주입된 공통 Tray panel.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayPanel TrayPanel => trayPanel;

        private XeriTrayPanel trayPanel = null;

        private XeriWindowTraySource source = null;
        private XeriTrayController controller = null;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Unity Editor toolbar를 찾아 Tray를 주입한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool InstallToUnityToolbar
        (
            IXeriWindowRegistry registry,
            XeriTrayOptions options = null
        )
        {
            var toolbarRoot = FindUnityToolbarRoot();
            if (toolbarRoot == null) return false;

            return Install(toolbarRoot, registry, options);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 toolbar root에 Tray를 주입한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Install
        (
            VisualElement toolbarRoot,
            IXeriWindowRegistry registry,
            XeriTrayOptions options = null
        )
        {
            if (toolbarRoot == null) return false;
            if (registry == null) return false;

            var exists = toolbarRoot.Q<XeriTrayPanel>(TrayRootName);
            if (exists != null)
            {
                trayPanel = exists;
                AddToolbarStyleSheet(trayPanel);
                BindTray(registry, options);
                return true;
            }

            trayPanel = new XeriTrayPanel { name = TrayRootName };
            trayPanel.AddToClassList(TrayUssClass);
            AddToolbarStyleSheet(trayPanel);
            toolbarRoot.Add(trayPanel);

            BindTray(registry, options);

            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray 표시를 즉시 다시 그린다.
        /// </summary>
        // ------------------------------------------------------------
        public void Reload()
        {
            controller?.Reload();
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 공통 Tray panel과 window source를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BindTray(IXeriWindowRegistry registry, XeriTrayOptions options)
        {
            if (controller != null) return;

            source = new XeriWindowTraySource(registry);
            controller = new XeriTrayController(source, trayPanel, CreateOptions(options));

            trayPanel.OnEntrySelect += OnTrayEntrySelect;
            trayPanel.OnEntryClose  += OnTrayEntryClose;

            controller.Reload();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity toolbar root VisualElement를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static VisualElement FindUnityToolbarRoot()
        {
            var toolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null) return null;

            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            var toolbar = toolbars.FirstOrDefault() as EditorWindow;

            return toolbar != null ? toolbar.rootVisualElement : null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity toolbar 전용 Tray 표시 옵션을 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriTrayOptions CreateOptions(XeriTrayOptions options)
        {
            var source = options ?? new XeriTrayOptions
            {
                VisibleContent = XeriTrayContent.Icon |
                                 XeriTrayContent.Badge |
                                 XeriTrayContent.StateMarker,
            };

            return new XeriTrayOptions
            {
                VisibleContent = source.VisibleContent,
                UssClass = string.IsNullOrEmpty(source.UssClass)
                ? TrayUssClass
                : $"{source.UssClass} {TrayUssClass}",
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity toolbar 전용 Tray USS를 panel에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AddToolbarStyleSheet(VisualElement element)
        {
            var styleSheet = Resources.Load<StyleSheet>(TrayUssPath);

            if (styleSheet == null)
            {
                throw new InvalidOperationException($"XeriUnityToolbarTray USS를 로드할 수 없습니다. Path: {TrayUssPath}");
            }

            element.styleSheets.Add(styleSheet);
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 선택을 show normal 명령으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnTrayEntrySelect(object sender, XeriTrayEventArgs e)
        {
            source.Restore(e.Entry);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 닫기 입력을 close 명령으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnTrayEntryClose(object sender, XeriTrayEventArgs e)
        {
            source.Close(e.Entry);
        }

    #endregion

    }
}
