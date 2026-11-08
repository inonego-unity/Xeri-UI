/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SplashParams.cs
수정일 : 2026-11-07

# 설명
Splash 콘텐츠의 진입, 유지와 퇴장 시간 및 목표 Alpha를 정의하는 불변 호출 인자.
모든 시간은 Unscaled Time 기준이다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Splash 한 번의 표시 시간 정책.
    /// </summary>
    // ============================================================
    public readonly struct SplashParams
    {

    #region 필드

        public float FadeInDuration { get; }
        public float HoldDuration { get; }
        public float FadeOutDuration { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 재생 중 Splash가 도달할 목표 Alpha이다.
        /// </summary>
        // ------------------------------------------------------------
        public float VisibleAlpha => visibleAlpha ?? 1f;
        private readonly float? visibleAlpha;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 0 이상의 유한한 재생 시간과 0~1 범위의 목표 Alpha를 설정한다.
        /// </summary>
        // ------------------------------------------------------------
        public SplashParams
        (
            float fadeInDuration,
            float holdDuration,
            float fadeOutDuration,
            float visibleAlpha = 1f
        )
        {
            ValidateDuration(fadeInDuration, nameof(fadeInDuration));
            ValidateDuration(holdDuration, nameof(holdDuration));
            ValidateDuration(fadeOutDuration, nameof(fadeOutDuration));

            if (float.IsNaN(visibleAlpha) || visibleAlpha < 0f || visibleAlpha > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(visibleAlpha));
            }

            FadeInDuration = fadeInDuration;
            HoldDuration = holdDuration;
            FadeOutDuration = fadeOutDuration;
            this.visibleAlpha = visibleAlpha;
        }

    #endregion

    #region 메서드

        public static SplashParams Default() => new SplashParams(0.25f, 1.5f, 0.25f);


        private static void ValidateDuration(float duration, string parameterName)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

    #endregion

    }
}
