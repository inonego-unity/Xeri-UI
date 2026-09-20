/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSpotlightTarget.cs
수정일 : 2026-09-17
# 설명
실제 VisualElement와 여백으로 UI Toolkit Spotlight 구멍 대상을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

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

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 VisualElement와 선택적 여백으로 Spotlight 대상을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKSpotlightTarget
        (
            VisualElement target,
            Vector4 padding = default
        ) : this()
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Padding = padding;
        }

    #endregion

    }
}
