/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriImmediateWindowStateTransitioner.cs
수정일 : 2026-10-03

# 설명
애니메이션 없이 Xeri window 상태 전환을 즉시 완료하는 transitioner.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// 애니메이션 없는 Xeri window 상태 전환 transitioner.
    /// </summary>
    // ============================================================
    public sealed class XeriImmediateWindowStateTransitioner : IXeriWindowStateTransitioner
    {

    #region 프로퍼티

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 전환 실행 상태.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowTransitionStatus Status => XeriWindowTransitionStatus.Idle;

        // ------------------------------------------------------------
        /// <summary>
        /// 전환 실행 중 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsRunning => false;

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 전환 목표 상태.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowState? PendingState => null;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환을 즉시 완료한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Transition(XeriWindowStateTransitionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Driver == null)
            {
                throw new ArgumentNullException(nameof(request.Driver));
            }

            var context = new XeriWindowStateTransitionContext(request);

            try
            {
                ApplyImmediate(request);
            }
            catch (Exception exception)
            {
                Exception failure = exception;

                try
                {
                    Rollback(request, context);
                }
                catch (Exception rollbackException)
                {
                    failure = new AggregateException(exception, rollbackException);
                }

                try
                {
                    request.OnError?.Invoke(failure);
                }
                catch (Exception callbackException)
                {
                    failure = new AggregateException(failure, callbackException);
                }

                if (ReferenceEquals(failure, exception))
                {
                    throw;
                }

                throw failure;
            }

            request.OnComplete?.Invoke();
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 즉시 전환은 취소할 실행 상태가 없다.
        /// </summary>
        // ------------------------------------------------------------
        public void Cancel(bool restoreVisual)
        {
            // NONE
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 요청 상태에 맞는 driver primitive를 즉시 호출한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ApplyImmediate(XeriWindowStateTransitionRequest request)
        {
            var driver = request.Driver;
            var isHiddenState = request.NextState == XeriWindowState.Minimized ||
                             request.NextState == XeriWindowState.Closed;

            if (!isHiddenState)
            {
                driver.Visibility.Set(true);
            }

            driver.CommitState(request.NextState);

            if (request.NextState == XeriWindowState.Maximized)
            {
                driver.ApplyMaximizedBounds();
            }
            else if (request.TargetBounds.HasValue)
            {
                driver.ApplyBounds(request.TargetBounds.Value);
            }

            if (isHiddenState)
            {
                driver.Visibility.Set(false);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 즉시 전환 적용 실패 시 시작 시점의 완료 상태와 bounds를 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Rollback
        (
            XeriWindowStateTransitionRequest request,
            XeriWindowStateTransitionContext context
        )
        {
            var driver = request.Driver;
            var errors = new List<Exception>();

            TryRollback(() => driver.Alpha.Set(1f), errors);

            if (request.PreviousState == XeriWindowState.Maximized)
            {
                TryRollback(driver.ApplyMaximizedBounds, errors);
            }
            else
            {
                TryRollback(() => driver.ApplyBounds(context.PreviousBounds), errors);
            }

            TryRollback(() => driver.CommitState(request.PreviousState), errors);
            TryRollback
            (
                () => driver.Visibility.Set
                (
                    request.PreviousState != XeriWindowState.Minimized &&
                    request.PreviousState != XeriWindowState.Closed
                ),
                errors
            );

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "Immediate Window 상태 전환 롤백 중 하나 이상의 복원이 실패했습니다.",
                errors
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 rollback primitive를 끝까지 시도하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void TryRollback
        (
            Action rollback,
            List<Exception> errors
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

    #endregion

    }
}
