/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayoutResolver.cs
수정일 : 2026-09-26

# 설명
PresentationLayout의 논리 topology와 placement를 순수 검증·정렬해 immutable PresentationPlan으로 resolve한다.
Runtime output 종류나 parent materialization 방식은 Plan이 아니라 materializer가 검증한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    internal static class PresentationLayoutResolver
    {
        internal static PresentationPlan Resolve(PresentationLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var sourceLayers = layout.Layers ??
                throw new InvalidOperationException("Presentation Layout Layer 목록이 null입니다.");
            var sourcePlacements = layout.Placements ??
                throw new InvalidOperationException("Presentation Layout Placement 목록이 null입니다.");
            if (sourceLayers.Count == 0)
            {
                throw new InvalidOperationException("Presentation Layout에는 Layer가 하나 이상 필요합니다.");
            }

            var layers = ResolveLayers(sourceLayers);
            var placements = ResolvePlacements(sourcePlacements, layers);
            return new PresentationPlan(layers, placements);
        }

        private static List<PresentationPlanLayer> ResolveLayers
        (
            IReadOnlyList<PresentationLayerDefinition> source
        )
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            var layers = new List<PresentationPlanLayer>(source.Count);

            for (var index = 0; index < source.Count; index++)
            {
                var definition = source[index] ??
                    throw new InvalidOperationException($"Presentation Layer {index}가 null입니다.");

                ValidateLayer(definition, ids, orders);
                layers.Add
                (
                    new PresentationPlanLayer
                    (
                        definition.LayerID,
                        definition.DisplayName,
                        definition.LayerOrder,
                        definition.Backend
                    )
                );
            }

            layers.Sort((left, right) => left.LayerOrder.CompareTo(right.LayerOrder));
            return layers;
        }

        private static void ValidateLayer
        (
            PresentationLayerDefinition definition,
            ISet<string> ids,
            ISet<int> orders
        )
        {
            if (string.IsNullOrWhiteSpace(definition.LayerID))
            {
                throw new InvalidOperationException("Presentation Layer ID가 비어 있습니다.");
            }

            if (!ids.Add(definition.LayerID))
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer ID '{definition.LayerID}'가 중복됩니다."
                );
            }

            if (!orders.Add(definition.LayerOrder))
            {
                throw new InvalidOperationException
                (
                    $"Presentation LayerOrder({definition.LayerOrder})가 중복됩니다."
                );
            }

            if (definition.LayerOrder < short.MinValue || definition.LayerOrder > short.MaxValue)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{definition.LayerID}' LayerOrder({definition.LayerOrder})가 native ScreenOverlay 정렬 범위({short.MinValue}~{short.MaxValue})를 벗어났습니다."
                );
            }

            if (!Enum.IsDefined(typeof(PresentationBackend), definition.Backend))
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{definition.LayerID}'의 Backend가 유효하지 않습니다."
                );
            }
        }

        private static List<PresentationPlanPlacement> ResolvePlacements
        (
            IReadOnlyList<PresentationPlacementDefinition> source,
            IReadOnlyList<PresentationPlanLayer> layers
        )
        {
            var layerMap = new Dictionary<string, PresentationPlanLayer>(StringComparer.Ordinal);
            var presentationIDs = new HashSet<string>(StringComparer.Ordinal);
            var localOrders = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            var placements = new List<PresentationPlanPlacement>(source.Count);

            for (var index = 0; index < layers.Count; index++)
            {
                layerMap.Add(layers[index].LayerID, layers[index]);
                localOrders.Add(layers[index].LayerID, new HashSet<int>());
            }

            for (var index = 0; index < source.Count; index++)
            {
                var definition = source[index] ??
                    throw new InvalidOperationException($"Presentation Placement {index}가 null입니다.");

                if (string.IsNullOrWhiteSpace(definition.PresentationID))
                {
                    throw new InvalidOperationException("Presentation ID가 비어 있습니다.");
                }

                if (!presentationIDs.Add(definition.PresentationID))
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation ID '{definition.PresentationID}'가 중복됩니다."
                    );
                }
                if (!layerMap.TryGetValue(definition.LayerID, out var layer))
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation '{definition.PresentationID}'의 Layer '{definition.LayerID}'가 정의되지 않았습니다."
                    );
                }

                if (!localOrders[definition.LayerID].Add(definition.LocalOrder))
                {
                    throw new InvalidOperationException
                    (
                        $"Layer '{definition.LayerID}'의 LocalOrder({definition.LocalOrder})가 중복됩니다."
                    );
                }

                placements.Add
                (
                    new PresentationPlanPlacement
                    (
                        definition.PresentationID,
                        layer,
                        definition.LocalOrder
                    )
                );
            }

            placements.Sort(ComparePlacement);
            return placements;
        }

        private static int ComparePlacement
        (
            PresentationPlanPlacement left,
            PresentationPlanPlacement right
        )
        {
            var layerComparison = left.Layer.LayerOrder.CompareTo(right.Layer.LayerOrder);
            return layerComparison != 0
                ? layerComparison
                : left.LocalOrder.CompareTo(right.LocalOrder);
        }
    }
}
