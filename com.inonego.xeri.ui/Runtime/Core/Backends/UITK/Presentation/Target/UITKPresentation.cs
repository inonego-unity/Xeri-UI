/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentation.cs
수정일 : 2026-09-19
# 설명
기존 VisualElement를 Xeri Presentation State에 연결한다.
Visual Tree hierarchy는 소유하지 않으며 opacity와 display 표현 상태만 제어한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// <br/> 기존 VisualElement를 Xeri Alpha·Visibility
    /// <br/> Presentation State에 연결하는 adapter.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class UITKPresentation :
        IPresentation,
        IPresentationAlphaTarget,
        IPresentationVisibilityTarget
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// VisualElement opacity에 적용되는 합성 Alpha State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha Alpha { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// VisualElement display에 적용되는 합성 Visibility State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationVisibility Visibility { get; }

        private readonly VisualElement root = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// VisualElement를 Presentation으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKPresentation(VisualElement root) : base()
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            Alpha = new PresentationAlpha(this);
            Visibility = new PresentationVisibility(this);
        }

    #endregion

    #region IPresentationAlphaTarget

        // ------------------------------------------------------------
        /// <summary>
        /// VisualElement 참조가 살아 있어 Alpha를 적용할 수 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        bool IPresentationAlphaTarget.IsValid => root != null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 VisualElement opacity.
        /// </summary>
        // ------------------------------------------------------------
        float IPresentationAlphaTarget.Alpha => root.resolvedStyle.opacity;

        // ------------------------------------------------------------
        /// <summary>
        /// 합성 Alpha를 VisualElement opacity에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void IPresentationAlphaTarget.SetAlpha(float alpha)
        {
            root.style.opacity = Mathf.Clamp01(alpha);
        }

    #endregion

    #region IPresentationVisibilityTarget

        // ----------------------------------------------------------------------
        /// <summary>
        /// VisualElement 참조가 살아 있어 Visibility를 적용할 수 있는지 여부.
        /// </summary>
        // ----------------------------------------------------------------------
        bool IPresentationVisibilityTarget.IsValid => root != null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 VisualElement display 상태.
        /// </summary>
        // ------------------------------------------------------------
        bool IPresentationVisibilityTarget.IsVisible =>
            root.resolvedStyle.display != DisplayStyle.None;

        // ------------------------------------------------------------
        /// <summary>
        /// 합성 Visibility를 VisualElement display에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void IPresentationVisibilityTarget.SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

    #endregion

    }
}
