/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IModalInteractionDriver.cs
수정일 : 2026-09-15

# 설명
Modality Policy가 Stack top 여부에 따라 해당 UI의 상호작용 우선권을 적용하는 backend 계약을 정의한다.
Presentation State와 Dim visual은 이 계약에 포함하지 않는다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Modal Stack의 상호작용 top 상태를 적용하는 backend 계약.
    /// </summary>
    // ============================================================
    public interface IModalInteractionDriver
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Modal이 Stack top으로 상호작용할 수 있는지 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void SetTop(bool isTop);
    }
}
