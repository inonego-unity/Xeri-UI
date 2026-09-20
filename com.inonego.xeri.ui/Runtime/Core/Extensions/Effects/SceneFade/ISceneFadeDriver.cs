/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ISceneFadeDriver.cs
수정일 : 2026-09-19
# 설명
Scene Fade View가 제공하는 Presentation State와 색상 적용 계약을 정의한다.
Fade Alpha는 일반 PresentationAlpha를 통해 전환하며 Scene Fade 계약은 색상 책임만 추가한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// Scene Fade Presentation에 색상 적용 기능을 추가한 backend 계약.
    /// </summary>
    // ======================================================================
    public interface ISceneFadeDriver : IPresentation
    {
        // ------------------------------------------------------------
        /// <summary>
        /// Fade 표시 색상을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void SetColor(Color color);
    }
}
