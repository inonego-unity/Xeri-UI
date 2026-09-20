/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationAlpha.cs
수정일 : 2026-09-19

# 설명
Presentation의 local Alpha State를 기존 MValue<float>로 관리한다.
local Modified 변경은 직접 연결된 backend에 반영하며 Composite 누적 Alpha는 State에 저장하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// MValue 기반 local Alpha State와 선택적 backend Target을 연결한다.
    /// </summary>
    // ======================================================================
    public sealed class PresentationAlpha : MValue<float>
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 직접 연결된 backend Target이 현재 적용 가능한지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsValid => target != null && target.IsValid;

        private readonly IPresentationAlphaTarget target = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Alpha identity 값 1을 Base로 가지는 local State를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha() : base(1.0f)
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// backend의 현재 Alpha를 Base로 사용하는 local State를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha(IPresentationAlphaTarget target) :
            base(RequireTarget(target).Alpha)
        {
            this.target = target;
            ApplyLocal();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 Base Alpha로 backend local State를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha
        (
            IPresentationAlphaTarget target,
            float baseAlpha
        ) :
        base(baseAlpha)
        {
            this.target = RequireTarget(target);
            ApplyLocal();
        }

    #endregion

    #region MValue

        // ------------------------------------------------------------
        /// <summary>
        /// Alpha Base를 0~1 범위로 제한한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void ProcessBase(in float prev, ref float next)
        {
            next = Mathf.Clamp01(next);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// local Modified 갱신을 직접 연결된 backend에 즉시 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void OnModifiedUpdated
        (
            in float prev,
            in float next
        )
        {
            ApplyLocal();
        }

    #endregion

    #region 적용

        // ----------------------------------------------------------------------
        /// <summary>
        /// Tree에서 전달한 parent Alpha와 local Modified를 곱해 backend에 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void ApplyInherited(float parentAlpha)
        {
            if (target == null) return;

            if (!target.IsValid)
            {
                throw new InvalidOperationException
                (
                    "Presentation Alpha Target이 유효하지 않습니다."
                );
            }

            target.SetAlpha
            (
                Mathf.Clamp01(parentAlpha * Modified)
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// local Modified 값을 직접 연결된 Alpha backend에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyLocal()
        {
            if (target == null) return;

            if (!target.IsValid)
            {
                throw new InvalidOperationException
                (
                    "Presentation Alpha Target이 유효하지 않습니다."
                );
            }

            target.SetAlpha(Modified);
        }

    #endregion

    #region 검증

        // ------------------------------------------------------------
        /// <summary>
        /// Alpha backend Target이 존재하고 현재 적용 가능한지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private static IPresentationAlphaTarget RequireTarget
        (
            IPresentationAlphaTarget target
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
                    "Presentation Alpha Target이 유효하지 않습니다."
                );
            }

            return target;
        }

    #endregion

    }
}
