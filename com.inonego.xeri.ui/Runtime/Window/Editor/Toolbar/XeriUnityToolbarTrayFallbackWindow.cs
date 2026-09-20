/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriUnityToolbarTrayFallbackWindow.cs
수정일 : 2026-09-20

# 설명
Unity toolbar Tray 주입 실패 시 공통 XeriTrayPanel을 표시하는 fallback EditorWindow.
========================================================================= BLOCK_HEADER_END */

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using inonego.Xeri.UI.Tray;

namespace inonego.Xeri.UI.Window.Editor
{
    // ============================================================
    /// <summary>
    /// Unity toolbar Tray fallback window.
    /// </summary>
    // ============================================================
    public sealed class XeriUnityToolbarTrayFallbackWindow : EditorWindow
    {

    #region 필드

        private IXeriWindowRegistry registry = null;
        private XeriTrayOptions options = null;
        private XeriTrayPanel trayPanel = null;
        private XeriWindowTraySource source = null;
        private XeriTrayController controller = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// fallback window를 열고 Tray를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public static XeriUnityToolbarTrayFallbackWindow Open
        (
            IXeriWindowRegistry registry,
            XeriTrayOptions options = null
        )
        {
            var window = GetWindow<XeriUnityToolbarTrayFallbackWindow>("Xeri Tray");
            window.Configure(registry, options);
            window.Show();

            return window;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Fallback window UI를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public void CreateGUI()
        {
            rootVisualElement.Clear();

            trayPanel = new XeriTrayPanel();
            trayPanel.style.flexGrow = 1f;
            rootVisualElement.Add(trayPanel);

            source = new XeriWindowTraySource(registry);
            controller = new XeriTrayController(source, trayPanel, options);

            trayPanel.OnEntrySelect += OnTrayEntrySelect;
            trayPanel.OnEntryClose  += OnTrayEntryClose;
            controller.Reload();
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Fallback window가 사용할 registry와 option을 설정한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Configure(IXeriWindowRegistry registry, XeriTrayOptions options = null)
        {
            this.registry = registry;
            this.options = options;

            if (trayPanel == null) return;

            CreateGUI();
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
