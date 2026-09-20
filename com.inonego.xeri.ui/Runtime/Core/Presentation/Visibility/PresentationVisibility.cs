/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationVisibility.cs
수정일 : 2026-09-19

# 설명
Presentation의 local Visibility State를 기존 MValue<bool>로 관리한다.
local Modified 변경은 직접 연결된 backend에 반영하며 Composite 누적 Visibility는 State에 저장하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// MValue 기반 local Visibility State와 선택적 backend Target을 연결한다.
    /// </summary>
    // ======================================================================
    public sealed class PresentationVisibility : MValue<bool>
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 직접 연결된 backend Target이 현재 적용 가능한지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsValid => target != null && target.IsValid;

        private readonly IPresentationVisibilityTarget target = null;

    #endregion

    #region 생성자

        // ----------------------------------------------------------------------
        /// <summary>
        /// Visibility identity 값 true를 Base로 가지는 local State를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public PresentationVisibility() : base(true)
        {
            // NONE
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// backend의 현재 Visibility를 Base로 사용하는 local State를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public PresentationVisibility(IPresentationVisibilityTarget target) :
            base(RequireTarget(target).IsVisible)
        {
            this.target = target;
            ApplyLocal();
        }

    #endregion

    #region MValue

        // ------------------------------------------------------------
        /// <summary>
        /// local Modified 갱신을 직접 연결된 backend에 즉시 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void OnModifiedUpdated
        (
            in bool prev,
            in bool next
        )
        {
            ApplyLocal();
        }

    #endregion

    #region 적용

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Tree에서 전달한 parent Visibility와 local Modified를 AND해 backend에 적용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void ApplyInherited(bool parentVisibility)
        {
            if (target == null) return;

            if (!target.IsValid)
            {
                throw new InvalidOperationException
                (
                    "Presentation Visibility Target이 유효하지 않습니다."
                );
            }

            target.SetVisible
            (
                parentVisibility && Modified
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// local Modified 값을 직접 연결된 Visibility backend에 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplyLocal()
        {
            if (target == null) return;

            if (!target.IsValid)
            {
                throw new InvalidOperationException
                (
                    "Presentation Visibility Target이 유효하지 않습니다."
                );
            }

            target.SetVisible(Modified);
        }

    #endregion

    #region 검증

        // ----------------------------------------------------------------------
        /// <summary>
        /// Visibility backend Target이 존재하고 현재 적용 가능한지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static IPresentationVisibilityTarget RequireTarget
        (
            IPresentationVisibilityTarget target
        )
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!target.IsValid)
            {
                throw new InvalidOperationException
                (
                    "Presentation Visibility Target이 유효하지 않습니다."
                );
            }

            return target;
        }

    #endregion

    }
}
