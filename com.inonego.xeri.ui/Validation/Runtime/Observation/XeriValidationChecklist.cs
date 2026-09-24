/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationChecklist.cs
수정일 : 2026-10-07

# 설명
generated Desktop.Checklist placement에 비강제형 Validation coverage 목록을 표시한다.
관찰 가능한 계약이 실제로 행사되면 항목을 기록하지만 사용 흐름이나 다음 동작을 gate하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// 관찰된 동작 목록을 비강제형 진행 표시로 제공한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationChecklist : IDisposable
    {

    #region 필드와 상태

        // ============================================================
        /// <summary>
        /// 기록 가능한 동작의 식별자.
        /// </summary>
        // ============================================================
        internal static class Item
        {

        #region 필드와 상태

            internal const string Host = "host";
            internal const string Taskbar = "taskbar";
            internal const string DesktopInput = "desktop-input";
            internal const string CoreScreen = "core-screen";
            internal const string WindowOpen = "window-open";
            internal const string WindowState = "window-state";
            internal const string TrayActivate = "tray-activate";
            internal const string ApplicationIsolation = "application-isolation";
            internal const string Modal = "modal";
            internal const string Overlay = "overlay";
            internal const string Fade = "fade";

        #endregion

        }

        private readonly VisualElement root = null;
        private readonly Label summary = null;
        private readonly Dictionary<string, Label> labels = new();
        private readonly HashSet<string> completed = new();

        private Lease<VisualElement> placementLease = null;
        private bool isDisposed = false;
        private Lease appearanceBinding = null;

    #endregion

    #region 목록 연결

        // ------------------------------------------------------------
        /// <summary>
        /// 관찰된 동작 목록을 비강제형 진행 표시로 제공한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationChecklist
        (
            UIContext context,
            XeriValidationAssets assets,
            XeriValidationAppearance appearance
        )
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (assets == null || assets.ChecklistTemplate == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            root = XeriValidationUI.CloneRoot
            (
                assets.ChecklistTemplate,
                "ChecklistRoot"
            );
            appearanceBinding = appearance.Bind(root);
            placementLease = context.AcquirePlacement
            (
                XeriValidationIDs.ChecklistPresentation,
                new XeriValidationPlacementSource(root, attach: true)
            );
            summary = XeriValidationUI.Require<Label>(root, "ChecklistSummary");

            Bind(Item.Host, "CheckHost");
            Bind(Item.Taskbar, "CheckTaskbar");
            Bind(Item.DesktopInput, "CheckDesktopInput");
            Bind(Item.CoreScreen, "CheckCoreScreen");
            Bind(Item.WindowOpen, "CheckWindowOpen");
            Bind(Item.WindowState, "CheckWindowState");
            Bind(Item.TrayActivate, "CheckTrayActivate");
            Bind(Item.ApplicationIsolation, "CheckApplicationIsolation");
            Bind(Item.Modal, "CheckModal");
            Bind(Item.Overlay, "CheckOverlay");
            Bind(Item.Fade, "CheckFade");

            RefreshSummary();
        }

    #endregion

    #region 관찰 결과 표시

        // ------------------------------------------------------------
        /// <summary>
        /// 상세 상태 표시를 펼치거나 접는다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SetExpanded(bool expanded)
        {
            root.EnableInClassList
            (
                "xeri-validation-checklist--collapsed",
                !expanded
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 처음 관찰된 항목과 그 근거를 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Mark(string item, string detail = null)
        {
            if (isDisposed || !labels.TryGetValue(item, out var label))
            {
                return;
            }

            completed.Add(item);
            label.text = string.IsNullOrEmpty(detail)
                ? "✓"
                : $"✓ {detail}";
            label.EnableInClassList("xeri-validation-checklist__state--complete", true);
            RefreshSummary();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기록 항목과 표시 요소를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Bind(string item, string elementName)
        {
            labels.Add(item, XeriValidationUI.Require<Label>(root, elementName));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기록된 항목 수를 요약한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshSummary()
        {
            summary.text = $"{completed.Count} / {labels.Count}";
        }

    #endregion

    #region 연결 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 이벤트 연결과 소유한 수명을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            appearanceBinding?.Dispose();
            appearanceBinding = null;
            placementLease?.Dispose();
            placementLease = null;
            labels.Clear();
            completed.Clear();
        }

    #endregion

    }
}
