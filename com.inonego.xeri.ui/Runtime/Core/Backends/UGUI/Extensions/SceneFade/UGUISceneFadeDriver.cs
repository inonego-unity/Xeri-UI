/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUISceneFadeDriver.cs
수정일 : 2026-09-19
# 설명
UGUI Image와 CanvasGroup을 Scene Fade Presentation State에 연결하고 Fade 색상을 적용한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UI;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI Scene Fade Presentation backend.
    /// </summary>
    // ============================================================
    public sealed class UGUISceneFadeDriver :
        MonoBehaviour,
        ISceneFadeDriver,
        IPresentationAlphaTarget,
        IPresentationVisibilityTarget
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Fade CanvasGroup에 적용되는 합성 Alpha State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha Alpha
        {
            get
            {
                alpha ??= new PresentationAlpha(this);
                return alpha;
            }
        }

        private PresentationAlpha alpha = null;
        // ------------------------------------------------------------
        /// <summary>
        /// Fade GameObject 활성 상태에 적용되는 합성 Visibility State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationVisibility Visibility
        {
            get
            {
                visibility ??= new PresentationVisibility(this);
                return visibility;
            }
        }

        private PresentationVisibility visibility = null;

        [SerializeField]
        private Image image = null;

        [SerializeField]
        private CanvasGroup canvasGroup = null;

    #endregion

    #region ISceneFadeDriver

        // ------------------------------------------------------------
        /// <summary>
        /// Fade Image 색상을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetColor(Color color)
        {
            if (image == null)
            {
                throw new MissingReferenceException("UGUI Scene Fade Image가 없습니다.");
            }

            image.color = color;
        }

    #endregion

    #region IPresentationAlphaTarget

        // ------------------------------------------------------------
        /// <summary>
        /// CanvasGroup이 살아 있어 Alpha를 적용할 수 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        bool IPresentationAlphaTarget.IsValid => this != null && canvasGroup != null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Fade CanvasGroup Alpha.
        /// </summary>
        // ------------------------------------------------------------
        float IPresentationAlphaTarget.Alpha =>
            canvasGroup != null ? canvasGroup.alpha : 0.0f;

        // ------------------------------------------------------------
        /// <summary>
        /// 합성 Alpha를 Fade CanvasGroup에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void IPresentationAlphaTarget.SetAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                throw new MissingReferenceException("UGUI Scene Fade CanvasGroup이 없습니다.");
            }

            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

    #endregion

    #region IPresentationVisibilityTarget

        // ------------------------------------------------------------
        /// <summary>
        /// Fade GameObject가 살아 있어 Visibility를 적용할 수 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        bool IPresentationVisibilityTarget.IsValid => this != null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Fade GameObject 활성 상태.
        /// </summary>
        // ------------------------------------------------------------
        bool IPresentationVisibilityTarget.IsVisible =>
            this != null && gameObject.activeSelf;

        // ------------------------------------------------------------
        /// <summary>
        /// 합성 Visibility를 Fade GameObject 활성 상태에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void IPresentationVisibilityTarget.SetVisible(bool visible)
        {
            if (this == null)
            {
                throw new MissingReferenceException("UGUI Scene Fade Driver가 파괴됐습니다.");
            }

            gameObject.SetActive(visible);
        }

    #endregion

    }
}
