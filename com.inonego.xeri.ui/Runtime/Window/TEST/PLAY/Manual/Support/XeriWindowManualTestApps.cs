/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowManualTestApps.cs
수정일 : 2026-09-24

# 설명
Window ContentRoot과 Application Screen에서 사용할 수동 테스트용 mini-app UI를 생성한다.

# 테스트 구성
 A: Task Board와 Preferences mini-app
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.TEST.Window
{
    internal sealed class XeriWindowTaskBoardTestApp
    {
        public VisualElement Root { get; }
        public Label Status { get; }

        internal XeriWindowTaskBoardTestApp(VisualElement root, Label status)
        {
            Root = root;
            Status = status;
        }
    }

    internal sealed class XeriWindowPreferencesTestApp
    {
        public VisualElement Root { get; }
        public Label Status { get; }

        internal XeriWindowPreferencesTestApp(VisualElement root, Label status)
        {
            Root = root;
            Status = status;
        }
    }

    // ============================================================
    /// <summary>
    /// 수동 Window 내부에서 사용할 mini-app factory.
    /// </summary>
    // ============================================================
    internal static class XeriWindowManualTestApps
    {

    #region A-1: Task Board

        // ----------------------------------------------------------------------
        /// <summary>
        /// TextField, Toggle, Button과 목록을 가진 Task Board를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal static XeriWindowTaskBoardTestApp CreateTaskBoard()
        {
            var root = new VisualElement
            {
                name = "TEST_TaskBoardApp",
            };
            root.AddToClassList("xeri-window-manual-app");

            var header = new VisualElement();
            header.AddToClassList("xeri-window-manual-app__header");

            var title = new Label("Task Board");
            title.AddToClassList("xeri-window-manual-app__title");

            var status = new Label("2 tasks");
            status.AddToClassList("xeri-window-manual-app__status");
            header.Add(title);
            header.Add(status);

            var inputRow = new VisualElement();
            inputRow.AddToClassList("xeri-window-manual-app__row");

            var input = new TextField
            {
                value = "Check window content",
            };
            input.AddToClassList("xeri-window-manual-app__input");

            var addButton = new Button
            {
                text = "Add",
            };
            addButton.AddToClassList("xeri-window-manual-app__button");

            inputRow.Add(input);
            inputRow.Add(addButton);

            var activeOnly = new Toggle("Active only")
            {
                value = true,
            };
            activeOnly.AddToClassList("xeri-window-manual-app__toggle");

            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("xeri-window-manual-app__list");
            list.Add(new Label("• Verify maximize bounds"));
            list.Add(new Label("• Verify pointer interaction"));

            addButton.clicked += () =>
            {
                var value = input.value?.Trim();

                if (string.IsNullOrEmpty(value)) return;

                list.Add(new Label($"• {value}"));
                status.text = $"{list.childCount} tasks";
                input.value = "";
            };

            root.Add(header);
            root.Add(inputRow);
            root.Add(activeOnly);
            root.Add(list);
            return new XeriWindowTaskBoardTestApp(root, status);
        }

    #endregion

    #region A-2: Preferences

        // ----------------------------------------------------------------------
        /// <summary>
        /// Dropdown, Toggle, Slider와 Apply 동작을 가진 Preferences를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal static XeriWindowPreferencesTestApp CreatePreferences()
        {
            var root = new VisualElement
            {
                name = "TEST_PreferencesApp",
            };
            root.AddToClassList("xeri-window-manual-app");

            var header = new VisualElement();
            header.AddToClassList("xeri-window-manual-app__header");

            var title = new Label("Preferences");
            title.AddToClassList("xeri-window-manual-app__title");

            var status = new Label("Saved");
            status.AddToClassList("xeri-window-manual-app__status");

            header.Add(title);
            header.Add(status);

            var density = new DropdownField
            (
                "Density",
                new List<string>
                {
                    "Compact",
                    "Comfortable",
                    "Spacious",
                },
                1
            );
            density.AddToClassList("xeri-window-manual-app__field");

            var notifications = new Toggle("Notifications")
            {
                value = true,
            };
            notifications.AddToClassList("xeri-window-manual-app__toggle");

            var scale = new Slider("UI Scale", 0.8f, 1.2f)
            {
                value = 1.0f,
            };
            scale.AddToClassList("xeri-window-manual-app__field");

            var applyButton = new Button
            {
                text = "Apply",
            };
            applyButton.AddToClassList("xeri-window-manual-app__button");
            applyButton.clicked += () =>
            {
                status.text =
                    $"{density.value} · {(notifications.value ? "On" : "Off")} · {scale.value:0.0}";
            };

            root.Add(header);
            root.Add(density);
            root.Add(notifications);
            root.Add(scale);
            root.Add(applyButton);
            return new XeriWindowPreferencesTestApp(root, status);
        }

    #endregion

    }
}
