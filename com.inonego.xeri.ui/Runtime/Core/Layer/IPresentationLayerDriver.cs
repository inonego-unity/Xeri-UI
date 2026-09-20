/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IPresentationLayerDriver.cs
수정일 : 2026-09-15

# 설명
Presentation Layer의 공통 활성 상태와 backend별 typed Root 계약만 정의한다.
표현 상태는 Layer 계약에 포함하지 않고 필요한 Root를 별도 Presentation으로 연결한다.
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
        /// backend 구성이 Layer Asset과 일치하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        bool Validate(PresentationLayerAsset asset, out string error);

        // ------------------------------------------------------------
        /// <summary>
        /// Layer의 공통 Screen Overlay 정렬 순서를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void SetOrder(int order);

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Root의 활성 상태를 적용한다.
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
