/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenInputSession.cs
수정일 : 2026-09-17
# 설명
한 Screen의 입력·Cursor 정책 점유와 닫기 입력 해제 대기 수명을 표현한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 한 Screen의 입력 정책 수명.
    /// </summary>
    // ============================================================
    public sealed class ScreenInputSession
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 정책 수명이 해제됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsReleased { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 닫기 입력 해제를 기다리는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsAwaitingRelease { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 해제 대기 중 이 Session의 Cursor 정책을 유지하는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool RetainsCursorWhileAwaitingRelease { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// Session을 생성한 Screen 정책.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenOptions Options { get; }

        private Action<ScreenInputSession, bool, bool> release = null;
        private Action onReleaseCompleted = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// backend 해제 callback을 가진 입력 Session을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal ScreenInputSession
        (
            ScreenOptions options,
            Action<ScreenInputSession, bool, bool> release
        ) : base()
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            this.release = release ?? throw new ArgumentNullException(nameof(release));
            IsReleased = false;
            IsAwaitingRelease = false;
            RetainsCursorWhileAwaitingRelease = false;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 해제 대기 여부와 함께 backend에 Session 반환을 요청한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Release
        (
            bool waitForInputRelease,
            bool retainCursorWhileAwaitingRelease = true,
            Action onReleaseCompleted = null
        )
        {
            if (IsReleased) return;

            this.onReleaseCompleted += onReleaseCompleted;

            try
            {
                release(this, waitForInputRelease, retainCursorWhileAwaitingRelease);
            }
            catch
            {
                this.onReleaseCompleted -= onReleaseCompleted;
                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// backend가 Session을 입력 해제 대기 상태로 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void MarkAwaitingRelease(bool retainCursor)
        {
            if (IsReleased) return;

            IsAwaitingRelease = true;
            RetainsCursorWhileAwaitingRelease = retainCursor;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// backend가 Session 수명을 최종 해제 상태로 확정하고 필요할 때 완료 callback을 알린다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void MarkReleased(bool invokeCompletionCallback = true)
        {
            if (IsReleased) return;

            IsAwaitingRelease = false;
            RetainsCursorWhileAwaitingRelease = false;
            IsReleased = true;
            release = null;

            var completionCallback = onReleaseCompleted;
            onReleaseCompleted = null;

            if (invokeCompletionCallback)
            {
                completionCallback?.Invoke();
            }
        }

    #endregion

    }
}
