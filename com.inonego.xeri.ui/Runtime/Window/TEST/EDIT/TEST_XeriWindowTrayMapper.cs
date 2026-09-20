/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowTrayMapper.cs
수정일 : 2026-09-20

# 설명
Xeri 윈도우 record와 handle을 공통 Tray entry로 변환하는 mapper/source 테스트.

# 테스트 구성
 M: Mapper 변환
 S: Source 공급과 command 연결
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

using NUnit;
using NUnit.Framework;

using inonego.Xeri.UI.Tray;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// Xeri Window Tray mapper/source 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowTrayMapper
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트용 윈도우 driver.
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

    #region M-1: Mapper

        // ------------------------------------------------------------
        /// <summary>
        /// Mapper는 record와 handle을 공통 Tray entry로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTrayMapper_Map_Record_Handle_TrayEntry_변환()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("inventory", CreateController());
            registry.TryGetRecord(handle, out var record);
            record.Title = "Inventory";
            record.Tooltip = "Open Inventory";
            record.Badge = new XeriTrayBadge("2", Color.red);

            var mapper = new XeriWindowTrayMapper();

            var entry = mapper.Map(record, handle);

            Assert.AreEqual("inventory", entry.ID);
            Assert.AreEqual("inventory", entry.PayloadID);
            Assert.AreEqual("Inventory", entry.Title);
            Assert.AreEqual("Open Inventory", entry.Tooltip);
            Assert.AreEqual("2", entry.Badge.Text);
            Assert.AreSame(handle, entry.Payload);
        }

    #endregion

    #region S-1: Source Entries

        // ------------------------------------------------------------
        /// <summary>
        /// Window Tray source는 최소화된 창만 Tray entry로 공급한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_GetEntries_최소화_창만_공급()
        {
            var registry = new XeriWindowRegistry();
            var normal = registry.Register("normal", CreateController());
            var minimized = registry.Register("minimized", CreateController());
            var source = new XeriWindowTraySource(registry);

            registry.TryGetController(minimized, out var controller);
            controller.Minimize();

            var entries = source.GetEntries();

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("minimized", entries[0].ID);
            Assert.IsTrue(registry.TryGetRecord(normal, out _));
        }

    #endregion

    #region S-2: Restore

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry payload handle은 registry restore 요청으로 전달된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_Restore_EntryPayload_Handle_사용()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("inventory", CreateController());
            var source = new XeriWindowTraySource(registry);

            registry.TryGetController(handle, out var controller);
            controller.Minimize();

            var entry = source.GetEntries()[0];
            source.Restore(entry);

            Assert.AreEqual(XeriWindowState.Normal, controller.Driver.State);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray 선택은 maximized에서 minimize된 window를 maximized 상태로 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_Restore_Maximized_Minimized_Maximized_복구()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("inventory", CreateController());
            var source = new XeriWindowTraySource(registry);

            registry.TryGetController(handle, out var controller);
            controller.Maximize();
            controller.Minimize();

            var entry = source.GetEntries()[0];
            source.Restore(entry);

            Assert.AreEqual(XeriWindowState.Maximized, controller.Driver.State);
        }

    #endregion

    #region S-3: Close

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry payload handle은 registry close 요청으로 전달된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_Close_EntryPayload_Handle_사용()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("inventory", CreateController());
            var source = new XeriWindowTraySource(registry);

            registry.TryGetController(handle, out var controller);
            controller.Minimize();

            var entry = source.GetEntries()[0];
            source.Close(entry);

            Assert.AreEqual(XeriWindowState.Closed, controller.Driver.State);
        }

    #endregion

    }
}
