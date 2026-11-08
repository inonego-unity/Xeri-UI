/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationTransitionHandle.cs
수정일 : 2026-11-07
# 설명
진행 중 Presentation Transition의 취소·종결 상태와 취소 알림을 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 진행 중 Presentation Transition Handle.
    /// </summary>
    // ============================================================
    public sealed class PresentationTransitionHandle : IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Transition이 아직 진행 중인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsPending => state == State.Pending;

        // ------------------------------------------------------------
        /// <summary>
        /// Transition이 정상 완료됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsCompleted => state == State.Completed;

        // ------------------------------------------------------------
        /// <summary>
        /// Transition이 취소됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsCancelled => state == State.Cancelled;

        // ------------------------------------------------------------
        /// <summary>
        /// Transition 적용이 실패했는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsFailed => state == State.Failed;

        private Action cancel = null;
        private State state = State.Pending;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 완료·실패가 아닌 취소로 Transition이 종료되면 한 번 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        public event Action OnCancelled;

    #endregion

    #region 내부 데이터

        private enum State
        {
            Pending = 0,
            Completed = 1,
            Cancelled = 2,
            Failed = 3,
        }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Transition 취소 동작을 소유하는 Handle을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal PresentationTransitionHandle(Action cancel) : base()
        {
            this.cancel = cancel;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Transition을 정상 완료 상태로 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool Complete()
        {
            if (state != State.Pending) return false;

            state = State.Completed;
            cancel = null;
            OnCancelled = null;
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Transition을 적용 실패 상태로 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool Fail()
        {
            if (state != State.Pending) return false;

            state = State.Failed;
            cancel = null;
            OnCancelled = null;
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 Transition을 취소한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Cancel()
        {
            if (state != State.Pending) return;

            var action = cancel;
            var onCancelled = OnCancelled;
            state = State.Cancelled;
            cancel = null;
            OnCancelled = null;

            var errors = new ExceptionBuffer();

            try
            {
                action?.Invoke();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            // 한 구독자의 실패가 다른 취소 알림을 막지 않도록 개별적으로 호출한다.
            if (onCancelled != null)
            {
                foreach (Action listener in onCancelled.GetInvocationList())
                {
                    try
                    {
                        listener();
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            if (errors.Count > 1)
            {
                throw errors.CreateAggregateException("Presentation Transition 취소와 알림이 실패했습니다.");
            }
        }

    #endregion

    #region 수명 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 Transition을 취소하고 Handle을 종결한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            Cancel();
        }

    #endregion

    }
}
