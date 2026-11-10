/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_Bar.cs
수정일 : 2026-11-10

# 설명
Bar 공통 비율·증감 전이와 UGUI·UITK 값 API의 동작 계약을 검증한다.

# 테스트 구성
 A: 비율과 변화 구간
 B: 전이와 표시 API
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Primitive;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.TEST.Bar
{
    // ============================================================
    /// <summary>
    /// Bar의 EditMode 계산·표시 계약 테스트.
    /// </summary>
    // ============================================================
    public class TEST_Bar
    {

    #region A: Bar 비율과 변화 구간

        // ------------------------------------------------------------
        /// <summary>
        /// 정상 범위와 유효하지 않은 수치를 0~1 표시 비율로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_BarState_ResolveRatio_정상범위와비정상입력()
        {
            Assert.AreEqual(0.25f, BarState.ResolveRatio(0f, 100f, 25f));
            Assert.AreEqual(1f, BarState.ResolveRatio(0f, 100f, 200f));
            Assert.AreEqual(0f, BarState.ResolveRatio(0f, 0f, 0f));
            Assert.AreEqual(0f, BarState.ResolveRatio(0f, 100f, float.NaN));
            Assert.AreEqual(0f, BarState.ResolveRatio(0f, float.PositiveInfinity, 5f));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 증가·감소 방향에 따라 현재 Fill과 Change Fill 구간을 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_BarState_Resolve_증가감소구간()
        {
            var increase = BarState.Resolve(0.25f, 0.75f);

            Assert.AreEqual(BarChange.Increase, increase.Change);
            Assert.AreEqual(0.25f, increase.ForegroundEnd);
            Assert.AreEqual(0.25f, increase.ChangeBegin);
            Assert.AreEqual(0.75f, increase.ChangeEnd);

            var decrease = BarState.Resolve(0.75f, 0.25f);

            Assert.AreEqual(BarChange.Decrease, decrease.Change);
            Assert.AreEqual(0.25f, decrease.ForegroundEnd);
            Assert.AreEqual(0.25f, decrease.ChangeBegin);
            Assert.AreEqual(0.75f, decrease.ChangeEnd);
        }

    #endregion

    #region B: 전이와 표시 API

        // ------------------------------------------------------------
        /// <summary>
        /// 즉시 전이는 현재 비율과 목표 비율을 같은 값으로 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_BarTransition_Set_즉시확정()
        {
            var transition = new BarTransition();
            var updates = 0;
            float observedCurrent = -1f;
            float observedTarget = -1f;

            void Observe(float current, float target)
            {
                updates++;
                observedCurrent = current;
                observedTarget = target;
            }

            transition.Set(0.75f, new TweenCurve(), true, Observe);
            transition.Set(0.25f, new TweenCurve(), true, Observe);

            Assert.AreEqual(2, updates);
            Assert.AreEqual(0.25f, observedCurrent);
            Assert.AreEqual(0.25f, observedTarget);
            Assert.AreEqual(0.25f, transition.CurrentRatio);
            Assert.AreEqual(0.25f, transition.TargetRatio);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// UGUI와 UITK Bar는 같은 범위·값 계약에서 같은 표시 비율을 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_Bar_UGUI_UITK_같은Range와Value_같은Ratio()
        {
            var go = new GameObject("TEST_Bar");
            try
            {
                var ugui = go.AddComponent<UGUIBar>();
                var uitk = new UITKBar();

                ugui.SetRange(-10f, 10f, true);
                uitk.SetRange(-10f, 10f, true);
                ugui.SetValue(5f, true);
                uitk.SetValue(5f, true);

                Assert.AreEqual(0.75f, ugui.Ratio);
                Assert.AreEqual(ugui.Ratio, uitk.Ratio);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

    #endregion

    }
}
