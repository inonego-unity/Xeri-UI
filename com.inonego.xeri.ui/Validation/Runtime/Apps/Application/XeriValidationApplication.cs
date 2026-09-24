/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationApplication.cs
수정일 : 2026-10-07

# 설명
Application Window의 Child PresentationSession과 Child UIContext 안에서 Home/Detail Screen, nested Modal과 Focus 복원을 행사한다.
Application A/B는 같은 Layout과 같은 Screen ID를 공유하지만 각 Window가 독립된 Context와 stack을 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// 독립 Context의 Screen 탐색과 Modal 수명을 연결한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationApplication : IDisposable
    {

    #region 필드와 상태

        private readonly XeriWindowSession window = null;
        private readonly XeriValidationAssets assets = null;
        private readonly XeriValidationAppearance appearance = null;
        private readonly XeriValidationApplicationScreenSource homeSource = null;
        private readonly XeriValidationApplicationScreenSource detailSource = null;

        private ScreenRegistrationHandle homeRegistration = null;
        private ScreenRegistrationHandle detailRegistration = null;
        private XeriValidationModalHandle modal = null;
        private XeriValidationModalHandle nestedModal = null;
        private bool isDisposed = false;

    #endregion

    #region Application 연결

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 Context의 Screen 탐색과 Modal 수명을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationApplication
        (
            XeriWindowSession window,
            XeriValidationAssets assets,
            XeriValidationAppearance appearance,
            string applicationName
        )
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));

            if (window.Context == null)
            {
                throw new InvalidOperationException
                (
                    "Validation Application Window에 Child UIContext가 없습니다."
                );
            }

            homeSource = new XeriValidationApplicationScreenSource
            (
                assets,
                applicationName,
                "HOME",
                "Go Detail",
                () => window.Context.Screens.Replace(XeriValidationIDs.ApplicationDetail),
                OpenModal,
                () => window.Context.Screens.Close()
            );
            detailSource = new XeriValidationApplicationScreenSource
            (
                assets,
                applicationName,
                "DETAIL",
                "Back Home",
                () => window.Context.Screens.Replace(XeriValidationIDs.ApplicationHome),
                OpenModal,
                () => window.Context.Screens.Close()
            );

            homeRegistration = window.Context.RegisterScreen
            (
                new ScreenOptions
                (
                    XeriValidationIDs.ApplicationHome,
                    openDuration: 0.12f,
                    closeDuration: 0.1f
                ),
                PresentationTarget.Local(XeriValidationIDs.ApplicationScreenLayer),
                homeSource
            );
            detailRegistration = window.Context.RegisterScreen
            (
                new ScreenOptions
                (
                    XeriValidationIDs.ApplicationDetail,
                    openDuration: 0.12f,
                    closeDuration: 0.1f
                ),
                PresentationTarget.Local(XeriValidationIDs.ApplicationScreenLayer),
                detailSource
            );

            var response = window.Context.Screens.Open(XeriValidationIDs.ApplicationHome);

            if (!response.Accepted)
            {
                throw new InvalidOperationException
                (
                    $"Validation Application Home Screen open이 거부됐습니다. {response}"
                );
            }
        }

    #endregion

    #region Modal 열기

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Context에서 Modal을 연다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenModal()
        {
            if (modal != null && !modal.IsDisposed)
            {
                return;
            }

            modal = XeriValidationModal.Open
            (
                window.Context,
                PresentationTarget.Local(XeriValidationIDs.ApplicationModalLayer),
                assets,
                appearance,
                "Application Modal",
                "This modal belongs to this Application Window child UIContext.",
                OpenNestedModal
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 Modal 위에 중첩 Modal을 연다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenNestedModal()
        {
            if (nestedModal != null && !nestedModal.IsDisposed)
            {
                return;
            }

            nestedModal = XeriValidationModal.Open
            (
                window.Context,
                PresentationTarget.Local(XeriValidationIDs.ApplicationModalLayer),
                assets,
                appearance,
                "Nested Modal",
                "Closing this modal restores focus to the modal below it."
            );
        }

    #endregion

    #region 소유 자원 반환

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
            nestedModal?.Dispose();
            modal?.Dispose();
            nestedModal = null;
            modal = null;

            detailRegistration?.Dispose();
            homeRegistration?.Dispose();
            detailRegistration = null;
            homeRegistration = null;

            detailSource.Dispose();
            homeSource.Dispose();
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// Application Screen 콘텐츠를 획득하고 반환한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationApplicationScreenSource : IScreenSource, IDisposable
    {

    #region 필드와 상태

        private readonly XeriValidationAssets assets = null;
        private readonly string applicationName = null;
        private readonly string screenName = null;
        private readonly string navigateText = null;
        private readonly Action navigate = null;
        private readonly Action openModal = null;
        private readonly Action closeScreen = null;
        private bool isDisposed = false;

    #endregion

    #region Screen Source 연결

        // ------------------------------------------------------------
        /// <summary>
        /// Application Screen 콘텐츠를 획득하고 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationApplicationScreenSource
        (
            XeriValidationAssets assets,
            string applicationName,
            string screenName,
            string navigateText,
            Action navigate,
            Action openModal,
            Action closeScreen
        )
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            this.screenName = screenName ?? throw new ArgumentNullException(nameof(screenName));
            this.navigateText = navigateText ?? throw new ArgumentNullException(nameof(navigateText));
            this.navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
            this.openModal = openModal ?? throw new ArgumentNullException(nameof(openModal));
            this.closeScreen = closeScreen ?? throw new ArgumentNullException(nameof(closeScreen));
        }

    #endregion

    #region 콘텐츠 획득과 반환

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation에 표시할 콘텐츠를 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenInstance Acquire(ScreenViewScope scope)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(XeriValidationApplicationScreenSource));
            }

            if (scope?.Layer is not IPresentationLayerDriver<VisualElement> layer)
            {
                throw new InvalidOperationException
                (
                    "Validation Application Screen Layer가 UITK Root를 제공하지 않습니다."
                );
            }

            var root = XeriValidationUI.CloneRoot
            (
                assets.ApplicationTemplate,
                "ApplicationScreenRoot"
            );
            assets.ApplyAppStyles(root);
            layer.Root.Add(root);

            var view = new XeriValidationApplicationScreenView
            (
                root,
                applicationName,
                screenName,
                navigateText,
                navigate,
                openModal,
                closeScreen
            );
            return new ScreenInstance
            (
                new UITKScreenDriver(root, view.DefaultFocus),
                view
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 소유한 콘텐츠를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Release(ScreenInstance instance)
        {
            if (instance?.StateHandler is not XeriValidationApplicationScreenView view)
            {
                throw new InvalidOperationException
                (
                    "Validation Application ScreenInstance의 View Handler가 올바르지 않습니다."
                );
            }

            view.Dispose();
        }

    #endregion

    #region 소유 자원 반환

        // ------------------------------------------------------------
        /// <summary>
        /// 이벤트 연결과 소유한 수명을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            isDisposed = true;
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// Application 입력과 Screen 수명 표시를 연결한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationApplicationScreenView :
        IScreenStateHandler,
        IDisposable
    {

    #region 필드와 상태

        // ------------------------------------------------------------
        /// <summary>
        /// 화면 진입 시 포커스를 받을 요소.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement DefaultFocus => navigateButton;
        private readonly Button navigateButton = null;

        private readonly VisualElement root = null;
        private readonly Button modalButton = null;
        private readonly Button closeButton = null;
        private readonly Label lifecycle = null;
        private readonly Action navigate = null;
        private readonly Action openModal = null;
        private readonly Action closeScreen = null;
        private bool isDisposed = false;

    #endregion

    #region 입력 연결

        // ------------------------------------------------------------
        /// <summary>
        /// Application 입력과 Screen 수명 표시를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationApplicationScreenView
        (
            VisualElement root,
            string applicationName,
            string screenName,
            string navigateText,
            Action navigate,
            Action openModal,
            Action closeScreen
        )
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
            this.openModal = openModal ?? throw new ArgumentNullException(nameof(openModal));
            this.closeScreen = closeScreen ?? throw new ArgumentNullException(nameof(closeScreen));

            XeriValidationUI.Require<Label>(root, "ApplicationName").text = applicationName;
            XeriValidationUI.Require<Label>(root, "ApplicationScreenName").text = screenName;
            lifecycle = XeriValidationUI.Require<Label>(root, "ApplicationLifecycle");
            navigateButton = XeriValidationUI.Require<Button>(root, "ApplicationNavigate");
            modalButton = XeriValidationUI.Require<Button>(root, "ApplicationModal");
            closeButton = XeriValidationUI.Require<Button>(root, "ApplicationCloseScreen");

            navigateButton.text = navigateText;
            navigateButton.clicked += this.navigate;
            modalButton.clicked += this.openModal;
            closeButton.clicked += this.closeScreen;
        }

    #endregion

    #region Screen 수명 표시

        // ------------------------------------------------------------
        /// <summary>
        /// 열리는 중인 Screen 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnOpening(ScreenStateContext context)
        {
            lifecycle.text = "Opening";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 Screen 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnOpened(ScreenStateContext context)
        {
            lifecycle.text = "Active";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 닫히는 중인 Screen 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnClosing(ScreenStateContext context)
        {
            lifecycle.text = "Closing";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종료된 Screen 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnClosed(ScreenStateContext context)
        {
            lifecycle.text = "Closed";
        }

    #endregion

    #region 소유 자원 반환

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
            navigateButton.clicked -= navigate;
            modalButton.clicked -= openModal;
            closeButton.clicked -= closeScreen;
            root.RemoveFromHierarchy();
        }

    #endregion

    }
}
