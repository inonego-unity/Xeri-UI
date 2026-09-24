/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowResizeManipulator.cs
수정일 : 2026-09-21

# 설명
XeriWindowResizeManipulator의 bounds 변환, 상태 차단과 입력 수명을 검증한다.

# 테스트 구성
 B: Edge/Corner bounds 변환
 S: Window 상태 차단
 L: Attach/Detach cursor 수명
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
    /// XeriWindowResizeManipulator 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowResizeManipulator
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

        // ============================================================
        /// <summary>
        /// 테스트용 resize cursor provider.
        /// </summary>
        // ============================================================
        private sealed class TestResizeCursorProvider : IXeriWindowResizeCursorProvider
        {
            public int ResetCount { get; private set; } = 0;

            public void Apply(XeriWindowResizeMode mode)
            {
                // NONE
            }

            public void Reset()
            {
                ResetCount++;
            }
        }

        private static EditorWindow CreateHostWindow(VisualElement element)
        {
            var window = EditorWindow.CreateInstance<EditorWindow>();

            window.Show();
            window.rootVisualElement.Add(element);

            return window;
        }

        private static Event CreatePointerEvent(EventType eventType, Vector2 pos)
        {
            return new Event
            {
                type = eventType,
                button = 0,
                mousePosition = pos,
            };
        }

        private static void SendPointerDown(VisualElement target, Vector2 pos)
        {
            var systemEvent = CreatePointerEvent(EventType.MouseDown, pos);

            using var pointerEvent = PointerDownEvent.GetPooled(systemEvent);

            pointerEvent.target = target;
            target.SendEvent(pointerEvent);
        }

        private static void SendPointerMove(VisualElement target, Vector2 pos)
        {
            var systemEvent = CreatePointerEvent(EventType.MouseMove, pos);

            using var pointerEvent = PointerMoveEvent.GetPooled(systemEvent);

            pointerEvent.target = target;
            target.SendEvent(pointerEvent);
        }

        private static void SendPointerUp(VisualElement target, Vector2 pos)
        {
            var systemEvent = CreatePointerEvent(EventType.MouseUp, pos);

            using var pointerEvent = PointerUpEvent.GetPooled(systemEvent);

            pointerEvent.target = target;
            target.SendEvent(pointerEvent);
        }

    #endregion

    #region B-1: 가장자리와 모서리 경계 변환

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> TopLeft resize가 MinSize에 걸리면 반대쪽 right/bottom edge를
        /// <br/> 유지한 채 bounds를 보정한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_TopLeft_MinSizeClamp_반대Edge유지()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver
            {
                Pos = new Vector2(100f, 80f),
                Size = new Vector2(300f, 200f),
            };
            var controller = new XeriWindowController(driver);
            var cursorProvider = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator
            (
                panel,
                controller,
                cursorProvider
            );
            var hostWindow = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();

                SendPointerDown(panel.ResizeTopLeft, Vector2.zero);
                SendPointerMove(panel.ResizeTopLeft, new Vector2(500f, 500f));

                Assert.AreEqual(new Vector2(248f, 200f), driver.Pos);
                Assert.AreEqual(new Vector2(152f, 80f), driver.Size);
                Assert.AreEqual(400f, driver.Pos.x + driver.Size.x);
                Assert.AreEqual(280f, driver.Pos.y + driver.Size.y);

                SendPointerUp(panel.ResizeTopLeft, new Vector2(500f, 500f));

                Assert.AreEqual(1, cursorProvider.ResetCount);
            }
            finally
            {
                manipulator.Detach();
                hostWindow.Close();
            }
        }

    #endregion

    #region S-1: 창 상태 차단

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Maximized Window에서는 resize pointer 흐름이 기존 bounds를 변경하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_Maximized_PointerResize_무시()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver
            {
                Pos = new Vector2(100f, 80f),
                Size = new Vector2(300f, 200f),
            };
            var controller = new XeriWindowController(driver);
            var manipulator = new XeriWindowResizeManipulator(panel, controller);
            var hostWindow = CreateHostWindow(panel);
            var initialPos = driver.Pos;
            var initialSize = driver.Size;

            controller.Maximize();

            try
            {
                manipulator.Attach();

                SendPointerDown(panel.ResizeBottomRight, Vector2.zero);
                SendPointerMove(panel.ResizeBottomRight, new Vector2(100f, 100f));
                SendPointerUp(panel.ResizeBottomRight, new Vector2(100f, 100f));

                Assert.AreEqual(initialPos, driver.Pos);
                Assert.AreEqual(initialSize, driver.Size);
            }
            finally
            {
                manipulator.Detach();
                hostWindow.Close();
            }
        }

    #endregion

    #region L-1: 연결와 분리 커서 수명

        // ----------------------------------------------------------------------
        /// <summary>
        /// Attach와 Detach는 주입된 cursor provider의 종료 상태를 한 번 복구한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_AttachDetach_CursorReset()
        {
            var panel = new XeriWindowPanel();
            var controller = new XeriWindowController(new TestWindowDriver());
            var cursorProvider = new TestResizeCursorProvider();

            var manipulator = new XeriWindowResizeManipulator(panel, controller, cursorProvider);

            manipulator.Attach();
            manipulator.Detach();

            Assert.AreEqual(1, cursorProvider.ResetCount);
        }

    #endregion

    }
}
