/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IPresentation.cs
수정일 : 2026-09-19

# 설명
Alpha와 Visibility 상태를 노출하는 Presentation 최소 계약을 정의한다.
각 상태는 기존 MValue 기반 concrete state이며 leaf와 Composite에서 동일하게 사용한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Alpha·Visibility 표현 상태에 참여하는 논리적 UI 대상.
    /// </summary>
    // ============================================================
    public interface IPresentation
    {
        // ------------------------------------------------------------
        /// <summary>
        /// Presentation의 Alpha State.
        /// </summary>
        // ------------------------------------------------------------
        PresentationAlpha Alpha { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation의 Visibility State.
        /// </summary>
        // ------------------------------------------------------------
        PresentationVisibility Visibility { get; }
    }
}
