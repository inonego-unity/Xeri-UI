/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationPlacementHost.cs
수정일 : 2026-09-29

# 설명
Scene-authored Presentation Placement의 optional physical mount root 공통 계약.
Topology와 ordering은 Plan이 소유하고 Host는 해당 Placement의 native root만 borrow한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ================================================================================
    /// <summary>
    /// 특정 Plan Placement의 Scene-authored mount root를 제공하는 공통 Host 계약.
    /// </summary>
    // ================================================================================
    public abstract class PresentationPlacementHost : MonoBehaviour
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Host가 제공할 stable Presentation ID.
        /// </summary>
        // ------------------------------------------------------------
        public string PresentationID => presentationID;

        [SerializeField]
        private string presentationID = "";

        internal abstract PresentationBackend Backend { get; }

        private bool isAcquired = false;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Plan Placement와 identity/backend를 검증한다.
        /// <br/> 검증된 concrete authored root를 borrow한다.
        /// </summary>
        // ------------------------------------------------------------
        internal IPresentationLayerDriver Acquire
        (
            PresentationPlanPlacement plan,
            IPresentationLayerDriver layerDriver,
            out Action release
        )
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (layerDriver == null)
            {
                throw new ArgumentNullException(nameof(layerDriver));
            }

            if (isAcquired)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Placement Host '{name}'가 이미 사용 중입니다."
                );
            }

            if
            (
                string.IsNullOrWhiteSpace(presentationID) ||
                !string.Equals
                (
                    presentationID,
                    plan.PresentationID,
                    StringComparison.Ordinal
                )
            )
            {
                throw new InvalidOperationException
                (
                    $"Presentation Placement Host '{name}'의 Presentation ID " +
                    $"'{presentationID}'가 Plan Placement '{plan.PresentationID}'와 " +
                    "일치하지 않습니다."
                );
            }

            if (Backend != plan.Layer.Backend)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Placement Host '{presentationID}' Backend({Backend})가 " +
                    $"Plan Backend({plan.Layer.Backend})와 일치하지 않습니다."
                );
            }

            var driver = AcquireCore(plan, layerDriver, out var releaseCore);

            if (driver == null)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Placement Host '{presentationID}'가 " +
                    "Placement Driver를 제공하지 않았습니다."
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
        /// Backend별 authored Placement Root를 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        internal abstract IPresentationLayerDriver AcquireCore
        (
            PresentationPlanPlacement plan,
            IPresentationLayerDriver layerDriver,
            out Action release
        );

    #endregion

    }
}
