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

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Tray;
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
            public PresentationAlpha Alpha { get; } = new();
            public PresentationVisibility Visibility { get; } = new();

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

            public void ApplyMaximizedBounds()
            {
                // NONE
            }
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Install은 지정된 fake toolbar root에 공통 TrayPanel을 한 번만 주입한다.
        /// </summary>
        // ----------------------------------------------------------------------
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

    #region I-2: 중복 Host

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 이미 다른 host가 주입한 Tray에는 두 번째 Controller binding을 만들지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_Install_다른Host_중복거부()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            var firstHost = new XeriUnityToolbarTray();
            var secondHost = new XeriUnityToolbarTray();

            Assert.IsTrue(firstHost.Install(root, registry));
            Assert.IsFalse(secondHost.Install(root, registry));

            Assert.IsNotNull(firstHost.TrayPanel);
            Assert.IsNull(secondHost.TrayPanel);
            Assert.AreEqual(1, root.Query("xeri-unity-toolbar-tray").ToList().Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// owner host Dispose 후에는 Panel이 제거되어 새 host가 다시 설치할 수 있다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_Dispose_OwnerPanel제거_재설치가능()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            var firstHost = new XeriUnityToolbarTray();
            var secondHost = new XeriUnityToolbarTray();

            Assert.IsTrue(firstHost.Install(root, registry));
            firstHost.Dispose();

            Assert.AreEqual(0, root.Query("xeri-unity-toolbar-tray").ToList().Count);
            Assert.IsTrue(secondHost.Install(root, registry));
            Assert.AreEqual(1, root.Query("xeri-unity-toolbar-tray").ToList().Count);
        }

    #endregion

    #region I-3: Options

        // ----------------------------------------------------------------------
        /// <summary>
        /// Toolbar 전용 class를 별도로 적용하면서 caller의 Tray options를 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_Install_Options_보존()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            var toolbarTray = new XeriUnityToolbarTray();
            var options = new XeriTrayOptions
            {
                UssClass = "custom-tray",
                Reorderable = true,
                ReorderAxis = XeriTrayReorderAxis.Vertical,
                AnimateReorder = false,
            };

            Assert.IsTrue(toolbarTray.Install(root, registry, options));

            Assert.IsTrue(toolbarTray.TrayPanel.ClassListContains("custom-tray"));
            Assert.IsTrue
            (
                toolbarTray.TrayPanel.ClassListContains("xeri-tray--unity-toolbar")
            );
            Assert.IsTrue(toolbarTray.TrayPanel.Reorderable);
            Assert.AreEqual
            (
                XeriTrayReorderAxis.Vertical,
                toolbarTray.TrayPanel.ReorderAxis
            );
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
