/* BLOCK_HEADER_BEGIN =======================================================================
파일명: TEST_XeriTrayReorder.cs
수정일 : 2026-09-20

# 설명
공통 Tray reorder 계산과 panel 이벤트 전달을 검증한다.

# 테스트 구성
 R: Reorder 계산
 P: Panel reorder 이벤트
 V: Reorder visual
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

using NUnit;
using NUnit.Framework;

using inonego.Xeri.UI.Tray;

namespace inonego.Xeri.UI.TEST.Tray
{
    // ============================================================
    /// <summary>
    /// 공통 Tray reorder 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriTrayReorder
    {

    #region R-1: Target Index

        // ------------------------------------------------------------
        /// <summary>
        /// Horizontal reorder는 pointer가 지난 insertion boundary로 target index를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderCalculator_Horizontal_TargetIndex_계산()
        {
            var calculator = new XeriTrayReorderCalculator();
            var bounds = CreateHorizontalBounds();

            var firstTarget = calculator.CalculateTargetIndex
            (
                bounds,
                0,
                new Vector2(46f, 20f),
                XeriTrayReorderAxis.Horizontal
            );
            var lastTarget = calculator.CalculateTargetIndex
            (
                bounds,
                0,
                new Vector2(140f, 20f),
                XeriTrayReorderAxis.Horizontal
            );

            Assert.AreEqual(1, firstTarget);
            Assert.AreEqual(2, lastTarget);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Horizontal reorder는 pointer가 boundary 앞에 있으면 기존 index를 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderCalculator_Horizontal_Boundary_전_유지()
        {
            var calculator = new XeriTrayReorderCalculator();
            var target = calculator.CalculateTargetIndex
            (
                CreateHorizontalBounds(),
                0,
                new Vector2(44f, 20f),
                XeriTrayReorderAxis.Horizontal
            );

            Assert.AreEqual(0, target);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Horizontal reorder는 이전 boundary를 지나면 앞쪽 target index를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderCalculator_Horizontal_Backward_TargetIndex_계산()
        {
            var calculator = new XeriTrayReorderCalculator();
            var firstTarget = calculator.CalculateTargetIndex
            (
                CreateHorizontalBounds(),
                2,
                new Vector2(94f, 20f),
                XeriTrayReorderAxis.Horizontal
            );
            var lastTarget = calculator.CalculateTargetIndex
            (
                CreateHorizontalBounds(),
                2,
                new Vector2(10f, 20f),
                XeriTrayReorderAxis.Horizontal
            );

            Assert.AreEqual(1, firstTarget);
            Assert.AreEqual(0, lastTarget);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Vertical reorder는 pointer가 지난 vertical insertion boundary로 target index를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderCalculator_Vertical_TargetIndex_계산()
        {
            var calculator = new XeriTrayReorderCalculator();
            var target = calculator.CalculateTargetIndex
            (
                CreateVerticalBounds(),
                0,
                new Vector2(20f, 96f),
                XeriTrayReorderAxis.Vertical
            );

            Assert.AreEqual(2, target);
        }

    #endregion

    #region P-1: Panel Event

        // ------------------------------------------------------------
        /// <summary>
        /// Panel은 reorder 요청을 OnEntryReorder 이벤트로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_InvokeEntryReorder_OnEntryReorder_발생()
        {
            var panel = new XeriTrayPanel();
            var entries = new[]
            {
                new XeriTrayEntry("a", "A"),
                new XeriTrayEntry("b", "B"),
            };
            var options = XeriTrayOptions.Default();
            options.Reorderable = true;

            XeriTrayReorderEventArgs eventArgs = null;
            panel.OnEntryReorder += (_, e) => eventArgs = e;
            panel.Reload(entries, options);

            panel.InvokeEntryReorder(new XeriTrayReorderRequest(entries[0], 0, 1));

            Assert.IsNotNull(eventArgs);
            Assert.AreSame(entries[0], eventArgs.Entry);
            Assert.AreEqual(0, eventArgs.SourceIndex);
            Assert.AreEqual(1, eventArgs.TargetIndex);
        }

    #endregion

    #region V-1: Reorder Visual

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder visual은 drag 중 원본 button을 숨기고 Clear 시 원래 표시 상태로 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderVisual_Move_Clear_Button_Visible_복구()
        {
            var panel = new XeriTrayPanel();
            var entries = new[]
            {
                new XeriTrayEntry("a", "A"),
                new XeriTrayEntry("b", "B"),
            };
            var options = XeriTrayOptions.Default();
            options.Reorderable = true;
            panel.Reload(entries, options);

            var button = panel.GetEntryButtons()[0];
            var session = new XeriTrayReorderSession(button, 0, Vector2.zero);
            var visual = new XeriTrayReorderVisual();

            visual.Move(session, new Vector2(8f, 0f), panel);

            Assert.IsFalse(button.visible);

            visual.Clear(session);

            Assert.IsTrue(button.visible);
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Horizontal reorder 계산용 entry bounds를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Rect[] CreateHorizontalBounds()
        {
            return new[]
            {
                new Rect(0f, 0f, 40f, 40f),
                new Rect(50f, 0f, 40f, 40f),
                new Rect(100f, 0f, 40f, 40f),
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Vertical reorder 계산용 entry bounds를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Rect[] CreateVerticalBounds()
        {
            return new[]
            {
                new Rect(0f, 0f, 40f, 40f),
                new Rect(0f, 50f, 40f, 40f),
                new Rect(0f, 100f, 40f, 40f),
            };
        }

    #endregion

    }
}
