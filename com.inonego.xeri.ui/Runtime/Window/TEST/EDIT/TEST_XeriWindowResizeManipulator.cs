/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowResizeManipulator.cs
수정일 : 2026-09-20

# 설명
XeriWindowResizeManipulator 확장 지점 테스트.

# 테스트 구성
 I: Injection
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

using NUnit;
using NUnit.Framework;

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

        // ============================================================
        /// <summary>
        /// 테스트용 resize cursor provider.
        /// </summary>
        // ============================================================
        private sealed class TestResizeCursorProvider : IXeriWindowResizeCursorProvider
        {
            public void Apply(XeriWindowResizeMode mode)
            {
            }

            public void Reset()
            {
            }
        }

    #endregion

    #region I-1: Cursor Provider

        // ------------------------------------------------------------
        /// <summary>
        /// Resize cursor provider를 외부에서 주입할 수 있다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowResizeManipulator_CursorProvider_주입()
        {
            var panel = new XeriWindowPanel();
            var controller = new XeriWindowController(new TestWindowDriver());
            var cursorProvider = new TestResizeCursorProvider();

            var manipulator = new XeriWindowResizeManipulator(panel, controller, cursorProvider);

            Assert.AreSame(cursorProvider, manipulator.CursorProvider);
        }

    #endregion

    }
}
