/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationIDs.cs
수정일 : 2026-10-07

# 설명
Xeri UI 통합 Validation Scene이 사용하는 Layer, Presentation, Screen과 Window stable ID를 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// Validation의 Presentation과 Window 식별자를 모은다.
    /// </summary>
    // ============================================================
    internal static class XeriValidationIDs
    {

    #region Presentation과 Window 식별자

        internal const string DesktopLayer = "Desktop";
        internal const string WindowLayer = "Desktop.Windows";
        internal const string OverlayLayer = "Desktop.Overlay";
        internal const string ModalLayer = "Desktop.Modal";
        internal const string SystemLayer = "SystemFade";

        internal const string StatusPresentation = "Desktop.Status";
        internal const string ChecklistPresentation = "Desktop.Checklist";
        internal const string TaskbarPresentation = "Desktop.Taskbar";

        internal const string WindowDestination = "Window";

        internal const string CoreHome = "Core.Home";
        internal const string CoreDetail = "Core.Detail";

        internal const string ApplicationScreenLayer = "Application.Screen";
        internal const string ApplicationModalLayer = "Application.Modal";
        internal const string ApplicationHome = "Application.Home";
        internal const string ApplicationDetail = "Application.Detail";

        internal const string CoreLabWindow = "validation.core-lab";
        internal const string WindowLabWindow = "validation.window-lab";
        internal const string ModalLabWindow = "validation.modal-lab";
        internal const string TaskBoardWindow = "validation.task-board";
        internal const string PreferencesWindow = "validation.preferences";
        internal const string ApplicationAWindow = "validation.application-a";
        internal const string ApplicationBWindow = "validation.application-b";

    #endregion

    }
}
