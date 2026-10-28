/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowApplicationOptions.cs
수정일 : 2026-10-28

# 설명
Application Window가 runtime에 선택할 Child PresentationLayout을 정의한다.
Window-specific Screen/Overlay/Modal Layer topology를 직접 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
    // ======================================================================
    /// <summary>
    /// <br/> 독립 Child PresentationSession과 Child UIContext를 사용하는
    /// <br/> Application Window 구성.
    /// </summary>
    // ======================================================================
    public sealed class XeriWindowApplicationOptions
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Application Window의 Child Presentation Layout이다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationLayout Layout { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Child Presentation Layout으로 Window 옵션을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowApplicationOptions(PresentationLayout layout)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

    #endregion

    }
}
