/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSpotlightTarget.cs
수정일 : 2026-11-06
# 설명
실제 VisualElement·여백과 USS 기반 명시적 클리핑 경계로 UI Toolkit Spotlight 구멍 대상을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Spotlight의 실제 UI 대상과 사방 여백.
    /// </summary>
    // ============================================================
    public readonly struct UITKSpotlightTarget
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Spotlight 구멍을 계산할 실제 VisualElement.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Target { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 왼쪽, 오른쪽, 위, 아래 순서의 추가 여백.
        /// </summary>
        // ------------------------------------------------------------
        public Vector4 Padding { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// USS에서 지정한 clipping ancestor의 명시적 경계 목록.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<UITKSpotlightClip> Clips => clips;

        private readonly IReadOnlyList<UITKSpotlightClip> clips;

    #endregion

    #region 생성자

        // ----------------------------------------------------------------------
        /// <summary>
        /// VisualElement·여백과 USS clip 경계로 Spotlight 대상을 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public UITKSpotlightTarget
        (
            VisualElement target,
            Vector4 padding = default,
            IReadOnlyList<UITKSpotlightClip> clips = null
        ) : this()
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Padding = padding;

            if (clips == null || clips.Count == 0)
            {
                this.clips = Array.Empty<UITKSpotlightClip>();
                return;
            }

            var copy = new UITKSpotlightClip[clips.Count];

            for (var i = 0; i < copy.Length; i++)
            {
                if (clips[i].Element == null)
                {
                    throw new ArgumentException("Spotlight clip 요소가 null입니다.", nameof(clips));
                }

                copy[i] = clips[i];
            }

            this.clips = Array.AsReadOnly(copy);
        }

    #endregion

    }
}
