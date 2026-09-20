/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIPresentation.cs
수정일 : 2026-09-19
# 설명
기존 UGUI GameObject와 선택적 CanvasGroup을 Xeri Presentation State에 연결한다.
GameObject hierarchy는 소유하지 않으며 Visibility와 가능한 경우 Alpha만 제어한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ================================================================================
    /// <summary>
    /// 기존 UGUI Root를 Xeri Alpha·Visibility Presentation State에 연결하는 adapter.
    /// </summary>
    // ================================================================================
    [Serializable]
    public sealed class UGUIPresentation :
        IPresentation,
        IPresentationAlphaTarget,
        IPresentationVisibilityTarget
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// CanvasGroup이 있을 때 제공되는 합성 Alpha State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha Alpha { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Root 활성 상태에 적용되는 합성 Visibility State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationVisibility Visibility { get; }

        private readonly GameObject root = null;
        private readonly CanvasGroup canvasGroup = null;

    #endregion

    #region 생성자

        // --------------------------------------------------------------------------------
        /// <summary>
        /// UGUI Root와 같은 GameObject의 선택적 CanvasGroup을 Presentation으로 연결한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public UGUIPresentation
        (
            GameObject root,
            CanvasGroup canvasGroup = null
        ) : base()
        {
            this.root = root != null
                ? root
                : throw new ArgumentNullException(nameof(root));
            this.canvasGroup = canvasGroup != null
                ? canvasGroup
                : root.GetComponent<CanvasGroup>();

            if (this.canvasGroup != null && this.canvasGroup.gameObject != root)
            {
                throw new ArgumentException
                (
                    "Presentation CanvasGroup은 Root GameObject에 있어야 합니다.",
                    nameof(canvasGroup)
                );
            }

            if (this.canvasGroup != null)
            {
                Alpha = new PresentationAlpha(this);
            }

            Visibility = new PresentationVisibility(this);
        }

    #endregion

    #region IPresentationAlphaTarget

        // ------------------------------------------------------------
        /// <summary>
        /// CanvasGroup이 살아 있어 Alpha를 적용할 수 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        bool IPresentationAlphaTarget.IsValid => canvasGroup != null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 CanvasGroup Alpha.
        /// </summary>
        // ------------------------------------------------------------
        float IPresentationAlphaTarget.Alpha =>
            canvasGroup != null ? canvasGroup.alpha : 0.0f;

        // ------------------------------------------------------------
        /// <summary>
        /// 합성 Alpha를 CanvasGroup에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void IPresentationAlphaTarget.SetAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                throw new MissingReferenceException("UGUI Presentation CanvasGroup이 없습니다.");
            }

            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

    #endregion

    #region IPresentationVisibilityTarget

        // ------------------------------------------------------------
        /// <summary>
        /// Root GameObject가 살아 있어 표시 상태를 적용할 수 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        bool IPresentationVisibilityTarget.IsValid => root != null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Root 활성 상태.
        /// </summary>
        // ------------------------------------------------------------
        bool IPresentationVisibilityTarget.IsVisible => root != null && root.activeSelf;

        // ------------------------------------------------------------
        /// <summary>
        /// 합성 Visibility를 Root 활성 상태에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void IPresentationVisibilityTarget.SetVisible(bool visible)
        {
            if (root == null)
            {
                throw new MissingReferenceException("UGUI Presentation Root가 없습니다.");
            }

            root.SetActive(visible);
        }

    #endregion

    }
}
