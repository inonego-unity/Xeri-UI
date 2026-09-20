/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKInteractionBlocker.cs
수정일 : 2026-09-17
# 설명
중첩 점유 수에 따라 전용 UI Toolkit Blocker Element의 표시와 Picking 상태를 적용한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Interaction Blocker 점유 수명을 소유한다.
    /// </summary>
    // ============================================================
    public sealed class UITKInteractionBlocker : IInteractionBlocker
    {

    #region 필드

        private readonly VisualElement blocker = null;
        private int acquisitionCount = 0;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 입력을 차단할 전용 VisualElement를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKInteractionBlocker(VisualElement blocker) : base()
        {
            this.blocker = blocker ?? throw new ArgumentNullException(nameof(blocker));
            ApplyState();
        }

    #endregion

    #region IInteractionBlocker

        // ------------------------------------------------------------
        /// <summary>
        /// Interaction Blocker를 한 번 점유하고 Lease를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public Lease Acquire()
        {
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
        /// 현재 점유 수를 UI Toolkit 표시와 Picking 상태에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyState()
        {
            var isActive = acquisitionCount > 0;
            blocker.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
            blocker.pickingMode = isActive ? PickingMode.Position : PickingMode.Ignore;
        }

    #endregion

    }
}
