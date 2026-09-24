/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IPresentationLayerDriver.cs
수정일 : 2026-09-22

# 설명
Presentation Layer의 backend별 View 배치 Root와 논리 활성 상태 계약을 정의한다.
Layer는 Native Output과 sibling ordering을 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Presentation Layer backend 계약.
    /// </summary>
    // ============================================================
    public interface IPresentationLayerDriver
    {
        // ------------------------------------------------------------
        /// <summary>
        /// Layer Root 자체의 backend 구성이 유효한지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        bool Validate(out string error);

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Root의 논리 활성 상태를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void SetActive(bool active);
    }

    // ============================================================
    /// <summary>
    /// backend별 표시 Root 타입을 제공하는 Presentation Layer 계약.
    /// </summary>
    // ============================================================
    public interface IPresentationLayerDriver<out TRoot> : IPresentationLayerDriver
    where TRoot : class
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 표시 View를 배치할 backend Root.
        /// </summary>
        // ------------------------------------------------------------
        TRoot Root { get; }
    }
}
