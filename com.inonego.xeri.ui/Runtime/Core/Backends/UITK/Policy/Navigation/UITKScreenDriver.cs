/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKScreenDriver.cs
수정일 : 2026-09-19
# 설명
VisualElement Screen Root를 Presentation State와 Screen Interaction 계약에 연결한다.
표현 상태는 UITKPresentation이, Focus 범위와 사용자 상호작용은 이 Driver가 담당한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Screen Presentation과 Interaction backend.
    /// </summary>
    // ============================================================
    public sealed class UITKScreenDriver : IScreenDriver
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Root가 현재 유효한지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsValid => root != null;

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Root의 합성 Alpha State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha Alpha => presentation.Alpha;

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Root의 합성 Visibility State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationVisibility Visibility => presentation.Visibility;

        // ------------------------------------------------------------
        /// <summary>
        /// Screen의 기본 Focus Element.
        /// </summary>
        // ------------------------------------------------------------
        public object DefaultFocus => defaultFocus;

        private readonly VisualElement defaultFocus = null;
        private readonly VisualElement root = null;
        private readonly UITKPresentation presentation = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Visual Tree Screen Root와 선택적 기본 Focus를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKScreenDriver
        (
            VisualElement root,
            VisualElement defaultFocus = null
        ) : base()
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.defaultFocus = defaultFocus;
            presentation = new UITKPresentation(root);
        }

    #endregion

    #region IScreenInteractionDriver

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 VisualElement가 Screen Root subtree에 속하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool ContainsFocus(object target)
        {
            if (!(target is VisualElement element)) return false;

            for (var current = element; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, root)) return true;
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Root의 enabled 상태와 picking 정책을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetInteractable(bool interactable)
        {
            root.SetEnabled(interactable);
            root.pickingMode = interactable ? PickingMode.Position : PickingMode.Ignore;
        }

    #endregion

    }
}
