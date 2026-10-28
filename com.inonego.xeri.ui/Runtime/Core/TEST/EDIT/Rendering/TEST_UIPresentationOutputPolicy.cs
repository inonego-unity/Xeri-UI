/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UIPresentationOutputPolicy.cs
수정일 : 2026-10-18

# 설명
프로젝트 단위 출력 정밀도/후처리 독립 설정과 단일 UIRuntime 소유권을 검증한다.

# 테스트 구성
 E: 정밀도·후처리 네 정책과 Native 기본값
 S: 설정 소유권의 중복 획득 방지
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    // ============================================================
    /// <summary>
    /// UIRuntime 설정에 의한 Native UI 출력 정책의 소유권을 검증한다.
    /// </summary>
    // ============================================================
    public sealed class TEST_UIPresentationOutputPolicy
    {

    #region E-1: 출력 정책과 Native 기본값

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Precision과 post-processing 설정 조합이 독립적으로 반영되고 해제 뒤 기본값으로 돌아간다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIPresentationOutputPolicy_정밀도와후처리모드는서로독립()
        {
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();

            try
            {
                Assert.IsFalse(settings.HighPrecisionOutput);
                Assert.AreEqual(UIPresentationPostProcessing.Unaffected, settings.UIPostProcessing);

                UIPresentationOutputPolicy.Bind(settings);
                Assert.IsTrue(UIPresentationOutputPolicy.IsConfigured);
                Assert.IsFalse(UIPresentationOutputPolicy.HighPrecisionOutput);
                Assert.IsFalse(UIPresentationOutputPolicy.TryGetComposition(out var precision, out var affected));
                Assert.IsFalse(precision);
                Assert.IsFalse(affected);

                JsonUtility.FromJsonOverwrite
                (
                    @"{""highPrecisionOutput"":false,""uiPostProcessing"":1}",
                    settings
                );

                Assert.IsFalse(UIPresentationOutputPolicy.HighPrecisionOutput);
                Assert.AreEqual
                (
                    UIPresentationPostProcessing.Affected,
                    UIPresentationOutputPolicy.PostProcessing
                );
                Assert.IsTrue(UIPresentationOutputPolicy.TryGetComposition(out precision, out affected));
                Assert.IsFalse(precision);
                Assert.IsTrue(affected);

                JsonUtility.FromJsonOverwrite
                (
                    @"{""highPrecisionOutput"":true,""uiPostProcessing"":0}",
                    settings
                );

                Assert.IsTrue(UIPresentationOutputPolicy.HighPrecisionOutput);
                Assert.AreEqual
                (
                    UIPresentationPostProcessing.Unaffected,
                    UIPresentationOutputPolicy.PostProcessing
                );
                Assert.IsTrue(UIPresentationOutputPolicy.TryGetComposition(out precision, out affected));
                Assert.IsTrue(precision);
                Assert.IsFalse(affected);

                JsonUtility.FromJsonOverwrite
                (
                    @"{""highPrecisionOutput"":true,""uiPostProcessing"":1}", settings
                );
                Assert.IsTrue(UIPresentationOutputPolicy.TryGetComposition(out precision, out affected));
                Assert.IsTrue(precision);
                Assert.IsTrue(affected);
            }
            finally
            {
                UIPresentationOutputPolicy.Unbind(settings);
                UnityEngine.Object.DestroyImmediate(settings);
            }

            Assert.IsFalse(UIPresentationOutputPolicy.IsConfigured);
            Assert.IsFalse(UIPresentationOutputPolicy.HighPrecisionOutput);
            Assert.IsFalse(UIPresentationOutputPolicy.TryGetComposition(out _, out _));
        }

    #endregion

    #region S-1: 단일 설정 소유권

        // ------------------------------------------------------------
        /// <summary>
        /// 다른 Runtime이 소유한 출력 정책을 중복 획득하지 못하도록 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIPresentationOutputPolicy_다른Runtime은출력정책을탈취하지못함()
        {
            var first = ScriptableObject.CreateInstance<UISettingsAsset>();
            var other = ScriptableObject.CreateInstance<UISettingsAsset>();

            try
            {
                UIPresentationOutputPolicy.Bind(first);

                Assert.Throws<InvalidOperationException>
                (
                    () => UIPresentationOutputPolicy.Bind(other)
                );

                UIPresentationOutputPolicy.Unbind(other);
                Assert.IsTrue(UIPresentationOutputPolicy.IsConfigured);
            }
            finally
            {
                UIPresentationOutputPolicy.Unbind(first);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

    #endregion

    }
}
