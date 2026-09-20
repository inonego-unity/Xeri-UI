/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayerAsset.cs
수정일 : 2026-09-17
# 설명
stable string ID와 공통 Screen Overlay 정렬 순서로 UI Layer 구성을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Layer의 직렬화 구성.
    /// </summary>
    // ============================================================
    [CreateAssetMenu
    (
        fileName = "Presentation Layer",
        menuName = "Xeri/UI/Presentation Layer"
    )]
    public sealed class PresentationLayerAsset : ScriptableObject
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Layer를 식별하는 stable string ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ID => id;

        [SerializeField]
        private string id = "";

        // ------------------------------------------------------------
        /// <summary>
        /// UGUI와 UITK Screen Overlay가 함께 사용하는 Layer 정렬 순서.
        /// </summary>
        // ------------------------------------------------------------
        public int Order => order;

        [SerializeField]
        private int order = 0;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Layer 구성이 Runtime 등록에 유효한지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException("Presentation Layer ID가 비어 있습니다.");
            }

            // UGUI와 UITK가 같은 값을 유지할 수 있는 공통 정렬 범위만 허용한다.
            if (order < short.MinValue || order > short.MaxValue)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer Order({order})가 공통 허용 범위" +
                    $"({short.MinValue}~{short.MaxValue})를 벗어났습니다."
                );
            }
        }

    #endregion

    }
}
