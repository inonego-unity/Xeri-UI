/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IScreenDriver.cs
수정일 : 2026-09-19
# 설명
Screen Navigation Policy가 사용하는 Presentation과 Interaction backend 계약을 정의한다.
표현 상태와 상호작용 계약은 분리하되 실제 backend는 두 계약을 함께 구현할 수 있다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// Screen 범위의 Focus 포함 관계와 상호작용 가능 상태를 적용하는 backend 계약.
    /// </summary>
    // ======================================================================
    public interface IScreenInteractionDriver
    {
        // ------------------------------------------------------------
        /// <summary>
        /// backend가 제공하는 기본 Focus 대상.
        /// </summary>
        // ------------------------------------------------------------
        object DefaultFocus { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Focus 대상이 이 Screen 범위에 속하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        bool ContainsFocus(object target);

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 범위의 사용자 상호작용 가능 상태를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void SetInteractable(bool interactable);
    }

    // ================================================================================
    /// <summary>
    /// Screen Navigation Policy가 사용하는 Presentation과 Interaction backend 계약.
    /// </summary>
    // ================================================================================
    public interface IScreenDriver : IPresentation, IScreenInteractionDriver
    {
        // --------------------------------------------------------------------------------
        /// <summary>
        /// Screen backend가 현재 Presentation과 Interaction을 제공할 수 있는지 여부.
        /// </summary>
        // --------------------------------------------------------------------------------
        bool IsValid { get; }
    }
}
