/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriApplicationWindowPresentation.cs
수정일 : 2026-10-05

# 설명
Application Window의 Child PresentationSession과 local Layer namespace 격리를 검증한다.

# 테스트 구성
 P: Child PresentationSession과 local UI world
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
    /// Application Window local Presentation 격리 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_XeriApplicationWindowPresentation
    {

    #region 콘텐츠 소유권

        // ------------------------------------------------------------
        /// <summary>
        /// 창 또는 Context 종료 시 콘텐츠를 먼저 한 번만 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [TestCase(false)]
        [TestCase(true)]
        public void TEST_WindowContent_종료경로와무관하게Context보다먼저반환(bool closeContext)
        {
            using var fixture = new WorkspaceFixture();
            var window = fixture.Workspace.OpenApplicationWindow
            (
                "owned-content", "Owned Content", Vector2.zero,
                new Vector2(320f, 240f), fixture.CreateApplicationOptions()
            );
            var context = window.Context;
            var count = 0;
            var contextWasAlive = false;
            var content = new Lease
            (
                () =>
                {
                    count++;
                    contextWasAlive = !context.IsDisposed;
                    using var late = new Lease(() => { });
                    Assert.Throws<ObjectDisposedException>(() => window.RegisterChild(late));
                }
            );
            window.RegisterChild(content);

            // 부모 주도의 Context 종료와 창 주도의 종료 모두 같은 소유권을 반환한다.
            if (closeContext)
            {
                context.Dispose();
            }
            else
            {
                window.Dispose();
            }

            using var rejected = new Lease(() => { });
            Assert.Throws<ObjectDisposedException>(() => window.RegisterChild(rejected));
            window.Dispose();
            Assert.AreEqual(1, count);
            Assert.IsTrue(contextWasAlive);
            Assert.IsTrue(context.IsDisposed);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 콘텐츠 하나가 실패해도 다른 콘텐츠와 Context를 끝까지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_WindowContent_일부정리실패_나머지소유권반환()
        {
            using var fixture = new WorkspaceFixture();
            var window = fixture.Workspace.OpenApplicationWindow
            (
                "failing-content", "Failing Content", Vector2.zero,
                new Vector2(320f, 240f), fixture.CreateApplicationOptions()
            );
            var context = window.Context;
            var released = false;
            window.RegisterChild(new Lease(() => released = true));
            window.RegisterChild(new Lease(() => throw new InvalidOperationException("content")));

            Assert.Throws<AggregateException>(() => window.Dispose());
            Assert.IsTrue(released);
            Assert.IsTrue(context.IsDisposed);
            Assert.IsTrue(window.IsDisposed);
            Assert.DoesNotThrow(() => window.Dispose());
        }

    #endregion

    #region P-1: 로컬 프레젠테이션 영역

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 두 Application Window는 같은 Child Layout을 사용해도
        /// <br/> 서로 다른 Context, Registry와 local Layer namespace를 소유한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriApplicationWindowPresentation_동일Layout_LocalUIWorld독립()
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
            var second = fixture.Workspace.OpenApplicationWindow
            (
                "application-second",
                "Application Second",
                new Vector2(60f, 70f),
                new Vector2(360f, 240f),
                applicationOptions
            );

            Assert.IsTrue(first.IsApplication);
            Assert.IsTrue(second.IsApplication);
            Assert.IsNotNull(first.Context);
            Assert.IsNotNull(second.Context);
            Assert.AreNotSame(first.Context, second.Context);
            Assert.AreNotSame(first.Context.Presentation.LayerRegistry, second.Context.Presentation.LayerRegistry);
            Assert.IsTrue(first.Context.Presentation.LayerRegistry.Contains("Application.Screen"));
            Assert.IsTrue(first.Context.Presentation.LayerRegistry.Contains("Application.Overlay"));
            Assert.IsTrue(first.Context.Presentation.LayerRegistry.Contains("Application.Modal"));
            Assert.IsTrue(second.Context.Presentation.LayerRegistry.Contains("Application.Screen"));
            Assert.IsTrue(second.Context.Presentation.LayerRegistry.Contains("Application.Overlay"));
            Assert.IsTrue(second.Context.Presentation.LayerRegistry.Contains("Application.Modal"));

            Assert.IsTrue
            (
                first.Context.Presentation.LayerRegistry.TryGet
                (
                    "Application.Screen",
                    out var firstLayer
                )
            );
            var firstLayerRoot = ((IPresentationLayerDriver<VisualElement>)firstLayer).Root;
            var firstSurfaceRoot = firstLayerRoot.parent;
            var firstPanel = fixture.LayerDriver.Root.Q<XeriWindowPanel>();
            Assert.IsNotNull(firstPanel);
            Assert.AreSame(firstPanel.ContentRoot, firstSurfaceRoot.parent);

            var firstContext = first.Context;
            var firstRegistry = firstContext.Presentation.LayerRegistry;
            first.Dispose();

            Assert.IsTrue(first.IsDisposed);
            Assert.IsTrue(firstContext.IsDisposed);
            Assert.IsTrue(firstRegistry.IsDisposed);
            Assert.IsNull(firstSurfaceRoot.parent);
            Assert.IsFalse(second.IsDisposed);
            Assert.IsFalse(second.Context.IsDisposed);
            Assert.IsTrue(second.Context.Presentation.LayerRegistry.Contains("Application.Screen"));

            var secondContext = second.Context;
            var secondRegistry = secondContext.Presentation.LayerRegistry;
            fixture.Workspace.Dispose();

            Assert.IsTrue(second.IsDisposed);
            Assert.IsTrue(secondContext.IsDisposed);
            Assert.IsTrue(secondRegistry.IsDisposed);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> sibling Application Window는 같은 Screen ID를 각각 등록해도
        /// <br/> 서로 Screen stack을 공유하지 않는다.
        /// <br/> A의 Replace/Close는 B의 같은 ID Screen 상태를 변경하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriApplicationWindowPresentation_SiblingScreenStack_동일ID독립()
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
            var second = fixture.Workspace.OpenApplicationWindow
            (
                "application-second",
                "Application Second",
                new Vector2(60f, 70f),
                new Vector2(360f, 240f),
                applicationOptions
            );
            var firstHome = new TestApplicationScreenSource();
            var firstDetail = new TestApplicationScreenSource();
            var secondHome = new TestApplicationScreenSource();
            var secondDetail = new TestApplicationScreenSource();

            first.Context.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Application.First.Screen",
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                PresentationTarget.Local("Application.Screen"),
                firstHome
            );
            first.Context.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Application.Second.Screen",
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                PresentationTarget.Local("Application.Screen"),
                firstDetail
            );
            second.Context.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Application.First.Screen",
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                PresentationTarget.Local("Application.Screen"),
                secondHome
            );
            second.Context.ScreenRegistry.Register
            (
                new ScreenOptions
                (
                    "Application.Second.Screen",
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                PresentationTarget.Local("Application.Screen"),
                secondDetail
            );

            Assert.IsTrue(first.Context.Screens.Open("Application.First.Screen").Accepted);
            Assert.IsTrue(second.Context.Screens.Open("Application.First.Screen").Accepted);
            Assert.AreEqual("Application.First.Screen", first.Context.Screens.Top.ID);
            Assert.AreEqual("Application.First.Screen", second.Context.Screens.Top.ID);

            Assert.IsTrue(first.Context.Screens.Replace("Application.Second.Screen").Accepted);

            Assert.AreEqual("Application.Second.Screen", first.Context.Screens.Top.ID);
            Assert.AreEqual("Application.First.Screen", second.Context.Screens.Top.ID);
            Assert.AreEqual(1, first.Context.Screens.Count);
            Assert.AreEqual(1, second.Context.Screens.Count);

            Assert.IsTrue(first.Context.Screens.Close());

            Assert.AreEqual(0, first.Context.Screens.Count);
            Assert.AreEqual(1, second.Context.Screens.Count);
            Assert.AreEqual("Application.First.Screen", second.Context.Screens.Top.ID);
        }

    #endregion

    }
}
