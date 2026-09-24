/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriApplicationWindowInput.cs
수정일 : 2026-09-24

# 설명
Application Window sibling Context의 Input contribution 전환을 검증한다.

# 테스트 구성
 I: inactive sibling Input contribution 제외와 재활성 복원
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

using static inonego.Xeri.UI.TEST.Window.XeriWindowWorkspaceTestSupport;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// Application Window Input contribution 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_XeriApplicationWindowInput
    {

    #region I-1: inactive sibling Input contribution

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> inactive Application Window의 살아 있는 Screen은 global gameplay policy에
        /// <br/> 기여하지 않고 Window 재활성화 시 같은 Session contribution을 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriApplicationWindowInput_InactiveSiblingInputContribution제외()
        {
            using var fixture = new WorkspaceFixture();
            fixture.GameplayMap.Enable();
            var applicationOptions = fixture.CreateApplicationOptions();
            var first = fixture.Workspace.OpenApplicationWindow
            (
                "application-first",
                "Application First",
                new Vector2(20f, 30f),
                new Vector2(320f, 220f),
                applicationOptions
            );
            first.Context.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Application.First.Screen",
                    blocksGameplayInput: true,
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                new TestApplicationScreenSource()
            );
            Assert.IsTrue(first.Context.Screens.Open("Application.First.Screen").Accepted);
            Assert.IsFalse(fixture.GameplayMap.enabled);

            var second = fixture.Workspace.OpenApplicationWindow
            (
                "application-second",
                "Application Second",
                new Vector2(60f, 70f),
                new Vector2(360f, 240f),
                applicationOptions
            );
            second.Context.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Application.Second.Screen",
                    blocksGameplayInput: false,
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                new TestApplicationScreenSource()
            );
            Assert.IsTrue(second.Context.Screens.Open("Application.Second.Screen").Accepted);

            Assert.AreEqual(1, first.Context.Screens.Count);
            Assert.AreEqual(1, second.Context.Screens.Count);
            Assert.IsTrue(fixture.GameplayMap.enabled);

            first.Focus();

            Assert.AreEqual(1, second.Context.Screens.Count);
            Assert.IsFalse(fixture.GameplayMap.enabled);

            second.Focus();

            Assert.AreEqual(1, first.Context.Screens.Count);
            Assert.IsTrue(fixture.GameplayMap.enabled);
        }

    #endregion

    }
}
