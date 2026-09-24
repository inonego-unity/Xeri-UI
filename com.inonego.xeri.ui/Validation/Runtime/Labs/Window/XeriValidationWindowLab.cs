/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationWindowLab.cs
수정일 : 2026-10-05

# 설명
Window state, StackLayer와 Tray projection을 한 화면에서 조작하는 instrumented Window Lab.
기본 OS Taskbar와 별도로 Minimized-only production Tray Source를 구성해 Source별 projection 독립성을 시각적으로 확인한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Tray;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// Window 상태와 Tray 투영을 연결한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationWindowLab : IDisposable
    {

    #region 필드와 상태

        // ------------------------------------------------------------
        /// <summary>
        /// Window에 연결할 콘텐츠 Root.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Root { get; }

        private readonly XeriWindowWorkspace workspace = null;
        private readonly IXeriWindowRegistry registry = null;
        private readonly XeriValidationAppearance appearance = null;
        private readonly XeriValidationAssets assets = null;
        private readonly XeriValidationChecklist checklist = null;
        private readonly XeriWindowTraySource minimizedSource = null;
        private readonly XeriTrayPanel minimizedPanel = null;
        private readonly XeriTrayController minimizedController = null;

        private readonly Button openToolButton = null;
        private readonly Button openTopButton = null;
        private readonly Button minimizeButton = null;
        private readonly Button maximizeButton = null;
        private readonly Button normalButton = null;
        private readonly Label status = null;

        private int spawnedWindowIndex = 0;
        private bool isDisposed = false;

    #endregion

    #region 화면과 Tray 연결

        // ------------------------------------------------------------
        /// <summary>
        /// Window 상태와 Tray 투영을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationWindowLab
        (
            XeriWindowWorkspace workspace,
            IXeriWindowRegistry registry,
            XeriValidationAssets assets,
            XeriValidationAppearance appearance,
            XeriValidationChecklist checklist
        )
        {
            this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.checklist = checklist;

            Root = XeriValidationUI.CloneRoot
            (
                assets.WindowLabTemplate,
                "WindowLabRoot"
            );
            assets.ApplyLabStyles(Root);
            openToolButton = XeriValidationUI.Require<Button>(Root, "WindowOpenTool");
            openTopButton = XeriValidationUI.Require<Button>(Root, "WindowOpenAlwaysOnTop");
            minimizeButton = XeriValidationUI.Require<Button>(Root, "WindowMinimizeActive");
            maximizeButton = XeriValidationUI.Require<Button>(Root, "WindowMaximizeActive");
            normalButton = XeriValidationUI.Require<Button>(Root, "WindowNormalActive");
            status = XeriValidationUI.Require<Label>(Root, "WindowLabStatus");
            var projectionHost = XeriValidationUI.Require<VisualElement>
            (
                Root,
                "WindowProjectionHost"
            );

            minimizedSource = workspace.CreateTraySource(XeriWindowTrayStateMask.Minimized);
            minimizedPanel = new XeriTrayPanel();
            minimizedController = new XeriTrayController
            (
                minimizedSource,
                minimizedPanel,
                new XeriTrayOptions
                {
                    VisibleContent =
                        XeriTrayContent.Title |
                        XeriTrayContent.StateMarker,
                    UssClass = "xeri-validation-projection-tray",
                    Reorderable = true,
                    ReorderAxis = XeriTrayReorderAxis.Horizontal,
                    AnimateReorder = true,
                }
            );

            minimizedController.OnEntrySelect += OnProjectionEntrySelect;
            minimizedController.OnEntryClose += OnProjectionEntryClose;
            minimizedPanel.OnEntryReorder += OnProjectionEntryReorder;
            projectionHost.Add(minimizedPanel);
            minimizedController.Reload();

            openToolButton.clicked += OpenToolWindow;
            openTopButton.clicked += OpenAlwaysOnTopWindow;
            minimizeButton.clicked += MinimizeActive;
            maximizeButton.clicked += MaximizeActive;
            normalButton.clicked += ShowNormalActive;
            status.text = "Taskbar: all windows · Secondary list: minimized only";
        }

    #endregion

    #region Utility Window 생성

        // ------------------------------------------------------------
        /// <summary>
        /// 일반 Utility Window를 연다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenToolWindow()
        {
            OpenUtilityWindow(false);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 최상단 Utility Window를 연다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenAlwaysOnTopWindow()
        {
            OpenUtilityWindow(true);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 ID와 충돌하지 않는 창을 연다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenUtilityWindow(bool alwaysOnTop)
        {
            string id;
            do
            {
                spawnedWindowIndex++;
                id = $"validation.window-lab.spawn-{spawnedWindowIndex}";
            }
            while (registry.TryGetHandle(id, out _));
            var view = new VisualElement();
            view.AddToClassList("xeri-validation-app");
            assets.ApplyAppStyles(view);
            view.Add(new Label(alwaysOnTop ? "Always On Top utility" : "Utility Window"));
            var field = new TextField("Editable value")
            {
                value = $"Window {spawnedWindowIndex}",
            };
            view.Add(field);

            var options = XeriWindowOptions.Default();

            if (alwaysOnTop)
            {
                options.StackLayer = XeriWindowStackLayer.AlwaysOnTop;
            }

            var record = new XeriWindowRecord
            {
                ID = id,
                Title = alwaysOnTop ? $"Top Utility {spawnedWindowIndex}" : $"Utility {spawnedWindowIndex}",
                Tooltip = "Window Lab generated utility",
                Pos = new Vector2(300f + spawnedWindowIndex * 18f, 180f + spawnedWindowIndex * 16f),
                Size = new Vector2(330f, 220f),
                NormalPos = new Vector2(300f + spawnedWindowIndex * 18f, 180f + spawnedWindowIndex * 16f),
                NormalSize = new Vector2(330f, 220f),
                StackLayer = options.StackLayer,
                ThemeID = alwaysOnTop
                    ? XeriWindowThemeClass.MinimalID
                    : XeriWindowThemeClass.WindowsID,
            };

            var session = workspace.OpenWindow(record, view, options);
            session.RegisterChild(appearance.Bind(session.Panel));
            status.text = alwaysOnTop ? "AlwaysOnTop Window opened" : "Utility Window opened";
            checklist?.Mark(XeriValidationChecklist.Item.WindowOpen, "Window Lab");
        }

    #endregion

    #region 활성 창 상태 전환

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 창의 최소화를 요청한다.
        /// </summary>
        // ------------------------------------------------------------
        private void MinimizeActive()
        {
            if (!TryGetActive(out var session))
            {
                return;
            }

            session.Controller.Minimize();
            status.text = $"{session.Handle.ID} → Minimized";

        }

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 창의 최대화를 요청한다.
        /// </summary>
        // ------------------------------------------------------------
        private void MaximizeActive()
        {
            if (!TryGetActive(out var session))
            {
                return;
            }

            session.Controller.Maximize();
            status.text = $"{session.Handle.ID} → Maximized";

        }

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 창의 일반 상태를 요청한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ShowNormalActive()
        {
            if (!TryGetActive(out var session))
            {
                return;
            }

            session.Controller.ShowNormal();
            session.Focus();
            status.text = $"{session.Handle.ID} → Normal";

        }

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 Handle에 대응하는 세션을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private bool TryGetActive(out XeriWindowSession session)
        {
            if (workspace.TryGetSession(registry.ActiveHandle, out session)) return true;

            session = null;
            status.text = "No active window";
            return false;
        }

    #endregion

    #region Tray 투영 입력

        // ------------------------------------------------------------
        /// <summary>
        /// Tray 항목의 창을 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnProjectionEntrySelect(object sender, XeriTrayEventArgs e)
        {
            minimizedSource.Activate(e.Entry);
            status.text = "Minimized projection entry activated";
            checklist?.Mark(XeriValidationChecklist.Item.TrayActivate);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray 항목의 창을 닫는다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnProjectionEntryClose(object sender, XeriTrayEventArgs e)
        {
            minimizedSource.Close(e.Entry);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 변경된 순서를 Source에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnProjectionEntryReorder(object sender, XeriTrayReorderEventArgs e)
        {
            if (e?.Entry?.Payload is not XeriWindowHandle handle)
            {
                return;
            }

            minimizedSource.MoveEntry(handle, e.TargetIndex);
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
            openToolButton.clicked -= OpenToolWindow;
            openTopButton.clicked -= OpenAlwaysOnTopWindow;
            minimizeButton.clicked -= MinimizeActive;
            maximizeButton.clicked -= MaximizeActive;
            normalButton.clicked -= ShowNormalActive;

            minimizedController.OnEntrySelect -= OnProjectionEntrySelect;
            minimizedController.OnEntryClose -= OnProjectionEntryClose;
            minimizedPanel.OnEntryReorder -= OnProjectionEntryReorder;
            minimizedPanel.RemoveFromHierarchy();
            minimizedController.Dispose();
            minimizedSource.Dispose();
            Root.RemoveFromHierarchy();
        }

    #endregion

    }
}
