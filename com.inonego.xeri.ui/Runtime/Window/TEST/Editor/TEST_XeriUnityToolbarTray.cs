/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriUnityToolbarTray.cs
수정일 : 2026-09-20

# 설명
Unity toolbar Tray host 테스트.

# 테스트 구성
 I: Fake toolbar 주입
 T: Tray 표시
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego.Xeri.UI.Window;
using inonego.Xeri.UI.Window.Editor;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// XeriUnityToolbarTray 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriUnityToolbarTray
    {

    #region 헬퍼
        // ============================================================
        /// <summary>
        /// 테스트용 window driver.
        /// </summary>
        // ============================================================
        private sealed class TestWindowDriver : IXeriWindowDriver
        {
            public Vector2 Pos { get; set; } = Vector2.zero;
            public Vector2 Size { get; set; } = new Vector2(200f, 120f);
            public XeriWindowState State { get; set; } = XeriWindowState.Normal;
            public XeriWindowState VisualState { get; private set; } = XeriWindowState.Normal;

            public Rect Bounds
            {
                get => new Rect(Pos, Size);
                set
                {
                    Pos = value.position;
                    Size = value.size;
                }
            }

            public void SetVisible(bool visible) {}

            public void CommitState(XeriWindowState state)
            {
                State = state;
                ApplyVisualState(state);
            }

            public void ApplyVisualState(XeriWindowState state)
            {
                VisualState = state;
            }

            public void ApplyBounds(Rect bounds)
            {
                Bounds = bounds;
            }

            public void ApplyMaximizedBounds() {}
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트용 controller를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriWindowController CreateController()
        {
            return new XeriWindowController(new TestWindowDriver());
        }

    #endregion

    #region I-1: Fake Toolbar 주입

        // ------------------------------------------------------------
        /// <summary>
        /// Install은 지정된 fake toolbar root에 공통 TrayPanel을 한 번만 주입한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_Install_FakeRoot_TrayPanel_한번만_주입()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            var toolbarTray = new XeriUnityToolbarTray();

            var first = toolbarTray.Install(root, registry);
            var second = toolbarTray.Install(root, registry);

            Assert.IsTrue(first);
            Assert.IsTrue(second);
            Assert.AreEqual(1, root.Query("xeri-unity-toolbar-tray").ToList().Count);
            Assert.IsTrue(toolbarTray.TrayPanel.ClassListContains("xeri-tray--unity-toolbar"));
        }

    #endregion

    #region I-2: 기존 Tray 참조

        // ------------------------------------------------------------
        /// <summary>
        /// 이미 주입된 Tray가 있으면 새 host도 기존 TrayPanel 참조를 잡는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_Install_기존Tray_참조()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            var firstHost = new XeriUnityToolbarTray();
            var secondHost = new XeriUnityToolbarTray();

            firstHost.Install(root, registry);
            secondHost.Install(root, registry);

            Assert.AreSame(firstHost.TrayPanel, secondHost.TrayPanel);
        }

    #endregion

    #region T-1: Tray 표시

        // ------------------------------------------------------------
        /// <summary>
        /// Minimized window가 toolbar TrayPanel entry로 표시된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_MinimizedWindow_TrayEntry_표시()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("inventory", CreateController());
            var toolbarTray = new XeriUnityToolbarTray();

            registry.TryGetController(handle, out var controller);
            controller.Minimize();

            toolbarTray.Install(root, registry);
            toolbarTray.Reload();

            var container = toolbarTray.TrayPanel.Q<VisualElement>("entry-container");

            Assert.IsNotNull(container);
            Assert.AreEqual(1, container.childCount);
        }

    #endregion

    }
}
