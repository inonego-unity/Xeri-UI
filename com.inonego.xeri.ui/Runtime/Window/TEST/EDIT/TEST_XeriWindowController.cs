/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowController.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 controller와 core 옵션 테스트.

# 테스트 구성
 O: 옵션 기본값
 M: 이동과 크기 변경
 S: 상태 명령
 C: 취소 가능한 요청
 E: 이벤트
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using NUnit;
using NUnit.Framework;

using inonego.Xeri;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 controller 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowController
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트용 윈도우 driver.
        /// </summary>
        // ============================================================
        private sealed class TestWindowDriver : IXeriWindowDriver
        {
            public Vector2 Pos { get; set; } = new Vector2(10f, 20f);
            public Vector2 Size { get; set; } = new Vector2(200f, 120f);
            public XeriWindowState State { get; set; } = XeriWindowState.Normal;
            public XeriWindowState VisualState { get; private set; } = XeriWindowState.Normal;
            public bool Visible { get; private set; } = true;
            public bool MaximizedBoundsApplied { get; private set; } = false;

            public Rect Bounds
            {
                get => new Rect(Pos, Size);
                set
                {
                    Pos = value.position;
                    Size = value.size;
                }
            }

            public void SetVisible(bool visible)
            {
                Visible = visible;
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
                MaximizedBoundsApplied = true;
            }
        }

        // ============================================================
        /// <summary>
        /// 테스트용 상태 전환 transitioner.
        /// </summary>
        // ============================================================
        private sealed class TestTransitioner : IXeriWindowStateTransitioner
        {
            public XeriWindowTransitionStatus Status => IsRunning
                ? XeriWindowTransitionStatus.Running
                : XeriWindowTransitionStatus.Idle;
            public bool IsRunning { get; private set; } = false;
            public XeriWindowState? PendingState { get; private set; } = null;
            public List<XeriWindowStateTransitionRequest> Requests { get; } = new();

            public bool Transition(XeriWindowStateTransitionRequest request)
            {
                Requests.Add(request);
                PendingState = request.NextState;

                if (request.NextState == XeriWindowState.Maximized)
                {
                    request.Driver.ApplyMaximizedBounds();
                }
                else if (request.TargetBounds.HasValue)
                {
                    request.Driver.ApplyBounds(request.TargetBounds.Value);
                }

                request.Driver.CommitState(request.NextState);
                PendingState = null;
                request.OnComplete?.Invoke();

                return true;
            }

            public void Cancel(bool restoreVisual)
            {
                PendingState = null;
                IsRunning = false;
            }
        }

    #endregion

    #region O-1: 기본 옵션

        // ------------------------------------------------------------
        /// <summary>
        /// XeriWindowOptions 기본값은 모든 기본 상호작용을 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowOptions_Default_기본_상호작용_활성화()
        {
            var options = XeriWindowOptions.Default();

            Assert.IsTrue(options.CanMove);
            Assert.IsTrue(options.CanResize);
            Assert.IsTrue(options.CanMinimize);
            Assert.IsTrue(options.CanMaximize);
            Assert.IsTrue(options.CanClose);
            Assert.IsTrue(options.CanFocus);
            Assert.IsTrue(options.CanTitleBarDoubleClickMaximize);
            Assert.IsFalse(options.HideDisabledButtons);
            Assert.AreEqual(new Vector2(152f, 80f), options.MinSize);
        }

    #endregion

    #region M-1: Move

        // ------------------------------------------------------------
        /// <summary>
        /// Move는 driver 위치를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Move_Driver_Pos_변경()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);

            controller.Move(new Vector2(30f, 40f));

            Assert.AreEqual(new Vector2(30f, 40f), driver.Pos);
        }

    #endregion

    #region M-2: Resize

        // ------------------------------------------------------------
        /// <summary>
        /// Resize는 옵션의 최소/최대 크기 범위로 driver 크기를 보정한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Resize_Min_Max_범위로_Size_보정()
        {
            var driver = new TestWindowDriver();
            var options = XeriWindowOptions.Default();
            options.MinSize = new Vector2(100f, 80f);
            options.MaxSize = new Vector2(300f, 240f);

            var controller = new XeriWindowController(driver, options);

            controller.Resize(new Vector2(40f, 400f));

            Assert.AreEqual(new Vector2(100f, 240f), driver.Size);
        }

    #endregion

    #region S-1: State Command

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 명령은 driver 상태를 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_StateCommand_Driver_State_전환()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);

            controller.Minimize();
            Assert.AreEqual(XeriWindowState.Minimized, driver.State);
            Assert.IsFalse(driver.Visible);

            controller.ShowNormal();
            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.IsTrue(driver.Visible);

            controller.Maximize();
            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
            Assert.IsTrue(driver.MaximizedBoundsApplied);

            controller.Close();
            Assert.AreEqual(XeriWindowState.Closed, driver.State);
            Assert.IsFalse(driver.Visible);
        }

    #endregion

    #region S-1-1: State Transitioner

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 명령은 controller 내부 transitioner를 경유한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_StateCommand_Transitioner_경유()
        {
            var driver = new TestWindowDriver();
            var transitioner = new TestTransitioner();
            var controller = new XeriWindowController(driver, null, transitioner);

            controller.Minimize();

            Assert.AreEqual(1, transitioner.Requests.Count);
            Assert.AreEqual(XeriWindowState.Minimized, transitioner.Requests[0].NextState);
        }

    #endregion

    #region S-2: State Rule

        // ------------------------------------------------------------
        /// <summary>
        /// Closed 상태에서는 후속 상태 전환 명령을 무시한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Closed_후_StateCommand_무시()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);

            controller.Close();
            controller.Maximize();
            controller.ShowNormal();
            controller.Minimize();

            Assert.AreEqual(XeriWindowState.Closed, driver.State);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Maximized에서 Normal로 돌아오면 controller snapshot bounds로 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Maximized_ShowNormal_RestoreBounds_복구()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            var normalPos = driver.Pos;
            var normalSize = driver.Size;

            controller.Maximize();
            controller.ShowNormal();

            Assert.AreEqual(normalPos, driver.Pos);
            Assert.AreEqual(normalSize, driver.Size);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// ShowNormal은 Minimized 이전 표시 상태와 무관하게 Normal 상태로 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Maximized_Minimized_ShowNormal_Normal_전환()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);

            controller.Maximize();
            controller.Minimize();
            controller.ShowNormal();

            Assert.AreEqual(XeriWindowState.Normal, driver.State);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Restore는 Minimized 이전 표시 상태로 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Maximized_Minimized_Restore_Maximized_복구()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);

            controller.Maximize();
            controller.Minimize();
            controller.Restore();

            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Restore는 Minimized가 아닌 표시 상태를 Normal로 토글하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Maximized_Restore_무시()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);

            controller.Maximize();
            controller.Restore();

            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// ShowNormal 명령은 명시적 target bounds가 있으면 snapshot 대신 해당 bounds를 사용한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_ShowNormal_TargetBounds_우선_사용()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            var targetBounds = new Rect(50f, 60f, 210f, 130f);

            controller.Maximize();
            controller.RequestStateCommand
            (
                new XeriWindowStateCommandRequest
                (
                    XeriWindowStateCommandKind.ShowNormal,
                    XeriWindowCommandSource.TitleBar,
                    targetBounds
                )
            );

            Assert.AreEqual(targetBounds.position, driver.Pos);
            Assert.AreEqual(targetBounds.size, driver.Size);
        }

    #endregion

    #region S-3: Disabled Option

        // ------------------------------------------------------------
        /// <summary>
        /// 비활성화된 옵션의 명령은 driver를 변경하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Disabled_Option_Driver_미변경()
        {
            var driver = new TestWindowDriver();
            var options = XeriWindowOptions.Default();
            options.CanMove = false;
            options.CanResize = false;
            options.CanMinimize = false;
            options.CanMaximize = false;
            options.CanClose = false;

            var controller = new XeriWindowController(driver, options);

            controller.Move(new Vector2(30f, 40f));
            controller.Resize(new Vector2(300f, 240f));
            controller.Minimize();
            controller.Maximize();
            controller.Close();

            Assert.AreEqual(new Vector2(10f, 20f), driver.Pos);
            Assert.AreEqual(new Vector2(200f, 120f), driver.Size);
            Assert.AreEqual(XeriWindowState.Normal, driver.State);
        }

    #endregion

    #region S-4: Closed State

        // ------------------------------------------------------------
        /// <summary>
        /// Closed 상태에서는 위치, 크기, 포커스 이벤트가 변경되지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Closed_State_Move_Resize_Focus_무시()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            var focusFired = false;

            controller.OnFocus += (_, _) => focusFired = true;
            controller.Close();
            controller.Move(new Vector2(30f, 40f));
            controller.Resize(new Vector2(300f, 240f));
            controller.Focus();

            Assert.AreEqual(new Vector2(10f, 20f), driver.Pos);
            Assert.AreEqual(new Vector2(200f, 120f), driver.Size);
            Assert.IsFalse(focusFired);
        }

    #endregion

    #region C-1: Close Cancel

        // ------------------------------------------------------------
        /// <summary>
        /// OnPreClose에서 Cancel을 설정하면 Close는 상태를 변경하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_OnPreClose_Cancel_Close_취소()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);

            controller.OnPreClose += (_, e) => e.Cancel = true;

            controller.Close();

            Assert.AreEqual(XeriWindowState.Normal, driver.State);
        }

    #endregion

    #region C-2: State Cancel

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 사전 이벤트에서 Cancel을 설정하면 상태 명령은 취소된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_PreState_Cancel_StateCommand_취소()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);

            controller.OnPreMinimize += (_, e) => e.Cancel = true;
            controller.OnPreMaximize += (_, e) => e.Cancel = true;

            controller.Minimize();
            controller.Maximize();

            Assert.AreEqual(XeriWindowState.Normal, driver.State);
        }

    #endregion

    #region E-1: Value Change Event

        // ------------------------------------------------------------
        /// <summary>
        /// Move와 Resize는 값 변경 이벤트를 발화한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Move_Resize_ValueChangeEvent_발화()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            ValueChangeEventArgs<Vector2> posArgs = default;
            ValueChangeEventArgs<Vector2> sizeArgs = default;

            controller.OnPosChange += (_, e) => posArgs = e;
            controller.OnSizeChange += (_, e) => sizeArgs = e;

            controller.Move(new Vector2(30f, 40f));
            controller.Resize(new Vector2(300f, 240f));

            Assert.AreEqual(new Vector2(10f, 20f), posArgs.Previous);
            Assert.AreEqual(new Vector2(30f, 40f), posArgs.Current);
            Assert.AreEqual(new Vector2(200f, 120f), sizeArgs.Previous);
            Assert.AreEqual(new Vector2(300f, 240f), sizeArgs.Current);
        }

    #endregion

    #region E-2: State Change Event

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 명령은 상태 변경 이벤트를 발화한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_StateCommand_StateChangeEvent_발화()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            ValueChangeEventArgs<XeriWindowState> stateArgs = default;

            controller.OnStateChange += (_, e) => stateArgs = e;

            controller.Minimize();

            Assert.AreEqual(XeriWindowState.Normal, stateArgs.Previous);
            Assert.AreEqual(XeriWindowState.Minimized, stateArgs.Current);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Restore 명령은 ShowNormal 이벤트가 아니라 Restore 이벤트를 발화한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_Restore_Event_ShowNormal_Event_분리()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);
            var restoreFired = false;
            var showNormalFired = false;

            controller.OnRestore += (_, _) => restoreFired = true;
            controller.OnShowNormal += (_, _) => showNormalFired = true;

            controller.Maximize();
            controller.Minimize();
            controller.Restore();

            Assert.IsTrue(restoreFired);
            Assert.IsFalse(showNormalFired);
            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// OnPreRestore에서 Cancel을 설정하면 Restore는 상태를 변경하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowController_OnPreRestore_Cancel_Restore_취소()
        {
            var driver = new TestWindowDriver();
            var controller = new XeriWindowController(driver);

            controller.Maximize();
            controller.Minimize();

            controller.OnPreRestore += (_, e) => e.Cancel = true;

            controller.Restore();

            Assert.AreEqual(XeriWindowState.Minimized, driver.State);
        }

    #endregion

    }
}
