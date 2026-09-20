/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIInteractionBlocker.cs
수정일 : 2026-09-17
# 설명
중첩 점유 수에 따라 명시적 UGUI Blocker Root와 CanvasGroup raycast 상태를 적용한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI Interaction Blocker 점유 수명을 소유한다.
    /// </summary>
    // ============================================================
    public sealed class UGUIInteractionBlocker : MonoBehaviour, IInteractionBlocker
    {

    #region 필드

        [SerializeField]
        private GameObject root = null;

        [SerializeField]
        private CanvasGroup canvasGroup = null;

        private int acquisitionCount = 0;

    #endregion

    #region IInteractionBlocker

        // ------------------------------------------------------------
        /// <summary>
        /// Interaction Blocker를 한 번 점유하고 Lease를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public Lease Acquire()
        {
            if (root == null && canvasGroup == null)
            {
                throw new InvalidOperationException
                (
                    "UGUI Interaction Blocker Root 또는 CanvasGroup이 필요합니다."
                );
            }

            acquisitionCount++;

            try
            {
                ApplyState();
            }
            catch
            {
                acquisitionCount--;
                throw;
            }

            return new Lease(Release);
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Interaction Blocker 점유를 한 번 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Release()
        {
            acquisitionCount--;
            ApplyState();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 점유 수를 UGUI 활성·raycast 상태에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyState()
        {
            ApplyState(acquisitionCount > 0);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 활성 상태를 UGUI Root와 CanvasGroup에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyState(bool isActive)
        {
            if (root != null)
            {
                root.SetActive(isActive);
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = isActive;
                canvasGroup.blocksRaycasts = isActive;
            }
        }

    #endregion

    #region Unity 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 활성화 시 현재 점유 수를 실제 UGUI 차단 상태에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnEnable()
        {
            ApplyState();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Component 비활성 중에는 별도 Root에 입력 차단을 남기지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDisable()
        {
            ApplyState(false);
        }

    #endregion

    }
}
