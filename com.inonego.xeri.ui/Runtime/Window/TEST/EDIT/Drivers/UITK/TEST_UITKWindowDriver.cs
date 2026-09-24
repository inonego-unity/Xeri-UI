/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UITKWindowDriver.cs
수정일 : 2026-09-20

# 설명
UITKWindowDriver style 반영 테스트.

# 테스트 구성
 B: Bounds 반영
 S: State 반영
 F: Core Focus Scope
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// UITKWindowDriver 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_UITKWindowDriver
    {

    #region B-1: 경계

        // ------------------------------------------------------------
        /// <summary>
        /// Pos와 Size 설정은 target style에 반영된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKWindowDriver_Pos_Size_Target_Style_반영()
        {
            var target = new VisualElement();
            var driver = new UITKWindowDriver(target);

            driver.Pos = new Vector2(10f, 20f);
            driver.Size = new Vector2(300f, 240f);

            Assert.AreEqual(10f, target.style.left.value.value);
            Assert.AreEqual(20f, target.style.top.value.value);
            Assert.AreEqual(300f, target.style.width.value.value);
            Assert.AreEqual(240f, target.style.height.value.value);
        }

    #endregion

    #region S-1: 상태

        // ------------------------------------------------------------
        /// <summary>
        /// CommitState는 표시 여부를 변경하지 않고 상태 class만 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKWindowDriver_CommitState_Display_유지()
        {
            var target = new VisualElement();
            var driver = new UITKWindowDriver(target);

            driver.Visibility.Set(true);
            driver.CommitState(XeriWindowState.Minimized);

            Assert.AreEqual(DisplayStyle.Flex, target.style.display.value);
            Assert.IsTrue(target.ClassListContains("xeri-window--minimized"));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// ApplyVisualState는 완료 상태 값을 바꾸지 않고 상태 class만 반영한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKWindowDriver_ApplyVisualState_State_유지()
        {
            var target = new VisualElement();
            var driver = new UITKWindowDriver(target);

            driver.CommitState(XeriWindowState.Maximized);
            driver.ApplyVisualState(XeriWindowState.Normal);

            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
            Assert.IsTrue(target.ClassListContains("xeri-window--normal"));
            Assert.IsFalse(target.ClassListContains("xeri-window--maximized"));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Core Presentation Visibility는 Window Root 표시 여부를 직접 반영한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKWindowDriver_PresentationVisibility_Display_반영()
        {
            var target = new VisualElement();
            var driver = new UITKWindowDriver(target);

            driver.Visibility.Set(false);
            Assert.AreEqual(DisplayStyle.None, target.style.display.value);

            driver.Visibility.Set(true);
            Assert.AreEqual(DisplayStyle.Flex, target.style.display.value);
        }

    #endregion

    #region S-2: 최대화 복원

        // ----------------------------------------------------------------------
        /// <summary>
        /// CommitState는 driver 내부에서 restore bounds를 소유하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKWindowDriver_CommitState_RestoreBounds_미소유()
        {
            var target = new VisualElement();
            var driver = new UITKWindowDriver(target);

            driver.Pos = new Vector2(10f, 20f);
            driver.Size = new Vector2(300f, 240f);
            driver.CommitState(XeriWindowState.Maximized);
            driver.Pos = new Vector2(0f, 0f);
            driver.Size = new Vector2(600f, 480f);
            driver.CommitState(XeriWindowState.Normal);

            Assert.AreEqual(new Vector2(0f, 0f), driver.Pos);
            Assert.AreEqual(new Vector2(600f, 480f), driver.Size);
        }

    #endregion

    #region B-2: 최대화 경계

        // ------------------------------------------------------------
        /// <summary>
        /// ApplyMaximizedBounds는 부모 영역 기준 layout 값을 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKWindowDriver_ApplyMaximizedBounds_부모영역_반영()
        {
            var target = new VisualElement();
            var driver = new UITKWindowDriver(target);

            driver.ApplyMaximizedBounds();

            Assert.AreEqual(0f, target.style.left.value.value);
            Assert.AreEqual(0f, target.style.top.value.value);
            Assert.AreEqual(0f, target.style.right.value.value);
            Assert.AreEqual(0f, target.style.bottom.value.value);
        }

    #endregion

    #region F-1: 코어 포커스 범위

        // ------------------------------------------------------------
        /// <summary>
        /// Window Root는 Core가 소비할 Focus Scope와 기본 대상을 제공한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKWindowDriver_FocusScope_Root와Child_포함()
        {
            var root = new VisualElement();
            var child = new VisualElement();
            var external = new VisualElement();
            root.Add(child);

            IFocusScope scope = new UITKWindowDriver(root);

            Assert.AreSame(root, scope.DefaultFocus);
            Assert.IsTrue(root.focusable);
            Assert.IsTrue(scope.ContainsFocus(root));
            Assert.IsTrue(scope.ContainsFocus(child));
            Assert.IsFalse(scope.ContainsFocus(external));
        }

    #endregion

    }
}
