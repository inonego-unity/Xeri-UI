/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowTrayMapper.cs
수정일 : 2026-10-02

# 설명
Xeri 윈도우 record와 handle을 공통 Tray entry로 변환하는 mapper/source 테스트.

# 테스트 구성
 M: Mapper 변환
 S: Source 공급과 command 연결
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
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
        private static XeriWindowController CreateController(XeriWindowOptions? options = null)
        {
            return new XeriWindowController(new TestWindowDriver(), options);
        }

    #endregion

    #region M-1: 매핑

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

    #region S-1: 소스 항목

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 Source는 Closed를 제외한 모든 live Window 상태를 공급한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_GetEntries_기본_All_Live상태_공급()
        {
            var registry = new XeriWindowRegistry();
            registry.Register("normal", CreateController());
            var minimized = registry.Register("minimized", CreateController());
            var maximized = registry.Register("maximized", CreateController());
            var closed = registry.Register("closed", CreateController());

            registry.TryGetController(minimized, out var minimizedController);
            registry.TryGetController(maximized, out var maximizedController);
            registry.TryGetController(closed, out var closedController);
            minimizedController.Minimize();
            maximizedController.Maximize();
            closedController.Close();
            registry.Focus(maximized);

            var source = new XeriWindowTraySource(registry);
            var entries = source.GetEntries();

            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual("normal", entries[0].ID);
            Assert.AreEqual("minimized", entries[1].ID);
            Assert.AreEqual("maximized", entries[2].ID);
            Assert.IsTrue(entries[0].IsVisible);
            Assert.IsFalse(entries[0].IsActive);
            Assert.IsFalse(entries[1].IsVisible);
            Assert.IsFalse(entries[1].IsActive);
            Assert.IsTrue(entries[2].IsVisible);
            Assert.IsTrue(entries[2].IsActive);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Source는 설정된 Window 상태 조합만 Tray entry로 공급한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_GetEntries_IncludedStates_조합_필터()
        {
            var registry = new XeriWindowRegistry();
            registry.Register("normal", CreateController());
            var minimized = registry.Register("minimized", CreateController());
            var maximized = registry.Register("maximized", CreateController());

            registry.TryGetController(minimized, out var minimizedController);
            registry.TryGetController(maximized, out var maximizedController);
            minimizedController.Minimize();
            maximizedController.Maximize();

            var source = new XeriWindowTraySource
            (
                registry,
                XeriWindowTrayStateMask.Normal | XeriWindowTrayStateMask.Maximized
            );
            var entries = source.GetEntries();

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("normal", entries[0].ID);
            Assert.AreEqual("maximized", entries[1].ID);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Source는 active와 close 허용 여부를 runtime Registry/Controller에서 채운다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_GetEntries_Runtime상태_반영()
        {
            var options = XeriWindowOptions.Default();
            options.CanClose = false;
            var registry = new XeriWindowRegistry();
            var handle = registry.Register
            (
                "window",
                CreateController(options)
            );
            registry.Focus(handle);

            var source = new XeriWindowTraySource(registry);
            var entry = source.GetEntries()[0];

            Assert.IsTrue(entry.IsActive);
            Assert.IsFalse(entry.CanClose);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지원하지 않는 상태 비트는 Source 생성 시 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_생성_InvalidStateMask_거부()
        {
            var registry = new XeriWindowRegistry();

            Assert.Throws<System.ArgumentOutOfRangeException>
            (
                () => new XeriWindowTraySource
                (
                    registry,
                    (XeriWindowTrayStateMask)(1 << 8)
                )
            );
        }

    #endregion

    #region S-2: 활성화

        // ----------------------------------------------------------------------
        /// <summary>
        /// Minimized Tray entry 활성화는 이전 Normal 상태로 복구하고 focus한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_Activate_Minimized_Normal_복구_활성화()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("inventory", CreateController());
            var source = new XeriWindowTraySource(registry);

            registry.TryGetController(handle, out var controller);
            controller.Minimize();

            var entry = source.GetEntries()[0];
            source.Activate(entry);

            Assert.AreEqual(XeriWindowState.Normal, controller.Driver.State);
            Assert.AreSame(handle, registry.ActiveHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Maximized에서 minimize된 entry 활성화는 Maximized 상태로 복구한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_Activate_Maximized_Minimized_Maximized_복구()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("inventory", CreateController());
            var source = new XeriWindowTraySource(registry);

            registry.TryGetController(handle, out var controller);
            controller.Maximize();
            controller.Minimize();

            var entry = source.GetEntries()[0];
            source.Activate(entry);

            Assert.AreEqual(XeriWindowState.Maximized, controller.Driver.State);
            Assert.AreSame(handle, registry.ActiveHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 표시 중인 Normal/Maximized entry 활성화는 상태를 유지하고 focus한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowTraySource_Activate_표시상태_유지_활성화()
        {
            var registry = new XeriWindowRegistry();
            var normal = registry.Register("normal", CreateController());
            var maximized = registry.Register("maximized", CreateController());

            registry.TryGetController(normal, out var normalController);
            registry.TryGetController(maximized, out var maximizedController);
            maximizedController.Maximize();

            var source = new XeriWindowTraySource(registry);
            var entries = source.GetEntries();

            source.Activate(entries[0]);

            Assert.AreEqual(XeriWindowState.Normal, normalController.Driver.State);
            Assert.AreSame(normal, registry.ActiveHandle);

            source.Activate(entries[1]);

            Assert.AreEqual(XeriWindowState.Maximized, maximizedController.Driver.State);
            Assert.AreSame(maximized, registry.ActiveHandle);
        }

    #endregion

    #region S-3: 닫기

        // ----------------------------------------------------------------------
        /// <summary>
        /// Tray entry payload handle은 registry close 요청으로 전달된다.
        /// </summary>
        // ----------------------------------------------------------------------
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
