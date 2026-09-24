/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowResizeCursorProvider.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 resize 방향에 맞는 cursor texture를 생성하고 적용한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Window resize cursor 적용자.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowResizeCursorProvider : IXeriWindowResizeCursorProvider
    {

    #region 필드

        private const int CURSOR_SIZE = 12;
        private const int CURSOR_CENTER = 6;

        private Texture2D activeCursor = null;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Resize 방향에 맞는 cursor를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Apply(XeriWindowResizeMode mode)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            ReleaseActiveCursor();

            if (mode == XeriWindowResizeMode.None)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }

            activeCursor = CreateCursorTexture(mode);
            Cursor.SetCursor
            (
                activeCursor,
                new Vector2(CURSOR_CENTER, CURSOR_CENTER),
                CursorMode.Auto
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity 기본 cursor로 복귀한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Reset()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            ReleaseActiveCursor();
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 생성한 임시 cursor texture의 native 수명을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReleaseActiveCursor()
        {
            var texture = activeCursor;
            activeCursor = null;

            if (texture == null) return;

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
                return;
            }

            UnityEngine.Object.DestroyImmediate(texture);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize cursor texture를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Texture2D CreateCursorTexture(XeriWindowResizeMode mode)
        {
            var texture = new Texture2D(CURSOR_SIZE, CURSOR_SIZE, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
            };

            ClearTexture(texture);
            DrawCursor(texture, mode, Color.black, 3);
            DrawCursor(texture, mode, Color.white, 1);
            texture.Apply();

            return texture;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Texture 전체를 투명하게 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ClearTexture(Texture2D texture)
        {
            for (var y = 0; y < CURSOR_SIZE; y++)
            {
                for (var x = 0; x < CURSOR_SIZE; x++)
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize 방향에 맞는 cursor 선과 화살표를 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DrawCursor(Texture2D texture, XeriWindowResizeMode mode, Color color, int thickness)
        {
            switch (mode)
            {
                case XeriWindowResizeMode.Left:
                case XeriWindowResizeMode.Right:
                    DrawHorizontalCursor(texture, color, thickness);
                    break;

                case XeriWindowResizeMode.Top:
                case XeriWindowResizeMode.Bottom:
                    DrawVerticalCursor(texture, color, thickness);
                    break;

                case XeriWindowResizeMode.TopLeft:
                case XeriWindowResizeMode.BottomRight:
                    DrawDiagonalDownCursor(texture, color, thickness);
                    break;

                case XeriWindowResizeMode.TopRight:
                case XeriWindowResizeMode.BottomLeft:
                    DrawDiagonalUpCursor(texture, color, thickness);
                    break;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 가로 resize cursor를 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DrawHorizontalCursor(Texture2D texture, Color color, int thickness)
        {
            DrawLine(texture, 2, 6, 9, 6, color, thickness);
            DrawLine(texture, 2, 6, 4, 4, color, thickness);
            DrawLine(texture, 2, 6, 4, 8, color, thickness);
            DrawLine(texture, 9, 6, 7, 4, color, thickness);
            DrawLine(texture, 9, 6, 7, 8, color, thickness);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 세로 resize cursor를 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DrawVerticalCursor(Texture2D texture, Color color, int thickness)
        {
            DrawLine(texture, 6, 2, 6, 9, color, thickness);
            DrawLine(texture, 6, 2, 4, 4, color, thickness);
            DrawLine(texture, 6, 2, 8, 4, color, thickness);
            DrawLine(texture, 6, 9, 4, 7, color, thickness);
            DrawLine(texture, 6, 9, 8, 7, color, thickness);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 좌상단-우하단 resize cursor를 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DrawDiagonalDownCursor(Texture2D texture, Color color, int thickness)
        {
            DrawLine(texture, 3, 9, 9, 3, color, thickness);
            DrawLine(texture, 3, 9, 3, 6, color, thickness);
            DrawLine(texture, 3, 9, 6, 9, color, thickness);
            DrawLine(texture, 9, 3, 6, 3, color, thickness);
            DrawLine(texture, 9, 3, 9, 6, color, thickness);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 우상단-좌하단 resize cursor를 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DrawDiagonalUpCursor(Texture2D texture, Color color, int thickness)
        {
            DrawLine(texture, 3, 3, 9, 9, color, thickness);
            DrawLine(texture, 3, 3, 3, 6, color, thickness);
            DrawLine(texture, 3, 3, 6, 3, color, thickness);
            DrawLine(texture, 9, 9, 6, 9, color, thickness);
            DrawLine(texture, 9, 9, 9, 6, color, thickness);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Texture에 두께가 있는 선을 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DrawLine
        (
            Texture2D texture,
            int x0,
            int y0,
            int x1,
            int y1,
            Color color,
            int thickness
        )
        {
            var dx = Mathf.Abs(x1 - x0);
            var dy = Mathf.Abs(y1 - y0);
            var sx = x0 < x1 ? 1 : -1;
            var sy = y0 < y1 ? 1 : -1;
            var err = dx - dy;

            while (true)
            {
                SetPixel(texture, x0, y0, color, thickness);

                if (x0 == x1 && y0 == y1) break;

                var e2 = err * 2;

                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 중심 pixel 주변을 포함해 색을 칠한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SetPixel
        (
            Texture2D texture,
            int x,
            int y,
            Color color,
            int thickness
        )
        {
            var radius = thickness / 2;

            for (var offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (var offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    var targetX = x + offsetX;
                    var targetY = y + offsetY;

                    if (targetX < 0 || targetX >= CURSOR_SIZE) continue;
                    if (targetY < 0 || targetY >= CURSOR_SIZE) continue;

                    texture.SetPixel(targetX, targetY, color);
                }
            }
        }

    #endregion

    }
}
