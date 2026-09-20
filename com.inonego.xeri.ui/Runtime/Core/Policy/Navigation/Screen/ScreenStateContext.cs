/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenStateContext.cs
수정일 : 2026-09-17
# 설명
Screen 상태 훅에 현재 Session과 상태, 취소 가능 범위를 비소유 참조로 전달한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Screen 상태 훅 실행 Context.
    /// </summary>
    // ============================================================
    public sealed class ScreenStateContext
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 훅 대상 Session의 비소유 참조.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenSession Session { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 훅이 알리는 Screen 상태.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenState State { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 훅에서 취소 요청이 허용되는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool CanCancel { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 처리자가 취소를 요청했는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsCancelled { get; private set; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 상태 훅 Context를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal ScreenStateContext
        (
            ScreenSession session,
            ScreenState state,
            bool canCancel
        ) : base()
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            State = state;
            CanCancel = canCancel;
            IsCancelled = false;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 취소 가능한 Opening 또는 Closing 훅에 취소를 요청한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Cancel()
        {
            if (!CanCancel)
            {
                throw new InvalidOperationException("현재 Screen 상태 훅은 취소할 수 없습니다.");
            }

            IsCancelled = true;
        }

    #endregion

    }
}
