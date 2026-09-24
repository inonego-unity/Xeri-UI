/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriApplicationWindowFocus.cs
수정일 : 2026-09-24

# 설명
Application Window sibling Context의 LastFocus와 authority 전환을 검증한다.

# 테스트 구성
 F: sibling Application Window Context Focus 복원
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
    /// Application Window Focus 복원 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_XeriApplicationWindowFocus
    {

    #region F-1: 애플리케이션 창 컨텍스트 마지막 포커스

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Application Window A→B→A 전환은 각 Child Context의
        /// <br/> 마지막 Screen Focus를 서로 덮지 않고 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriApplicationWindowFocus_A_B_A_ContextFocus복원()
        {
            using var fixture = new WorkspaceFixture();
            var applicationOptions = fixture.CreateApplicationOptions();
            var first = fixture.Workspace.OpenApplicationWindow
            (
                "application-first",
                "Application First",
                new Vector2(20f, 30f),
                new Vector2(320f, 220f),
                applicationOptions
            );
            var firstSource = new TestApplicationScreenSource();
            first.Context.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Application.First.Screen",
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                PresentationTarget.Local("Application.Screen"),
                firstSource
            );
            Assert.IsTrue(first.Context.Screens.Open("Application.First.Screen").Accepted);
            Assert.AreSame(firstSource.DefaultFocus, fixture.FocusDriver.Current);
            fixture.FocusDriver.Select(firstSource.SecondaryFocus);

            var second = fixture.Workspace.OpenApplicationWindow
            (
                "application-second",
                "Application Second",
                new Vector2(60f, 70f),
                new Vector2(360f, 240f),
                applicationOptions
            );
            var secondSource = new TestApplicationScreenSource();
            second.Context.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Application.Second.Screen",
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                PresentationTarget.Local("Application.Screen"),
                secondSource
            );
            Assert.IsTrue(second.Context.Screens.Open("Application.Second.Screen").Accepted);
            Assert.IsFalse(first.Context.IsEffective);
            Assert.IsTrue(second.Context.IsEffective);
            Assert.AreSame(secondSource.DefaultFocus, fixture.FocusDriver.Current);
            fixture.FocusDriver.Select(secondSource.SecondaryFocus);

            first.Focus();

            Assert.IsTrue(first.Context.IsEffective);
            Assert.IsFalse(second.Context.IsEffective);
            Assert.AreSame(firstSource.SecondaryFocus, fixture.FocusDriver.Current);

            second.Focus();

            Assert.IsFalse(first.Context.IsEffective);
            Assert.IsTrue(second.Context.IsEffective);
            Assert.AreSame(secondSource.SecondaryFocus, fixture.FocusDriver.Current);
        }

    #endregion

    }
}
