/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowContainer.cs
수정일 : 2026-10-04

# 설명
XeriWindowContainer의 낮은 수준 Panel host와 Registry order 반영을 검증한다.

# 테스트 구성
 C: Container 구성
 W: Panel 부착·분리
 O: Window order
 R: Root 확장
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// XeriWindowContainer 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowContainer
    {

    #region 헬퍼

        // ----------------------------------------------------------------------
        /// <summary>
        /// Registry 등록과 Panel 생성을 외부에서 완료한 뒤 Container에 연결한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static XeriWindowHandle CreateWindow
        (
            XeriWindowContainer container,
            string id,
            string title,
            VisualElement view,
            XeriWindowStackLayer stackLayer,
            out XeriWindowPanel panel
        )
        {
            var options = XeriWindowOptions.Default();
            options.StackLayer = stackLayer;
            var record = new XeriWindowRecord
            {
                ID = id,
                Title = title,
                Pos = Vector2.zero,
                Size = new Vector2(240f, 160f),
                StackLayer = stackLayer,
            };
            panel = container.CreatePanel(record, options);
            panel.AttachView(view);

            var driver = new UITKWindowDriver(panel)
            {
                Pos = record.Pos,
                Size = record.Size,
            };
            driver.CommitState(record.State);
            var controller = new XeriWindowController
            (
                driver,
                options,
                new XeriImmediateWindowStateTransitioner()
            );
            var handle = container.Registry.Register(id, controller, record);
            container.AttachWindow(handle, panel);

            return handle;
        }

    #endregion

    #region C-1: 기본 구성

        // ------------------------------------------------------------
        /// <summary>
        /// 생성된 Container가 Window Layer를 제공한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowContainer_Construct_Layer_제공()
        {
            var container = new XeriWindowContainer(new XeriWindowRegistry());

            Assert.IsNotNull(container.WindowLayer);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Container와 빈 Window Layer는 자체 hit-test를 받지 않는다.
        /// <br/> 실제 Window Panel만 PickingMode.Position으로 입력을 받는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowContainer_Construct_빈영역Picking_무시()
        {
            var container = new XeriWindowContainer(new XeriWindowRegistry());
            CreateWindow
            (
                container,
                "window",
                "Window",
                new Label("Content"),
                XeriWindowStackLayer.Normal,
                out var panel
            );

            Assert.AreEqual(PickingMode.Ignore, container.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, container.WindowLayer.pickingMode);
            Assert.AreEqual(PickingMode.Position, panel.pickingMode);
        }

    #endregion

    #region W-1: 패널 부착

        // ----------------------------------------------------------------------
        /// <summary>
        /// Registry에 등록된 Handle과 준비된 Panel만 표시 hierarchy에 부착한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowContainer_AttachWindow_준비된Panel_부착()
        {
            var container = new XeriWindowContainer(new XeriWindowRegistry());
            var view = new Label("Content");
            var handle = CreateWindow
            (
                container,
                "inventory",
                "Inventory",
                view,
                XeriWindowStackLayer.Normal,
                out var panel
            );

            Assert.IsTrue(container.Registry.Contains(handle));
            Assert.AreEqual(1, container.WindowLayer.childCount);
            Assert.AreSame(panel, container.WindowLayer[0]);
            Assert.AreSame(view, panel.ContentRoot[0]);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Registry에 없는 Handle은 Container 표시 소유권으로 받아들이지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowContainer_AttachWindow_미등록Handle_거부()
        {
            var registry = new XeriWindowRegistry();
            var container = new XeriWindowContainer(registry);
            var otherRegistry = new XeriWindowRegistry();
            var controller = new XeriWindowController
            (
                new UITKWindowDriver(new XeriWindowPanel())
            );
            var handle = otherRegistry.Register("other", controller);

            Assert.Throws<System.InvalidOperationException>
            (
                () => container.AttachWindow(handle, new XeriWindowPanel())
            );
        }

    #endregion

    #region W-2: 패널 분리

        // ------------------------------------------------------------
        /// <summary>
        /// DetachWindow는 Panel 표시만 분리하고 Registry 등록은 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowContainer_DetachWindow_Registry_유지()
        {
            var container = new XeriWindowContainer(new XeriWindowRegistry());
            var handle = CreateWindow
            (
                container,
                "inventory",
                "Inventory",
                new Label("Content"),
                XeriWindowStackLayer.Normal,
                out _
            );

            var detached = container.DetachWindow(handle);

            Assert.IsTrue(detached);
            Assert.AreEqual(0, container.WindowLayer.childCount);
            Assert.IsTrue(container.Registry.Contains(handle));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Dispose는 표시 hierarchy만 비우고 Registry 등록 소유권을 건드리지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowContainer_Dispose_Registry_유지()
        {
            var container = new XeriWindowContainer(new XeriWindowRegistry());
            var handle = CreateWindow
            (
                container,
                "inventory",
                "Inventory",
                new Label("Content"),
                XeriWindowStackLayer.Normal,
                out _
            );

            container.Dispose();

            Assert.AreEqual(0, container.WindowLayer.childCount);
            Assert.IsTrue(container.Registry.Contains(handle));
            Assert.Throws<System.ObjectDisposedException>
            (
                () => container.AttachWindow(handle, new XeriWindowPanel())
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 같은 Handle에 다른 Panel을 중복 연결하면 기존 표시를 보존하고 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowContainer_AttachWindow_동일Handle_다른Panel_거부()
        {
            var container = new XeriWindowContainer(new XeriWindowRegistry());
            var handle = CreateWindow
            (
                container,
                "inventory",
                "Inventory",
                new Label("Content"),
                XeriWindowStackLayer.Normal,
                out var panel
            );

            Assert.Throws<System.InvalidOperationException>
            (
                () => container.AttachWindow(handle, new XeriWindowPanel())
            );
            Assert.AreEqual(1, container.WindowLayer.childCount);
            Assert.AreSame(panel, container.WindowLayer[0]);
        }

    #endregion

    #region O-1: 창 순서

        // ----------------------------------------------------------------------
        /// <summary>
        /// Registry order가 바뀌면 Window Layer의 실제 Panel 순서도 갱신된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowContainer_BringToFront_WindowLayer_Order_갱신()
        {
            var container = new XeriWindowContainer(new XeriWindowRegistry());
            var first = CreateWindow
            (
                container,
                "first",
                "First",
                new Label("First"),
                XeriWindowStackLayer.Normal,
                out _
            );
            CreateWindow
            (
                container,
                "second",
                "Second",
                new Label("Second"),
                XeriWindowStackLayer.Normal,
                out _
            );

            container.Registry.BringToFront(first);

            Assert.AreEqual("Second", GetPanelTitle(container.WindowLayer[0]));
            Assert.AreEqual("First", GetPanelTitle(container.WindowLayer[1]));
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// AlwaysOnTop Window는 Normal Window focus 이후에도 Window Layer 앞쪽을 유지한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowContainer_AlwaysOnTop_WindowLayer_Order_유지()
        {
            var container = new XeriWindowContainer(new XeriWindowRegistry());
            var normal = CreateWindow
            (
                container,
                "normal",
                "Normal",
                new Label("Normal"),
                XeriWindowStackLayer.Normal,
                out _
            );
            var top = CreateWindow
            (
                container,
                "top",
                "Top",
                new Label("Top"),
                XeriWindowStackLayer.AlwaysOnTop,
                out _
            );

            container.Registry.Focus(normal);

            Assert.AreEqual("Normal", GetPanelTitle(container.WindowLayer[0]));
            Assert.AreEqual("Top", GetPanelTitle(container.WindowLayer[1]));
            Assert.IsTrue(container.Registry.Contains(top));
        }

    #endregion

    #region R-1: 루트 확장

        // ----------------------------------------------------------------------
        /// <summary>
        /// Root에 외부 UI를 추가해도 Window Layer order 적용과 섞이지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowContainer_Root에_외부_UI를_붙여도_Window_Order_유지()
        {
            var container = new XeriWindowContainer(new XeriWindowRegistry());
            var external = new Label("External");
            var first = CreateWindow
            (
                container,
                "first",
                "First",
                new Label("First"),
                XeriWindowStackLayer.Normal,
                out _
            );
            CreateWindow
            (
                container,
                "second",
                "Second",
                new Label("Second"),
                XeriWindowStackLayer.Normal,
                out _
            );

            container.Add(external);
            container.Registry.BringToFront(first);

            Assert.AreSame(external, container[1]);
            Assert.AreEqual(2, container.WindowLayer.childCount);
            Assert.AreEqual("Second", GetPanelTitle(container.WindowLayer[0]));
            Assert.AreEqual("First", GetPanelTitle(container.WindowLayer[1]));
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Panel title label text를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetPanelTitle(VisualElement element)
        {
            return ((XeriWindowPanel)element).Q<Label>("title-label").text;
        }

    #endregion

    }
}
