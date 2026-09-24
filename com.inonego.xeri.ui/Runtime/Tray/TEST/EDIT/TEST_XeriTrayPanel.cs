/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriTrayPanel.cs
수정일 : 2026-09-20

# 설명
공통 UITK Tray panel/button 표시 테스트.

# 테스트 구성
 P: Panel entry 생성과 Core Presentation
 V: VisibleContent 표시 조합
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Tray;

namespace inonego.Xeri.UI.TEST.Tray
{
    // ============================================================
    /// <summary>
    /// 공통 UITK Tray panel 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriTrayPanel
    {

    #region P-1: Entry 생성

        // ------------------------------------------------------------
        /// <summary>
        /// Reload는 entry 목록만큼 Tray button을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_Reload_Entry_개수만큼_Button_생성()
        {
            var panel = new XeriTrayPanel();
            var entries = new[]
            {
                new XeriTrayEntry("a", "A"),
                new XeriTrayEntry("b", "B"),
            };

            panel.Reload(entries, XeriTrayOptions.Default());

            var container = panel.Q<VisualElement>("entry-container");

            Assert.IsNotNull(container);
            Assert.AreEqual(2, container.childCount);
        }

    #endregion

    #region P-2: Entry Order

        // ------------------------------------------------------------
        /// <summary>
        /// Reload는 전달된 entry 순서 그대로 Tray button을 배치한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_Reload_Entry_Order_유지()
        {
            var panel = new XeriTrayPanel();
            var entries = new[]
            {
                new XeriTrayEntry("second", "Second"),
                new XeriTrayEntry("first", "First"),
                new XeriTrayEntry("top", "Top"),
            };

            panel.Reload(entries, XeriTrayOptions.Default());

            var container = panel.Q<VisualElement>("entry-container");

            Assert.AreEqual("second", ((XeriTrayButton)container[0]).Entry.ID);
            Assert.AreEqual("first", ((XeriTrayButton)container[1]).Entry.ID);
            Assert.AreEqual("top", ((XeriTrayButton)container[2]).Entry.ID);
        }

    #endregion

    #region P-3: Core Presentation

        // ----------------------------------------------------------------------
        /// <summary>
        /// Core Presentation Visibility는 Tray Root 표시 여부를 직접 반영한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_PresentationVisibility_Display_반영()
        {
            var panel = new XeriTrayPanel();

            panel.Visibility.Set(false);
            Assert.AreEqual(DisplayStyle.None, panel.style.display.value);

            panel.Visibility.Set(true);
            Assert.AreEqual(DisplayStyle.Flex, panel.style.display.value);
        }

    #endregion

    #region P-4: Option Validation

        // ------------------------------------------------------------
        /// <summary>
        /// 정의되지 않은 ReorderAxis 값은 Panel Reload에서 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_Reload_InvalidReorderAxis_거부()
        {
            var panel = new XeriTrayPanel();
            var options = XeriTrayOptions.Default();
            options.ReorderAxis = (XeriTrayReorderAxis)999;

            Assert.Throws<System.ArgumentOutOfRangeException>
            (
                () => panel.Reload(null, options)
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 정의되지 않은 VisibleContent flag bit는 Panel Reload에서 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_Reload_UnknownVisibleContentFlag_거부()
        {
            var panel = new XeriTrayPanel();
            var options = XeriTrayOptions.Default();
            options.VisibleContent = (XeriTrayContent)(1 << 12);

            Assert.Throws<System.ArgumentOutOfRangeException>
            (
                () => panel.Reload(null, options)
            );
        }

    #endregion

    #region V-1: Icon Only

        // ------------------------------------------------------------
        /// <summary>
        /// VisibleContent가 Icon이면 title과 close button은 숨겨진다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayButton_VisibleContent_Icon_Title_CloseButton_숨김()
        {
            var entry = new XeriTrayEntry("id", "Title")
            {
                CanClose = true,
            };
            var options = new XeriTrayOptions
            {
                VisibleContent = XeriTrayContent.Icon,
            };

            var button = new XeriTrayButton(entry, options);

            Assert.AreEqual(DisplayStyle.Flex, button.Q("entry-icon").style.display.value);
            Assert.AreEqual(DisplayStyle.None, button.Q("entry-title").style.display.value);
            Assert.AreEqual(DisplayStyle.None, button.Q("entry-close-button").style.display.value);
        }

    #endregion

    #region V-2: Badge

        // ------------------------------------------------------------
        /// <summary>
        /// Badge 표시 옵션과 badge 텍스트가 있으면 badge label이 표시된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayButton_VisibleContent_Badge_Badge_표시()
        {
            var entry = new XeriTrayEntry("id", "Title")
            {
                Badge = new XeriTrayBadge("3", Color.red),
            };
            var options = new XeriTrayOptions
            {
                VisibleContent = XeriTrayContent.Badge,
            };

            var button = new XeriTrayButton(entry, options);
            var badge = button.Q<Label>("entry-badge");

            Assert.AreEqual("3", badge.text);
            Assert.AreEqual(DisplayStyle.Flex, badge.style.display.value);
        }

    #endregion

    #region V-3: State Marker

        // ----------------------------------------------------------------------
        /// <summary>
        /// StateMarker 표시 옵션과 active 상태가 있으면 state marker가 표시된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayButton_VisibleContent_StateMarker_Active_표시()
        {
            var entry = new XeriTrayEntry("id", "Title")
            {
                IsActive = true,
            };
            var options = new XeriTrayOptions
            {
                VisibleContent = XeriTrayContent.StateMarker,
            };

            var button = new XeriTrayButton(entry, options);

            Assert.AreEqual(DisplayStyle.Flex, button.Q("entry-state-marker").style.display.value);
        }

    #endregion

    }
}
