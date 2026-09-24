/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriApplicationWindowPresentation.cs
수정일 : 2026-09-24

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

    #region P-1: local Presentation world

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

    #endregion

    }
}
