/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriApplicationWindowModal.cs
수정일 : 2026-09-24

# 설명
Application Window Child Context 내부 nested Modal과 Focus 복원을 검증한다.

# 테스트 구성
 M: Window-local nested Modal과 underlying Screen Focus 복원
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
    /// Application Window Modal 복원 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_XeriApplicationWindowModal
    {

    #region M-1: Application Window local Nested Modal

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Application Window의 Nested Modal은 Child Context 안에서
        /// <br/> LIFO Focus를 복원하고 마지막 Modal 해제 뒤
        /// <br/> underlying Screen LastFocus로 돌아간다.
        /// <br/> sibling Window Modal state는 변경하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriApplicationWindowModal_NestedModal_LastFocus복원()
        {
            using var fixture = new WorkspaceFixture();
            var applicationOptions = fixture.CreateApplicationOptions();
            var window = fixture.Workspace.OpenApplicationWindow
            (
                "application",
                "Application",
                new Vector2(20f, 30f),
                new Vector2(320f, 220f),
                applicationOptions
            );
            var sibling = fixture.Workspace.OpenApplicationWindow
            (
                "application-sibling",
                "Application Sibling",
                new Vector2(60f, 70f),
                new Vector2(360f, 240f),
                applicationOptions
            );
            window.Focus();
            var screenSource = new TestApplicationScreenSource();
            window.Context.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Application.First.Screen",
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                screenSource
            );
            Assert.IsTrue(window.Context.Screens.Open("Application.First.Screen").Accepted);
            fixture.FocusDriver.Select(screenSource.SecondaryFocus);

            var firstModal = OpenApplicationModal
            (
                window.Context,
                "Application.Modal.A",
                "Modal A",
                out var firstModalSecondary
            );
            fixture.FocusDriver.Select(firstModalSecondary);
            var secondModal = OpenApplicationModal
            (
                window.Context,
                "Application.Modal.B",
                "Modal B",
                out var secondModalSecondary
            );
            fixture.FocusDriver.Select(secondModalSecondary);

            Assert.AreEqual(2, window.Context.Modals.Count);
            Assert.AreEqual(0, sibling.Context.Modals.Count);

            secondModal.Dispose();

            Assert.AreEqual(1, window.Context.Modals.Count);
            Assert.AreSame(firstModalSecondary, fixture.FocusDriver.Current);

            firstModal.Dispose();

            Assert.AreEqual(0, window.Context.Modals.Count);
            Assert.AreEqual(0, sibling.Context.Modals.Count);
            Assert.IsFalse(sibling.IsDisposed);
            Assert.AreSame(screenSource.SecondaryFocus, fixture.FocusDriver.Current);
        }

    #endregion

    }
}
