/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationHost.cs
수정일 : 2026-10-07

# 설명
app-wide semantic destination을 Root PresentationSession Layer에 resolve하고 Layer Lease를 발급한다.
Root Plan의 local topology를 변경하지 않으며 destination identity와 Layer identity를 분리한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Runtime app-wide Presentation destination resolver.
    /// </summary>
    // ============================================================
    public sealed class PresentationHost
    {

    #region 필드

        public bool IsReleased { get; private set; }

        private readonly PresentationSession rootSession = null;
        private readonly Dictionary<string, string> layerIDsByDestination =
            new(StringComparer.Ordinal);

    #endregion

    #region 생성자

        internal PresentationHost
        (
            PresentationSession rootSession,
            IReadOnlyList<PresentationDestinationDefinition> destinations
        )
        {
            this.rootSession = rootSession ?? throw new ArgumentNullException(nameof(rootSession));

            if (destinations == null)
            {
                throw new ArgumentNullException(nameof(destinations));
            }

            for (var index = 0; index < destinations.Count; index++)
            {
                var definition = destinations[index];

                if (definition == null)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Destination {index} 정의가 비어 있습니다."
                    );
                }

                if (string.IsNullOrWhiteSpace(definition.DestinationID))
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Destination {index} ID가 비어 있습니다."
                    );
                }

                if (string.IsNullOrWhiteSpace(definition.LayerID))
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Destination '{definition.DestinationID}' Layer ID가 비어 있습니다."
                    );
                }

                if (!layerIDsByDestination.TryAdd(definition.DestinationID, definition.LayerID))
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Destination '{definition.DestinationID}'가 중복됐습니다."
                    );
                }

                if (!rootSession.LayerRegistry.Contains(definition.LayerID))
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Destination '{definition.DestinationID}'이 참조하는 " +
                        $"Layer '{definition.LayerID}'가 Root Session에 없습니다."
                    );
                }
            }
        }

    #endregion

    #region 레이어 획득

        public PresentationLayerLease AcquireLayer(string destinationID)
        {
            ThrowIfReleased();

            if
            (
                string.IsNullOrWhiteSpace(destinationID) ||
                !layerIDsByDestination.TryGetValue(destinationID, out var layerID)
            )
            {
                throw new InvalidOperationException
                (
                    $"Presentation Destination '{destinationID}'이 등록되어 있지 않습니다."
                );
            }

            try
            {
                return rootSession.AcquireLayer(layerID);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Destination '{destinationID}'의 Layer '{layerID}'을 획득할 수 없습니다.",
                    exception
                );
            }
        }

        public bool Contains(string destinationID)
        {
            return
                !IsReleased &&
                !string.IsNullOrWhiteSpace(destinationID) &&
                layerIDsByDestination.ContainsKey(destinationID);
        }

        private void ThrowIfReleased()
        {
            if (IsReleased)
            {
                throw new ObjectDisposedException(nameof(PresentationHost));
            }
        }

    #endregion

    #region 수명 해제

        internal void Release()
        {
            IsReleased = true;
            layerIDsByDestination.Clear();
        }

    #endregion

    }
}
