/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_Bar.cs
수정일 : 2026-11-10

# 설명
DOTween 실행 중 Bar 전이 콜백 실패와 재진입의 Tween 소유권을 검증한다.

# 테스트 구성
 A: 전이 시작과 실패
 B: 완료 시점과 테스트 지원
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Reflection;

using UnityEngine;
using UnityEngine.TestTools;

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
    /// Bar의 PlayMode 전이·Tween 소유권 테스트.
    /// </summary>
    // ============================================================
    public class TEST_Bar
    {

    #region A: 전이 시작과 실패

        // ----------------------------------------------------------------------
        /// <summary>
        /// 시작 시 onUpdate 예외가 발생하면 새 Tween을 종료하고 소유권을 해제한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_BarTransition_첫Observer실패_Tween종료()
        {
        #if DOTWEEN
            var transition = new BarTransition();
            var curve = new TweenCurve { Duration = 0.2f, Delay = 0f };
            var baseline = GetActiveTweenCount();
            var field = GetTweenField();

            Assert.Throws<InvalidOperationException>
            (
                () => transition.Set
                (
                    0.75f,
                    curve,
                    false,
                    (_, _) => throw new InvalidOperationException("observer failure")
                )
            );

            Assert.IsNull(field.GetValue(transition));

            yield return null;

            Assert.AreEqual(baseline, GetActiveTweenCount());
        #else
            Assert.Ignore("DOTWEEN 활성 구성에서 실행되는 테스트입니다.");
            yield return null;
        #endif
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이전 Tween의 observer가 재진입 후 실패해도 새 Tween의 소유권은 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_BarTransition_Observer재진입실패_새Tween유지()
        {
        #if DOTWEEN
            var transition = new BarTransition();
            var curve = new TweenCurve { Duration = 0.2f, Delay = 0f };
            var field = GetTweenField();
            object successor = null;

            Assert.Throws<InvalidOperationException>
            (
                () => transition.Set
                (
                    0.25f,
                    curve,
                    false,
                    (_, _) =>
                    {
                        transition.Set(0.75f, curve, false, (_, _) => { });
                        successor = field.GetValue(transition);
                        throw new InvalidOperationException("previous observer failure");
                    }
                )
            );

            Assert.IsNotNull(successor);
            Assert.AreSame(successor, field.GetValue(transition));

            transition.Stop();
            Assert.IsNull(field.GetValue(transition));

            yield return null;
        #else
            Assert.Ignore("DOTWEEN 활성 구성에서 실행되는 테스트입니다.");
            yield return null;
        #endif
        }

    #endregion

    #region B: 완료 시점과 테스트 지원

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 마지막 Tween tick의 observer가 재진입해도 완료된 이전 Tween은 새 Tween을 지우지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_BarTransition_마지막Tick재진입_새Tween소유권유지()
        {
        #if DOTWEEN
            var transition = new BarTransition();
            var field = GetTweenField();
            var reentered = false;
            object successor = null;

            StartRetargetOnLastTick
            (
                transition,
                () =>
                {
                    reentered = true;
                    successor = field.GetValue(transition);
                }
            );

            yield return new WaitForSecondsRealtime(0.2f);

            Assert.IsTrue(reentered);
            Assert.IsNotNull(successor);

            yield return null;

            Assert.AreSame(successor, field.GetValue(transition));
            transition.Stop();
        #else
            Assert.Ignore("DOTWEEN 활성 구성에서 실행되는 테스트입니다.");
            yield return null;
        #endif
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 완료 시점으로 직접 이동할 때 재진입한 새 Tween의 소유권을 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_BarTransition_정확한완료시점재진입_새Tween유지()
        {
        #if DOTWEEN
            var transition = new BarTransition();
            var field = GetTweenField();
            var reentered = false;
            object successor = null;

            StartRetargetOnLastTick
            (
                transition,
                () =>
                {
                    reentered = true;
                    successor = field.GetValue(transition);
                }
            );

            var firstTween = field.GetValue(transition);
            Assert.IsNotNull(firstTween);

            GotoTween(firstTween, 0.05f);

            Assert.IsTrue(reentered);
            Assert.IsNotNull(successor);
            Assert.AreSame(successor, field.GetValue(transition));

            transition.Stop();
            yield return null;
        #else
            Assert.Ignore("DOTWEEN 활성 구성에서 실행되는 테스트입니다.");
            yield return null;
        #endif
        }

    #if DOTWEEN

        // ------------------------------------------------------------
        /// <summary>
        /// 마지막 tick에서 새 전이를 시작하도록 observer를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void StartRetargetOnLastTick
        (
            BarTransition transition,
            Action onRetarget
        )
        {
            var didRetarget = false;

            transition.Set
            (
                0.25f,
                new TweenCurve { Duration = 0.05f, Delay = 0f },
                false,
                (current, target) =>
                {
                    if (didRetarget || current < 0.249f) return;

                    didRetarget = true;
                    transition.Set
                    (
                        0.75f,
                        new TweenCurve { Duration = 1f, Delay = 0f },
                        false,
                        (_, _) => { }
                    );
                    onRetarget();
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// DOTween의 Goto를 통해 정확한 완료 tick을 동기적으로 실행한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void GotoTween(object tween, float elapsed)
        {
            var extensions = Type.GetType("DG.Tweening.TweenExtensions, DOTween");
            Assert.IsNotNull(extensions);

            foreach (var candidate in extensions.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (candidate.Name != "Goto") continue;

                var parameters = candidate.GetParameters();
                if (parameters.Length != 3 || parameters[1].ParameterType != typeof(float)) continue;

                var method = candidate.IsGenericMethodDefinition
                    ? candidate.MakeGenericMethod(tween.GetType())
                    : candidate;

                method.Invoke(null, new object[] { tween, elapsed, false });
                return;
            }

            Assert.Fail("DOTween TweenExtensions.Goto를 찾지 못했습니다.");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 재진입 시 전이 객체의 실제 소유권을 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static FieldInfo GetTweenField()
        {
            var field = typeof(BarTransition).GetField
            (
                "currentTween",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.IsNotNull(field);
            return field;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Test Assembly의 플러그인 직접 참조 없이 DOTween 활성 수를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int GetActiveTweenCount()
        {
            var dotweenType = Type.GetType("DG.Tweening.DOTween, DOTween");
            Assert.IsNotNull(dotweenType);

            var countMethod = dotweenType.GetMethod
            (
                "TotalActiveTweens",
                BindingFlags.Static | BindingFlags.Public
            );
            Assert.IsNotNull(countMethod);

            return (int)countMethod.Invoke(null, null);
        }

    #endif

    #endregion

    }
}
