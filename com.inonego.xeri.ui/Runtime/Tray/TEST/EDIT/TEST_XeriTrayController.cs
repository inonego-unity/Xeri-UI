/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriTrayController.cs
수정일 : 2026-10-06

# 설명
공통 Tray controller 이벤트 라우팅 테스트.

# 테스트 구성
 R: Reload 흐름
 E: Entry 이벤트 전달
 D: Dispose 구독 해제
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
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
            public int ReloadSubscriberCount => onReloadRequired?.GetInvocationList().Length ?? 0;
            public int ReloadRemoveAttemptCount { get; private set; }
            public bool ThrowOnReloadRemove { get; set; }

            private EventHandler onReloadRequired = null;

            public event EventHandler OnReloadRequired
            {
                add => onReloadRequired += value;
                remove
                {
                    ReloadRemoveAttemptCount++;

                    if (ThrowOnReloadRemove)
                    {
                        throw new InvalidOperationException("injected source unsubscribe failure");
                    }

                    onReloadRequired -= value;
                }
            }

            public IReadOnlyList<XeriTrayEntry> Entries = Array.Empty<XeriTrayEntry>();
            public Action ReadingEntries;

            // ------------------------------------------------------------
            /// <summary>
            /// 현재 entry 목록을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public IReadOnlyList<XeriTrayEntry> GetEntries()
            {
                ReadingEntries?.Invoke();
                return Entries;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Reload 필요 이벤트를 발화한다.
            /// </summary>
            // ------------------------------------------------------------
            public void InvokeReloadRequired()
            {
                onReloadRequired?.Invoke(this, EventArgs.Empty);
            }
        }

        // ============================================================
        /// <summary>
        /// 테스트용 Tray renderer.
        /// </summary>
        // ============================================================
        private sealed class TestTrayRenderer : IXeriTrayRenderer
        {
            public int SelectSubscriberCount => onEntrySelect?.GetInvocationList().Length ?? 0;
            public int CloseSubscriberCount => onEntryClose?.GetInvocationList().Length ?? 0;
            public int SelectRemoveAttemptCount { get; private set; }
            public int CloseRemoveAttemptCount { get; private set; }
            public bool ThrowOnCloseAdd { get; set; }
            public bool ThrowOnSelectRemove { get; set; }

            private EventHandler<XeriTrayEventArgs> onEntrySelect = null;
            private EventHandler<XeriTrayEventArgs> onEntryClose = null;

            public event EventHandler<XeriTrayEventArgs> OnEntrySelect
            {
                add => onEntrySelect += value;
                remove
                {
                    SelectRemoveAttemptCount++;

                    if (ThrowOnSelectRemove)
                    {
                        throw new InvalidOperationException("injected select unsubscribe failure");
                    }

                    onEntrySelect -= value;
                }
            }

            public event EventHandler<XeriTrayEventArgs> OnEntryClose
            {
                add
                {
                    onEntryClose += value;

                    if (ThrowOnCloseAdd)
                    {
                        throw new InvalidOperationException("injected close subscribe failure");
                    }
                }
                remove
                {
                    CloseRemoveAttemptCount++;
                    onEntryClose -= value;
                }
            }

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
                onEntrySelect?.Invoke(this, new XeriTrayEventArgs(entry));
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Entry 닫기 입력을 발화한다.
            /// </summary>
            // ------------------------------------------------------------
            public void InvokeEntryClose(XeriTrayEntry entry)
            {
                onEntryClose?.Invoke(this, new XeriTrayEventArgs(entry));
            }
        }

    #endregion

    #region C-1: 구성

        // ------------------------------------------------------------
        /// <summary>
        /// Source 또는 Renderer가 없으면 Controller 생성을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Construct_NullDependency_거부()
        {
            Assert.Throws<ArgumentNullException>
            (
                () => new XeriTrayController(null, new TestTrayRenderer())
            );
            Assert.Throws<ArgumentNullException>
            (
                () => new XeriTrayController(new TestTraySource(), null)
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 뒤쪽 event subscribe가 실패하면 앞서 연결된 Source/Renderer 구독을 모두 롤백한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Construct_CloseSubscribe실패_선행구독Rollback()
        {
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer
            {
                ThrowOnCloseAdd = true,
            };

            Assert.Throws<InvalidOperationException>
            (
                () => new XeriTrayController(source, renderer)
            );

            Assert.AreEqual(0, source.ReloadSubscriberCount);
            Assert.AreEqual(0, renderer.SelectSubscriberCount);
            Assert.AreEqual(0, renderer.CloseSubscriberCount);
            Assert.AreEqual(1, source.ReloadRemoveAttemptCount);
            Assert.AreEqual(1, renderer.SelectRemoveAttemptCount);
            Assert.AreEqual(1, renderer.CloseRemoveAttemptCount);
        }

    #endregion

    #region R-1: 재로드

        // ------------------------------------------------------------
        /// <summary>
        /// Reload는 source entry 목록을 renderer에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Reload_Source_Entry_Renderer_전달()
        {
            var entries = new[]
            {
                new XeriTrayEntry("id", "Title"),
            };
            var source = new TestTraySource
            {
                Entries = entries,
            };
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);

            controller.Reload();

            Assert.AreSame(entries, renderer.Entries);
            Assert.IsNotNull(renderer.Options);
        }

    #endregion

    #region R-2: 조회 중 수명 종료

        // ------------------------------------------------------------
        /// <summary>
        /// Source 조회 중 종료되면 renderer 호출을 중단한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Source조회중Dispose_Renderer호출안함()
        {
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            source.ReadingEntries = controller.Dispose;

            controller.Reload();

            Assert.IsNull(renderer.Entries);
            Assert.IsNull(renderer.Options);
            Assert.AreEqual(0, source.ReloadSubscriberCount);
            Assert.AreEqual(0, renderer.SelectSubscriberCount);
            Assert.AreEqual(0, renderer.CloseSubscriberCount);
        }

    #endregion

    #region R-2: 재로드 필요

        // ----------------------------------------------------------------------
        /// <summary>
        /// Source reload 요청은 controller 이벤트와 renderer reload로 전달된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Source_OnReloadRequired_Reload_및_이벤트_전달()
        {
            var entries = new[]
            {
                new XeriTrayEntry("id", "Title"),
            };
            var source = new TestTraySource
            {
                Entries = entries,
            };
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            var fired = false;

            controller.OnReloadRequired += (_, _) => fired = true;

            source.InvokeReloadRequired();

            Assert.IsTrue(fired);
            Assert.AreSame(entries, renderer.Entries);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Reload observer 하나가 실패해도 나머지 observer와 renderer reload를 계속 처리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_ReloadObserver실패_나머지Observer와Reload_계속()
        {
            var entries = new[]
            {
                new XeriTrayEntry("id", "Title"),
            };
            var source = new TestTraySource
            {
                Entries = entries,
            };
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            var observerCount = 0;

            controller.OnReloadRequired += (_, _) =>
            {
                throw new InvalidOperationException("injected reload observer failure");
            };
            controller.OnReloadRequired += (_, _) => observerCount++;

            Assert.Throws<InvalidOperationException>
            (
                source.InvokeReloadRequired
            );

            Assert.AreEqual(1, observerCount);
            Assert.AreSame(entries, renderer.Entries);
        }

    #endregion

    #region E-1: 선택

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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Select observer 하나가 실패해도 뒤 observer까지 독립적으로 호출한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_SelectObserver실패_다음Observer_호출()
        {
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            var observerCount = 0;

            controller.OnEntrySelect += (_, _) =>
            {
                throw new InvalidOperationException("injected select observer failure");
            };
            controller.OnEntrySelect += (_, _) => observerCount++;

            Assert.Throws<InvalidOperationException>
            (
                () => renderer.InvokeEntrySelect(new XeriTrayEntry("id", "Title"))
            );

            Assert.AreEqual(1, observerCount);
        }

    #endregion

    #region E-2: 닫기

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
            var closeCount = 0;

            controller.OnPreEntryClose += (_, e) =>
            {
                e.Cancel = true;
                eventArgs = e;
            };
            controller.OnEntryClose += (_, _) => closeCount++;

            renderer.InvokeEntryClose(entry);

            Assert.IsNotNull(eventArgs);
            Assert.IsTrue(eventArgs.Cancel);
            Assert.AreSame(entry, eventArgs.Entry);
            Assert.AreEqual(0, closeCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// PreClose observer가 실패해도 뒤 observer를 호출하되 승인 close는 발행하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_PreCloseObserver실패_다음Observer호출_Close중단()
        {
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            var preCloseCount = 0;
            var closeCount = 0;

            controller.OnPreEntryClose += (_, _) =>
            {
                throw new InvalidOperationException("injected pre-close observer failure");
            };
            controller.OnPreEntryClose += (_, _) => preCloseCount++;
            controller.OnEntryClose += (_, _) => closeCount++;

            Assert.Throws<InvalidOperationException>
            (
                () => renderer.InvokeEntryClose(new XeriTrayEntry("id", "Title"))
            );

            Assert.AreEqual(1, preCloseCount);
            Assert.AreEqual(0, closeCount);
        }

    #endregion

    #region E-3: 닫기 승인

        // ------------------------------------------------------------
        /// <summary>
        /// 취소되지 않은 Renderer 닫기 입력은 승인 이벤트로 전달된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Renderer_OnEntryClose_승인이벤트_전달()
        {
            var entry = new XeriTrayEntry("id", "Title");
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            XeriTrayEventArgs eventArgs = null;

            controller.OnEntryClose += (_, e) => eventArgs = e;

            renderer.InvokeEntryClose(entry);

            Assert.IsNotNull(eventArgs);
            Assert.AreSame(entry, eventArgs.Entry);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// CanClose=false entry는 Renderer가 닫기 입력을 내도 Controller가 무시한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Renderer_OnEntryClose_CanCloseFalse_무시()
        {
            var entry = new XeriTrayEntry("id", "Title")
            {
                CanClose = false,
            };
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            var preCloseCount = 0;
            var closeCount = 0;

            controller.OnPreEntryClose += (_, _) => preCloseCount++;
            controller.OnEntryClose += (_, _) => closeCount++;

            renderer.InvokeEntryClose(entry);

            Assert.AreEqual(0, preCloseCount);
            Assert.AreEqual(0, closeCount);
        }

    #endregion

    #region E-4: 콜백 종료 재진입

        // ----------------------------------------------------------------------
        /// <summary>
        /// Reload 알림 callback에서 Dispose되면 renderer reload를 이어가지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_ReloadCallback_Dispose_후속Reload_중단()
        {
            var source = new TestTraySource
            {
                Entries = new[]
                {
                    new XeriTrayEntry("id", "Title"),
                },
            };
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);

            controller.OnReloadRequired += (_, _) => controller.Dispose();

            source.InvokeReloadRequired();

            Assert.IsNull(renderer.Entries);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// PreClose callback에서 Dispose되면 승인된 close 이벤트를 발행하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_PreCloseCallback_Dispose_승인Close_중단()
        {
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            var closeCount = 0;

            controller.OnPreEntryClose += (_, _) => controller.Dispose();
            controller.OnEntryClose += (_, _) => closeCount++;

            renderer.InvokeEntryClose(new XeriTrayEntry("id", "Title"));

            Assert.AreEqual(0, closeCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 선행 Source 구독의 Dispose 뒤에는 Controller handler를 중단한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Source선행구독_Dispose_Handler중단()
        {
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            XeriTrayController controller = null;
            var reloadCount = 0;

            source.OnReloadRequired += (_, _) => controller.Dispose();
            controller = new XeriTrayController(source, renderer);
            controller.OnReloadRequired += (_, _) => reloadCount++;

            source.InvokeReloadRequired();

            Assert.AreEqual(0, reloadCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 앞선 Renderer 구독자가 Controller를 Dispose하면 같은 선택 입력 전달을 중단한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Renderer선행구독_Dispose_Select중단()
        {
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            XeriTrayController controller = null;
            var selectCount = 0;

            renderer.OnEntrySelect += (_, _) => controller.Dispose();
            controller = new XeriTrayController(source, renderer);
            controller.OnEntrySelect += (_, _) => selectCount++;

            renderer.InvokeEntrySelect(new XeriTrayEntry("id", "Title"));

            Assert.AreEqual(0, selectCount);
        }

    #endregion

    #region D-1: 해제

        // ----------------------------------------------------------------------
        /// <summary>
        /// Dispose 이후 Source와 Renderer 이벤트는 Controller로 전달되지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Dispose_Source와Renderer_이벤트_해제()
        {
            var entry = new XeriTrayEntry("id", "Title");
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer();
            var controller = new XeriTrayController(source, renderer);
            var reloadCount = 0;
            var selectCount = 0;

            controller.OnReloadRequired += (_, _) => reloadCount++;
            controller.OnEntrySelect += (_, _) => selectCount++;
            controller.Dispose();

            source.InvokeReloadRequired();
            renderer.InvokeEntrySelect(entry);

            Assert.AreEqual(0, reloadCount);
            Assert.AreEqual(0, selectCount);
            Assert.Throws<ObjectDisposedException>(controller.Reload);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 한 unsubscribe가 실패해도 나머지 Source/Renderer 구독 해제를 모두 시도한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayController_Dispose_SelectUnsubscribe실패_나머지해제계속()
        {
            var source = new TestTraySource();
            var renderer = new TestTrayRenderer
            {
                ThrowOnSelectRemove = true,
            };
            var controller = new XeriTrayController(source, renderer);

            Assert.Throws<AggregateException>(controller.Dispose);

            Assert.AreEqual(1, renderer.SelectSubscriberCount);
            Assert.AreEqual(0, renderer.CloseSubscriberCount);
            Assert.AreEqual(0, source.ReloadSubscriberCount);
            Assert.AreEqual(1, renderer.SelectRemoveAttemptCount);
            Assert.AreEqual(1, renderer.CloseRemoveAttemptCount);
            Assert.AreEqual(1, source.ReloadRemoveAttemptCount);
            Assert.DoesNotThrow(controller.Dispose);
        }

    #endregion

    }
}
