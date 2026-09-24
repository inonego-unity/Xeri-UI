/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriApplicationWindowLifetime.cs
수정일 : 2026-10-07

# 설명
Application Window 종료 시 Child Context와 Presentation 자원 정리를 검증한다.

# 테스트 구성
 L: Child Screen·Modal·Context·Presentation teardown
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
    /// Application Window teardown 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_XeriApplicationWindowLifetime
    {

    #region L-1: 애플리케이션 창 자식 UI 해제

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 열린 Screen과 Modal을 가진 Application Window 종료는
        /// <br/> Child Context를 먼저 정리하고 PresentationSession과
        /// <br/> Layer Registry를 terminal 상태로 만들고 sibling Window는
        /// <br/> 정상 상태로 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriApplicationWindowLifetime_Close_ChildUI전체정리()
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
            var context = window.Context;
            var presentation = context.Presentation;
            var registry = presentation.LayerRegistry;
            var siblingContext = sibling.Context;
            var siblingPresentation = siblingContext.Presentation;
            var siblingRegistry = siblingPresentation.LayerRegistry;
            var screenSource = new TestApplicationScreenSource();
            context.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Application.First.Screen",
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                PresentationTarget.Local("Application.Screen"),
                screenSource
            );
            Assert.IsTrue(context.Screens.Open("Application.First.Screen").Accepted);
            var modal = OpenApplicationModal
            (
                context,
                "Application.Modal",
                "Closing Modal",
                out _
            );

            Assert.Throws<InvalidOperationException>(presentation.Dispose);
            Assert.IsFalse(presentation.IsDisposed);
            Assert.IsFalse(context.IsDisposed);

            window.Dispose();

            Assert.IsTrue(window.IsDisposed);
            Assert.IsTrue(context.IsDisposed);
            Assert.IsTrue(presentation.IsDisposed);
            Assert.IsTrue(registry.IsDisposed);
            Assert.IsTrue(modal.IsDisposed);
            Assert.AreEqual(1, screenSource.ReleaseCount);
            Assert.IsFalse(sibling.IsDisposed);
            Assert.IsFalse(siblingContext.IsDisposed);
            Assert.IsFalse(siblingPresentation.IsDisposed);
            Assert.IsFalse(siblingRegistry.IsDisposed);
            Assert.IsTrue(siblingRegistry.Contains("Application.Screen"));
            Assert.AreSame(fixture.FocusDriver.Fallback, fixture.FocusDriver.Current);
        }

    #endregion

    }
}
