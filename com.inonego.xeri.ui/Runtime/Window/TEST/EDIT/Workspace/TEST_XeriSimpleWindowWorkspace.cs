/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriSimpleWindowWorkspace.cs
수정일 : 2026-09-24

# 설명
Simple Window의 persistent Focus record와 Minimize 복원 계약을 검증한다.

# 테스트 구성
 F: persistent LastFocus와 Minimize fallback 복원
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
    /// Simple Window Focus 복원 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_XeriSimpleWindowWorkspace
    {

    #region F-1: persistent LastFocus

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Simple Window A→B→A 전환은 Window lifetime 동안 persistent record를 유지해
        /// <br/> 각 Window의 마지막 Focus를 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriSimpleWindowWorkspace_A_B_A_LastFocus복원()
        {
            using var fixture = new WorkspaceFixture();
            var firstView = new VisualElement();
            var firstControl = new Button
            {
                name = "First Control",
            };
            firstView.Add(firstControl);
            var secondView = new VisualElement();
            var secondControl = new Button
            {
                name = "Second Control",
            };
            secondView.Add(secondControl);
            var first = OpenWindow(fixture.Workspace, "first", firstView);
            var second = OpenWindow(fixture.Workspace, "second", secondView);

            fixture.FocusDriver.Select(secondControl);
            first.Focus();
            fixture.FocusDriver.Select(firstControl);

            second.Focus();

            Assert.AreSame(secondControl, fixture.FocusDriver.Current);

            first.Focus();

            Assert.AreSame(firstControl, fixture.FocusDriver.Current);
            Assert.IsFalse(first.FocusScope.IsDisposed);
            Assert.IsFalse(second.FocusScope.IsDisposed);
        }

    #endregion

    #region F-2: Minimize Focus 복원

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Window Focus와 Minimize 흐름은 Core Focus Scope를 다음 Window와
        /// <br/> Parent fallback으로 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriSimpleWindowWorkspace_Minimize_CoreFocusScope_복원()
        {
            using var fixture = new WorkspaceFixture();

            var first = OpenWindow(fixture.Workspace, "first");
            var second = OpenWindow(fixture.Workspace, "second");
            var firstScope = (IFocusScope)first.Controller.Driver;
            var secondScope = (IFocusScope)second.Controller.Driver;

            Assert.IsTrue(secondScope.ContainsFocus(fixture.FocusDriver.Current));

            first.Focus();

            Assert.IsTrue(firstScope.ContainsFocus(fixture.FocusDriver.Current));

            first.Controller.Minimize();

            Assert.IsTrue(secondScope.ContainsFocus(fixture.FocusDriver.Current));

            second.Controller.Minimize();

            Assert.AreSame(fixture.FocusDriver.Fallback, fixture.FocusDriver.Current);
        }

    #endregion

    }
}
