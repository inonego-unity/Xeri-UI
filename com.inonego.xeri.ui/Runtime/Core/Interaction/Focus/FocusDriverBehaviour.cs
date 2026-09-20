/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FocusDriverBehaviour.cs
수정일 : 2026-09-17
# 설명
Unity Focus backend이 공통 Driver에 참여하기 위한 Component 계약을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Unity Focus backend의 공통 Component 계약.
    /// </summary>
    // ============================================================
    public abstract class FocusDriverBehaviour : MonoBehaviour, IFocusDriver
    {

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// backend의 native Focus 대상이 바뀌었을 때 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        internal event Action<FocusDriverBehaviour> OnFocusChanged = null;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 대상이 이 backend이 다루는 Focus 타입인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract bool CanSelect(object target);

        // ------------------------------------------------------------
        /// <summary>
        /// backend의 Host 구성을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ValidateConfiguration()
        {
            if (!enabled)
            {
                throw new InvalidOperationException
                (
                    $"{GetType().Name} Focus Driver가 비활성 상태입니다."
                );
            }

            ValidateBackendConfiguration();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation Layer가 등록되었음을 backend에 알린다.
        /// </summary>
        // ------------------------------------------------------------
        internal void RegisterLayer(IPresentationLayerDriver driver)
        {
            HandleLayerRegistered(driver);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// backend별 Host 구성을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        protected virtual void ValidateBackendConfiguration()
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// backend이 필요한 Presentation Layer 정보를 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        protected virtual void HandleLayerRegistered(IPresentationLayerDriver driver)
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// native Focus 변경을 공통 Driver에 알린다.
        /// </summary>
        // ------------------------------------------------------------
        protected void NotifyFocusChanged()
        {
            OnFocusChanged?.Invoke(this);
        }

    #endregion

    #region IFocusDriver

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 선택된 Focus 대상.
        /// </summary>
        // ------------------------------------------------------------
        public abstract object Current { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 대상이 현재 선택 가능한지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract bool IsValid(object target);

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 대상을 현재 Focus로 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract void Select(object target);

        // ------------------------------------------------------------
        /// <summary>
        /// 명시적 대상이 없을 때 사용할 대체 Focus를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract object FindFallback();

    #endregion

    }
}
