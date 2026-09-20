/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKModalInteractionDriver.cs
수정일 : 2026-09-17
# 설명
Modality Policy의 Stack 상단 여부를 UI Toolkit Root 상호작용 상태에 적용한다.
Dim 같은 시각 표현은 별도 Presentation으로 구성하고 이 Driver가 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Modal 상호작용 backend.
    /// </summary>
    // ============================================================
    public sealed class UITKModalInteractionDriver : IModalInteractionDriver
    {

    #region 필드

        private readonly VisualElement root = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Modal Root를 상호작용 backend에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKModalInteractionDriver(VisualElement root) : base()
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
        }

    #endregion

    #region IModalInteractionDriver

        // ------------------------------------------------------------
        /// <summary>
        /// Stack 상단 Modal만 enabled와 picking을 받도록 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetTop(bool isTop)
        {
            root.SetEnabled(isTop);
            root.pickingMode = isTop ? PickingMode.Position : PickingMode.Ignore;
        }

    #endregion

    }
}
