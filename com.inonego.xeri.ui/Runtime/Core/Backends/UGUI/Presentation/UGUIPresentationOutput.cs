/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIPresentationOutput.cs
수정일 : 2026-09-26

# 설명
Top-level UGUI ScreenOverlay Layer 하나의 Canvas Native Output을 제공한다.
CanvasScaler, GraphicRaycaster와 기타 authoring 값은 template prefab이 소유하고 LayerOrder만 runtime에 반영한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UI;

namespace inonego.Xeri.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class UGUIPresentationOutput : MonoBehaviour
    {
        public Canvas Canvas => canvas != null ? canvas : GetComponent<Canvas>();
        public RectTransform Root => Canvas != null ? Canvas.transform as RectTransform : null;

        private Canvas canvas = null;

        internal void Initialize(int layerOrder)
        {
            canvas = GetComponent<Canvas>();

            if (!Validate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            canvas.sortingOrder = layerOrder;
        }

        public bool Validate(out string error)
        {
            if (Canvas == null || Root == null)
            {
                error = "UGUI Presentation Output Canvas Root가 없습니다.";
                return false;
            }

            if (Canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                error = "UGUI Presentation Output은 Screen Space - Overlay여야 합니다.";
                return false;
            }

            if (GetComponent<CanvasScaler>() == null)
            {
                error = "UGUI Presentation Output CanvasScaler가 없습니다.";
                return false;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                error = "UGUI Presentation Output GraphicRaycaster가 없습니다.";
                return false;
            }

            error = "";
            return true;
        }
    }
}
