/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSpotlightParams.cs
수정일 : 2026-09-17
# 설명
여러 UI Toolkit Spotlight 대상과 바깥 입력 차단 여부를 불변 호출 인자로 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Spotlight 표시 호출 인자.
    /// </summary>
    // ============================================================
    public sealed class UITKSpotlightParams
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 동시에 표시할 실제 Spotlight 대상 목록.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<UITKSpotlightTarget> Targets => targets;

        private readonly UITKSpotlightTarget[] targets = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Spotlight 구멍 바깥의 Pointer 입력을 차단할지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool BlocksOutsideInput { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Spotlight 대상 목록을 복사해 변경 불가능한 호출 인자를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKSpotlightParams
        (
            IReadOnlyList<UITKSpotlightTarget> targets,
            bool blocksOutsideInput = true
        ) : base()
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (targets.Count == 0)
            {
                throw new ArgumentException("UITK Spotlight 대상이 비어 있습니다.", nameof(targets));
            }

            this.targets = new UITKSpotlightTarget[targets.Count];

            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i].Target == null)
                {
                    throw new ArgumentException
                    (
                        $"UITK Spotlight 대상 {i}의 VisualElement가 null입니다.",
                        nameof(targets)
                    );
                }

                this.targets[i] = targets[i];
            }

            BlocksOutsideInput = blocksOutsideInput;
        }

    #endregion

    }
}
