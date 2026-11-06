/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSpotlightClip.cs
수정일 : 2026-11-06

# 설명
UI Toolkit Spotlight가 USS 클리핑 경계로 사용할 조상과 clip box를 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// USS 클리핑 경계와 콘텐츠·패딩 영역 선택.
    /// </summary>
    // ============================================================
    public readonly struct UITKSpotlightClip
    {
        public VisualElement Element { get; }
        public OverflowClipBox Box { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// USS clipping ancestor와 클리핑 영역을 지정한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKSpotlightClip
        (
            VisualElement element,
            OverflowClipBox box = OverflowClipBox.PaddingBox
        ) : this()
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Box = box;
        }
    }
}
