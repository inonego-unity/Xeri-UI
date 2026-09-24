/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowApplicationManualUI.cs
수정일 : 2026-09-24

# 설명
Application Window 수동 테스트에서 실제 Screen 전환·Close와 Modal stack을 조작할 UI helper를 제공한다.

# 테스트 구성
 S: Window-local Screen Source
 M: Window-local Modal helper
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.TEST.Window
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// 수동 Application Window용 UITK Screen Source.
    /// </summary>
    // ============================================================
    internal sealed class XeriWindowManualScreenSource : IScreenSource
    {

    #region 필드

        public VisualElement Root { get; private set; }
        public Button NavigateButton { get; private set; }
        public Button ModalButton { get; private set; }
        public Button CloseScreenButton { get; private set; }
        public object DefaultFocus => NavigateButton;
        public int ReleaseCount { get; private set; }

        private readonly string title = null;
        private readonly string navigateText = null;
        private readonly Action navigate = null;
        private readonly Action openModal = null;
        private readonly Action closeScreen = null;

    #endregion

    #region 생성자

        public XeriWindowManualScreenSource
        (
            string title,
            string navigateText,
            Action navigate,
            Action openModal,
            Action closeScreen
        )
        {
            this.title = title ?? throw new ArgumentNullException(nameof(title));
            this.navigateText = navigateText ?? throw new ArgumentNullException(nameof(navigateText));
            this.navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
            this.openModal = openModal ?? throw new ArgumentNullException(nameof(openModal));
            this.closeScreen = closeScreen ?? throw new ArgumentNullException(nameof(closeScreen));
        }

    #endregion

    #region IScreenSource

        public ScreenInstance Acquire(ScreenViewScope scope)
        {
            if (scope.Layer is not IPresentationLayerDriver<VisualElement> layer)
            {
                throw new InvalidOperationException
                (
                    "Application Window Screen Layer가 UITK Root를 제공하지 않습니다."
                );
            }

            Root = new VisualElement
            {
                name = $"TEST_ApplicationScreen_{title}",
            };
            Root.AddToClassList("xeri-window-manual-app");

            var header = new VisualElement();
            header.AddToClassList("xeri-window-manual-app__header");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("xeri-window-manual-app__title");
            var status = new Label("Screen");
            status.AddToClassList("xeri-window-manual-app__status");
            header.Add(titleLabel);
            header.Add(status);

            var description = new Label
            (
                "This content is owned by the Application Window child UIContext."
            );
            description.AddToClassList("xeri-window-manual-app__description");

            var actions = new VisualElement();
            actions.AddToClassList("xeri-window-manual-app__row");

            NavigateButton = new Button(navigate)
            {
                text = navigateText,
            };
            NavigateButton.AddToClassList("xeri-window-manual-app__button");

            ModalButton = new Button(openModal)
            {
                text = "Open Modal",
            };
            ModalButton.AddToClassList("xeri-window-manual-app__button");

            CloseScreenButton = new Button(closeScreen)
            {
                text = "Close Screen",
            };
            CloseScreenButton.AddToClassList("xeri-window-manual-app__button");

            actions.Add(NavigateButton);
            actions.Add(ModalButton);
            actions.Add(CloseScreenButton);

            Root.Add(header);
            Root.Add(description);
            Root.Add(actions);
            layer.Root.Add(Root);

            return new ScreenInstance
            (
                new UITKScreenDriver(Root, NavigateButton)
            );
        }

        public void Release(ScreenInstance instance)
        {
            ReleaseCount++;
            Root?.RemoveFromHierarchy();
            Root = null;
            NavigateButton = null;
            ModalButton = null;
            CloseScreenButton = null;
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// Modal root 안의 Focus containment를 제공한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriWindowManualVisualFocusScope : IFocusScope
    {
        public object DefaultFocus { get; }

        private readonly VisualElement root = null;

        internal XeriWindowManualVisualFocusScope
        (
            VisualElement root,
            object defaultFocus
        )
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            DefaultFocus = defaultFocus;
        }

        public bool ContainsFocus(object target)
        {
            if (target is not VisualElement element)
            {
                return false;
            }

            for (var current = element; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, root))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class XeriWindowManualModalHandle : IDisposable
    {
        public ModalSession Session { get; private set; }
        public Button NestedButton { get; private set; }
        public Button CloseButton { get; private set; }
        public bool IsDisposed => Session == null || Session.IsDisposed;

        internal void Initialize
        (
            ModalSession session,
            Button nestedButton,
            Button closeButton
        )
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            NestedButton = nestedButton;
            CloseButton = closeButton;
        }

        public void Dispose()
        {
            Session?.Dispose();
        }
    }

    // ======================================================================
    /// <summary>
    /// Application Window 내부 Modal을 UITK Common API 경로로 연다.
    /// </summary>
    // ======================================================================
    internal static class XeriWindowApplicationManualUI
    {

    #region M-1: Modal

        internal static XeriWindowManualModalHandle OpenModal
        (
            UIContext context,
            string presentationID,
            string title,
            Action openNested = null
        )
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var modalRoot = new VisualElement
            {
                name = $"TEST_Modal_{title}",
            };
            modalRoot.AddToClassList("xeri-window-manual-modal");

            var card = new VisualElement();
            card.AddToClassList("xeri-window-manual-modal__card");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("xeri-window-manual-modal__title");

            var description = new Label
            (
                "This modal belongs only to this Application Window."
            );
            description.AddToClassList("xeri-window-manual-modal__description");

            var actions = new VisualElement();
            actions.AddToClassList("xeri-window-manual-app__row");

            Button nestedButton = null;

            if (openNested != null)
            {
                nestedButton = new Button(openNested)
                {
                    text = "Open Nested",
                };
                nestedButton.AddToClassList("xeri-window-manual-app__button");
                actions.Add(nestedButton);
            }

            var closeButton = new Button
            {
                text = "Close",
            };
            closeButton.AddToClassList("xeri-window-manual-app__button");
            actions.Add(closeButton);

            card.Add(titleLabel);
            card.Add(description);
            card.Add(actions);
            modalRoot.Add(card);

            var handle = new XeriWindowManualModalHandle();
            var session = UITKModal.OpenWithFocus
            (
                context,
                presentationID,
                modalRoot,
                new XeriWindowManualVisualFocusScope
                (
                    modalRoot,
                    closeButton
                )
            );
            handle.Initialize(session, nestedButton, closeButton);
            closeButton.clicked += handle.Dispose;
            return handle;
        }

    #endregion

    }
}
