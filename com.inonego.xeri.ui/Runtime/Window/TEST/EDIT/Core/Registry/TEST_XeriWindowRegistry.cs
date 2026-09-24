/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowRegistry.cs
수정일 : 2026-10-05

# 설명
Xeri 커스텀 윈도우 registry 테스트.

# 테스트 구성
 R: 등록과 제거
 F: 포커스와 순서
 E: 이벤트
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.TestTools;

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
    /// Xeri 커스텀 윈도우 registry 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowRegistry
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

    #region R-1: 등록

        // ------------------------------------------------------------
        /// <summary>
        /// Register는 활성 Window를 암묵적으로 변경하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Register_ActiveHandle_변경안함()
        {
            var registry = new XeriWindowRegistry();

            var handle = registry.Register("inventory", CreateController());

            Assert.IsTrue(registry.Contains(handle));
            Assert.IsNull(registry.ActiveHandle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 stable ID를 다시 등록하면 소유권 충돌로 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Register_중복ID_거부()
        {
            var registry = new XeriWindowRegistry();
            registry.Register("window", CreateController());

            Assert.Throws<InvalidOperationException>
            (
                () => registry.Register("window", CreateController())
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 공백-only Window ID는 stable key로 등록할 수 없다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Register_WhitespaceID_거부()
        {
            Assert.Throws<ArgumentException>
            (
                () => new XeriWindowRegistry().Register
                (
                    "   ",
                    CreateController()
                )
            );
        }

    #endregion

    #region R-2: 등록 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Unregister는 handle을 무효화하고 record 목록에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Unregister_Handle_무효화()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("inventory", CreateController());

            var removed = registry.Unregister(handle);

            Assert.IsTrue(removed);
            Assert.IsFalse(handle.IsValid);
            Assert.AreEqual(0, registry.Records.Count);
        }

    #endregion

    #region R-3: 등록 해제 구독 해제

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Unregister 이후 Controller 변경은 제거된 Record와 Registry 이벤트를 갱신하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Unregister_Controller_동기화구독_해제()
        {
            var registry = new XeriWindowRegistry();
            var controller = CreateController();
            var handle = registry.Register("inventory", controller);
            Assert.IsTrue(registry.TryGetRecord(handle, out var record));

            registry.Unregister(handle);
            var collectionChangeCount = 0;
            registry.OnCollectionChange += (_, _) => collectionChangeCount++;

            controller.Minimize();

            Assert.AreEqual(XeriWindowState.Normal, record.State);
            Assert.AreEqual(0, collectionChangeCount);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Active Window 등록 해제는 null active 변경을 한 번 알린다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Unregister_ActiveHandle_Null변경_알림()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("inventory", CreateController());
            registry.Focus(handle);
            XeriWindowEventArgs changed = new XeriWindowEventArgs();
            var activeChangeCount = 0;
            registry.OnActiveChange += (_, e) =>
            {
                activeChangeCount++;
                changed = e;
            };

            registry.Unregister(handle);

            Assert.IsNull(registry.ActiveHandle);
            Assert.AreEqual(1, activeChangeCount);
            Assert.IsNotNull(changed);
            Assert.IsNull(changed.Handle);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Register 입력과 조회 Record를 바꿔도 Registry 내부 live state는 변경되지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Record_외부변경_LiveState와_분리()
        {
            var input = new XeriWindowRecord
            {
                ID = "first",
                Title = "Initial",
            };
            var registry = new XeriWindowRegistry();
            var handle = registry.Register
            (
                "first",
                CreateController(),
                input
            );

            input.ID = "input-changed";
            input.Title = "Input Changed";
            Assert.IsTrue(registry.TryGetRecord(handle, out var snapshot));

            snapshot.ID = "snapshot-changed";
            snapshot.Title = "Snapshot Changed";
            Assert.IsTrue(registry.TryGetRecord(handle, out var current));

            Assert.AreEqual("first", current.ID);
            Assert.AreEqual("Initial", current.Title);
            Assert.AreEqual("first", registry.Records[0].ID);
            Assert.AreEqual("Initial", registry.Records[0].Title);
        }

    #endregion

    #region F-1: 포커스

        // ------------------------------------------------------------
        /// <summary>
        /// Focus는 active handle과 같은 layer의 앞쪽 순서를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Focus_ActiveHandle과_Order_갱신()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            registry.Register("second", CreateController());

            registry.Focus(first);

            Assert.AreSame(first, registry.ActiveHandle);
            Assert.AreEqual("second", registry.Records[0].ID);
            Assert.AreEqual("first", registry.Records[1].ID);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// CanFocus=false Window는 직접 Focus해도 active가 되지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Focus_CanFocusFalse_무시()
        {
            var options = XeriWindowOptions.Default();
            options.CanFocus = false;
            var registry = new XeriWindowRegistry();
            var handle = registry.Register
            (
                "window",
                CreateController(options)
            );

            registry.Focus(handle);

            Assert.IsNull(registry.ActiveHandle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Minimized Window는 직접 Focus해도 active가 되지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Focus_Minimized_무시()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("window", CreateController());
            Assert.IsTrue(registry.TryGetController(handle, out var controller));
            controller.Minimize();

            registry.Focus(handle);

            Assert.IsNull(registry.ActiveHandle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Deactivate는 지정 Window가 active일 때만 active 상태를 비운다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Deactivate_ActiveHandle_해제()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            var second = registry.Register("second", CreateController());
            registry.Focus(first);

            registry.Deactivate(second);
            Assert.AreSame(first, registry.ActiveHandle);

            registry.Deactivate(first);
            Assert.IsNull(registry.ActiveHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Active Window가 Minimized 또는 Closed가 되면 active 상태를 자동 해제한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_ActiveWindow_HiddenState_자동해제()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("window", CreateController());
            Assert.IsTrue(registry.TryGetController(handle, out var controller));

            registry.Focus(handle);
            controller.Minimize();
            Assert.IsNull(registry.ActiveHandle);

            controller.Restore();
            registry.Focus(handle);
            controller.Close();
            Assert.IsNull(registry.ActiveHandle);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Restore 완료 observer가 실패해도 이미 복원된 Window에 Registry Focus 정책을 적용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_RestoreObserver실패_Focus정책계속()
        {
            var registry = new XeriWindowRegistry();
            var controller = CreateController();
            var handle = registry.Register("window", controller);
            controller.Minimize();
            controller.OnRestore += (_, _) =>
                throw new InvalidOperationException("injected restore observer failure");

            Assert.Throws<InvalidOperationException>
            (
                () => registry.Restore(handle)
            );

            Assert.AreEqual(XeriWindowState.Normal, controller.State);
            Assert.AreSame(handle, registry.ActiveHandle);
        }

    #endregion

    #region F-2: 상태 동기화

        // ------------------------------------------------------------
        /// <summary>
        /// Controller 상태 변경은 registry record에 동기화된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Controller_State_Record에_동기화()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("window", CreateController());

            registry.TryGetController(handle, out var controller);
            controller.Minimize();

            Assert.IsTrue(registry.TryGetRecord(handle, out var record));
            Assert.AreEqual(XeriWindowState.Minimized, record.State);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Controller의 Normal bounds와 Minimized restore 상태를 Record에 동기화한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Controller_복원상태_Record에_동기화()
        {
            var registry = new XeriWindowRegistry();
            var controller = CreateController();
            var handle = registry.Register("window", controller);

            controller.Move(new Vector2(30f, 40f));
            controller.Resize(new Vector2(260f, 180f));

            Assert.IsTrue(registry.TryGetRecord(handle, out var record));
            Assert.AreEqual(controller.NormalBounds.position, record.NormalPos);
            Assert.AreEqual(controller.NormalBounds.size, record.NormalSize);

            controller.Maximize();
            controller.Minimize();

            Assert.IsTrue(registry.TryGetRecord(handle, out record));
            Assert.AreEqual(XeriWindowState.Minimized, record.State);
            Assert.AreEqual
            (
                XeriWindowState.Maximized,
                record.MinimizedRestoreState
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 앞선 위치 값 구독자가 실패해도 Registry bounds 동기화와 Move 이벤트를 계속 처리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_PosChange구독실패_Record동기화_계속()
        {
            var controller = CreateController();
            var moveFired = false;
            controller.OnPosChange += (_, _) =>
            {
                throw new InvalidOperationException("injected pos observer failure");
            };
            controller.OnMove += (_, _) => moveFired = true;
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("window", controller);
            var target = new Vector2(90f, 120f);

            Assert.Throws<InvalidOperationException>
            (
                () => controller.Move(target)
            );

            Assert.IsTrue(registry.TryGetRecord(handle, out var record));
            Assert.AreEqual(target, record.Pos);
            Assert.IsTrue(moveFired);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 앞선 상태 이벤트 구독자가 실패해도 Registry 동기화와 상태별 이벤트를 계속 처리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_StateEvent구독실패_Record동기화_계속()
        {
            var controller = CreateController();
            var maximizeFired = false;
            controller.OnStateChange += (_, _) =>
            {
                throw new InvalidOperationException("injected state observer failure");
            };
            controller.OnMaximize += (_, _) => maximizeFired = true;
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("window", controller);

            Assert.Throws<InvalidOperationException>(controller.Maximize);

            Assert.IsTrue(registry.TryGetRecord(handle, out var record));
            Assert.AreEqual(XeriWindowState.Maximized, record.State);
            Assert.IsTrue(maximizeFired);
        }

    #endregion

    #region F-3: 스택 순서

        // ----------------------------------------------------------------------
        /// <summary>
        /// BringToFront는 지정한 window를 같은 layer의 가장 앞으로 이동시킨다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_BringToFront_Record_Order_갱신()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            registry.Register("second", CreateController());

            registry.BringToFront(first);

            Assert.AreEqual("second", registry.Records[0].ID);
            Assert.AreEqual("first", registry.Records[1].ID);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// SendToBack는 지정한 window를 같은 layer의 가장 뒤로 이동시킨다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_SendToBack_Record_Order_갱신()
        {
            var registry = new XeriWindowRegistry();
            registry.Register("first", CreateController());
            var second = registry.Register("second", CreateController());

            registry.SendToBack(second);

            Assert.AreEqual("second", registry.Records[0].ID);
            Assert.AreEqual("first", registry.Records[1].ID);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// AlwaysOnTop layer는 Normal layer보다 항상 앞에 정렬된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_SetStackLayer_AlwaysOnTop_앞에_정렬()
        {
            var registry = new XeriWindowRegistry();
            var normal = registry.Register("normal", CreateController());
            var top = registry.Register("top", CreateController());

            registry.SetStackLayer(top, XeriWindowStackLayer.AlwaysOnTop);
            registry.Focus(normal);

            Assert.AreEqual("normal", registry.Records[0].ID);
            Assert.AreEqual("top", registry.Records[1].ID);
            Assert.IsTrue(registry.TryGetRecord(top, out var record));
            Assert.AreEqual(XeriWindowStackLayer.AlwaysOnTop, record.StackLayer);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 정의되지 않은 StackLayer 값은 Registry 경계에서 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_SetStackLayer_InvalidEnum_거부()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("window", CreateController());

            Assert.Throws<ArgumentOutOfRangeException>
            (
                () => registry.SetStackLayer
                (
                    handle,
                    (XeriWindowStackLayer)999
                )
            );
        }

    #endregion

    #region E-1: 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 등록, 제거, 활성, 순서 변경 이벤트가 발화된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Register_Unregister_Focus_이벤트_발화()
        {
            var registry = new XeriWindowRegistry();
            var collectionChangeCount = 0;
            var orderChangeCount = 0;
            var registerFired = false;
            var unregisterFired = false;
            var activeFired = false;

            registry.OnCollectionChange += (_, _) => collectionChangeCount++;
            registry.OnOrderChange += (_, _) => orderChangeCount++;
            registry.OnRegister += (_, _) => registerFired = true;
            registry.OnUnregister += (_, _) => unregisterFired = true;
            registry.OnActiveChange += (_, _) => activeFired = true;

            var handle = registry.Register("inventory", CreateController());
            registry.Focus(handle);
            registry.Unregister(handle);

            Assert.IsTrue(registerFired);
            Assert.IsTrue(unregisterFired);
            Assert.IsTrue(activeFired);
            Assert.GreaterOrEqual(collectionChangeCount, 2);
            Assert.GreaterOrEqual(orderChangeCount, 2);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Register observer 실패에도 등록과 후속 알림을 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Register_Observer실패_등록유지()
        {
            var registry = new XeriWindowRegistry();
            var registerCount = 0;
            var unregisterCount = 0;
            var collectionCount = 0;

            registry.OnRegister += (_, _) =>
            {
                registerCount++;
                throw new InvalidOperationException("injected register observer failure");
            };
            registry.OnUnregister += (_, _) => unregisterCount++;
            registry.OnCollectionChange += (_, _) => collectionCount++;

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: injected register observer failure");
            var handle = registry.Register("window", CreateController());

            Assert.AreEqual(1, registerCount);
            Assert.AreEqual(0, unregisterCount);
            Assert.AreEqual(1, collectionCount);
            Assert.AreEqual(1, registry.Records.Count);
            Assert.IsTrue(registry.TryGetHandle("window", out var current));
            Assert.AreSame(handle, current);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Register observer가 provisional 등록을 제거하고 같은 ID를 다시 등록하면
        /// <br/> 바깥 Register는 성공 Handle을 반환하지 않고 새 등록 소유권도 건드리지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Register중교체_바깥등록실패와새등록보존()
        {
            var registry = new XeriWindowRegistry();
            var replacing = false;
            var notified = new List<XeriWindowHandle>();
            XeriWindowHandle replacement = null;

            registry.OnRegister += (_, e) =>
            {
                if (replacing) return;

                replacing = true;
                Assert.IsTrue(registry.Unregister(e.Handle));
                replacement = registry.Register("window", CreateController());
            };

            registry.OnRegister += (_, e) => notified.Add(e.Handle);

            Assert.Throws<InvalidOperationException>
            (
                () => registry.Register("window", CreateController())
            );

            Assert.IsNotNull(replacement);
            Assert.IsTrue(registry.Contains(replacement));
            Assert.IsTrue(registry.TryGetHandle("window", out var current));
            Assert.AreSame(replacement, current);
            Assert.AreEqual(1, registry.Records.Count);
            Assert.AreEqual(1, notified.Count);
            Assert.AreSame(replacement, notified[0]);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Active observer가 실패해도 Focus order와 나머지 observer 호출을 끝까지 적용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Focus_Observer실패_Order와다른Observer_유지()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            registry.Register("second", CreateController());
            var activeCount = 0;
            var orderCount = 0;

            registry.OnActiveChange += (_, _) =>
            {
                throw new InvalidOperationException("injected active observer failure");
            };
            registry.OnActiveChange += (_, _) => activeCount++;
            registry.OnOrderChange += (_, _) => orderCount++;

            Assert.Throws<InvalidOperationException>
            (
                () => registry.Focus(first)
            );

            Assert.AreSame(first, registry.ActiveHandle);
            Assert.AreEqual("first", registry.Records[1].ID);
            Assert.AreEqual(1, activeCount);
            Assert.AreEqual(1, orderCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Unregister observer가 실패해도 제거·order·active 해제 observer를 끝까지 처리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Unregister_Observer실패_나머지정리_계속()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("window", CreateController());
            registry.Focus(handle);
            var collectionCount = 0;
            var orderCount = 0;
            var activeCount = 0;

            registry.OnUnregister += (_, _) =>
            {
                throw new InvalidOperationException("injected unregister observer failure");
            };
            registry.OnCollectionChange += (_, _) => collectionCount++;
            registry.OnOrderChange += (_, _) => orderCount++;
            registry.OnActiveChange += (_, _) => activeCount++;

            Assert.Throws<InvalidOperationException>
            (
                () => registry.Unregister(handle)
            );

            Assert.IsFalse(registry.Contains(handle));
            Assert.IsNull(registry.ActiveHandle);
            Assert.AreEqual(1, collectionCount);
            Assert.AreEqual(1, orderCount);
            Assert.AreEqual(1, activeCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Active 해제 observer가 실패해도 hidden state collection 변경 알림을 계속 처리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_HiddenState_ActiveObserver실패_Collection알림_계속()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("window", CreateController());
            Assert.IsTrue(registry.TryGetController(handle, out var controller));
            registry.Focus(handle);
            var collectionCount = 0;

            registry.OnActiveChange += (_, _) =>
            {
                throw new InvalidOperationException("injected active observer failure");
            };
            registry.OnCollectionChange += (_, _) => collectionCount++;

            Assert.Throws<InvalidOperationException>(controller.Minimize);

            Assert.IsNull(registry.ActiveHandle);
            Assert.AreEqual(1, collectionCount);
            Assert.IsTrue(registry.TryGetRecord(handle, out var record));
            Assert.AreEqual(XeriWindowState.Minimized, record.State);
        }

    #endregion

    }
}
