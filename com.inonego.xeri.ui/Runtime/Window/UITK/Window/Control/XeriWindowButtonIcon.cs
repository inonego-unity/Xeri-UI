/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowButtonIcon.cs
수정일 : 2026-09-20

# 설명
XeriWindow control button에 표시되는 벡터 아이콘.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// XeriWindow control button icon type.
    /// </summary>
    // ============================================================
    public enum XeriWindowButtonIconType
    {
        Minimize,
        Maximize,
        Close,
    }

    // ============================================================
    /// <summary>
    /// Font 렌더링에 의존하지 않는 window control button icon.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowButtonIcon : VisualElement
    {

    #region 필드

        private const float DEFAULT_ICON_SIZE = 9f;
        private const float DEFAULT_STROKE_WIDTH = 1.25f;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 icon type.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowButtonIconType IconType => iconType;

        private readonly XeriWindowButtonIconType iconType;

        // ------------------------------------------------------------
        /// <summary>
        /// Icon stroke size.
        /// </summary>
        // ------------------------------------------------------------
        public float IconSize
        {
            get => iconSize;
            set
            {
                iconSize = Mathf.Max(1f, value);
                MarkDirtyRepaint();
            }
        }

        private float iconSize = DEFAULT_ICON_SIZE;

        // ------------------------------------------------------------
        /// <summary>
        /// Icon stroke width.
        /// </summary>
        // ------------------------------------------------------------
        public float StrokeWidth
        {
            get => strokeWidth;
            set
            {
                strokeWidth = Mathf.Max(0.5f, value);
                MarkDirtyRepaint();
            }
        }

        private float strokeWidth = DEFAULT_STROKE_WIDTH;

    #endregion

    #region 생성자

        public XeriWindowButtonIcon(XeriWindowButtonIconType iconType) : base()
        {
            this.iconType = iconType;

            name = GetIconName(iconType);
            pickingMode = PickingMode.Ignore;
            AddToClassList("xeri-window__button-icon");

            generateVisualContent += OnGenerateVisualContent;
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Icon type에 맞는 선형 symbol을 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var rect = contentRect;

            painter.lineWidth = strokeWidth;
            painter.strokeColor = resolvedStyle.color;

            switch (iconType)
            {
                case XeriWindowButtonIconType.Minimize: DrawMinimize(painter, rect); break;
                case XeriWindowButtonIconType.Maximize: DrawMaximize(painter, rect); break;
                case XeriWindowButtonIconType.Close:    DrawClose(painter, rect);    break;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Minimize symbol을 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private void DrawMinimize(Painter2D painter, Rect rect)
        {
            var center = rect.center;
            var y = center.y + 2f;
            var halfWidth = iconSize * 0.5f;

            DrawLine
            (
                painter,
                new Vector2(center.x - halfWidth, y),
                new Vector2(center.x + halfWidth, y)
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Maximize symbol을 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private void DrawMaximize(Painter2D painter, Rect rect)
        {
            var center = rect.center;
            var halfSize = iconSize * 0.5f;
            var left = center.x - halfSize;
            var right = center.x + halfSize;
            var top = center.y - halfSize;
            var bottom = center.y + halfSize;

            painter.BeginPath();
            painter.MoveTo(new Vector2(left, top));
            painter.LineTo(new Vector2(right, top));
            painter.LineTo(new Vector2(right, bottom));
            painter.LineTo(new Vector2(left, bottom));
            painter.ClosePath();
            painter.Stroke();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Close symbol을 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private void DrawClose(Painter2D painter, Rect rect)
        {
            var center = rect.center;
            var halfSize = iconSize * 0.5f;
            var min = new Vector2(center.x - halfSize, center.y - halfSize);
            var max = new Vector2(center.x + halfSize, center.y + halfSize);
            var topRight = new Vector2(max.x, min.y);
            var bottomLeft = new Vector2(min.x, max.y);

            DrawLine(painter, min, max);
            DrawLine(painter, topRight, bottomLeft);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 하나의 stroke line을 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DrawLine(Painter2D painter, Vector2 start, Vector2 end)
        {
            painter.BeginPath();
            painter.MoveTo(start);
            painter.LineTo(end);
            painter.Stroke();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Icon element name을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetIconName(XeriWindowButtonIconType iconType)
        {
            return iconType switch
            {
                XeriWindowButtonIconType.Minimize => "minimize-button-icon",
                XeriWindowButtonIconType.Maximize => "maximize-button-icon",
                XeriWindowButtonIconType.Close    => "close-button-icon",
                _                                 => "window-button-icon",
            };
        }

    #endregion

    }
}
