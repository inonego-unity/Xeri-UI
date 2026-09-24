/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowMaximizedDragCalculator.cs
수정일 : 2026-09-21

# 설명
XeriWindowMaximizedDragCalculator 테스트.

# 테스트 구성
 C: Maximized drag 계산
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// XeriWindowMaximizedDragCalculator 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowMaximizedDragCalculator
    {

    #region C-1: Maximized Drag 계산

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Maximized titlebar drag는 pointer의 가로 비율을 normal window 위치에 반영한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowMaximizedDragCalculator_PointerRatio_Pos_계산()
        {
            var pos = XeriWindowMaximizedDragCalculator.CalculateRestoredPos
            (
                new Vector2(960f, 12f),
                new Vector2(400f, 240f),
                new Vector2(1920f, 1080f),
                24f
            );

            Assert.AreEqual(new Vector2(760f, 0f), pos);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Titlebar보다 낮은 pointer는 titlebar 높이를 기준으로 normal window 위치를 계산한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowMaximizedDragCalculator_TitleBarHeight_Pos_계산()
        {
            var pos = XeriWindowMaximizedDragCalculator.CalculateRestoredPos
            (
                new Vector2(1800f, 40f),
                new Vector2(400f, 240f),
                new Vector2(1920f, 1080f),
                24f
            );

            Assert.AreEqual(new Vector2(1425f, 16f), pos);
        }

    #endregion

    }
}
