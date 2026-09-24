/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayPanel.cs
수정일 : 2026-10-03

# 설명
공통 Tray entry 목록을 표시하고 UI Core Presentation 상태를 제공하는 UITK Tray panel.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// 공통 Tray entry 목록을 표시하는 UITK panel.
    /// </summary>
    // ============================================================
    public sealed class XeriTrayPanel :
        VisualElement,
        IXeriTrayRenderer,
        IXeriTrayReorderTarget,
        IPresentation
    {

    #region 필드

        private const string TRAY_PANEL_UXML_PATH = "XeriUI/Tray/XeriTrayPanel";
        private const string TRAY_PANEL_USS_PATH  = "XeriUI/Tray/XeriTrayPanelStyles";

        // ------------------------------------------------------------
        /// <summary>
        /// Tray Root의 합성 Alpha State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha Alpha => presentation.Alpha;

        // ------------------------------------------------------------
        /// <summary>
        /// Tray Root의 합성 Visibility State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationVisibility Visibility => presentation.Visibility;

        private readonly UITKPresentation presentation = null;

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
        private IXeriTrayReorderAnimator customReorderAnimator = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder proxy 생성에 사용할 현재 Tray 표시 옵션.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayOptions ReorderOptions => reorderOptions;

        private XeriTrayOptions reorderOptions = XeriTrayOptions.Default();

        private readonly XeriTrayReorderManipulator reorderManipulator = null;
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
            presentation = new UITKPresentation(this);
            LoadStyleSheet();

            entryContainer = CreateEntryContainer();
            reorderManipulator = new XeriTrayReorderManipulator(this);
            entryContainer.AddManipulator(reorderManipulator);

            hierarchy.Add(entryContainer);

            ReplaceReorderAnimator(new XeriTrayNoReorderAnimator());
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
            reorderManipulator.CancelActive();
            ApplyOptions(options);

            UnbindEntryButtons();
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
            var nextAnimator = animator ??
                CreateDefaultReorderAnimator(reorderOptions);

            ReplaceReorderAnimator(nextAnimator);
            customReorderAnimator = animator;
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
            InvokeHandlers(OnEntryReorder, new XeriTrayReorderEventArgs(request));
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 options에 맞는 기본 reorder animator를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static IXeriTrayReorderAnimator CreateDefaultReorderAnimator
        (
            XeriTrayOptions options
        )
        {
            return options.AnimateReorder
                ? new XeriTrayReorderAnimator()
                : new XeriTrayNoReorderAnimator();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Active reorder를 취소한 뒤 실제 animator를 교체한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReplaceReorderAnimator(IXeriTrayReorderAnimator animator)
        {
            reorderManipulator?.CancelActive();
            reorderAnimator?.Clear(this);
            reorderAnimator = animator ??
                throw new ArgumentNullException(nameof(animator));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 Tray button의 Panel 이벤트 연결을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnbindEntryButtons()
        {
            foreach (var button in entryButtons)
            {
                if (button == null) continue;

                button.OnEntrySelect -= OnButtonEntrySelect;
                button.OnEntryClose -= OnButtonEntryClose;
            }
        }

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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Tray 표시와 reorder에 사용하는 enum 옵션 값이 정의된 범위인지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ValidateOptions(XeriTrayOptions options)
        {
            if (!Enum.IsDefined(typeof(XeriTrayReorderAxis), options.ReorderAxis))
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(options),
                    options.ReorderAxis,
                    "정의되지 않은 Tray reorder axis입니다."
                );
            }

            var unknownContent = options.VisibleContent & ~XeriTrayContent.All;

            if (unknownContent != XeriTrayContent.None)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(options),
                    options.VisibleContent,
                    "정의되지 않은 Tray content flag가 포함되어 있습니다."
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray 표시 옵션을 root class와 reorder 설정에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyOptions(XeriTrayOptions options)
        {
            var nextOptions = options ?? XeriTrayOptions.Default();
            ValidateOptions(nextOptions);

            if (customReorderAnimator == null)
            {
                ReplaceReorderAnimator
                (
                    CreateDefaultReorderAnimator(nextOptions)
                );
            }

            if (!string.IsNullOrEmpty(appliedOptionClass))
            {
                RemoveFromClassList(appliedOptionClass);
            }

            reorderOptions = nextOptions;
            appliedOptionClass = nextOptions.UssClass;
            reorderable = nextOptions.Reorderable;
            reorderAxis = nextOptions.ReorderAxis;

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
            InvokeHandlers(OnEntrySelect, e);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Button의 entry 닫기 이벤트를 panel 이벤트로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnButtonEntryClose(object sender, XeriTrayEventArgs e)
        {
            InvokeHandlers(OnEntryClose, e);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Panel observer를 독립적으로 호출하고 모든 실패를 호출자에게 함께 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void InvokeHandlers<TEventArgs>
        (
            EventHandler<TEventArgs> handlers,
            TEventArgs eventArgs
        )
        where TEventArgs : EventArgs
        {
            if (handlers == null) return;

            var errors = new List<Exception>();

            foreach (EventHandler<TEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, eventArgs);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "Tray Panel 이벤트 처리 중 하나 이상의 observer가 실패했습니다.",
                errors
            );
        }

    #endregion

    }
}
