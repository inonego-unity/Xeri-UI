/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_SplashParams.cs
수정일 : 2026-11-08

# 설명
Splash 호출 인자의 목표 Alpha 기본값과 범위를 검증한다.
========================================================================= BLOCK_HEADER_END */

using System;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    // ============================================================
    /// <summary>
    /// Splash 목표 Alpha 호출 계약을 검증한다.
    /// </summary>
    // ============================================================
    [TestFixture]
    public sealed class TEST_SplashParams
    {

    #region P-1: 표시 알파 검증

        // ------------------------------------------------------------
        /// <summary>
        /// 명시 생성과 기본 구조체 값은 불투명한 목표를 사용한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_SplashParams_기본Alpha_불투명()
        {
            Assert.AreEqual(1f, SplashParams.Default().VisibleAlpha);
            Assert.AreEqual(1f, default(SplashParams).VisibleAlpha);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 목표 Alpha를 보존하고 범위를 벗어난 값은 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_SplashParams_목표Alpha_유효범위()
        {
            var parameters = new SplashParams(0f, 0f, 0f, 0.4f);

            Assert.AreEqual(0.4f, parameters.VisibleAlpha);
            Assert.AreEqual(0f, new SplashParams(0f, 0f, 0f, 0f).VisibleAlpha);

            Assert.Throws<ArgumentOutOfRangeException>
            (
                () => new SplashParams(0f, 0f, 0f, float.NaN)
            );
            Assert.Throws<ArgumentOutOfRangeException>
            (
                () => new SplashParams(0f, 0f, 0f, -0.1f)
            );
            Assert.Throws<ArgumentOutOfRangeException>
            (
                () => new SplashParams(0f, 0f, 0f, 1.1f)
            );
        }

    #endregion

    }
}
