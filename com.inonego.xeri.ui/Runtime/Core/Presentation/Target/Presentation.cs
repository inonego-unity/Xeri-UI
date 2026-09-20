/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : Presentation.cs
수정일 : 2026-09-19
# 설명
기존 UI backend Target을 Xeri Presentation State에 연결하는 기본 Presentation 구현이다.
Alpha와 Visibility는 서로 독립 capability이며 제공된 Target에 대해서만 생성한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// <br/> 기존 UI backend를 Alpha·Visibility
    /// <br/> Presentation State에 연결하는 기본 Presentation.
    /// </summary>
    // ============================================================
    public sealed class Presentation : IPresentation
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 합성 가능한 Alpha State. Alpha Target이 없으면 null이다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha Alpha { get; }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 합성 가능한 Visibility State. Visibility Target이 없으면 null이다.
        /// </summary>
        // ----------------------------------------------------------------------
        public PresentationVisibility Visibility { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 Alpha State를 하나의 Presentation leaf로 묶는다.
        /// </summary>
        // ------------------------------------------------------------
        public Presentation(PresentationAlpha alpha) :
            this(alpha, null)
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 Visibility State를 하나의 Presentation leaf로 묶는다.
        /// </summary>
        // ------------------------------------------------------------
        public Presentation(PresentationVisibility visibility) :
            this(null, visibility)
        {
            // NONE
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 기존 Alpha·Visibility State를 하나의 Presentation leaf로 묶는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public Presentation
        (
            PresentationAlpha alpha,
            PresentationVisibility visibility
        ) : base()
        {
            if (alpha == null && visibility == null)
            {
                throw new ArgumentException
                (
                    "Presentation에는 하나 이상의 State가 필요합니다."
                );
            }

            Alpha = alpha;
            Visibility = visibility;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 제공한 backend Target으로 지원 가능한 Presentation State를 구성한다.
        /// <br/> Alpha와 Visibility 중 하나 이상의 Target이 필요하다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public Presentation
        (
            IPresentationAlphaTarget alphaTarget = null,
            IPresentationVisibilityTarget visibilityTarget = null
        ) : base()
        {
            if (alphaTarget == null && visibilityTarget == null)
            {
                throw new ArgumentException("Presentation에는 하나 이상의 State Target이 필요합니다.");
            }

            if (alphaTarget != null)
            {
                Alpha = new PresentationAlpha(alphaTarget);
            }

            if (visibilityTarget != null)
            {
                Visibility = new PresentationVisibility(visibilityTarget);
            }
        }

    #endregion

    }
}
