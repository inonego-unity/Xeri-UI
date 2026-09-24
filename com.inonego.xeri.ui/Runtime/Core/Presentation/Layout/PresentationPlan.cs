/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationPlan.cs
수정일 : 2026-09-26

# 설명
PresentationLayout을 resolve한 immutable Layer와 placement snapshot을 보관한다.
Plan은 runtime materialization 방식이나 Unity Object 수명을 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace inonego.Xeri.UI
{
    internal sealed class PresentationPlanLayer
    {
        internal string LayerID { get; }
        internal string DisplayName { get; }
        internal int LayerOrder { get; }
        internal PresentationBackend Backend { get; }

        internal PresentationPlanLayer
        (
            string layerID,
            string displayName,
            int layerOrder,
            PresentationBackend backend
        )
        {
            LayerID = layerID;
            DisplayName = displayName;
            LayerOrder = layerOrder;
            Backend = backend;
        }
    }

    internal sealed class PresentationPlanPlacement
    {
        internal string PresentationID { get; }
        internal PresentationPlanLayer Layer { get; }
        internal int LocalOrder { get; }

        internal PresentationPlanPlacement
        (
            string presentationID,
            PresentationPlanLayer layer,
            int localOrder
        )
        {
            PresentationID = presentationID;
            Layer = layer;
            LocalOrder = localOrder;
        }
    }
    internal sealed class PresentationPlan
    {
        internal IReadOnlyList<PresentationPlanLayer> Layers { get; }
        internal IReadOnlyList<PresentationPlanPlacement> Placements { get; }

        private readonly Dictionary<string, PresentationPlanLayer> layersByID;
        private readonly Dictionary<string, PresentationPlanPlacement> placementsByID;

        internal PresentationPlan
        (
            IList<PresentationPlanLayer> layers,
            IList<PresentationPlanPlacement> placements
        )
        {
            Layers = new ReadOnlyCollection<PresentationPlanLayer>(layers);
            Placements = new ReadOnlyCollection<PresentationPlanPlacement>(placements);
            layersByID = new Dictionary<string, PresentationPlanLayer>(StringComparer.Ordinal);
            placementsByID =
                new Dictionary<string, PresentationPlanPlacement>(StringComparer.Ordinal);

            for (var index = 0; index < layers.Count; index++)
            {
                layersByID.Add(layers[index].LayerID, layers[index]);
            }
            for (var index = 0; index < placements.Count; index++)
            {
                placementsByID.Add(placements[index].PresentationID, placements[index]);
            }
        }

        internal bool TryGetLayer(string layerID, out PresentationPlanLayer layer)
        {
            if (string.IsNullOrWhiteSpace(layerID))
            {
                layer = null;
                return false;
            }

            return layersByID.TryGetValue(layerID, out layer);
        }

        internal bool TryGetPlacement
        (
            string presentationID,
            out PresentationPlanPlacement placement
        )
        {
            if (string.IsNullOrWhiteSpace(presentationID))
            {
                placement = null;
                return false;
            }

            return placementsByID.TryGetValue(presentationID, out placement);
        }
    }
}
