/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIWorldProjector.cs
수정일 : 2026-09-17
# 설명
World 위치를 UGUI RectTransform 로컬 위치로 변환한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// World 위치의 UGUI 로컬 Projection을 계산한다.
    /// </summary>
    // ============================================================
    public sealed class UGUIWorldProjector
    {

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// World 위치를 지정 Layer RectTransform의 로컬 위치로 투영한다.
        /// </summary>
        // ------------------------------------------------------------
        public ProjectionResult Project
        (
            Vector3 worldPosition,
            Camera camera,
            RectTransform layerRoot
        )
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (layerRoot == null)
            {
                throw new ArgumentNullException(nameof(layerRoot));
            }

            var screenPosition = camera.WorldToScreenPoint(worldPosition);

            var canvas = layerRoot.GetComponentInParent<Canvas>();
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            var succeeded = RectTransformUtility.ScreenPointToLocalPointInRectangle
            (
                layerRoot,
                screenPosition,
                eventCamera,
                out var localPosition
            );

            return new ProjectionResult
            (
                succeeded,
                localPosition,
                screenPosition.z < 0.0f
            );
        }

    #endregion

    }
}
