/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIInteractionBlocker.cs
수정일 : 2026-11-05
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
        private bool isApplyingState = false;

    #endregion

    #region 상호작용 차단 구현

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
        /// Root가 Blocker 자체를 비활성화하는 순환 소유 구성을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ValidateConfiguration()
        {
            if
            (
                root != null &&
                (ReferenceEquals(root, gameObject) || transform.IsChildOf(root.transform))
            )
            {
                throw new InvalidOperationException
                (
                    "UGUI Interaction Blocker Root는 Blocker Component의 GameObject나 조상일 수 없습니다."
                );
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 점유 수와 Component 수명에서 현재 입력 차단 상태를 계산한다.
        /// <br/> 동기 Root 콜백에서 점유가 바뀌면 바깥 적용 흐름이 최신 상태를 반영한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplyState()
        {
            ValidateConfiguration();
            if (isApplyingState) return;

            isApplyingState = true;

            try
            {
                while (true)
                {
                    var isActive = isActiveAndEnabled && acquisitionCount > 0;

                    if (root != null && root.activeSelf != isActive)
                    {
                        root.SetActive(isActive);
                    }

                    if (canvasGroup != null)
                    {
                        canvasGroup.interactable = isActive;
                        canvasGroup.blocksRaycasts = isActive;
                    }

                    if (isActive == (isActiveAndEnabled && acquisitionCount > 0)) return;
                }
            }
            finally
            {
                isApplyingState = false;
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
            ApplyState();
        }

    #endregion

    }
}
