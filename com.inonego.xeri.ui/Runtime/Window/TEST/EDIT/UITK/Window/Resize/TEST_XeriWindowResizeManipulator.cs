/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowResizeManipulator.cs
수정일 : 2026-11-08

# 설명
XeriWindowResizeManipulator의 bounds 변환, 상태 차단과 입력 수명을 검증한다.

# 테스트 구성
- B-1: Edge/Corner bounds 변환
- S-1: Window 상태 차단
- P-1: 포인터 시작·예외 및 데이터 보존
- P-2: 캡처 소유권과 종료
- P-3: 복합 버튼 종료 정책
- L-1: Attach/Detach 정리와 cursor 수명
- L-2: 다중 Window 커서 소유권
========================================================================= BLOCK_HEADER_END */

using System;
using System.Reflection;

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
            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Window의 투명도 표현 상태.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationAlpha Alpha { get; } = new();

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Window의 표시 여부 상태.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationVisibility Visibility { get; } = new();

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Window의 현재 위치.
            /// </summary>
            // ------------------------------------------------------------
            public Vector2 Pos { get; set; } = Vector2.zero;

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Window의 현재 크기.
            /// </summary>
            // ------------------------------------------------------------
            public Vector2 Size { get; set; } = new Vector2(200f, 120f);

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Window의 논리 상태.
            /// </summary>
            // ------------------------------------------------------------
            public XeriWindowState State { get; set; } = XeriWindowState.Normal;

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Window에 적용된 시각 상태.
            /// </summary>
            // ------------------------------------------------------------
            public XeriWindowState VisualState { get; private set; } = XeriWindowState.Normal;

            // ------------------------------------------------------------
            /// <summary>
            /// Bounds 적용 시 일회성 예외 주입 여부.
            /// </summary>
            // ------------------------------------------------------------
            public bool ThrowOnApplyBounds { get; set; } = false;

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Window의 위치와 크기를 함께 조회·설정한다.
            /// </summary>
            // ------------------------------------------------------------
            public Rect Bounds
            {
                get => new Rect(Pos, Size);
                set
                {
                    Pos = value.position;
                    Size = value.size;
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Driver 상태를 확정한다.
            /// </summary>
            // ------------------------------------------------------------
            public void CommitState(XeriWindowState state)
            {
                State = state;
                ApplyVisualState(state);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Driver의 시각 상태를 반영한다.
            /// </summary>
            // ------------------------------------------------------------
            public void ApplyVisualState(XeriWindowState state)
            {
                VisualState = state;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Resize Bounds 적용 실패를 주입하거나 Bounds를 저장한다.
            /// </summary>
            // ------------------------------------------------------------
            public void ApplyBounds(Rect bounds)
            {
                if (ThrowOnApplyBounds)
                {
                    ThrowOnApplyBounds = false;
                    throw new InvalidOperationException("injected resize bounds failure");
                }

                Bounds = bounds;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Driver에서는 최대화 Bounds 변경을 생략한다.
            /// </summary>
            // ------------------------------------------------------------
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
            // ------------------------------------------------------------
            /// <summary>
            /// Cursor 복원 호출 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int ResetCount { get; private set; } = 0;

            // ------------------------------------------------------------
            /// <summary>
            /// Cursor 적용 호출 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int ApplyCount { get; private set; } = 0;

            // ------------------------------------------------------------
            /// <summary>
            /// Cursor 적용 중 실행할 테스트 callback.
            /// </summary>
            // ------------------------------------------------------------
            public Action<XeriWindowResizeMode> OnApply { get; set; } = null;

            // ------------------------------------------------------------
            /// <summary>
            /// Cursor 복원 중 실행할 테스트 callback.
            /// </summary>
            // ------------------------------------------------------------
            public Action OnReset { get; set; } = null;

            // ------------------------------------------------------------
            /// <summary>
            /// Cursor 적용 횟수와 재진입 callback을 기록한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Apply(XeriWindowResizeMode mode)
            {
                ApplyCount++;
                OnApply?.Invoke(mode);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Cursor 복원 횟수와 재진입 callback을 기록한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Reset()
            {
                ResetCount++;
                OnReset?.Invoke();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 요소를 실제 UI Toolkit panel에 부착한다.
        /// </summary>
        // ------------------------------------------------------------
        private static EditorWindow CreateHostWindow(VisualElement element)
        {
            var window = EditorWindow.CreateInstance<EditorWindow>();

            window.Show();
            window.rootVisualElement.Add(element);

            return window;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 버튼과 좌표로 테스트 포인터 입력을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Event CreatePointerEvent(EventType eventType, Vector2 pos, int button = 0)
        {
            return new Event
            {
                type = eventType,
                button = button,
                mousePosition = pos,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 전역 커서 소유권을 테스트용으로 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriWindowResizeCursorProvider GetCursorOwner()
        {
            var field = typeof(XeriWindowResizeCursorProvider).GetField
            (
                "currentOwner",
                BindingFlags.Static | BindingFlags.NonPublic
            );

            Assert.IsNotNull(field);
            return (XeriWindowResizeCursorProvider)field.GetValue(null);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Provider가 소유한 임시 커서 텍스처를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Texture2D GetCursorTexture(XeriWindowResizeCursorProvider provider)
        {
            var field = typeof(XeriWindowResizeCursorProvider).GetField
            (
                "activeCursor",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.IsNotNull(field);
            return (Texture2D)field.GetValue(provider);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 대상 handle에 PointerDownEvent를 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SendPointerDown(VisualElement target, Vector2 pos, int button = 0)
        {
            var systemEvent = CreatePointerEvent(EventType.MouseDown, pos, button);

            using var pointerEvent = PointerDownEvent.GetPooled(systemEvent);

            pointerEvent.target = target;
            target.SendEvent(pointerEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 대상 handle에 PointerMoveEvent를 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SendPointerMove(VisualElement target, Vector2 pos)
        {
            var systemEvent = CreatePointerEvent(EventType.MouseMove, pos);

            using var pointerEvent = PointerMoveEvent.GetPooled(systemEvent);

            pointerEvent.target = target;
            target.SendEvent(pointerEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 대상 handle에 PointerUpEvent를 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SendPointerUp(VisualElement target, Vector2 pos, int button = 0)
        {
            var systemEvent = CreatePointerEvent(EventType.MouseUp, pos, button);

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

    #region P-1: 포인터 시작·예외 및 데이터 보존

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Resize 중 중복 Down은 시작 기준을 덮어쓰지 않으며 우클릭은 Resize를 시작하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_중복Down과우클릭_기존포인터보존()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator(panel, new XeriWindowController(driver), cursor);
            var host = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();

                SendPointerDown(panel.ResizeRight, Vector2.zero);
                var initialApplyCount = cursor.ApplyCount;
                SendPointerDown(panel.ResizeRight, new Vector2(100f, 0f));
                Assert.AreEqual(initialApplyCount, cursor.ApplyCount);

                SendPointerMove(panel.ResizeRight, new Vector2(40f, 0f));
                Assert.AreEqual(240f, driver.Size.x);
                SendPointerUp(panel.ResizeRight, new Vector2(40f, 0f));

                SendPointerDown(panel.ResizeBottom, Vector2.zero, 1);
                SendPointerMove(panel.ResizeBottom, new Vector2(0f, 40f));
                Assert.AreEqual(120f, driver.Size.y);
            }
            finally
            {
                manipulator.Detach();
                host.Close();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 시작 중 커서 적용 callback이 Detach해도 캡처가 남지 않고 재연결된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_시작중Detach_소유권복구()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator
            (
                panel,
                new XeriWindowController(driver),
                cursor
            );
            var host = CreateHostWindow(panel);

            try
            {
                cursor.OnApply = _ => manipulator.Detach();
                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);

                Assert.IsFalse(panel.ResizeRight.HasPointerCapture(PointerId.mousePointerId));
                Assert.AreEqual(1, cursor.ResetCount);

                cursor.OnApply = null;
                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerMove(panel.ResizeRight, new Vector2(35f, 0f));
                SendPointerUp(panel.ResizeRight, new Vector2(35f, 0f));

                Assert.AreEqual(235f, driver.Size.x);
                Assert.AreEqual(2, cursor.ResetCount);
            }
            finally
            {
                cursor.OnApply = null;
                manipulator.Detach();
                host.Close();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 시작 중 Up이 재진입하면 capture 없이 종료하고 다음 입력을 허용한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_시작중Up_활성화방지()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator
            (
                panel,
                new XeriWindowController(driver),
                cursor
            );
            var host = CreateHostWindow(panel);
            var method = typeof(XeriWindowResizeManipulator).GetMethod
            (
                "OnPointerUp",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(method);

            try
            {
                cursor.OnApply = _ =>
                {
                    using var up = PointerUpEvent.GetPooled
                    (
                        CreatePointerEvent(EventType.MouseUp, Vector2.zero)
                    );
                    up.target = panel.ResizeRight;
                    method.Invoke
                    (
                        manipulator,
                        new object[]
                        {
                            up,
                        }
                    );
                };

                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);

                Assert.IsFalse(panel.ResizeRight.HasPointerCapture(PointerId.mousePointerId));
                Assert.AreEqual(1, cursor.ResetCount);

                cursor.OnApply = null;
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerMove(panel.ResizeRight, new Vector2(25f, 0f));
                SendPointerUp(panel.ResizeRight, new Vector2(25f, 0f));
                Assert.AreEqual(225f, driver.Size.x);
                Assert.AreEqual(2, cursor.ResetCount);
            }
            finally
            {
                cursor.OnApply = null;
                manipulator.Detach();
                host.Close();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// ResizeBounds 실패 시 capture·cursor를 정리하고 재시도할 수 있다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_이동실패_포인터정리후재시도()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator(panel, new XeriWindowController(driver), cursor);
            var host = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                driver.ThrowOnApplyBounds = true;

                using var move = PointerMoveEvent.GetPooled
                (
                    CreatePointerEvent(EventType.MouseMove, new Vector2(40f, 0f))
                );
                move.target = panel.ResizeRight;
                var method = typeof(XeriWindowResizeManipulator).GetMethod
                (
                    "OnPointerMove",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                Assert.IsNotNull(method);

                var error = Assert.Throws<TargetInvocationException>
                (
                    () => method.Invoke
                    (
                        manipulator,
                        new object[]
                        {
                            move,
                        }
                    )
                );
                Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
                Assert.AreEqual(1, cursor.ResetCount);

                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerMove(panel.ResizeRight, new Vector2(40f, 0f));
                SendPointerUp(panel.ResizeRight, new Vector2(40f, 0f));

                Assert.AreEqual(240f, driver.Size.x);
                Assert.AreEqual(2, cursor.ResetCount);
            }
            finally
            {
                manipulator.Detach();
                host.Close();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Handle 소유가 아닌 userData는 연결, resize, 해제를 거쳐도 변경하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_UserData_기존값보존()
        {
            var panel = new XeriWindowPanel();
            var marker = new object();
            panel.ResizeRight.userData = marker;
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator
            (
                panel,
                new XeriWindowController(driver),
                cursor
            );
            var host = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();
                Assert.AreSame(marker, panel.ResizeRight.userData);
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerMove(panel.ResizeRight, new Vector2(25f, 0f));
                SendPointerUp(panel.ResizeRight, new Vector2(25f, 0f));
                Assert.AreEqual(225f, driver.Size.x);
                Assert.AreSame(marker, panel.ResizeRight.userData);
            }
            finally
            {
                manipulator.Detach();
                host.Close();
            }

            Assert.AreSame(marker, panel.ResizeRight.userData);
        }

    #endregion

    #region P-2: 캡처 소유권과 종료

        // ------------------------------------------------------------
        /// <summary>
        /// 종료 중 커서 Reset callback이 Detach해도 종료가 중복되지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_종료중Detach_정리한번만()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator
            (
                panel,
                new XeriWindowController(driver),
                cursor
            );
            var host = CreateHostWindow(panel);

            try
            {
                cursor.OnReset = manipulator.Detach;
                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerUp(panel.ResizeRight, Vector2.zero);

                Assert.AreEqual(1, cursor.ResetCount);
                Assert.IsFalse(panel.ResizeRight.HasPointerCapture(PointerId.mousePointerId));

                cursor.OnReset = null;
                manipulator.Attach();
                SendPointerDown(panel.ResizeBottom, Vector2.zero);
                SendPointerMove(panel.ResizeBottom, new Vector2(0f, 30f));
                SendPointerUp(panel.ResizeBottom, new Vector2(0f, 30f));

                Assert.AreEqual(150f, driver.Size.y);
                Assert.AreEqual(2, cursor.ResetCount);
            }
            finally
            {
                cursor.OnReset = null;
                manipulator.Detach();
                host.Close();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 외부 capture 해제 후에는 resize를 이어가지 않고 cursor를 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_Capture상실_진행종료()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator(panel, new XeriWindowController(driver), cursor);
            var host = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                Assert.IsTrue(panel.ResizeRight.HasPointerCapture(PointerId.mousePointerId));

                panel.ResizeRight.ReleasePointer(PointerId.mousePointerId);

                // CaptureOut 통지가 지연되더라도 이미 해제한 포인터의 첫 Move는 무시한다.
                SendPointerMove(panel.ResizeRight, new Vector2(40f, 0f));
                Assert.AreEqual(200f, driver.Size.x);
                Assert.AreEqual(1, cursor.ResetCount);

                SendPointerMove(panel.ResizeRight, new Vector2(80f, 0f));
                Assert.AreEqual(200f, driver.Size.x);
            }
            finally
            {
                manipulator.Detach();
                host.Close();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이전 핸들의 지연된 CaptureOut은 새 핸들의 리사이즈를 중단하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_이전HandleCaptureOut_새Handle보존()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var controller = new XeriWindowController(driver);
            var manipulator = new XeriWindowResizeManipulator(panel, controller, cursor);
            var host = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerUp(panel.ResizeRight, Vector2.zero);
                Assert.AreEqual(1, cursor.ResetCount);

                SendPointerDown(panel.ResizeBottom, Vector2.zero);
                Assert.IsTrue(panel.ResizeBottom.HasPointerCapture(PointerId.mousePointerId));

                using var staleEvent = PointerCaptureOutEvent.GetPooled
                (
                    panel.ResizeRight,
                    panel.ResizeBottom,
                    PointerId.mousePointerId
                );
                panel.ResizeRight.SendEvent(staleEvent);

                Assert.AreEqual(1, cursor.ResetCount);
                Assert.IsTrue(panel.ResizeBottom.HasPointerCapture(PointerId.mousePointerId));

                SendPointerMove(panel.ResizeBottom, new Vector2(0f, 30f));
                Assert.AreEqual(150f, driver.Size.y);
                SendPointerUp(panel.ResizeBottom, new Vector2(0f, 30f));
                Assert.AreEqual(2, cursor.ResetCount);
            }
            finally
            {
                manipulator.Detach();
                host.Close();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 핸들에서 새로 캡처한 포인터도 이전 CaptureOut과 구별한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_동일Handle_지연CaptureOut무시()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator
            (
                panel,
                new XeriWindowController(driver),
                cursor
            );
            var host = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerUp(panel.ResizeRight, Vector2.zero);
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                Assert.IsTrue(panel.ResizeRight.HasPointerCapture(PointerId.mousePointerId));

                using var staleEvent = PointerCaptureOutEvent.GetPooled
                (
                    panel.ResizeRight,
                    null,
                    PointerId.mousePointerId
                );
                panel.ResizeRight.SendEvent(staleEvent);

                Assert.IsTrue(panel.ResizeRight.HasPointerCapture(PointerId.mousePointerId));
                SendPointerMove(panel.ResizeRight, new Vector2(25f, 0f));
                Assert.AreEqual(225f, driver.Size.x);
                SendPointerUp(panel.ResizeRight, new Vector2(25f, 0f));
                Assert.AreEqual(2, cursor.ResetCount);
            }
            finally
            {
                manipulator.Detach();
                host.Close();
            }
        }

    #endregion

    #region P-3: 복합 버튼 종료 정책

        // ------------------------------------------------------------
        /// <summary>
        /// 시작 버튼이 유지된 보조 버튼 Up은 리사이즈를 끝내지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_복합버튼_시작버튼해제까지유지()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator
            (
                panel,
                new XeriWindowController(driver),
                cursor
            );
            var host = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerDown(panel.ResizeRight, Vector2.zero, 1);
                SendPointerUp(panel.ResizeRight, Vector2.zero, 1);

                Assert.AreEqual(0, cursor.ResetCount);
                SendPointerMove(panel.ResizeRight, new Vector2(30f, 0f));
                Assert.AreEqual(230f, driver.Size.x);

                SendPointerUp(panel.ResizeRight, new Vector2(30f, 0f));
                Assert.AreEqual(1, cursor.ResetCount);

                SendPointerMove(panel.ResizeRight, new Vector2(60f, 0f));
                Assert.AreEqual(230f, driver.Size.x);
            }
            finally
            {
                manipulator.Detach();
                host.Close();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 보조 버튼을 누른 채 시작 버튼을 놓으면 Move 경로에서 즉시 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_복합버튼_시작버튼먼저해제()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator
            (
                panel,
                new XeriWindowController(driver),
                cursor
            );
            var host = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerDown(panel.ResizeRight, Vector2.zero, 1);
                Assert.IsTrue(panel.ResizeRight.HasPointerCapture(PointerId.mousePointerId));

                // 다른 버튼이 눌린 상태의 MouseUp은 UITK에서 PointerMove로 전달될 수 있다.
                using var releaseMove = PointerMoveEvent.GetPooled
                (
                    CreatePointerEvent(EventType.MouseUp, new Vector2(30f, 0f))
                );
                Assert.AreEqual(0, releaseMove.pressedButtons & 1);
                Assert.AreEqual(2, releaseMove.pressedButtons & 2);

                releaseMove.target = panel.ResizeRight;
                panel.ResizeRight.SendEvent(releaseMove);

                Assert.AreEqual(200f, driver.Size.x);
                Assert.AreEqual(1, cursor.ResetCount);

                SendPointerMove(panel.ResizeRight, new Vector2(60f, 0f));
                Assert.AreEqual(200f, driver.Size.x);
                SendPointerUp(panel.ResizeRight, Vector2.zero, 1);
                Assert.AreEqual(1, cursor.ResetCount);
            }
            finally
            {
                manipulator.Detach();
                host.Close();
            }
        }

    #endregion

    #region L-1: 연결·분리와 커서 수명

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

        // ------------------------------------------------------------
        /// <summary>
        /// Reset 예외에도 등록을 해제하고 다시 연결할 수 있다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_해제중Reset예외_미연결후재시도()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var cursor = new TestResizeCursorProvider();
            var manipulator = new XeriWindowResizeManipulator
            (
                panel,
                new XeriWindowController(driver),
                cursor
            );
            var host = CreateHostWindow(panel);

            try
            {
                manipulator.Attach();
                cursor.OnReset = () => throw new InvalidOperationException("injected cursor reset failure");

                Assert.Throws<InvalidOperationException>(() => manipulator.Detach());
                cursor.OnReset = null;

                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerMove(panel.ResizeRight, new Vector2(30f, 0f));
                Assert.AreEqual(200f, driver.Size.x);

                manipulator.Attach();
                SendPointerDown(panel.ResizeRight, Vector2.zero);
                SendPointerMove(panel.ResizeRight, new Vector2(30f, 0f));
                SendPointerUp(panel.ResizeRight, new Vector2(30f, 0f));
                Assert.AreEqual(230f, driver.Size.x);
            }
            finally
            {
                cursor.OnReset = null;
                manipulator.Detach();
                host.Close();
            }
        }

    #endregion

    #region L-2: 다중 Window 커서 소유권

        // ----------------------------------------------------------------------
        /// <summary>
        /// 새 커서 소유자는 이전 텍스처를 반환하고, 이전 Reset은 현재 소유자를 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_커서소유권이전_이전Reset무시()
        {
            var first = new XeriWindowResizeCursorProvider();
            var second = new XeriWindowResizeCursorProvider();

            try
            {
                first.Apply(XeriWindowResizeMode.Right);
                var firstTexture = GetCursorTexture(first);
                Assert.IsNotNull(firstTexture);
                Assert.AreSame(first, GetCursorOwner());

                second.Apply(XeriWindowResizeMode.Bottom);
                var secondTexture = GetCursorTexture(second);
                Assert.IsTrue(firstTexture == null);
                Assert.IsNull(GetCursorTexture(first));
                Assert.AreSame(second, GetCursorOwner());
                Assert.IsNotNull(secondTexture);

                first.Reset();
                first.Apply(XeriWindowResizeMode.None);
                Assert.AreSame(second, GetCursorOwner());
                Assert.AreSame(secondTexture, GetCursorTexture(second));
                Assert.IsTrue(secondTexture != null);

                second.Apply(XeriWindowResizeMode.TopLeft);
                Assert.IsTrue(secondTexture == null);
                Assert.AreSame(second, GetCursorOwner());

                second.Reset();
                Assert.IsNull(GetCursorOwner());
                Assert.IsNull(GetCursorTexture(second));
            }
            finally
            {
                second.Reset();
                first.Reset();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 다른 Window의 Detach는 활성 resize Window의 커서와 입력을 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_다른WindowDetach_현재커서보존()
        {
            var firstPanel = new XeriWindowPanel();
            var secondPanel = new XeriWindowPanel();
            var firstDriver = new TestWindowDriver();
            var secondDriver = new TestWindowDriver();
            var firstCursor = new XeriWindowResizeCursorProvider();
            var secondCursor = new XeriWindowResizeCursorProvider();
            var first = new XeriWindowResizeManipulator
            (
                firstPanel,
                new XeriWindowController(firstDriver),
                firstCursor
            );
            var second = new XeriWindowResizeManipulator
            (
                secondPanel,
                new XeriWindowController(secondDriver),
                secondCursor
            );
            var firstHost = CreateHostWindow(firstPanel);
            var secondHost = CreateHostWindow(secondPanel);

            try
            {
                first.Attach();
                second.Attach();
                SendPointerDown(secondPanel.ResizeRight, Vector2.zero);

                var texture = GetCursorTexture(secondCursor);
                Assert.IsNotNull(texture);
                Assert.AreSame(secondCursor, GetCursorOwner());

                first.Detach();

                Assert.AreSame(secondCursor, GetCursorOwner());
                Assert.AreSame(texture, GetCursorTexture(secondCursor));
                Assert.IsTrue(texture != null);

                SendPointerMove(secondPanel.ResizeRight, new Vector2(40f, 0f));
                Assert.AreEqual(240f, secondDriver.Size.x);
                SendPointerUp(secondPanel.ResizeRight, new Vector2(40f, 0f));

                Assert.IsNull(GetCursorOwner());
                Assert.IsTrue(texture == null);
            }
            finally
            {
                first.Detach();
                second.Detach();
                firstCursor.Reset();
                secondCursor.Reset();
                firstHost.Close();
                secondHost.Close();
            }
        }

    #endregion

    }
}
