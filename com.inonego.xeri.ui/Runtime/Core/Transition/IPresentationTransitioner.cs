/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IPresentationTransitioner.cs
수정일 : 2026-07-29

# 설명
backend 독립 Presentation Transition 실행과 완료·오류 callback 계약을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Presentation Transition 실행 backend 계약.
    /// </summary>
    // ============================================================
    public interface IPresentationTransitioner : IDisposable
    {
        // ------------------------------------------------------------
        /// <summary>
        /// Transition을 시작하고 취소 가능한 Handle을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        PresentationTransitionHandle Play
        (
            PresentationTransitionParams parameters,
            Action onCompleted,
            Action<Exception> onFailed
        );
    }
}
