/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IPresentationSource.cs
수정일 : 2026-09-15

# 설명
Presentation Layer에 View를 획득하고 반환하는 일반 Presentation Source 계약을 정의한다.
특정 Overlay 의미를 갖지 않으며 SceneFade·Tooltip·Toast 등 상위 기능이 동일한 lifetime 경계를 재사용한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Presentation View 획득과 반환을 소유하는 Source.
    /// </summary>
    // ============================================================
    public interface IPresentationSource<TView>
    where TView : class
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Layer Driver에서 Presentation View를 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        TView Acquire(IPresentationLayerDriver layer);

        // ------------------------------------------------------------
        /// <summary>
        /// Source가 획득한 Presentation View를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        void Release(TView view);
    }
}
