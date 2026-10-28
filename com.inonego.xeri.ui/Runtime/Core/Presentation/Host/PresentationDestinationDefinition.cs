/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationDestinationDefinition.cs
수정일 : 2026-10-28

# 설명
Runtime PresentationHost destination과 Root PresentationSession Layer의 명시적 매핑을 직렬화한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Host Destination과 Root Layer의 직렬화된 매핑이다.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class PresentationDestinationDefinition
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Host에서 사용할 Destination 식별자이다.
        /// </summary>
        // ------------------------------------------------------------
        public string DestinationID => destinationID;

        [SerializeField]
        private string destinationID = "";

        // ------------------------------------------------------------
        /// <summary>
        /// Destination이 가리키는 Root Layer 식별자이다.
        /// </summary>
        // ------------------------------------------------------------
        public string LayerID => layerID;

        [SerializeField]
        private string layerID = "";

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Destination과 Layer 식별자로 매핑을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationDestinationDefinition(string destinationID, string layerID)
        {
            this.destinationID = destinationID ?? string.Empty;
            this.layerID = layerID ?? string.Empty;
        }

    #endregion

    }
}
