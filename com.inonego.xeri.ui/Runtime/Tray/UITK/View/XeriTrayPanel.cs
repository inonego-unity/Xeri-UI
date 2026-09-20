/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayPanel.cs
수정일 : 2026-09-20

# 설명
공통 Tray entry 목록을 표시하는 UITK Tray panel.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// 공통 Tray entry 목록을 표시하는 UITK panel.
    /// </summary>
    // ============================================================
    public sealed class XeriTrayPanel : VisualElement, IXeriTrayRenderer, IXeriTrayReorderTarget
    {

    #region 필드

        private const string TRAY_PANEL_UXML_PATH = "XeriUI/Tray/XeriTrayPanel";
        private const string TRAY_PANEL_USS_PATH  = "XeriUI/Tray/XeriTrayPanel";

        // ------------------------------------------------------------
        /// <summary>
        /// Entry button들을 직접 포함하는 container.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement EntryContainer => entryContainer;

        private readonly VisualElement entryContainer = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 입력 허용 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool Reorderable => reorderable;

        private bool reorderable = false;

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder가 잠기는 이동 축.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayReorderAxis ReorderAxis => reorderAxis;

        private XeriTrayReorderAxis reorderAxis = XeriTrayReorderAxis.Horizontal;

        // ------------------------------------------------------------
        /// <summary>
        /// Preview offset을 적용하는 animator.
        /// </summary>
        // ------------------------------------------------------------
        public IXeriTrayReorderAnimator ReorderAnimator => reorderAnimator;

        private IXeriTrayReorderAnimator reorderAnimator = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder proxy 생성에 사용할 현재 Tray 표시 옵션.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayOptions ReorderOptions => reorderOptions;

        private XeriTrayOptions reorderOptions = XeriTrayOptions.Default();

        private readonly List<XeriTrayButton> entryButtons = new();
        private readonly List<Rect> entryBounds = new();

        private string appliedOptionClass = string.Empty;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 선택 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriTrayEventArgs> OnEntrySelect = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 닫기 입력 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriTrayEventArgs> OnEntryClose = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry reorder 요청 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriTrayReorderEventArgs> OnEntryReorder = null;

    #endregion

    #region 생성자

        public XeriTrayPanel() : base()
        {
            name = "xeri-tray";
            AddToClassList("xeri-tray");
            LoadStyleSheet();

            entryContainer = CreateEntryContainer();
            entryContainer.AddManipulator(new XeriTrayReorderManipulator(this));

            hierarchy.Add(entryContainer);

            SetReorderAnimator(new XeriTrayNoReorderAnimator());
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 목록을 다시 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Reload(IReadOnlyList<XeriTrayEntry> entries, XeriTrayOptions options)
        {
            ApplyOptions(options);

            reorderAnimator?.Clear(this);
            entryContainer.Clear();
            entryButtons.Clear();

            if (entries == null) return;

            foreach (var entry in entries)
            {
                var button = new XeriTrayButton(entry, options);
                button.OnEntrySelect += OnButtonEntrySelect;
                button.OnEntryClose  += OnButtonEntryClose;

                entryButtons.Add(button);
                entryContainer.Add(button);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder preview animator를 지정한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetReorderAnimator(IXeriTrayReorderAnimator animator)
        {
            reorderAnimator?.Clear(this);
            reorderAnimator = animator ?? new XeriTrayNoReorderAnimator();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 표시 중인 Tray button 목록을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<XeriTrayButton> GetEntryButtons()
        {
            return entryButtons;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Entry container 좌표계의 Tray button bounds 목록을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<Rect> GetEntryBounds()
        {
            entryBounds.Clear();

            foreach (var button in entryButtons)
            {
                if (button == null) continue;

                entryBounds.Add(button.layout);
            }

            return entryBounds;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 확정 요청을 상위 계층으로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        public void InvokeEntryReorder(XeriTrayReorderRequest request)
        {
            OnEntryReorder?.Invoke(this, new XeriTrayReorderEventArgs(request));
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Tray panel USS를 Resources에서 로드해 현재 element에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void LoadStyleSheet()
        {
            var styleSheet = Resources.Load<StyleSheet>(TRAY_PANEL_USS_PATH);

            if (styleSheet == null)
            {
                throw new InvalidOperationException($"XeriTrayPanel USS를 로드할 수 없습니다. Path: {TRAY_PANEL_USS_PATH}");
            }

            styleSheets.Add(styleSheet);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray panel UXML에서 entry container를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static VisualElement CreateEntryContainer()
        {
            var template = Resources.Load<VisualTreeAsset>(TRAY_PANEL_UXML_PATH);

            if (template == null)
            {
                throw new InvalidOperationException($"XeriTrayPanel UXML을 로드할 수 없습니다. Path: {TRAY_PANEL_UXML_PATH}");
            }

            var tree = template.CloneTree();
            var container = tree.Q<VisualElement>("entry-container");

            if (container == null)
            {
                throw new InvalidOperationException("XeriTrayPanel UXML에 entry-container가 없습니다.");
            }

            container.RemoveFromHierarchy();

            return container;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray 표시 옵션을 root class와 reorder 설정에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyOptions(XeriTrayOptions options)
        {
            reorderOptions = options ?? XeriTrayOptions.Default();

            if (!string.IsNullOrEmpty(appliedOptionClass))
            {
                RemoveFromClassList(appliedOptionClass);
            }

            appliedOptionClass = reorderOptions.UssClass;
            reorderable = reorderOptions.Reorderable &&
                          reorderOptions.ReorderMode != XeriTrayReorderMode.Disabled;
            reorderAxis = reorderOptions.ReorderAxis;

            SetReorderAnimator
            (
                reorderOptions.AnimateReorder
                    ? new XeriTrayReorderAnimator()
                    : new XeriTrayNoReorderAnimator()
            );

            if (!string.IsNullOrEmpty(appliedOptionClass))
            {
                AddToClassList(appliedOptionClass);
            }
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Button의 entry 선택 이벤트를 panel 이벤트로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnButtonEntrySelect(object sender, XeriTrayEventArgs e)
        {
            OnEntrySelect?.Invoke(this, e);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Button의 entry 닫기 이벤트를 panel 이벤트로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnButtonEntryClose(object sender, XeriTrayEventArgs e)
        {
            OnEntryClose?.Invoke(this, e);
        }

    #endregion

    }
}
