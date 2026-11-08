/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriUITKWindowStateTransitioner.cs
수정일 : 2026-11-08

# 설명
Xeri window 상태 전환 lifecycle과 UITK animator를 연결한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
    // ======================================================================
    /// <summary>
    /// UITK animation을 사용하는 Xeri window 상태 전환 transitioner.
    /// </summary>
    // ======================================================================
    public sealed class XeriUITKWindowStateTransitioner : IXeriWindowStateTransitioner
    {

    #region 필드

        private readonly IXeriWindowStateAnimator animator = null;

        private XeriWindowStateTransitionRequest currentRequest = null;
        private XeriWindowStateTransitionContext currentContext;
        private int transitionID = 0;

    #endregion

    #region 프로퍼티

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 전환 실행 상태.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowTransitionStatus Status => status;

        private XeriWindowTransitionStatus status = XeriWindowTransitionStatus.Idle;

        // ------------------------------------------------------------
        /// <summary>
        /// 전환 실행 중 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsRunning => status == XeriWindowTransitionStatus.Running;

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 전환 목표 상태.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowState? PendingState => pendingState;

        private XeriWindowState? pendingState = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// UITK window state transitioner를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriUITKWindowStateTransitioner(IXeriWindowStateAnimator animator) : base()
        {
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환을 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Transition(XeriWindowStateTransitionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // 외부에서 변경 가능한 요청을 실행 단위의 독립 사본으로 고정한다.
            var execution = SnapshotRequest(request);
            if (execution.Driver == null)
            {
                throw new ArgumentNullException(nameof(request.Driver));
            }

            if (!ResolveRunningRequest(execution)) return false;

            // Snapshot 생성에 실패해도 실행 중 상태가 게시되지 않는다.
            var expectedID = transitionID;
            var context = new XeriWindowStateTransitionContext(execution);
            if (expectedID != transitionID) return false;

            currentRequest = execution;
            currentContext = context;
            pendingState = execution.NextState;
            status = XeriWindowTransitionStatus.Running;
            var id = ++transitionID;

            try
            {
                if (!PrepareDriver(execution, id)) return false;

                if (!execution.Animate)
                {
                    Complete(id);
                    return true;
                }

                animator.Play
                (
                    context,
                    () => Complete(id),
                    exception => Fail(id, exception)
                );

                return true;
            }
            catch (Exception exception)
            {
                if (id != transitionID)
                {
                    throw;
                }

                Fail(id, exception);
                return true;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 상태 전환을 취소한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Cancel(bool restoreVisual)
        {
            if (!IsRunning) return;

            var request = currentRequest;
            var context = currentContext;
            var errors = new ExceptionBuffer();

            // Animator 정리나 취소 통지의 재진입은 이전 실행을 다시 완료할 수 없다.
            ClearRunningState();
            var cancelID = transitionID;

            try
            {
                animator.Cancel(restoreVisual);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (restoreVisual && cancelID == transitionID)
            {
                try
                {
                    RollbackState(request, context, cancelID);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            try
            {
                request?.OnCancel?.Invoke();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            ThrowErrors("Window 상태 전환 취소", errors);
        }

    #endregion

    #region 요청 Snapshot과 충돌 정책

        // ------------------------------------------------------------
        /// <summary>
        /// 실행 요청의 Driver·상태·bounds·콜백을 한 시점의 사본으로 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriWindowStateTransitionRequest SnapshotRequest(XeriWindowStateTransitionRequest request)
        {
            return new XeriWindowStateTransitionRequest
            {
                Driver = request.Driver,
                PreviousState = request.PreviousState,
                NextState = request.NextState,
                TargetBounds = request.TargetBounds,
                InterruptPolicy = request.InterruptPolicy,
                Animate = request.Animate,
                OnComplete = request.OnComplete,
                OnCancel = request.OnCancel,
                OnError = request.OnError,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 전환과 새 요청의 충돌 정책을 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool ResolveRunningRequest(XeriWindowStateTransitionRequest request)
        {
            if (!IsRunning) return true;

            if
            (
             pendingState == request.NextState &&
             request.InterruptPolicy == XeriWindowTransitionInterruptPolicy.IgnoreSameTarget
            )
            {
                return false;
            }

            if (request.InterruptPolicy == XeriWindowTransitionInterruptPolicy.RejectWhileRunning)
            {
                return false;
            }

            var canceledID = transitionID;
            Cancel(true);

            // 취소 통지가 다른 전환을 시작했다면 기존 요청을 덮어쓰지 않는다.
            return transitionID == canceledID + 1;
        }

    #endregion

    #region 상태 적용과 전환 종료

        // ------------------------------------------------------------
        /// <summary>
        /// Visual animation 시작 전에 표시 여부만 준비한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool PrepareDriver(XeriWindowStateTransitionRequest request, int id)
        {
            // 표시되는 상태로 들어가는 animation은 완료 전에도 element가 보일 수 있어야 한다.
            if (request.NextState != XeriWindowState.Minimized && request.NextState != XeriWindowState.Closed)
            {
                request.Driver.Visibility.Set(true);
                if (id != transitionID) return false;
            }

            // Driver 호출 중 새 전환이 시작되면 이전 요청의 시각 상태를 덮어쓰지 않는다.
            request.Driver.ApplyVisualState(request.NextState);
            return id == transitionID;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Animation 완료 후 최종 driver primitive를 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Complete(int id)
        {
            if (id != transitionID) return;

            var request = currentRequest;
            var context = currentContext;
            Exception failure = null;
            var terminal = false;

            try
            {
                if (!ApplyCompletedState(request, id)) return;
            }
            catch (Exception exception)
            {
                if (id != transitionID) return;

                failure = exception;

                try
                {
                    RollbackState(request, context, id);
                }
                catch (Exception rollbackException)
                {
                    failure = new AggregateException(exception, rollbackException);
                }
            }
            finally
            {
                if (id == transitionID)
                {
                    ClearRunningState();
                    terminal = true;
                }
            }

            // Driver나 rollback에서 새 실행이 시작됐다면 이전 완료 통지를 전달하지 않는다.
            if (!terminal) return;

            if (failure != null)
            {
                request?.OnError?.Invoke(failure);
                return;
            }

            request?.OnComplete?.Invoke();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Animation 실패 시 이전 완료 상태로 되돌리고 running 고착을 방지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void Fail(int id, Exception exception)
        {
            if (id != transitionID) return;

            var request = currentRequest;
            var context = currentContext;
            Exception failure = exception;
            var terminal = false;

            try
            {
                RollbackState(request, context, id);
            }
            catch (Exception rollbackException)
            {
                failure = new AggregateException(exception, rollbackException);
            }
            finally
            {
                if (id == transitionID)
                {
                    ClearRunningState();
                    terminal = true;
                }
            }

            if (terminal)
            {
                request?.OnError?.Invoke(failure);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Animation 완료 후 driver의 실제 완료 상태를 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool ApplyCompletedState(XeriWindowStateTransitionRequest request, int id)
        {
            if (request.NextState == XeriWindowState.Maximized)
            {
                request.Driver.ApplyMaximizedBounds();
                if (id != transitionID) return false;
            }
            else if (request.TargetBounds.HasValue)
            {
                request.Driver.ApplyBounds(request.TargetBounds.Value);
                if (id != transitionID) return false;
            }

            request.Driver.CommitState(request.NextState);
            if (id != transitionID) return false;

            if (request.NextState == XeriWindowState.Minimized || request.NextState == XeriWindowState.Closed)
            {
                request.Driver.Visibility.Set(false);
                if (id != transitionID) return false;
            }

            return true;
        }

    #endregion

    #region 상태 롤백과 실행 정리

        // ------------------------------------------------------------
        /// <summary>
        /// Animation 실패 시 driver를 전환 전 완료 상태로 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
        private void RollbackState
        (
            XeriWindowStateTransitionRequest request,
            XeriWindowStateTransitionContext context,
            int id
        )
        {
            if (request?.Driver == null || id != transitionID) return;

            var driver = request.Driver;
            var errors = new ExceptionBuffer();

            TryRollback(() => driver.Alpha.Set(1f), ref errors);
            if (id != transitionID) return;

            if (request.PreviousState == XeriWindowState.Maximized)
            {
                TryRollback(driver.ApplyMaximizedBounds, ref errors);
            }
            else
            {
                TryRollback(() => driver.ApplyBounds(context.PreviousBounds), ref errors);
            }

            if (id != transitionID) return;
            TryRollback(() => driver.CommitState(request.PreviousState), ref errors);
            if (id != transitionID) return;

            TryRollback
            (
                () => driver.Visibility.Set
                (
                    request.PreviousState != XeriWindowState.Minimized &&
                    request.PreviousState != XeriWindowState.Closed
                ),
                ref errors
            );

            // 소유권이 전환됐다면 새 실행에 이전 rollback 오류를 전달하지 않는다.
            if (id != transitionID) return;
            ThrowErrors("Window 상태 전환 롤백", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 rollback primitive를 끝까지 시도하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void TryRollback
        (
            Action rollback,
            ref ExceptionBuffer errors
        )
        {
            try
            {
                rollback();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 전환 실행 상태를 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ClearRunningState()
        {
            transitionID++;
            currentRequest = null;
            currentContext = default;
            pendingState = null;
            status = XeriWindowTransitionStatus.Idle;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 수집된 transition 정리 오류를 한 번 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ThrowErrors
        (
            string message,
            ExceptionBuffer errors
        )
        {
            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException(message);
        }

    #endregion

    }
}
