/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUISpotlightParams.cs
수정일 : 2026-09-17
# 설명
여러 UGUI Spotlight 대상과 바깥 입력 차단 여부를 불변 호출 인자로 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI Spotlight 표시 호출 인자.
    /// </summary>
    // ============================================================
    public sealed class UGUISpotlightParams
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 동시에 표시할 실제 Focus 대상 목록.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<UGUISpotlightTarget> Targets => targets;

        private readonly UGUISpotlightTarget[] targets = null;

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
        public UGUISpotlightParams
        (
            IReadOnlyList<UGUISpotlightTarget> targets,
            bool blocksOutsideInput = true
        ) : base()
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (targets.Count == 0)
            {
                throw new ArgumentException("UGUI Spotlight 대상이 비어 있습니다.", nameof(targets));
            }

            this.targets = new UGUISpotlightTarget[targets.Count];

            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i].Target == null)
                {
                    throw new ArgumentException
                    (
                        $"UGUI Spotlight 대상 {i}의 RectTransform이 null입니다.",
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
