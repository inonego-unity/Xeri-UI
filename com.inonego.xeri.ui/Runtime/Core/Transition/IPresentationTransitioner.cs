/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IPresentationTransitioner.cs
수정일 : 2026-11-08

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
        /// <br/> Transition을 시작하고 취소 가능한 Handle을 반환한다.
        /// <br/> 완료·실패는 배타적이며 대응 callback은 한 번만 호출한다.
        /// <br/> Handle 종결 후 전달하며 Play 반환 전에도 호출할 수 있다.
        /// <br/> 취소는 Handle의 OnCancelled로 전달한다.
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
