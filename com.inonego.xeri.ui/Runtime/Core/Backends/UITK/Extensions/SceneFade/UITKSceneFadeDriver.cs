/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSceneFadeDriver.cs
수정일 : 2026-09-19
# 설명
UI Toolkit Scene Fade VisualElement를 Presentation State에 연결하고 Fade 색상을 적용한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Scene Fade Presentation backend.
    /// </summary>
    // ============================================================
    public sealed class UITKSceneFadeDriver :
        ISceneFadeDriver,
        IPresentationAlphaTarget,
        IPresentationVisibilityTarget
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Fade opacity에 적용되는 합성 Alpha State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha Alpha { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Fade 표시 상태에 적용되는 합성 Visibility State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationVisibility Visibility { get; }

        private readonly VisualElement root = null;
        private readonly VisualElement viewRoot = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Visual Tree에 추가된 Fade Root를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKSceneFadeDriver(VisualElement root) : this(root, root)
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Fade 표시 Root와 Source가 추가한 View Root를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal UITKSceneFadeDriver
        (
            VisualElement root,
            VisualElement viewRoot
        ) : base()
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.viewRoot = viewRoot ?? throw new ArgumentNullException(nameof(viewRoot));
            this.root.pickingMode = PickingMode.Position;
            Alpha = new PresentationAlpha(this);
            Visibility = new PresentationVisibility(this);
        }

    #endregion

    #region ISceneFadeDriver

        // ------------------------------------------------------------
        /// <summary>
        /// Fade Root의 배경 색상을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetColor(Color color)
        {
            root.style.backgroundColor = color;
        }

    #endregion

    #region IPresentationAlphaTarget

        // ------------------------------------------------------------
        /// <summary>
        /// Fade Root가 Visual Tree에 연결돼 Alpha를 적용할 수 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        bool IPresentationAlphaTarget.IsValid =>
            root != null && viewRoot != null && viewRoot.parent != null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Fade Root opacity.
        /// </summary>
        // ------------------------------------------------------------
        float IPresentationAlphaTarget.Alpha => root.resolvedStyle.opacity;

        // ------------------------------------------------------------
        /// <summary>
        /// 합성 Alpha를 Fade Root opacity에 적용한다.
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
        /// Fade View가 Visual Tree에 연결돼 Visibility를 적용할 수 있는지 여부.
        /// </summary>
        // ----------------------------------------------------------------------
        bool IPresentationVisibilityTarget.IsValid =>
            root != null && viewRoot != null && viewRoot.parent != null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Fade Root 표시 상태.
        /// </summary>
        // ------------------------------------------------------------
        bool IPresentationVisibilityTarget.IsVisible =>
            root.resolvedStyle.display != DisplayStyle.None;

        // ------------------------------------------------------------
        /// <summary>
        /// 합성 Visibility를 Fade Root display에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void IPresentationVisibilityTarget.SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

    #endregion

    }
}
