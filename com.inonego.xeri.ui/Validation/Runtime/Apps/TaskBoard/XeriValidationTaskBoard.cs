/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationTaskBoard.cs
수정일 : 2026-10-05

# 설명
Validation Desktop에서 일반 사용 흐름을 제공하는 Task Board natural application.
Window 이동·크기 조절·Focus와 별개로 TextField, Button, Toggle 등 실제 UITK Control 입력을 행사한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// 작업 항목의 입력과 완료 상태를 관리한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationTaskBoard : IDisposable
    {

    #region 화면과 상태

        // ------------------------------------------------------------
        /// <summary>
        /// Task Board의 표시 Root.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Root { get; }

        private readonly TextField input = null;
        private readonly Button addButton = null;
        private readonly VisualElement list = null;
        private readonly Label status = null;
        private readonly List<Toggle> tasks = new();

        private bool isDisposed = false;

    #endregion

    #region 화면 연결과 정리

        // ------------------------------------------------------------
        /// <summary>
        /// 앱 화면과 입력 이벤트를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationTaskBoard
        (
            XeriValidationAssets assets,
            XeriValidationChecklist checklist
        )
        {
            if (assets == null || assets.TaskBoardTemplate == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            Root = XeriValidationUI.CloneRoot
            (
                assets.TaskBoardTemplate,
                "TaskBoardRoot"
            );
            assets.ApplyAppStyles(Root);
            input = XeriValidationUI.Require<TextField>(Root, "TaskInput");
            addButton = XeriValidationUI.Require<Button>(Root, "TaskAdd");
            list = XeriValidationUI.Require<VisualElement>(Root, "TaskList");
            status = XeriValidationUI.Require<Label>(Root, "TaskStatus");

            addButton.clicked += AddTask;
            AddTask("Review validation topology");
            AddTask("Exercise window interactions");
            input.value = "Check free-form interaction";
            RefreshStatus();
            checklist?.Mark(XeriValidationChecklist.Item.WindowOpen, "Task Board");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 빈 입력을 제외하고 새 항목을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AddTask()
        {
            var text = input.value?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            AddTask(text);
            input.value = string.Empty;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 체크 표시와 문구가 함께 있는 작업 항목을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AddTask(string text)
        {
            // 항목 문구를 체크 표시와 같은 입력 행에 둔다.
            var toggle = new Toggle
            {
                text = text,
            };
            toggle.AddToClassList("xeri-validation-app__task");
            toggle.RegisterValueChangedCallback(_ => RefreshStatus());
            tasks.Add(toggle);
            list.Add(toggle);
            RefreshStatus();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 완료한 항목 수를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshStatus()
        {
            var complete = 0;

            foreach (var task in tasks)
            {
                if (task.value)
                {
                    complete++;
                }
            }

            status.text = $"{complete} / {tasks.Count} complete";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이벤트와 화면 연결을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            addButton.clicked -= AddTask;
            Root.RemoveFromHierarchy();
            tasks.Clear();
        }

    #endregion

    }
}
