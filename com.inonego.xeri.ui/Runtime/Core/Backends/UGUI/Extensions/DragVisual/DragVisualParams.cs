/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DragVisualParams.cs
수정일 : 2026-09-17
# 설명
UGUI Drag Visual 대상과 사용할 Presentation Layer ID를 불변 호출 인자로 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI Drag Visual 시작과 Draggable 연결 호출 인자.
    /// </summary>
    // ============================================================
    public readonly struct DragVisualParams
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 중 Presentation Layer로 옮길 RectTransform.
        /// </summary>
        // ------------------------------------------------------------
        public RectTransform Target { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag Visual을 표시할 Presentation Layer ID.
        /// </summary>
        // ------------------------------------------------------------
        public string LayerID { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// UGUI Drag Visual 호출 인자를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public DragVisualParams
        (
            RectTransform target,
            string layerID
        ) : this()
        {
            Target = target != null
                ? target
                : throw new ArgumentNullException(nameof(target));

            if (string.IsNullOrWhiteSpace(layerID))
            {
                throw new ArgumentException
                (
                    "Drag Visual Layer ID가 비어 있습니다.",
                    nameof(layerID)
                );
            }

            LayerID = layerID;
        }

    #endregion

    }
}
