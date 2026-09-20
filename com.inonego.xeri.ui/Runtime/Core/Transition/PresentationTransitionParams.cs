/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationTransitionParams.cs
수정일 : 2026-09-17
# 설명
float 값 Setter와 시작·종료 값, 시간 정책을 Presentation Transition 호출 인자로 묶는다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Primitive;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Presentation Transition 실행 인자.
    /// </summary>
    // ============================================================
    public readonly struct PresentationTransitionParams
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 값을 적용할 Target.
        /// </summary>
        // ------------------------------------------------------------
        public IValueSetter<float> Target { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Transition 시작 값.
        /// </summary>
        // ------------------------------------------------------------
        public float StartValue { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Transition 종료 값.
        /// </summary>
        // ------------------------------------------------------------
        public float EndValue { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Transition 재생 시간.
        /// </summary>
        // ------------------------------------------------------------
        public float Duration { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Unscaled 시간으로 Transition을 재생할지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool UsesUnscaledTime { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Transition 실행 인자를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationTransitionParams
        (
            IValueSetter<float> target,
            float startValue,
            float endValue,
            float duration,
            bool usesUnscaledTime
        ) : this()
        {
            if (!duration.IsFinite() || duration < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            Target = target ?? throw new ArgumentNullException(nameof(target));
            StartValue = startValue;
            EndValue = endValue;
            Duration = duration;
            UsesUnscaledTime = usesUnscaledTime;
        }

    #endregion

    }
}
