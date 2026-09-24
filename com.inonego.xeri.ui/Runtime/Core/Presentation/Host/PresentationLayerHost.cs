/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayerHost.cs
수정일 : 2026-09-29

# 설명
Scene-authored Presentation Layer의 explicit materialization identity와 borrow lifetime 공통 계약.
Backend 구현은 Native Output, LayerRoot와 ManagedRoot를 검증해 Runtime Layer Driver를 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// Scene-authored Presentation Layer materialization의 공통 Host 계약.
    /// </summary>
    // ======================================================================
    public abstract class PresentationLayerHost : MonoBehaviour
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Host가 materialize할 stable Layer ID.
        /// </summary>
        // ------------------------------------------------------------
        public string LayerID => layerID;

        [SerializeField]
        private string layerID = "";

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이 Layer Host가 명시적으로 제공하는 authored Placement Host 목록.
        /// </summary>
        // ----------------------------------------------------------------------
        public IReadOnlyList<PresentationPlacementHost> PlacementHosts =>
            placementHosts ?? Array.Empty<PresentationPlacementHost>();

        [SerializeField]
        private PresentationPlacementHost[] placementHosts =
            Array.Empty<PresentationPlacementHost>();

        internal abstract PresentationBackend Backend { get; }

        private bool isAcquired = false;

    #endregion

    #region 메서드

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Plan Layer와 identity/backend를 검증하고 concrete Scene Host를 borrow한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal IPresentationLayerDriver Acquire
        (
            PresentationPlanLayer plan,
            out Action release
        )
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (isAcquired)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer Host '{name}'가 이미 사용 중입니다."
                );
            }

            if
            (
                string.IsNullOrWhiteSpace(layerID) ||
                !string.Equals(layerID, plan.LayerID, StringComparison.Ordinal)
            )
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer Host '{name}'의 Layer ID '{layerID}'가 " +
                    $"Plan Layer '{plan.LayerID}'와 일치하지 않습니다."
                );
            }

            if (Backend != plan.Backend)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer Host '{layerID}' Backend({Backend})가 " +
                    $"Plan Backend({plan.Backend})와 일치하지 않습니다."
                );
            }

            var driver = AcquireCore(plan, out var releaseCore);

            if (driver == null)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer Host '{layerID}'가 Runtime Layer Driver를 제공하지 않았습니다."
                );
            }

            isAcquired = true;
            var isReleased = false;
            release = () =>
            {
                if (isReleased) return;

                isReleased = true;

                try
                {
                    releaseCore?.Invoke();
                }
                finally
                {
                    isAcquired = false;
                }
            };
            return driver;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Backend별 Scene Layer materialization을 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        internal abstract IPresentationLayerDriver AcquireCore
        (
            PresentationPlanLayer plan,
            out Action release
        );

    #endregion

    }
}
