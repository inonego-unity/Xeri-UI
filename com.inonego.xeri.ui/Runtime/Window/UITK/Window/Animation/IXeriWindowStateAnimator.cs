/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriWindowStateAnimator.cs
수정일 : 2026-09-20

# 설명
Xeri window 상태 전환 중 UITK visual animation을 실행하는 계약.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri window 상태 전환 visual animator 계약.
    /// </summary>
    // ============================================================
    public interface IXeriWindowStateAnimator
    {

    #region 프로퍼티

        // ------------------------------------------------------------
        /// <summary>
        /// animation 실행 중 여부.
        /// </summary>
        // ------------------------------------------------------------
        bool IsRunning { get; }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 visual animation을 실행한다.
        /// </summary>
        // ------------------------------------------------------------
        void Play
        (
            XeriWindowStateTransitionContext context,
            Action onComplete,
            Action<Exception> onError
        );

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 visual animation을 취소한다.
        /// </summary>
        // ------------------------------------------------------------
        void Cancel(bool restoreVisual);

    #endregion

    }
}
