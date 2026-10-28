/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UICloseIcon.cs
수정일 : 2026-10-19

# 설명
창과 트레이가 공유하는 닫기 아이콘의 벡터 도형과 UITK 표시 요소.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 글꼴에 의존하지 않는 닫기 아이콘.
    /// </summary>
    // ============================================================
    internal sealed class UICloseIcon : VisualElement
    {

    #region 생성과 표시

        internal UICloseIcon() : base()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 버튼에서 상속한 색상으로 닫기 도형을 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.lineWidth = 1.25f;
            painter.strokeColor = resolvedStyle.color;
            Draw(painter, contentRect, 8f);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 영역 중앙에 두 대각선을 그린다.
        /// </summary>
        // ------------------------------------------------------------
        internal static void Draw(Painter2D painter, Rect rect, float size)
        {
            var center = rect.center;
            var halfSize = size * 0.5f;
            var min = new Vector2(center.x - halfSize, center.y - halfSize);
            var max = new Vector2(center.x + halfSize, center.y + halfSize);

            painter.BeginPath();
            painter.MoveTo(min);
            painter.LineTo(max);
            painter.MoveTo(new Vector2(max.x, min.y));
            painter.LineTo(new Vector2(min.x, max.y));
            painter.Stroke();
        }

    #endregion

    }
}
