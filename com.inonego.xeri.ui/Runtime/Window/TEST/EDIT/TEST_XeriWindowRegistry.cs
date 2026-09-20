/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowRegistry.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 registry 테스트.

# 테스트 구성
 R: 등록과 제거
 F: 포커스와 순서
 E: 이벤트
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

using NUnit;
using NUnit.Framework;

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

    #region R-1: Register

        // ------------------------------------------------------------
        /// <summary>
        /// Register는 handle을 반환하고 같은 ID는 기존 handle을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Register_중복_ID_기존_Handle_반환()
        {
            var registry = new XeriWindowRegistry();
            var handle = registry.Register("inventory", CreateController());
            var duplicate = registry.Register("inventory", CreateController());

            Assert.IsNotNull(handle);
            Assert.AreSame(handle, duplicate);
            Assert.IsTrue(handle.IsValid);
        }

    #endregion

    #region R-2: Unregister

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

    #region F-1: Focus

        // ------------------------------------------------------------
        /// <summary>
        /// Focus는 active handle과 focus order를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowRegistry_Focus_ActiveHandle_및_FocusOrder_갱신()
        {
            var registry = new XeriWindowRegistry();
            var first = registry.Register("first", CreateController());
            var second = registry.Register("second", CreateController());

            registry.Focus(first);

            Assert.AreSame(first, registry.ActiveHandle);
            Assert.IsTrue(registry.TryGetRecord(first, out var firstRecord));
            Assert.IsTrue(registry.TryGetRecord(second, out var secondRecord));
            Assert.Greater(firstRecord.FocusOrder, secondRecord.FocusOrder);
        }

    #endregion

    #region F-2: State Sync

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

    #endregion

    #region F-3: Stack Order

        // ------------------------------------------------------------
        /// <summary>
        /// BringToFront는 지정한 window를 같은 layer의 가장 앞으로 이동시킨다.
        /// </summary>
        // ------------------------------------------------------------
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

    #endregion

    #region E-1: Events

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

    #endregion

    }
}
