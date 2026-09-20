/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayButton.cs
수정일 : 2026-09-20

# 설명
공통 Tray entry 하나를 표시하는 UITK VisualElement.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// 공통 Tray entry 하나를 표시하는 UITK 버튼 view.
    /// </summary>
    // ============================================================
    public sealed class XeriTrayButton : VisualElement
    {

    #region 필드

        private const string TRAY_BUTTON_UXML_PATH = "XeriUI/Tray/XeriTrayButton";
        private const string TRAY_BUTTON_USS_PATH  = "XeriUI/Tray/XeriTrayButton";

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 표시 중인 entry.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayEntry Entry => entry;

        private XeriTrayEntry entry = null;

        private readonly VisualElement iconElement = null;
        private readonly Label titleLabel = null;
        private readonly Label badgeLabel = null;
        private readonly VisualElement stateMarker = null;
        private readonly Button closeButton = null;

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

    #endregion

    #region 생성자

        public XeriTrayButton() : base()
        {
            name = "tray-button";
            AddToClassList("xeri-tray-button");
            LoadStyleSheet();

            CreateElements
            (
                out var createdStateMarker,
                out var createdIconElement,
                out var createdTitleLabel,
                out var createdBadgeLabel,
                out var createdCloseButton
            );

            stateMarker = createdStateMarker;
            iconElement = createdIconElement;
            titleLabel  = createdTitleLabel;
            badgeLabel  = createdBadgeLabel;
            closeButton = createdCloseButton;

            hierarchy.Add(stateMarker);
            hierarchy.Add(iconElement);
            hierarchy.Add(titleLabel);
            hierarchy.Add(badgeLabel);
            hierarchy.Add(closeButton);

            RegisterCallback<ClickEvent>(OnClick);
            closeButton.RegisterCallback<ClickEvent>(OnCloseClick);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Entry와 표시 옵션을 지정해 Tray button을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayButton(XeriTrayEntry entry, XeriTrayOptions options) : this()
        {
            Refresh(entry, options);
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 표시할 entry와 옵션을 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Refresh(XeriTrayEntry entry, XeriTrayOptions options)
        {
            this.entry = entry;

            var content = options != null
                ? options.VisibleContent
                : XeriTrayContent.All;

            tooltip = entry != null ? entry.Tooltip : string.Empty;

            RefreshIcon(content);
            RefreshTitle(content);
            RefreshBadge(content);
            RefreshStateMarker(content);
            RefreshCloseButton(content);
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Tray button USS를 Resources에서 로드해 현재 element에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void LoadStyleSheet()
        {
            var styleSheet = Resources.Load<StyleSheet>(TRAY_BUTTON_USS_PATH);

            if (styleSheet == null)
            {
                throw new InvalidOperationException($"XeriTrayButton USS를 로드할 수 없습니다. Path: {TRAY_BUTTON_USS_PATH}");
            }

            styleSheets.Add(styleSheet);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray button UXML에서 주요 element를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CreateElements
        (
            out VisualElement stateMarker,
            out VisualElement iconElement,
            out Label titleLabel,
            out Label badgeLabel,
            out Button closeButton
        )
        {
            var template = Resources.Load<VisualTreeAsset>(TRAY_BUTTON_UXML_PATH);

            if (template == null)
            {
                throw new InvalidOperationException($"XeriTrayButton UXML을 로드할 수 없습니다. Path: {TRAY_BUTTON_UXML_PATH}");
            }

            var tree = template.CloneTree();

            stateMarker = tree.Q<VisualElement>("entry-state-marker");
            iconElement = tree.Q<VisualElement>("entry-icon");
            titleLabel  = tree.Q<Label>("entry-title");
            badgeLabel  = tree.Q<Label>("entry-badge");
            closeButton = tree.Q<Button>("entry-close-button");

            if
            (
                stateMarker == null || iconElement == null || titleLabel == null ||
                badgeLabel == null || closeButton == null
            )
            {
                throw new InvalidOperationException("XeriTrayButton UXML에 필수 element가 없습니다.");
            }

            stateMarker.RemoveFromHierarchy();
            iconElement.RemoveFromHierarchy();
            titleLabel.RemoveFromHierarchy();
            badgeLabel.RemoveFromHierarchy();
            closeButton.RemoveFromHierarchy();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Icon 표시 상태를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshIcon(XeriTrayContent content)
        {
            var visible = content.HasFlag(XeriTrayContent.Icon);
            iconElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (entry != null && entry.Icon != null)
            {
                iconElement.style.backgroundImage = new StyleBackground(entry.Icon);
                return;
            }

            iconElement.style.backgroundImage = StyleKeyword.Null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Title 표시 상태를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshTitle(XeriTrayContent content)
        {
            titleLabel.text = entry != null ? entry.Title : string.Empty;
            titleLabel.style.display = content.HasFlag(XeriTrayContent.Title)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Badge 표시 상태를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshBadge(XeriTrayContent content)
        {
            var badge = entry != null ? entry.Badge : default;
            var visible = content.HasFlag(XeriTrayContent.Badge) && badge.IsVisible;

            badgeLabel.text = badge.Text ?? string.Empty;
            badgeLabel.style.backgroundColor = badge.Color;
            badgeLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// State marker 표시 상태를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshStateMarker(XeriTrayContent content)
        {
            var visible = content.HasFlag(XeriTrayContent.StateMarker) &&
                          entry != null && entry.IsActive;

            stateMarker.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Close button 표시 상태를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshCloseButton(XeriTrayContent content)
        {
            var visible = content.HasFlag(XeriTrayContent.CloseButton) &&
                          entry != null && entry.CanClose;

            closeButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 선택 이벤트를 발생시킨다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnClick(ClickEvent evt)
        {
            if (entry == null) return;

            OnEntrySelect?.Invoke(this, new XeriTrayEventArgs(entry));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 닫기 이벤트를 발생시킨다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnCloseClick(ClickEvent evt)
        {
            if (entry == null) return;

            evt.StopPropagation();

            OnEntryClose?.Invoke(this, new XeriTrayEventArgs(entry));
        }

    #endregion

    }
}
