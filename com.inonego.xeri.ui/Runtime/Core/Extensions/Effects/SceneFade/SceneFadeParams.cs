/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SceneFadeParams.cs
수정일 : 2026-09-17
# 설명
Scene Fade 색상과 Transition 시간을 불변 호출 인자로 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Primitive;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Scene Fade 실행 인자.
    /// </summary>
    // ============================================================
    public readonly struct SceneFadeParams
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Fade Overlay 색상.
        /// </summary>
        // ------------------------------------------------------------
        public Color Color { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Fade Transition 재생 시간.
        /// </summary>
        // ------------------------------------------------------------
        public float Duration { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Scene Fade 실행 인자를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public SceneFadeParams(Color color, float duration) : this()
        {
            if (!duration.IsFinite() || duration < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            Color = color;
            Duration = duration;
        }

    #endregion

    }
}
