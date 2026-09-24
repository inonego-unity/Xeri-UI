/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationTaskbarBinding.cs
수정일 : 2026-10-07

# 설명
Window Workspace Registry를 production XeriWindowTraySource → XeriTrayController → XeriTrayPanel 경로로 authored Taskbar placement에 연결한다.
Taskbar는 모든 live Window state를 표시하며 선택은 활성화/최소화/이전 상태 복원, 닫기는 Close, drag reorder는 Source order 변경으로 전달한다.
직계 자식인 authored Placement 내부에 가로 ScrollView를 획득하고 그 콘텐츠로 Tray를 배치한다.
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
    /// Window 목록을 authored Taskbar 내부의 Tray에 연결한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationTaskbarBinding : IDisposable
    {

    #region 상태

        // ------------------------------------------------------------
        /// <summary>
        /// 전체 live Window 상태를 제공하는 Tray source.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowTraySource Source => source;
        private readonly XeriWindowTraySource source = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 스크롤 콘텐츠로 표시하는 Tray view.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayPanel Panel => panel;
        private readonly XeriTrayPanel panel = null;

        private readonly IXeriWindowRegistry registry = null;
        private readonly XeriTrayController controller = null;
        private readonly XeriValidationChecklist checklist = null;
        private Lease<VisualElement> placementLease = null;
        private bool isDisposed = false;

    #endregion

    #region 연결과 해제

        // ------------------------------------------------------------
        /// <summary>
        /// authored Placement 안에 scroll과 Tray의 수명을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationTaskbarBinding
        (
            UIContext context,
            XeriWindowWorkspace workspace,
            IXeriWindowRegistry registry,
            XeriValidationChecklist checklist
        ) : base()
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.checklist = checklist;
            source = workspace.CreateTraySource(XeriWindowTrayStateMask.All);
            panel = new XeriTrayPanel();
            // Placement topology는 유지하고 overflow만 획득한 view 내부에서 처리한다.
            var scroll = new ScrollView(ScrollViewMode.Horizontal)
            {
                name = "DesktopTaskbarScroll",
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Auto,
            };
            scroll.AddToClassList("xeri-validation-taskbar");
            scroll.Add(panel);
            placementLease = context.AcquirePlacement
            (
                XeriValidationIDs.TaskbarPresentation,
                new XeriValidationPlacementSource(scroll, attach: true)
            );
            var options = new XeriTrayOptions
            {
                VisibleContent =
                    XeriTrayContent.Title |
                    XeriTrayContent.CloseButton,
                UssClass = "xeri-validation-taskbar__tray",
                Reorderable = true,
                ReorderAxis = XeriTrayReorderAxis.Horizontal,
                AnimateReorder = true,
            };
            controller = new XeriTrayController(source, panel, options);

            controller.OnEntrySelect += OnEntrySelect;
            controller.OnEntryClose += OnEntryClose;
            panel.OnEntryReorder += OnEntryReorder;
            controller.Reload();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 구독과 Tray를 해제한 뒤 scroll view를 Placement에서 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            // 닫기와 선택 콜백이 해제 중인 source에 접근하지 않도록 먼저 끊는다.
            controller.OnEntrySelect -= OnEntrySelect;
            controller.OnEntryClose -= OnEntryClose;
            panel.OnEntryReorder -= OnEntryReorder;

            controller.Dispose();
            source.Dispose();
            placementLease?.Dispose();
            placementLease = null;
        }

    #endregion

    #region Tray 입력

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 Window는 최소화하고 나머지는 활성화 또는 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnEntrySelect(object sender, XeriTrayEventArgs e)
        {
            if (e?.Entry?.Payload is not XeriWindowHandle handle) return;
            if (!registry.TryGetController(handle, out var window)) return;

            // 진행 중인 상태 전환도 고려해 재클릭을 반대 동작으로 연결한다.
            if
            (
                ReferenceEquals(registry.ActiveHandle, handle) &&
                window.EffectiveState != XeriWindowState.Minimized &&
                window.EffectiveState != XeriWindowState.Closed &&
                window.Options.CanMinimize
            )
            {
                window.Minimize();
            }
            else
            {
                // Restore는 최소화 전 Normal/Maximized 상태를 보존한다.
                source.Activate(e.Entry);
            }

            checklist?.Mark(XeriValidationChecklist.Item.TrayActivate, e.Entry.Title);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 해당 entry의 Window만 닫는다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnEntryClose(object sender, XeriTrayEventArgs e)
        {
            source.Close(e.Entry);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 드래그한 entry 순서를 실제 source에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnEntryReorder(object sender, XeriTrayReorderEventArgs e)
        {
            if (e?.Entry?.Payload is not XeriWindowHandle handle)
            {
                return;
            }

            source.MoveEntry(handle, e.TargetIndex);
        }

    #endregion

    }
}
