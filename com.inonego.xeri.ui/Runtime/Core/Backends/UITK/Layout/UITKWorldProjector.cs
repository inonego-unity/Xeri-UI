/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKWorldProjector.cs
수정일 : 2026-09-17
# 설명
World 위치를 UI Toolkit VisualElement 로컬 위치로 변환한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// World 위치의 UI Toolkit 로컬 Projection을 계산한다.
    /// </summary>
    // ============================================================
    public sealed class UITKWorldProjector
    {

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// World 위치를 지정 VisualElement Root의 로컬 위치로 투영한다.
        /// </summary>
        // ------------------------------------------------------------
        public ProjectionResult Project
        (
            Vector3 worldPosition,
            Camera camera,
            VisualElement layerRoot
        )
        {
            if (layerRoot == null)
            {
                throw new ArgumentNullException(nameof(layerRoot));
            }

            if (layerRoot.panel == null)
            {
                return new ProjectionResult(false, default, false);
            }

            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            var screenPosition = camera.WorldToScreenPoint(worldPosition);

            var panelPosition = RuntimePanelUtils.ScreenToPanel
            (
                layerRoot.panel,
                new Vector2
                (
                    screenPosition.x,
                    Screen.height - screenPosition.y
                )
            );
            var localPosition = layerRoot.WorldToLocal(panelPosition);

            return new ProjectionResult
            (
                true,
                localPosition,
                screenPosition.z < 0.0f
            );
        }

    #endregion

    }
}
