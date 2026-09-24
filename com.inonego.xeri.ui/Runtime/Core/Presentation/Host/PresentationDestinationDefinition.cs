/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationDestinationDefinition.cs
수정일 : 2026-10-07

# 설명
Runtime PresentationHost destination과 Root PresentationSession Layer의 명시적 매핑을 직렬화한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    [Serializable]
    public sealed class PresentationDestinationDefinition
    {

    #region 필드

        public string DestinationID => destinationID;

        [SerializeField]
        private string destinationID = "";

        public string LayerID => layerID;

        [SerializeField]
        private string layerID = "";

    #endregion

    #region 생성자

        public PresentationDestinationDefinition(string destinationID, string layerID)
        {
            this.destinationID = destinationID ?? string.Empty;
            this.layerID = layerID ?? string.Empty;
        }

    #endregion

    }
}
