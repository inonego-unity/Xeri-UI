/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriTrayController.cs
수정일 : 2026-09-20

# 설명
공통 Tray controller 이벤트 라우팅 테스트.

# 테스트 구성
 R: Reload 흐름
 E: Entry 이벤트 전달
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using NUnit;
using NUnit.Framework;

using inonego.Xeri.UI.Tray;

namespace inonego.Xeri.UI.TEST.Tray
{
    // ============================================================
    /// <summary>
    /// 공통 Tray controller 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriTrayController
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트용 Tray source.
        /// </summary>
        // ============================================================
        private sealed class TestTraySource : IXeriTraySource
        {
            public event EventHandler OnReloadRequired = null;

            public IReadOnlyList<XeriTrayEntry> Entries = Array.Empty<XeriTrayEntry>();

            // ------------------------------------------------------------
            /// <summary>
            /// 현재 entry 목록을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public IReadOnlyList<XeriTrayEntry> GetEntries()
            {
                return Entries;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Reload 필요 이벤트를 발화한다.
            /// </summary>
            // ------------------------------------------------------------
            public void InvokeReloadRequired()
            {
                OnReloadRequired?.Invoke(this, EventArgs.Empty);
            }
        }

        // ============================================================
        /// <summary>
        /// 테스트용 Tray renderer.
        /// </summary>
        // ============================================================
        private sealed class TestTrayRenderer : IXeriTrayRenderer
        {
            public event EventHandler<XeriTrayEventArgs> OnEntrySelect = null;
            public event EventHandler<XeriTrayEventArgs> OnEntryClose = null;

            public IReadOnlyList<XeriTrayEntry> Entries = null;
            public XeriTrayOptions Options = null;

            // ------------------------------------------------------------
            /// <summary>
            /// Reload 입력을 기록한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Reload(IReadOnlyList<XeriTrayEntry> entries, XeriTrayOptions options)
            {
                Entries = entries;
                Options = options;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Entry 선택 입력을 발화한다.
            /// </summary>
            // ------------------------------------------------------------
            public void InvokeEntrySelect(XeriTrayEntry entry)
            {
                OnEntrySelect?.Invoke(this, new XeriTrayEventArgs(entry));
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Entry 닫기 입력을 발화한다.
            /// </summary>
            // ------------------------------------------------------------
            public void InvokeEntryClose(XeriTrayEntry entry)
            {
                OnEntryClose?.Invoke(this, new XeriTrayEventArgs(entry));
            }
        }

    #endregion

    #region R-1: Reload

        // ------------------------------------------------------------
        /// <summary>
        /// Reload는 source entry 목록을 renderer에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Reload_Source_Entry_Renderer_전달()
        {
            var entries = new[] { new XeriTrayEntry("id", "Title") };
            var source = new TestTraySource { Entries = entries };
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);

            controller.Reload();

            Assert.AreSame(entries, renderer.Entries);
            Assert.IsNotNull(renderer.Options);
        }

    #endregion

    #region R-2: ReloadRequired

        // ------------------------------------------------------------
        /// <summary>
        /// Source reload 요청은 controller 이벤트와 renderer reload로 전달된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Source_OnReloadRequired_Reload_및_이벤트_전달()
        {
            var entries = new[] { new XeriTrayEntry("id", "Title") };
            var source = new TestTraySource { Entries = entries };
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            var fired = false;

            controller.OnReloadRequired += (_, _) => fired = true;

            source.InvokeReloadRequired();

            Assert.IsTrue(fired);
            Assert.AreSame(entries, renderer.Entries);
        }

    #endregion

    #region E-1: Select

        // ------------------------------------------------------------
        /// <summary>
        /// Renderer entry 선택은 controller 외부 이벤트로 전달된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Renderer_OnEntrySelect_외부_이벤트_전달()
        {
            var entry = new XeriTrayEntry("id", "Title");
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            XeriTrayEventArgs eventArgs = null;

            controller.OnEntrySelect += (_, e) => eventArgs = e;

            renderer.InvokeEntrySelect(entry);

            Assert.IsNotNull(eventArgs);
            Assert.AreSame(entry, eventArgs.Entry);
        }

    #endregion

    #region E-2: Close

        // ------------------------------------------------------------
        /// <summary>
        /// Renderer entry 닫기 입력은 취소 가능한 사전 이벤트로 전달된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Renderer_OnEntryClose_OnPreEntryClose_전달()
        {
            var entry = new XeriTrayEntry("id", "Title");
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            XeriTrayCancelEventArgs eventArgs = null;

            controller.OnPreEntryClose += (_, e) =>
            {
                e.Cancel = true;
                eventArgs = e;
            };

            renderer.InvokeEntryClose(entry);

            Assert.IsNotNull(eventArgs);
            Assert.IsTrue(eventArgs.Cancel);
            Assert.AreSame(entry, eventArgs.Entry);
        }

    #endregion

    }
}
