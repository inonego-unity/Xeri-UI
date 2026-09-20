/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IScreenSource.cs
수정일 : 2026-09-17
# 설명
Screen View와 Presenter를 원자적으로 획득·Bind하고 대칭으로 반환하는 Source 계약을 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// ScreenInstance 획득과 반환을 소유하는 Source.
    /// </summary>
    // ============================================================
    public interface IScreenSource
    {
        // ----------------------------------------------------------------------
        /// <summary>
        /// Scope에 맞는 View와 Presenter를 Bind하고 ScreenInstance를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        ScreenInstance Acquire(ScreenViewScope scope);

        // ------------------------------------------------------------
        /// <summary>
        /// Presenter를 Unbind한 뒤 View를 원래 공급 경로에 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        void Release(ScreenInstance instance);
    }
}
