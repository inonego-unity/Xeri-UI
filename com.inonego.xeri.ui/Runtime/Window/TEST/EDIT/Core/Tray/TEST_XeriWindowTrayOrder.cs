/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowTrayOrder.cs
수정일 : 2026-09-21

# 설명
XeriWindowTraySource의 minimized window projection과 Tray 표시 순서를 검증한다.

# 테스트 구성
 T: Tray order
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

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
    /// Window Tray 순서 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowTrayOrder
    {

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// 테스트용 Window driver.
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

    #endregion

    #region T-1: Minimized Projection

        // ------------------------------------------------------------
        /// <summary>
        /// Source는 minimized window만 Tray entry로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_GetEntries_Minimized_Window만_반환()
        {
            var registry = new XeriWindowRegistry();
            registry.Register("normal", CreateController());
            var minimized = registry.Register("minimized", CreateController());
            var source = new XeriWindowTraySource(registry);

            Minimize(registry, minimized);

            var entries = source.GetEntries();

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("minimized", entries[0].ID);
        }

    #endregion

    #region T-2: Reorder

        // ------------------------------------------------------------
        /// <summary>
        /// MoveEntry는 source 내부 Tray entry 순서를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_MoveEntry_Entry_Order_변경()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            var second = registry.Register("second", CreateController());
            var third = registry.Register("third", CreateController());
            var source = new XeriWindowTraySource(registry);

            Minimize(registry, first);
            Minimize(registry, second);
            Minimize(registry, third);

            source.MoveEntry(third, 0);

            var entries = source.GetEntries();

            Assert.AreEqual("third", entries[0].ID);
            Assert.AreEqual("first", entries[1].ID);
            Assert.AreEqual("second", entries[2].ID);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// MoveEntry는 window registry order를 변경하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_MoveEntry_Window_Order_유지()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            var second = registry.Register("second", CreateController());
            var third = registry.Register("third", CreateController());
            var source = new XeriWindowTraySource(registry);

            Minimize(registry, first);
            Minimize(registry, second);
            Minimize(registry, third);

            source.MoveEntry(third, 0);

            Assert.AreEqual("first", registry.Records[0].ID);
            Assert.AreEqual("second", registry.Records[1].ID);
            Assert.AreEqual("third", registry.Records[2].ID);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// MoveEntry가 성공하면 reload 요청 이벤트를 발생시킨다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_MoveEntry_OnReloadRequired_발생()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            var second = registry.Register("second", CreateController());
            var source = new XeriWindowTraySource(registry);
            var eventCount = 0;

            Minimize(registry, first);
            Minimize(registry, second);

            source.OnReloadRequired += (_, _) => eventCount++;
            source.MoveEntry(second, 0);

            Assert.AreEqual(1, eventCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Reload observer 하나가 실패해도 뒤 observer까지 호출한 뒤 오류를 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_ReloadObserver실패_다음Observer_호출()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            var second = registry.Register("second", CreateController());
            var source = new XeriWindowTraySource(registry);
            var observerCount = 0;

            Minimize(registry, first);
            Minimize(registry, second);

            source.OnReloadRequired += (_, _) =>
            {
                throw new InvalidOperationException("injected reload observer failure");
            };
            source.OnReloadRequired += (_, _) => observerCount++;

            Assert.Throws<InvalidOperationException>
            (
                () => source.MoveEntry(second, 0)
            );

            Assert.AreEqual(1, observerCount);
            Assert.AreEqual("second", source.GetEntries()[0].ID);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Normal 상태 window에 대한 MoveEntry는 Tray 순서를 변경하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_MoveEntry_Normal_Window_무시()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            var second = registry.Register("second", CreateController());
            var source = new XeriWindowTraySource(registry);

            Minimize(registry, first);

            source.MoveEntry(second, 0);

            var entries = source.GetEntries();

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("first", entries[0].ID);
        }

    #endregion

    #region T-3: Order Cleanup

        // ------------------------------------------------------------
        /// <summary>
        /// Normal로 복구된 window는 source order에서 제거된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_GetEntries_Normal_복구시_Order에서_제거()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            var second = registry.Register("second", CreateController());
            var source = new XeriWindowTraySource(registry);

            Minimize(registry, first);
            Minimize(registry, second);

            registry.ShowNormal(first);

            var entries = source.GetEntries();

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("second", entries[0].ID);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unregister된 window는 source order에서 제거된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_GetEntries_Unregister시_Order에서_제거()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            var second = registry.Register("second", CreateController());
            var source = new XeriWindowTraySource(registry);

            Minimize(registry, first);
            Minimize(registry, second);

            registry.Unregister(first);

            var entries = source.GetEntries();

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("second", entries[0].ID);
        }

    #endregion

    #region T-4: Dispose

        // ------------------------------------------------------------
        /// <summary>
        /// Dispose 이후 registry 변경은 reload 이벤트를 발생시키지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_Dispose_Registry_이벤트_해제()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("window", CreateController());
            var source = new XeriWindowTraySource(registry);
            var eventCount = 0;

            source.OnReloadRequired += (_, _) => eventCount++;
            source.Dispose();

            Minimize(registry, handle);

            Assert.AreEqual(0, eventCount);
            Assert.Throws<System.ObjectDisposedException>
            (
                () => source.GetEntries()
            );
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트용 controller를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriWindowController CreateController()
        {
            return new XeriWindowController(new TestWindowDriver());
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 handle의 window를 minimized 상태로 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Minimize(XeriWindowRegistry registry, XeriWindowHandle handle)
        {
            registry.TryGetController(handle, out var controller);
            controller.Minimize();
        }

    #endregion

    }
}
