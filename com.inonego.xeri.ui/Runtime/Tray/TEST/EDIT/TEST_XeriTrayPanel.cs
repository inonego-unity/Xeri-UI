/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriTrayPanel.cs
수정일 : 2026-10-04

# 설명
공통 UITK Tray panel/button 표시 테스트.

# 테스트 구성
 P: Panel entry 생성과 Core Presentation
 V: VisibleContent 표시 조합
========================================================================= BLOCK_HEADER_END */

using System;

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

    #region 헬퍼

        private sealed class TestReorderAnimator : IXeriTrayReorderAnimator
        {
            public bool ThrowOnClear { get; set; }

            public void Preview
            (
                IXeriTrayReorderTarget target,
                XeriTrayReorderSession session
            )
            {
                // NONE
            }

            public void Commit
            (
                IXeriTrayReorderTarget target,
                XeriTrayReorderSession session
            )
            {
                Clear(target);
            }

            public void Cancel
            (
                IXeriTrayReorderTarget target,
                XeriTrayReorderSession session
            )
            {
                Clear(target);
            }

            public void Clear(IXeriTrayReorderTarget target)
            {
                if (ThrowOnClear)
                {
                    throw new InvalidOperationException
                    (
                        "injected reorder animator clear failure"
                    );
                }
            }
        }

    #endregion

    #region P-1: 항목 생성

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

    #region P-2: 항목 순서

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

    #region P-3: 코어 프레젠테이션 연동

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

    #region P-4: 옵션 검증

        // ------------------------------------------------------------
        /// <summary>
        /// 정의되지 않은 ReorderAxis 값은 Panel Reload에서 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_Reload_InvalidReorderAxis_거부()
        {
            var panel = new XeriTrayPanel();
            var previous = XeriTrayOptions.Default();
            previous.Reorderable = true;
            previous.ReorderAxis = XeriTrayReorderAxis.Vertical;
            previous.AnimateReorder = false;
            panel.Reload(null, previous);
            var previousAnimator = panel.ReorderAnimator;
            var options = XeriTrayOptions.Default();
            options.ReorderAxis = (XeriTrayReorderAxis)999;

            Assert.Throws<System.ArgumentOutOfRangeException>
            (
                () => panel.Reload(null, options)
            );

            Assert.AreSame(previous, panel.ReorderOptions);
            Assert.IsTrue(panel.Reorderable);
            Assert.AreEqual(XeriTrayReorderAxis.Vertical, panel.ReorderAxis);
            Assert.AreSame(previousAnimator, panel.ReorderAnimator);
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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Animator 교체 정리 실패는 기존 animator를 유지하고 다음 교체 재시도를 허용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_SetReorderAnimator_Clear실패_기존Animator유지()
        {
            var panel = new XeriTrayPanel();
            var current = new TestReorderAnimator();
            var replacement = new TestReorderAnimator();
            panel.SetReorderAnimator(current);
            current.ThrowOnClear = true;

            Assert.Throws<InvalidOperationException>
            (
                () => panel.SetReorderAnimator(replacement)
            );

            Assert.AreSame(current, panel.ReorderAnimator);

            current.ThrowOnClear = false;
            Assert.DoesNotThrow
            (
                () => panel.SetReorderAnimator(replacement)
            );
            Assert.AreSame(replacement, panel.ReorderAnimator);
        }

    #endregion

    #region P-5: 관찰자 격리

        // ----------------------------------------------------------------------
        /// <summary>
        /// Reorder observer 하나가 실패해도 뒤 observer까지 같은 요청을 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_ReorderObserver실패_다음Observer호출()
        {
            var panel = new XeriTrayPanel();
            var entry = new XeriTrayEntry("id", "Title");
            var observerCount = 0;
            panel.OnEntryReorder += (_, _) =>
            {
                throw new InvalidOperationException("injected reorder observer failure");
            };
            panel.OnEntryReorder += (_, _) => observerCount++;

            Assert.Throws<InvalidOperationException>
            (
                () => panel.InvokeEntryReorder
                (
                    new XeriTrayReorderRequest(entry, 0, 1)
                )
            );

            Assert.AreEqual(1, observerCount);
        }

    #endregion

    #region V-1: 아이콘 전용

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

    #region V-2: 배지

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

    #region V-3: 상태 표시

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
