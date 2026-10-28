/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayout.cs
수정일 : 2026-10-28

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
    // ============================================================
    /// <summary>
    /// Presentation Layer의 식별자, 순서와 Backend 정의이다.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class PresentationLayerDefinition
    {
        // ------------------------------------------------------------
        /// <summary>
        /// Layer의 고유 식별자이다.
        /// </summary>
        // ------------------------------------------------------------
        public string LayerID => layerID;
        [SerializeField] private string layerID = "";

        // ------------------------------------------------------------
        /// <summary>
        /// 에디터에서 표시하는 Layer 이름이다.
        /// </summary>
        // ------------------------------------------------------------
        public string DisplayName => displayName;
        [SerializeField] private string displayName = "";

        // ------------------------------------------------------------
        /// <summary>
        /// Layer 사이의 표시 순서이다.
        /// </summary>
        // ------------------------------------------------------------
        public int LayerOrder => layerOrder;
        [SerializeField] private int layerOrder = 0;

        // ------------------------------------------------------------
        /// <summary>
        /// Layer를 구현할 UI Backend이다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationBackend Backend => backend;
        [SerializeField] private PresentationBackend backend = PresentationBackend.UITK;

        // ------------------------------------------------------------
        /// <summary>
        /// Layer 구성 값으로 정의를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
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

    // ============================================================
    /// <summary>
    /// Presentation의 Layer와 Layer 내 순서 정의이다.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class PresentationPlacementDefinition
    {
        // ------------------------------------------------------------
        /// <summary>
        /// Placement의 Presentation 식별자이다.
        /// </summary>
        // ------------------------------------------------------------
        public string PresentationID => presentationID;
        [SerializeField] private string presentationID = "";

        // ------------------------------------------------------------
        /// <summary>
        /// Placement가 속하는 Layer 식별자이다.
        /// </summary>
        // ------------------------------------------------------------
        public string LayerID => layerID;
        [SerializeField] private string layerID = "";

        // ------------------------------------------------------------
        /// <summary>
        /// Layer 안에서의 Placement 순서이다.
        /// </summary>
        // ------------------------------------------------------------
        public int LocalOrder => localOrder;
        [SerializeField] private int localOrder = 0;

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation과 Layer 및 로컬 순서로 정의를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
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

        // ------------------------------------------------------------
        /// <summary>
        /// Layout에 정의된 Layer 목록이다.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<PresentationLayerDefinition> Layers => layers;
        [SerializeField] private List<PresentationLayerDefinition> layers = new();

        // ------------------------------------------------------------
        /// <summary>
        /// Layout에 정의된 Placement 목록이다.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<PresentationPlacementDefinition> Placements => placements;
        [SerializeField] private List<PresentationPlacementDefinition> placements = new();

    #endregion

    }
}
