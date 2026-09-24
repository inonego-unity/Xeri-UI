/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayout.cs
수정일 : 2026-10-07

# 설명
Presentation scope의 Layer 정의와 Presentation placement를 직렬화한다.
LayerOrder와 LocalOrder의 유일한 authoring source를 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.UI
{
    [Serializable]
    public sealed class PresentationLayerDefinition
    {
        public string LayerID => layerID;
        [SerializeField] private string layerID = "";

        public string DisplayName => displayName;
        [SerializeField] private string displayName = "";

        public int LayerOrder => layerOrder;
        [SerializeField] private int layerOrder = 0;

        public PresentationBackend Backend => backend;
        [SerializeField] private PresentationBackend backend = PresentationBackend.UITK;

        public PresentationLayerDefinition
        (
            string layerID,
            string displayName,
            int layerOrder,
            PresentationBackend backend
        )
        {
            this.layerID = layerID;
            this.displayName = displayName;
            this.layerOrder = layerOrder;
            this.backend = backend;
        }
    }

    [Serializable]
    public sealed class PresentationPlacementDefinition
    {
        public string PresentationID => presentationID;
        [SerializeField] private string presentationID = "";

        public string LayerID => layerID;
        [SerializeField] private string layerID = "";

        public int LocalOrder => localOrder;
        [SerializeField] private int localOrder = 0;

        public PresentationPlacementDefinition(string presentationID, string layerID, int localOrder)
        {
            this.presentationID = presentationID;
            this.layerID = layerID;
            this.localOrder = localOrder;
        }
    }

    // ======================================================================
    /// <summary>
    /// <br/> Layer topology와 Presentation placement를 함께 정의하는
    /// <br/> authoring configuration.
    /// </summary>
    // ======================================================================
    [CreateAssetMenu(fileName = "Presentation Layout", menuName = "Xeri/UI/Presentation Layout")]
    public sealed class PresentationLayout : ScriptableObject
    {

    #region 필드

        public IReadOnlyList<PresentationLayerDefinition> Layers => layers;
        [SerializeField] private List<PresentationLayerDefinition> layers = new();

        public IReadOnlyList<PresentationPlacementDefinition> Placements => placements;
        [SerializeField] private List<PresentationPlacementDefinition> placements = new();

    #endregion

    }
}
