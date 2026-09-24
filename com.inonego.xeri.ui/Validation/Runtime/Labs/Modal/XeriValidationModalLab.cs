/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationModalLab.cs
수정일 : 2026-10-07

# 설명
Root UIContext의 global Modal stack과 nested focus 복원을 자유롭게 조작하는 instrumented Modal Lab.
Modal Host destination을 실제 UITKModal 경로로 사용하며 close 순서와 현재 depth를 화면에 표시한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// Root Modal 조작과 깊이 표시를 연결한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationModalLab : IDisposable
    {

    #region 필드와 상태

        // ------------------------------------------------------------
        /// <summary>
        /// Lab 콘텐츠를 표시하는 루트.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Root { get; }

        private readonly UIContext context = null;
        private readonly XeriValidationAssets assets = null;
        private readonly XeriValidationAppearance appearance = null;
        private readonly XeriValidationChecklist checklist = null;
        private readonly Button openButton = null;
        private readonly Button nestedButton = null;
        private readonly Button closeTopButton = null;
        private readonly Label status = null;

        private XeriValidationModalHandle modal = null;
        private XeriValidationModalHandle nested = null;
        private bool isDisposed = false;

    #endregion

    #region 화면 연결

        // ------------------------------------------------------------
        /// <summary>
        /// Root Modal 조작과 깊이 표시를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationModalLab
        (
            UIContext context,
            XeriValidationAssets assets,
            XeriValidationAppearance appearance,
            XeriValidationChecklist checklist
        ) : base()
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            this.checklist = checklist;

            Root = XeriValidationUI.CloneRoot
            (
                assets.ModalLabTemplate,
                "ModalLabRoot"
            );
            assets.ApplyLabStyles(Root);
            openButton = XeriValidationUI.Require<Button>(Root, "ModalOpen");
            nestedButton = XeriValidationUI.Require<Button>(Root, "ModalNested");
            closeTopButton = XeriValidationUI.Require<Button>(Root, "ModalCloseTop");
            status = XeriValidationUI.Require<Label>(Root, "ModalLabStatus");

            context.Modals.OnStackChanged += Refresh;
            openButton.clicked += OpenModal;
            nestedButton.clicked += OpenNested;
            closeTopButton.clicked += CloseTop;
            Refresh();
        }

    #endregion

    #region Modal 조작과 관찰

        // ------------------------------------------------------------
        /// <summary>
        /// 첫 Modal을 열고 깊이를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenModal()
        {
            if (modal != null && !modal.IsDisposed)
            {
                Refresh();
                return;
            }

            modal = XeriValidationModal.Open
            (
                context,
                PresentationTarget.Host(PresentationDestinationID.Modal),
                assets,
                appearance,
                "Global Modal A",
                "This Modal belongs to the root Validation UIContext.",
                OpenNested
            );
            checklist?.Mark(XeriValidationChecklist.Item.Modal, "Depth 1");
            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 중첩 Modal을 열고 깊이를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenNested()
        {
            if (modal == null || modal.IsDisposed)
            {
                OpenModal();
            }

            if (nested != null && !nested.IsDisposed)
            {
                Refresh();
                return;
            }

            nested = XeriValidationModal.Open
            (
                context,
                PresentationTarget.Host(PresentationDestinationID.Modal),
                assets,
                appearance,
                "Global Modal B",
                "Closing this top modal should restore focus to Modal A."
            );
            checklist?.Mark(XeriValidationChecklist.Item.Modal, "Nested");
            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Lab에서 연 최상위 Modal을 닫는다.
        /// </summary>
        // ------------------------------------------------------------
        private void CloseTop()
        {
            if (nested != null && !nested.IsDisposed)
            {
                nested.Dispose();
                nested = null;
                Refresh();
                return;
            }

            if (modal != null && !modal.IsDisposed)
            {
                modal.Dispose();
                modal = null;
            }

            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 Modal 스택 깊이를 화면에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Refresh()
        {
            // Lab 종료 중 발생하는 Modal 해제는 표시를 갱신하지 않는다.
            if (isDisposed) return;

            status.text = $"Root modal depth · {context.Modals.Count}";
        }

    #endregion

    #region 연결 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 구독과 소유 수명을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            context.Modals.OnStackChanged -= Refresh;
            openButton.clicked -= OpenModal;
            nestedButton.clicked -= OpenNested;
            closeTopButton.clicked -= CloseTop;
            nested?.Dispose();
            modal?.Dispose();
            nested = null;
            modal = null;
            Root.RemoveFromHierarchy();
        }

    #endregion

    }
}
