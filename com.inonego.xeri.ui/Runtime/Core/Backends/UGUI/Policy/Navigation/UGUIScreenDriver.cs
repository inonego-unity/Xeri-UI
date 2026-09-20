/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIScreenDriver.cs
수정일 : 2026-09-19
# 설명
UGUI Screen Root를 Presentation State와 Screen Interaction 계약에 연결한다.
표현 상태는 UGUIPresentation이, Focus 범위와 사용자 상호작용은 이 Driver가 담당한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI Screen Presentation과 Interaction backend.
    /// </summary>
    // ============================================================
    public sealed class UGUIScreenDriver : MonoBehaviour, IScreenDriver
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// backend 참조가 현재 유효한지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsValid => root != null && canvasGroup != null;

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Root의 합성 Alpha State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha Alpha => Presentation.Alpha;

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Root의 합성 Visibility State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationVisibility Visibility => Presentation.Visibility;

        // ------------------------------------------------------------
        /// <summary>
        /// 직렬화한 기본 Focus GameObject.
        /// </summary>
        // ------------------------------------------------------------
        public object DefaultFocus => defaultFocus;

        [SerializeField]
        private GameObject defaultFocus = null;

        private UGUIPresentation Presentation
        {
            get
            {
                presentation ??= new UGUIPresentation(root, canvasGroup);
                return presentation;
            }
        }

        [SerializeField]
        private GameObject root = null;

        [SerializeField]
        private CanvasGroup canvasGroup = null;

        private UGUIPresentation presentation = null;

    #endregion

    #region IScreenInteractionDriver

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 GameObject가 Screen Root hierarchy에 속하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool ContainsFocus(object target)
        {
            if (!(target is GameObject gameObject) || gameObject == null || root == null)
            {
                return false;
            }

            return gameObject == root || gameObject.transform.IsChildOf(root.transform);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// CanvasGroup 상호작용과 raycast 상태를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetInteractable(bool interactable)
        {
            if (canvasGroup == null) return;

            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }

    #endregion

    }
}
