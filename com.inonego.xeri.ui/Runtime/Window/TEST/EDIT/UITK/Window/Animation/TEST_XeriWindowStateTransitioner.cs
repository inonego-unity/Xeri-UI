/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowStateTransitioner.cs
수정일 : 2026-10-03

# 설명
Xeri window 상태 전환 transitioner 테스트.

# 테스트 구성
 I: Immediate transitioner
 U: UITK transitioner
========================================================================= BLOCK_HEADER_END */

using System;

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
    /// Xeri window 상태 전환 transitioner 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowStateTransitioner
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트용 window driver.
        /// </summary>
        // ============================================================
        private sealed class TestWindowDriver : IXeriWindowDriver
        {
            public PresentationAlpha Alpha { get; } = new();
            public PresentationVisibility Visibility { get; } = new();

            public Vector2 Pos { get; set; } = new Vector2(10f, 20f);
            public Vector2 Size { get; set; } = new Vector2(200f, 120f);
            public XeriWindowState State { get; set; } = XeriWindowState.Normal;
            public XeriWindowState VisualState { get; private set; } = XeriWindowState.Normal;
            public bool Visible => Visibility.Modified;
            public bool MaximizedBoundsApplied { get; private set; } = false;
            public bool ThrowOnApplyBounds { get; set; } = false;
            public bool ThrowOnApplyMaximizedBounds { get; set; } = false;

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
                if (ThrowOnApplyBounds)
                {
                    ThrowOnApplyBounds = false;
                    throw new InvalidOperationException("injected bounds failure");
                }

                Bounds = bounds;
            }

            public void ApplyMaximizedBounds()
            {
                if (ThrowOnApplyMaximizedBounds)
                {
                    ThrowOnApplyMaximizedBounds = false;
                    throw new InvalidOperationException("injected maximize bounds failure");
                }

                MaximizedBoundsApplied = true;
            }
        }

        // ============================================================
        /// <summary>
        /// animation target bounds 조회 실패를 one-shot으로 주입한다.
        /// </summary>
        // ============================================================
        private sealed class TestAnimationTargetProvider : IXeriWindowAnimationTargetProvider
        {
            public int FailureCount { get; set; } = 0;
            public Rect TargetBounds { get; set; } = new Rect(0f, 0f, 800f, 600f);

            public Rect GetTargetBounds(XeriWindowState nextState, Rect currentBounds)
            {
                if (FailureCount <= 0) return TargetBounds;

                FailureCount--;
                throw new InvalidOperationException("injected animation target failure");
            }
        }

        // ============================================================
        /// <summary>
        /// 테스트용 animator.
        /// </summary>
        // ============================================================
        private sealed class TestAnimator : IXeriWindowStateAnimator
        {
            public bool IsRunning { get; private set; } = false;
            public XeriWindowStateTransitionContext LastContext { get; private set; }
            public Action CompleteAction { get; private set; } = null;
            public Action<Exception> ErrorAction { get; private set; } = null;
            public bool? LastCancelRestoreVisual { get; private set; } = null;
            public bool ThrowOnPlay { get; set; } = false;

            public void Play
            (
                XeriWindowStateTransitionContext context,
                Action onComplete,
                Action<Exception> onError
            )
            {
                if (ThrowOnPlay)
                {
                    throw new InvalidOperationException("injected play failure");
                }

                IsRunning = true;
                LastContext = context;
                CompleteAction = () =>
                {
                    IsRunning = false;
                    onComplete?.Invoke();
                };
                ErrorAction = exception =>
                {
                    IsRunning = false;
                    onError?.Invoke(exception);
                };
            }

            public void Cancel(bool restoreVisual)
            {
                LastCancelRestoreVisual = restoreVisual;
                IsRunning = false;
                CompleteAction = null;
                ErrorAction = null;
            }
        }

    #endregion

    #region I-1: 즉시

        // ----------------------------------------------------------------------
        /// <summary>
        /// Immediate transitioner는 Minimize를 즉시 완료하고 숨김 상태를 반영한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriImmediateWindowStateTransitioner_Minimize_즉시_숨김()
        {
            var driver = new TestWindowDriver();
            var transitioner = new XeriImmediateWindowStateTransitioner();
            var completed = false;

            transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Minimized,
                    OnComplete = () => completed = true,
                }
            );

            Assert.AreEqual(XeriWindowState.Minimized, driver.State);
            Assert.IsFalse(driver.Visible);
            Assert.IsTrue(completed);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Immediate transitioner는 Maximize bounds primitive를 호출한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriImmediateWindowStateTransitioner_Maximize_Bounds_반영()
        {
            var driver = new TestWindowDriver();
            var transitioner = new XeriImmediateWindowStateTransitioner();

            transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                }
            );

            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
            Assert.IsTrue(driver.Visible);
            Assert.IsTrue(driver.MaximizedBoundsApplied);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 즉시 전환 primitive 적용이 실패하면 이전 완료 상태와 bounds를 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriImmediateWindowStateTransitioner_MaximizeBounds실패_StateRollback()
        {
            var driver = new TestWindowDriver
            {
                ThrowOnApplyMaximizedBounds = true,
            };
            var transitioner = new XeriImmediateWindowStateTransitioner();
            var previousBounds = driver.Bounds;
            Exception reported = null;

            Assert.Throws<InvalidOperationException>
            (
                () => transitioner.Transition
                (
                    new XeriWindowStateTransitionRequest
                    {
                        Driver = driver,
                        PreviousState = XeriWindowState.Normal,
                        NextState = XeriWindowState.Maximized,
                        OnError = exception => reported = exception,
                    }
                )
            );

            Assert.IsNotNull(reported);
            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.AreEqual(XeriWindowState.Normal, driver.VisualState);
            Assert.AreEqual(previousBounds, driver.Bounds);
            Assert.IsTrue(driver.Visible);
            Assert.AreEqual(1f, driver.Alpha.Modified);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Rollback bounds 복원이 실패해도 완료 상태와 visibility 복원을 계속 시도한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriImmediateWindowStateTransitioner_RollbackBounds실패_나머지복원계속()
        {
            var driver = new TestWindowDriver
            {
                ThrowOnApplyBounds = true,
                ThrowOnApplyMaximizedBounds = true,
            };
            var transitioner = new XeriImmediateWindowStateTransitioner();

            var exception = Assert.Throws<AggregateException>
            (
                () => transitioner.Transition
                (
                    new XeriWindowStateTransitionRequest
                    {
                        Driver = driver,
                        PreviousState = XeriWindowState.Normal,
                        NextState = XeriWindowState.Maximized,
                    }
                )
            );

            Assert.GreaterOrEqual(exception.InnerExceptions.Count, 2);
            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.AreEqual(XeriWindowState.Normal, driver.VisualState);
            Assert.IsTrue(driver.Visible);
            Assert.AreEqual(1f, driver.Alpha.Modified);
        }

    #endregion

    #region U-1: UITK 전환

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> UITK transitioner는 animation 완료 전까지 driver 완료 상태와
        /// <br/> pending 상태를 분리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_Animation_완료전_DriverState_유지()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);

            transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                }
            );

            Assert.IsTrue(transitioner.IsRunning);
            Assert.AreEqual(XeriWindowState.Maximized, transitioner.PendingState);
            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.AreEqual(XeriWindowState.Maximized, driver.VisualState);
            Assert.IsTrue(driver.Visible);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// UITK transitioner는 animation 완료 후 최종 primitive와 완료 callback을 호출한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_Animation_완료후_State_확정()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            var completed = false;

            transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                    OnComplete = () => completed = true,
                }
            );

            animator.CompleteAction.Invoke();

            Assert.IsFalse(transitioner.IsRunning);
            Assert.IsNull(transitioner.PendingState);
            Assert.IsTrue(driver.MaximizedBoundsApplied);
            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
            Assert.AreEqual(XeriWindowState.Maximized, driver.VisualState);
            Assert.IsTrue(completed);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// UITK transitioner는 animation 실패 시 이전 완료 상태로 rollback한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_Animation_실패시_State_Rollback()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            var errorRaised = false;

            var previousBounds = driver.Bounds;
            transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                    OnError = _ => errorRaised = true,
                }
            );

            driver.Bounds = new Rect(80f, 90f, 500f, 400f);
            driver.Alpha.Set(0.25f);
            animator.ErrorAction.Invoke(new Exception("animation fail"));

            Assert.IsFalse(transitioner.IsRunning);
            Assert.IsNull(transitioner.PendingState);
            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.AreEqual(XeriWindowState.Normal, driver.VisualState);
            Assert.AreEqual(previousBounds, driver.Bounds);
            Assert.AreEqual(1f, driver.Alpha.Modified);
            Assert.IsFalse(driver.MaximizedBoundsApplied);
            Assert.IsTrue(driver.Visible);
            Assert.IsTrue(errorRaised);

            animator.CompleteAction?.Invoke();

            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.AreEqual(previousBounds, driver.Bounds);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Animation 실패 rollback의 bounds 복원이 실패해도 상태와 visibility 복원을 계속한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_RollbackBounds실패_나머지복원계속()
        {
            var driver = new TestWindowDriver
            {
                ThrowOnApplyBounds = true,
            };
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            Exception reported = null;

            transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                    OnError = exception => reported = exception,
                }
            );

            animator.ErrorAction.Invoke(new InvalidOperationException("injected animation failure"));

            Assert.IsInstanceOf<AggregateException>(reported);
            Assert.IsFalse(transitioner.IsRunning);
            Assert.IsNull(transitioner.PendingState);
            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.AreEqual(XeriWindowState.Normal, driver.VisualState);
            Assert.IsTrue(driver.Visible);
            Assert.AreEqual(1f, driver.Alpha.Modified);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Animator Play의 동기 예외도 이전 완료 상태로 rollback하고 running을 종료한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_Play동기예외_Rollback후종료()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator
            {
                ThrowOnPlay = true,
            };
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            var errorRaised = false;
            var previousBounds = driver.Bounds;

            var started = transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                    OnError = _ => errorRaised = true,
                }
            );

            Assert.IsTrue(started);
            Assert.IsFalse(transitioner.IsRunning);
            Assert.IsNull(transitioner.PendingState);
            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.AreEqual(previousBounds, driver.Bounds);
            Assert.IsTrue(errorRaised);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// UITK transitioner는 animation 비활성 요청을 즉시 완료한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_Animation_비활성_즉시완료()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            var completed = false;

            transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Maximized,
                    NextState = XeriWindowState.Normal,
                    TargetBounds = new Rect(30f, 40f, 220f, 140f),
                    Animate = false,
                    OnComplete = () => completed = true,
                }
            );

            Assert.IsFalse(transitioner.IsRunning);
            Assert.IsNull(transitioner.PendingState);
            Assert.IsFalse(animator.IsRunning);
            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.AreEqual(new Vector2(30f, 40f), driver.Pos);
            Assert.AreEqual(new Vector2(220f, 140f), driver.Size);
            Assert.IsTrue(completed);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Animation 비활성 요청의 완료 콜백 예외를 transition 실패로 재분류하지 않고 전파한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_Animation_비활성_완료콜백예외_전파()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            var errorRaised = false;

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => transitioner.Transition
                (
                    new XeriWindowStateTransitionRequest
                    {
                        Driver = driver,
                        PreviousState = XeriWindowState.Maximized,
                        NextState = XeriWindowState.Normal,
                        Animate = false,
                        OnComplete = () => throw new InvalidOperationException("injected completion failure"),
                        OnError = _ => errorRaised = true,
                    }
                )
            );

            Assert.AreEqual("injected completion failure", exception.Message);
            Assert.IsFalse(transitioner.IsRunning);
            Assert.IsNull(transitioner.PendingState);
            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.IsFalse(animator.IsRunning);
            Assert.IsFalse(errorRaised);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 같은 target 상태 요청은 IgnoreSameTarget 정책에서 중복 실행되지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_동일_Target_중복요청_무시()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);

            transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                    InterruptPolicy = XeriWindowTransitionInterruptPolicy.IgnoreSameTarget,
                }
            );
            var result = transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                    InterruptPolicy = XeriWindowTransitionInterruptPolicy.IgnoreSameTarget,
                }
            );

            Assert.IsFalse(result);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// CancelAndReplace는 이전 완료 visual 복원을 요청한 뒤 새 target을 시작한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_CancelAndReplace_Visual복원후교체()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);

            transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                }
            );
            var replaced = transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Minimized,
                    InterruptPolicy = XeriWindowTransitionInterruptPolicy.CancelAndReplace,
                }
            );

            Assert.IsTrue(replaced);
            Assert.AreEqual(true, animator.LastCancelRestoreVisual);
            Assert.AreEqual(XeriWindowState.Minimized, transitioner.PendingState);
        }

    #endregion

    #region U-2: 애니메이터

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Play 초기 visual 적용이 실패하면 content input block을 복원하고
        /// <br/> animator state도 종료해 같은 animator를 다시 사용할 수 있다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateAnimator_Play초기실패_Input복원후재시도()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var targetProvider = new TestAnimationTargetProvider
            {
                FailureCount = 1,
            };
            var options = XeriWindowAnimationOptions.Default();
            options.Enabled = true;
            var animator = new XeriUITKWindowStateAnimator
            (
                panel,
                options,
                targetProvider
            );
            var originalPickingMode = panel.ContentRoot.pickingMode;
            var context = new XeriWindowStateTransitionContext
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                }
            );

            Assert.Throws<InvalidOperationException>
            (
                () => animator.Play(context, null, null)
            );

            Assert.IsFalse(animator.IsRunning);
            Assert.AreEqual(originalPickingMode, panel.ContentRoot.pickingMode);
            Assert.AreEqual(XeriWindowState.Normal, driver.VisualState);

            var completed = false;
            Assert.DoesNotThrow
            (
                () => animator.Play
                (
                    context,
                    () => completed = true,
                    null
                )
            );

            Assert.IsTrue(completed);
            Assert.IsFalse(animator.IsRunning);
            Assert.AreEqual(originalPickingMode, panel.ContentRoot.pickingMode);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// UITK animator는 비활성 옵션에서 즉시 완료된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateAnimator_Disabled_즉시완료()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var animator = new XeriUITKWindowStateAnimator
            (
                panel,
                XeriWindowAnimationOptions.Immediate()
            );
            var completed = false;

            animator.Play
            (
                new XeriWindowStateTransitionContext
                (
                    new XeriWindowStateTransitionRequest
                    {
                        Driver = driver,
                        PreviousState = XeriWindowState.Normal,
                        NextState = XeriWindowState.Minimized,
                    }
                ),
                () => completed = true,
                _ => {}
            );

            Assert.IsFalse(animator.IsRunning);
            Assert.IsTrue(completed);
        }

    #endregion

    }
}
