/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationHostMapResolver.cs
수정일 : 2026-11-08

# 설명
PresentationPlan과 Scene Layer/Placement Host의 매핑을 사전 검증하고 구성한다.
Output 생성이나 Host 소유권 획득을 수행하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Scene Host와 PresentationPlan의 연결 정합성을 검증한다.
    /// </summary>
    // ============================================================
    internal static class PresentationHostMapResolver
    {

    #region 호스트 매핑 검증

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> explicit Scene Layer Host를 Plan Layer와 대조한다.
        /// <br/> Unity mutation 전에 검증된 lookup map으로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        internal static Dictionary<string, PresentationLayerHost> CreateLayerHostMap
        (
            PresentationPlan plan,
            IReadOnlyList<PresentationLayerHost> layerHosts
        )
        {
            var hosts = new Dictionary<string, PresentationLayerHost>
            (
                StringComparer.Ordinal
            );
            var borrowedPanels = new HashSet<PanelSettings>();

            if (layerHosts == null) return hosts;

            for (var index = 0; index < layerHosts.Count; index++)
            {
                var host = layerHosts[index];

                if (host == null)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host {index} 참조가 비어 있습니다."
                    );
                }

                if
                (
                    string.IsNullOrWhiteSpace(host.LayerID) ||
                    !plan.TryGetLayer(host.LayerID, out var planLayer)
                )
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host '{host.name}'의 Layer ID " +
                        $"'{host.LayerID}'가 현재 Plan에 없습니다."
                    );
                }

                if (host.Backend != planLayer.Backend)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host '{host.LayerID}' Backend({host.Backend})가 " +
                        $"Plan Backend({planLayer.Backend})와 일치하지 않습니다."
                    );
                }

                if (!hosts.TryAdd(host.LayerID, host))
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host '{host.LayerID}'가 중복됐습니다."
                    );
                }

                if (host is UITKPresentationLayerHost)
                {
                    var output = host.GetComponent<UITKPresentationOutput>();
                    var settings = output != null ? output.PanelRenderer?.panelSettings : null;
                    if (settings != null && !borrowedPanels.Add(settings))
                    {
                        throw new InvalidOperationException
                        (
                            $"독립 UITK Layer Host '{host.LayerID}'가 다른 Output의 PanelSettings를 공유합니다."
                        );
                    }
                }
            }

            return hosts;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Layer Host가 제공한 Placement Host를 Plan Placement와 대조한다.
        /// <br/> 검증된 PresentationID lookup map으로 만든다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal static Dictionary<string, PresentationPlacementHost> CreatePlacementHostMap
        (
            PresentationPlan plan,
            IReadOnlyDictionary<string, PresentationLayerHost> layerHosts
        )
        {
            var hosts = new Dictionary<string, PresentationPlacementHost>
            (
                StringComparer.Ordinal
            );

            if (layerHosts == null) return hosts;

            foreach (var pair in layerHosts)
            {
                var layerHost = pair.Value;
                var placementHosts = layerHost.PlacementHosts;

                for (var index = 0; index < placementHosts.Count; index++)
                {
                    var host = placementHosts[index];

                    if (host == null)
                    {
                        throw new InvalidOperationException
                        (
                            $"Layer Host '{layerHost.LayerID}' Placement Host {index} 참조가 비어 있습니다."
                        );
                    }

                    if
                    (
                        string.IsNullOrWhiteSpace(host.PresentationID) ||
                        !plan.TryGetPlacement(host.PresentationID, out var placement)
                    )
                    {
                        throw new InvalidOperationException
                        (
                            $"Presentation Placement Host '{host.name}'의 Presentation ID " +
                            $"'{host.PresentationID}'가 현재 Plan에 없습니다."
                        );
                    }

                    if
                    (
                        !string.Equals
                        (
                            placement.Layer.LayerID,
                            layerHost.LayerID,
                            StringComparison.Ordinal
                        )
                    )
                    {
                        throw new InvalidOperationException
                        (
                            $"Presentation Placement Host '{host.PresentationID}'는 " +
                            $"Plan Layer '{placement.Layer.LayerID}'가 아니라 " +
                            $"Layer Host '{layerHost.LayerID}'에 연결되어 있습니다."
                        );
                    }

                    if (host.Backend != placement.Layer.Backend)
                    {
                        throw new InvalidOperationException
                        (
                            $"Presentation Placement Host '{host.PresentationID}' " +
                            $"Backend({host.Backend})가 Plan Backend({placement.Layer.Backend})와 " +
                            "일치하지 않습니다."
                        );
                    }

                    if (!hosts.TryAdd(host.PresentationID, host))
                    {
                        throw new InvalidOperationException
                        (
                            $"Presentation Placement Host '{host.PresentationID}'가 중복됐습니다."
                        );
                    }
                }
            }

            return hosts;
        }

    #endregion

    }
}
