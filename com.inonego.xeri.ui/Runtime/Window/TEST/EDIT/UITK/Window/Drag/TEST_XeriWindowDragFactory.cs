/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowDragFactory.cs
수정일 : 2026-09-20

# 설명
XeriWindowDragFactory titlebar drag binding 테스트.

# 테스트 구성
 F: Factory 생성
 T: Titlebar 상호작용
========================================================================= BLOCK_HEADER_END */

using UnityEditor;
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
    /// XeriWindowDragFactory 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowDragFactory
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트용 윈도우 driver.
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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 테스트 대상 VisualElement가 UI Toolkit panel을 갖도록 EditorWindow에 부착한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private static EditorWindow CreateHostWindow(VisualElement element)
        {
            var window = EditorWindow.CreateInstance<EditorWindow>();

            window.Show();
            window.rootVisualElement.Add(element);

            return window;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Mouse event를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Event CreateMouseEvent(EventType eventType, int clickCount)
        {
            return new Event
            {
                type = eventType,
                button = 0,
                clickCount = clickCount,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Pointer event를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Event CreatePointerEvent(EventType eventType, Vector2 pos)
        {
            return new Event
            {
                type = eventType,
                button = 0,
                mousePosition = pos,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// MouseDownEvent를 대상 element에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SendMouseDown
        (
            VisualElement target,
            int clickCount
        )
        {
            var systemEvent = CreateMouseEvent(EventType.MouseDown, clickCount);

            using var mouseDownEvent = MouseDownEvent.GetPooled(systemEvent);

            mouseDownEvent.target = target;
            target.SendEvent(mouseDownEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// MouseUpEvent를 대상 element에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SendMouseUp
        (
            VisualElement target,
            int clickCount
        )
        {
            var systemEvent = CreateMouseEvent(EventType.MouseUp, clickCount);

            using var mouseUpEvent = MouseUpEvent.GetPooled(systemEvent);

            mouseUpEvent.target = target;
            target.SendEvent(mouseUpEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// PointerDownEvent를 대상 element에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SendPointerDown(VisualElement target, Vector2 pos)
        {
            var systemEvent = CreatePointerEvent(EventType.MouseDown, pos);

            using var pointerDownEvent = PointerDownEvent.GetPooled(systemEvent);

            pointerDownEvent.target = target;
            target.SendEvent(pointerDownEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// PointerMoveEvent를 대상 element에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SendPointerMove(VisualElement target, Vector2 pos)
        {
            var systemEvent = CreatePointerEvent(EventType.MouseMove, pos);

            using var pointerMoveEvent = PointerMoveEvent.GetPooled(systemEvent);

            pointerMoveEvent.target = target;
            target.SendEvent(pointerMoveEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// PointerUpEvent를 대상 element에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SendPointerUp(VisualElement target, Vector2 pos)
        {
            var systemEvent = CreatePointerEvent(EventType.MouseUp, pos);

            using var pointerUpEvent = PointerUpEvent.GetPooled(systemEvent);

            pointerUpEvent.target = target;
            target.SendEvent(pointerUpEvent);
        }

    #endregion

    #region F-1: 독립 바인딩

        // ------------------------------------------------------------
        /// <summary>
        /// Factory는 창마다 독립적인 titlebar drag binding을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowDragFactory_CreateTitleBarDrag_창마다_독립_Binding_생성()
        {
            var factory = new XeriWindowDragFactory();
            var first = factory.CreateTitleBarDrag(new XeriWindowPanel(), CreateController());
            var second = factory.CreateTitleBarDrag(new XeriWindowPanel(), CreateController());

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
            Assert.AreNotSame(first.DragManipulator, second.DragManipulator);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Titlebar drag는 titlebar layout을 absolute로 변경하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowDragFactory_CreateTitleBarDrag_TitleBar_Absolute_강제_안함()
        {
            var factory = new XeriWindowDragFactory();
            var drag = factory.CreateTitleBarDrag(new XeriWindowPanel(), CreateController());

            Assert.IsFalse(drag.DragManipulator.ForceAbsolutePosition);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Titlebar drag는 이동 중인 window 내부 좌표계 대신
        /// <br/> 안정적인 panel 좌표 provider를 사용한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowDragFactory_CreateTitleBarDrag_TitleBar_CoordinateProvider_사용()
        {
            var factory = new XeriWindowDragFactory();
            var drag = factory.CreateTitleBarDrag(new XeriWindowPanel(), CreateController());

            Assert.IsInstanceOf<XeriWindowTitleBarCoordinateProvider>
            (
                drag.DragManipulator.CoordinateProvider
            );
        }

    #endregion

    #region T-1: 제목 동작 필터

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Title action 내부 target은 titlebar double click 대상으로 처리하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTitleBarManipulator_TitleAction_Target_판별()
        {
            var panel = new XeriWindowPanel();
            var manipulator = new XeriWindowTitleBarManipulator(panel, CreateController());

            Assert.IsTrue(manipulator.IsTitleActionTarget(panel.MinimizeButton));
            Assert.IsTrue(manipulator.IsTitleActionTarget(panel.MaximizeButton));
            Assert.IsTrue(manipulator.IsTitleActionTarget(panel.CloseButton));
            Assert.IsTrue(manipulator.IsTitleActionTarget(panel.CloseButtonIcon));
            Assert.IsFalse(manipulator.IsTitleActionTarget(panel.TitleBar));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Titlebar click 대상 판정은 좌클릭 titlebar target에서만 처리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTitleBarManipulator_TitleBar_Click_Target_판정()
        {
            var panel = new XeriWindowPanel();
            var manipulator = new XeriWindowTitleBarManipulator(panel, CreateController());

            Assert.IsTrue(manipulator.CanAcceptTitleBarClick(panel.TitleBar, 0));
            Assert.IsFalse(manipulator.CanAcceptTitleBarClick(panel.TitleBar, 1));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar 밖 target은 double click으로 처리하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTitleBarManipulator_TitleBar_외부_Target_DoubleClick_제외()
        {
            var panel = new XeriWindowPanel();
            var manipulator = new XeriWindowTitleBarManipulator(panel, CreateController());
            var externalElement = new VisualElement();

            Assert.IsFalse(manipulator.CanAcceptTitleBarClick(externalElement, 0));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Control button click은 titlebar double click으로 처리하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTitleBarManipulator_TitleAction_Click_DoubleClick_제외()
        {
            var panel = new XeriWindowPanel();
            var manipulator = new XeriWindowTitleBarManipulator(panel, CreateController());

            Assert.IsFalse(manipulator.CanAcceptTitleBarClick(panel.MinimizeButton, 0));
            Assert.IsFalse(manipulator.CanAcceptTitleBarClick(panel.MaximizeButton, 0));
            Assert.IsFalse(manipulator.CanAcceptTitleBarClick(panel.CloseButton, 0));
            Assert.IsFalse(manipulator.CanAcceptTitleBarClick(panel.CloseButtonIcon, 0));
        }

    #endregion

    #region T-2: 제목 표시줄 이벤트 흐름

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Titlebar에 전달된 Unity MouseDownEvent는 MouseUp 전까지 maximize로 이어지지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTitleBarManipulator_TitleBar_MouseDown_Click_Maximize_지연()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            var manipulator = new XeriWindowTitleBarManipulator(panel, controller);
            var hostWindow = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();

                SendMouseDown(panel.TitleBar, 2);

                Assert.AreEqual(XeriWindowState.Normal, driver.State);
            }
            finally
            {
                manipulator.Detach();
                hostWindow.Close();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Titlebar에 전달된 두 번의 Unity MouseDownEvent와
        /// <br/> MouseUpEvent click은 maximize로 이어진다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTitleBarManipulator_TitleBar_MouseDown_MouseUp_두번_Click_Maximize()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            var manipulator = new XeriWindowTitleBarManipulator(panel, controller);
            var hostWindow = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();

                SendMouseDown(panel.TitleBar, 1);
                SendMouseUp(panel.TitleBar, 1);
                SendMouseDown(panel.TitleBar, 1);
                SendMouseUp(panel.TitleBar, 1);

                Assert.AreEqual(XeriWindowState.Maximized, driver.State);
            }
            finally
            {
                manipulator.Detach();
                hostWindow.Close();
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 외부 clickCount가 섞인 첫 titlebar click은 double click으로 처리하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTitleBarManipulator_TitleBar_첫_ClickCount_2_Maximize_제외()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            var manipulator = new XeriWindowTitleBarManipulator(panel, controller);
            var hostWindow = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();

                SendMouseDown(panel.TitleBar, 2);
                SendMouseUp(panel.TitleBar, 2);

                Assert.AreEqual(XeriWindowState.Normal, driver.State);
            }
            finally
            {
                manipulator.Detach();
                hostWindow.Close();
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Titlebar에 전달된 Unity MouseDownEvent single click은 maximize로 이어지지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTitleBarManipulator_TitleBar_MouseDown_SingleClick_Maximize_제외()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            var manipulator = new XeriWindowTitleBarManipulator(panel, controller);
            var hostWindow = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();

                SendMouseDown(panel.TitleBar, 1);

                Assert.AreEqual(XeriWindowState.Normal, driver.State);
            }
            finally
            {
                manipulator.Detach();
                hostWindow.Close();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Control button 영역의 Unity MouseDownEvent double click은
        /// <br/> maximize로 이어지지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTitleBarManipulator_TitleAction_MouseDown_DoubleClick_Maximize_제외()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            var manipulator = new XeriWindowTitleBarManipulator(panel, controller);
            var hostWindow = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();

                SendMouseDown(panel.CloseButton, 2);

                Assert.AreEqual(XeriWindowState.Normal, driver.State);
            }
            finally
            {
                manipulator.Detach();
                hostWindow.Close();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Maximized 상태에서 double click 후보 입력이 drag로 전환되면
        /// <br/> MouseUp double click 토글은 취소된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTitleBarManipulator_Maximized_DoubleClick_Drag_전환시_Toggle_취소()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver
            {
                State = XeriWindowState.Maximized,
            };
            var controller = new XeriWindowController(driver);
            var manipulator = new XeriWindowTitleBarManipulator(panel, controller);
            var hostWindow = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();

                SendMouseDown(panel.TitleBar, 2);

                Assert.AreEqual(XeriWindowState.Maximized, driver.State);

                SendPointerDown(panel.TitleBar, new Vector2(100f, 10f));
                SendPointerMove(panel.TitleBar, new Vector2(120f, 10f));

                Assert.AreEqual(XeriWindowState.Normal, driver.State);

                SendMouseUp(panel.TitleBar, 2);
                SendPointerUp(panel.TitleBar, new Vector2(120f, 10f));

                Assert.AreEqual(XeriWindowState.Normal, driver.State);
            }
            finally
            {
                manipulator.Detach();
                hostWindow.Close();
            }
        }

    #endregion

    }
}
