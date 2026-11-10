/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowStateTransitioner.cs
수정일 : 2026-11-10

# 설명
Xeri window 상태 전환 transitioner 테스트.

# 테스트 구성
 A–C: 테스트 대역
 D: 즉시 전환과 롤백
 E–K: UITK 전환·완료·실패·재진입
========================================================================= BLOCK_HEADER_END */

using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.TestTools;
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

    #region A: 테스트 Driver

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
            public bool ThrowOnReadBounds { get; set; } = false;
            public Action<XeriWindowState> OnCommitState { get; set; } = null;
            public Action<Rect> OnApplyBounds { get; set; } = null;

            public Rect Bounds
            {
                get
                {
                    if (ThrowOnReadBounds)
                    {
                        ThrowOnReadBounds = false;
                        throw new InvalidOperationException("injected bounds snapshot failure");
                    }

                    return new Rect(Pos, Size);
                }
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
                OnCommitState?.Invoke(state);
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
                OnApplyBounds?.Invoke(bounds);
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

    #endregion

    #region B: 테스트 Animation Target

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

    #endregion

    #region C: 테스트 Animator

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
            public Action OnCancelAction { get; set; } = null;

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

                var callback = OnCancelAction;
                OnCancelAction = null;
                callback?.Invoke();
            }
        }

    #endregion

    #region D: 즉시 전환과 롤백

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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Driver callback이 요청을 변경해도 즉시 전환은 시작 시점의 계약을 따른다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriImmediateWindowStateTransitioner_Driver재진입_요청Snapshot유지()
        {
            var driver = new TestWindowDriver();
            var otherDriver = new TestWindowDriver();
            var transitioner = new XeriImmediateWindowStateTransitioner();
            var completed = false;
            var changedCompleted = false;
            var request = new XeriWindowStateTransitionRequest
            {
                Driver = driver,
                PreviousState = XeriWindowState.Normal,
                NextState = XeriWindowState.Minimized,
                OnComplete = () => completed = true,
            };

            driver.OnCommitState = _ =>
            {
                request.Driver = otherDriver;
                request.PreviousState = XeriWindowState.Maximized;
                request.NextState = XeriWindowState.Maximized;
                request.OnComplete = () => changedCompleted = true;
            };

            Assert.IsTrue(transitioner.Transition(request));

            Assert.AreEqual(XeriWindowState.Minimized, driver.State);
            Assert.IsFalse(driver.Visible);
            Assert.IsFalse(driver.MaximizedBoundsApplied);
            Assert.AreEqual(XeriWindowState.Normal, otherDriver.State);
            Assert.IsTrue(completed);
            Assert.IsFalse(changedCompleted);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Driver 실패 도중 요청이 변경돼도 최초 상태로 복원하고 최초 오류 콜백에 알린다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriImmediateWindowStateTransitioner_실패중요청변경_원래상태로복원()
        {
            var driver = new TestWindowDriver();
            var transitioner = new XeriImmediateWindowStateTransitioner();
            var errorReported = false;
            var changedErrorReported = false;
            var request = new XeriWindowStateTransitionRequest
            {
                Driver = driver,
                PreviousState = XeriWindowState.Normal,
                NextState = XeriWindowState.Minimized,
                OnError = _ => errorReported = true,
            };

            driver.OnCommitState = state =>
            {
                if (state != XeriWindowState.Minimized) return;

                request.PreviousState = XeriWindowState.Maximized;
                request.NextState = XeriWindowState.Closed;
                request.OnError = _ => changedErrorReported = true;
                throw new InvalidOperationException("injected commit failure");
            };

            Assert.Throws<InvalidOperationException>(() => transitioner.Transition(request));

            Assert.AreEqual(XeriWindowState.Normal, driver.State);
            Assert.AreEqual(XeriWindowState.Normal, driver.VisualState);
            Assert.IsFalse(driver.MaximizedBoundsApplied);
            Assert.IsTrue(driver.Visible);
            Assert.IsTrue(errorReported);
            Assert.IsFalse(changedErrorReported);
        }

    #endregion

    #region E: UITK 전환 진행·완료 및 실패 복원

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

            Assert.AreEqual(XeriWindowTransitionStatus.Idle, transitioner.Status);

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
            Assert.AreEqual(XeriWindowTransitionStatus.Running, transitioner.Status);
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
            Assert.AreEqual(XeriWindowTransitionStatus.Idle, transitioner.Status);
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
            Assert.AreEqual(XeriWindowTransitionStatus.Idle, transitioner.Status);
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

    #endregion

    #region F: UITK 즉시 완료와 콜백 예외

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

    #endregion

    #region G: UITK 중복 요청과 교체

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

    #region H: UITK 시작 실패와 취소 재진입

        // ----------------------------------------------------------------------
        /// <summary>
        /// Snapshot getter 실패는 진행 중 상태를 남기지 않고 다음 전환을 허용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_Snapshot실패_상태미점유와재시도()
        {
            var driver = new TestWindowDriver
            {
                ThrowOnReadBounds = true,
            };
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            var request = new XeriWindowStateTransitionRequest
            {
                Driver = driver,
                PreviousState = XeriWindowState.Normal,
                NextState = XeriWindowState.Maximized,
            };

            Assert.Throws<InvalidOperationException>(() => transitioner.Transition(request));
            Assert.IsFalse(transitioner.IsRunning);
            Assert.IsNull(transitioner.PendingState);
            Assert.IsFalse(animator.IsRunning);

            Assert.IsTrue(transitioner.Transition(request));
            animator.CompleteAction?.Invoke();
            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
            Assert.IsFalse(transitioner.IsRunning);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 취소 통지에서 새 전환을 시작하면 원래 교체 요청은 이를 덮어쓰지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_취소콜백재진입_새전환보존()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            var nestedStarted = false;

            transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                    OnCancel = () => nestedStarted = transitioner.Transition
                    (
                        new XeriWindowStateTransitionRequest
                        {
                            Driver = driver,
                            PreviousState = XeriWindowState.Normal,
                            NextState = XeriWindowState.Minimized,
                        }
                    ),
                }
            );

            var replaced = transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Closed,
                }
            );

            Assert.IsTrue(nestedStarted);
            Assert.IsFalse(replaced);
            Assert.AreEqual(XeriWindowState.Minimized, transitioner.PendingState);
            Assert.IsTrue(animator.IsRunning);

            animator.CompleteAction?.Invoke();
            Assert.AreEqual(XeriWindowState.Minimized, driver.State);
            Assert.IsFalse(transitioner.IsRunning);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Animator 정리 도중의 새 전환은 이전 요청의 rollback에 의해 지워지지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_Animator취소재진입_새전환보존()
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

            var nestedStarted = false;
            animator.OnCancelAction = () => nestedStarted = transitioner.Transition
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Minimized,
                }
            );

            transitioner.Cancel(true);

            Assert.IsTrue(nestedStarted);
            Assert.IsTrue(transitioner.IsRunning);
            Assert.AreEqual(XeriWindowState.Minimized, transitioner.PendingState);

            animator.CompleteAction?.Invoke();
            Assert.AreEqual(XeriWindowState.Minimized, driver.State);
            Assert.IsFalse(transitioner.IsRunning);
        }

    #endregion

    #region I: UITK 요청 Snapshot과 실행 소유권

        // ----------------------------------------------------------------------
        /// <summary>
        /// 시작 이후 호출자가 Request를 바꿔도 최초 Driver·목표·콜백 계약을 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_요청변경_시작Snapshot으로완료()
        {
            var driver = new TestWindowDriver();
            var otherDriver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            var originalCompleted = false;
            var changedCompleted = false;
            var request = new XeriWindowStateTransitionRequest
            {
                Driver = driver,
                PreviousState = XeriWindowState.Normal,
                NextState = XeriWindowState.Maximized,
                OnComplete = () => originalCompleted = true,
            };

            Assert.IsTrue(transitioner.Transition(request));
            request.Driver = otherDriver;
            request.PreviousState = XeriWindowState.Minimized;
            request.NextState = XeriWindowState.Closed;
            request.TargetBounds = new Rect(40f, 50f, 60f, 70f);
            request.OnComplete = () => changedCompleted = true;

            Assert.AreEqual(XeriWindowState.Maximized, transitioner.PendingState);
            animator.CompleteAction?.Invoke();

            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
            Assert.IsTrue(driver.MaximizedBoundsApplied);
            Assert.AreEqual(XeriWindowState.Normal, otherDriver.State);
            Assert.IsTrue(originalCompleted);
            Assert.IsFalse(changedCompleted);
            Assert.IsFalse(transitioner.IsRunning);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 완료 상태 Commit 중 다른 전환이 재진입해도 이전 완료·숨김 처리를 이어가지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_완료Commit재진입_새실행상태유지()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            var nestedStarted = false;
            var oldCompleted = false;
            driver.OnCommitState = state =>
            {
                if (state != XeriWindowState.Closed) return;

                driver.OnCommitState = null;
                nestedStarted = transitioner.Transition
                (
                    new XeriWindowStateTransitionRequest
                    {
                        Driver = driver,
                        PreviousState = XeriWindowState.Normal,
                        NextState = XeriWindowState.Maximized,
                    }
                );
            };

            Assert.IsTrue
            (
                transitioner.Transition
                (
                    new XeriWindowStateTransitionRequest
                    {
                        Driver = driver,
                        PreviousState = XeriWindowState.Normal,
                        NextState = XeriWindowState.Closed,
                        OnComplete = () => oldCompleted = true,
                    }
                )
            );
            animator.CompleteAction?.Invoke();

            Assert.IsTrue(nestedStarted);
            Assert.IsFalse(oldCompleted);
            Assert.IsTrue(transitioner.IsRunning);
            Assert.AreEqual(XeriWindowState.Maximized, transitioner.PendingState);
            Assert.IsTrue(driver.Visible);
            Assert.AreEqual(XeriWindowState.Maximized, driver.VisualState);

            animator.CompleteAction?.Invoke();
            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
            Assert.IsFalse(transitioner.IsRunning);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 취소 rollback의 Driver 호출에서 새 전환이 시작되면 이전 복원을 중지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateTransitioner_롤백재진입_새Visual보존()
        {
            var driver = new TestWindowDriver();
            var animator = new TestAnimator();
            var transitioner = new XeriUITKWindowStateTransitioner(animator);
            Assert.IsTrue
            (
                transitioner.Transition
                (
                    new XeriWindowStateTransitionRequest
                    {
                        Driver = driver,
                        PreviousState = XeriWindowState.Normal,
                        NextState = XeriWindowState.Maximized,
                    }
                )
            );

            var nestedStarted = false;
            driver.OnApplyBounds = _ =>
            {
                driver.OnApplyBounds = null;
                nestedStarted = transitioner.Transition
                (
                    new XeriWindowStateTransitionRequest
                    {
                        Driver = driver,
                        PreviousState = XeriWindowState.Normal,
                        NextState = XeriWindowState.Minimized,
                    }
                );
            };

            transitioner.Cancel(true);

            Assert.IsTrue(nestedStarted);
            Assert.IsTrue(transitioner.IsRunning);
            Assert.AreEqual(XeriWindowState.Minimized, transitioner.PendingState);
            Assert.AreEqual(XeriWindowState.Minimized, driver.VisualState);

            animator.CompleteAction?.Invoke();
            Assert.AreEqual(XeriWindowState.Minimized, driver.State);
            Assert.IsFalse(transitioner.IsRunning);
        }

    #endregion

    #region J: UITK Animator 시작과 즉시 완료

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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Animation 비활성 시 Maximize라도 목표 Bounds 조회를 수행하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateAnimator_Disabled_Maximize_NoTargetLookup()
        {
            var driver = new TestWindowDriver();
            var provider = new TestAnimationTargetProvider
            {
                FailureCount = 1,
            };
            var animator = new XeriUITKWindowStateAnimator
            (
                new VisualElement(),
                XeriWindowAnimationOptions.Immediate(),
                provider
            );
            var context = new XeriWindowStateTransitionContext
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Maximized,
                }
            );
            var completed = false;

            Assert.DoesNotThrow
            (
                () => animator.Play(context, () => completed = true, null)
            );

            Assert.IsTrue(completed);
            Assert.AreEqual(1, provider.FailureCount);
            Assert.IsFalse(animator.IsRunning);
            Assert.AreEqual(1f, driver.Alpha.Modified);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// NaN과 무한대 duration은 animation 완료 불능 상태를 만들므로 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateAnimator_NonFiniteDuration_생성거부()
        {
            var values = new[]
            {
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity,
            };

            foreach (var duration in values)
            {
                var options = new XeriWindowAnimationOptions
                {
                    Enabled = true,
                    Duration = duration,
                    HiddenOpacity = 0f,
                };

                Assert.Throws<ArgumentOutOfRangeException>
                (
                    () => new XeriUITKWindowStateAnimator(new VisualElement(), options)
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 유효하지 않은 hidden opacity는 Window alpha 보간 전에 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateAnimator_NonFiniteOpacity_생성거부()
        {
            var values = new[]
            {
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity,
            };

            foreach (var opacity in values)
            {
                var options = new XeriWindowAnimationOptions
                {
                    Enabled = true,
                    Duration = 0.1f,
                    HiddenOpacity = opacity,
                };

                Assert.Throws<ArgumentOutOfRangeException>
                (
                    () => new XeriUITKWindowStateAnimator(new VisualElement(), options)
                );
            }
        }

    #endregion

    #region K: UITK Animator 완료 통지와 Bounds 처리

        // ----------------------------------------------------------------------
        /// <summary>
        /// Scheduler 완료 통지 예외는 완료된 animation의 실패로 재분류하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateAnimator_비동기완료콜백예외_종료상태와통지유지()
        {
            var panel = new XeriWindowPanel();
            var driver = new TestWindowDriver();
            var animator = new XeriUITKWindowStateAnimator
            (
                panel,
                new XeriWindowAnimationOptions
                {
                    Enabled = true,
                    Duration = 0.1f,
                    HiddenOpacity = 0f,
                }
            );
            var context = new XeriWindowStateTransitionContext
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Normal,
                    NextState = XeriWindowState.Normal,
                }
            );

            // Panel 없이도 실제 scheduler tick 경로의 종료·통지 순서를 직접 검증한다.
            var type = typeof(XeriUITKWindowStateAnimator);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            void SetField(string name, object value)
            {
                var field = type.GetField(name, flags);
                Assert.IsNotNull(field);
                field.SetValue(animator, value);
            }

            var completed = false;
            var errorReported = false;
            SetField("context", context);
            SetField("isRunning", true);
            SetField("startTime", Time.realtimeSinceStartup - 1f);
            SetField
            (
                "onComplete",
                (Action)(() =>
                {
                    completed = true;
                    throw new InvalidOperationException("injected scheduler completion failure");
                })
            );
            SetField("onError", (Action<Exception>)(_ => errorReported = true));

            var update = type.GetMethod("UpdateAnimation", flags);
            Assert.IsNotNull(update);
            LogAssert.Expect(LogType.Exception, new Regex("injected scheduler completion failure"));
            Assert.DoesNotThrow(() => update.Invoke(animator, null));

            Assert.IsTrue(completed);
            Assert.IsFalse(errorReported);
            Assert.IsFalse(animator.IsRunning);
            Assert.AreEqual(1f, driver.Alpha.Modified);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 숨김 상태 전환은 bounds 보간이 없으므로 TargetProvider를 호출하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUITKWindowStateAnimator_숨김전환_BoundsProvider미호출()
        {
            var driver = new TestWindowDriver();
            var provider = new TestAnimationTargetProvider
            {
                FailureCount = 1,
            };
            var animator = new XeriUITKWindowStateAnimator
            (
                new VisualElement(),
                XeriWindowAnimationOptions.Immediate(),
                provider
            );
            var context = new XeriWindowStateTransitionContext
            (
                new XeriWindowStateTransitionRequest
                {
                    Driver = driver,
                    PreviousState = XeriWindowState.Maximized,
                    NextState = XeriWindowState.Closed,
                }
            );
            var completed = false;

            Assert.DoesNotThrow
            (
                () => animator.Play(context, () => completed = true, null)
            );

            Assert.IsTrue(completed);
            Assert.IsFalse(animator.IsRunning);
            Assert.AreEqual(1, provider.FailureCount);
        }

    #endregion

    }
}
