/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIModalInteractionDriver.cs
수정일 : 2026-09-17
# 설명
Modality Policy의 Stack 상단 여부를 UGUI CanvasGroup 상호작용 상태에 적용한다.
Dim 같은 시각 표현은 별도 Presentation으로 구성하고 이 Driver가 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI Modal 상호작용 backend.
    /// </summary>
    // ============================================================
    [DisallowMultipleComponent]
    public sealed class UGUIModalInteractionDriver : MonoBehaviour, IModalInteractionDriver
    {

    #region 필드

        [SerializeField]
        private CanvasGroup canvasGroup = null;

    #endregion

    #region IModalInteractionDriver

        // ------------------------------------------------------------
        /// <summary>
        /// Stack 상단 Modal만 상호작용과 raycast를 받도록 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetTop(bool isTop)
        {
            if (canvasGroup == null)
            {
                throw new InvalidOperationException
                (
                    "UGUI Modal CanvasGroup이 연결되지 않았습니다."
                );
            }

            canvasGroup.interactable = isTop;
            canvasGroup.blocksRaycasts = isTop;
        }

    #endregion

    }
}
