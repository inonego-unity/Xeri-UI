/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationCoreLab.cs
수정일 : 2026-10-07

# 설명
Root UIContext의 Host Target 기반 Presentation, Spotlight와 Scene Fade를 실제 public API로 행사하는 instrumented Core Lab.
각 동작은 자유롭게 반복할 수 있으며 ValidationChecklist는 결과를 관찰만 하고 진행을 제한하지 않는다.
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
    /// Core 조작과 화면 수명을 연결한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationCoreLab : IDisposable
    {

    #region 필드와 상태

        // ------------------------------------------------------------
        /// <summary>
        /// Lab 콘텐츠를 표시하는 루트.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Root { get; }

        private readonly UIRuntime runtime = null;
        private readonly XeriValidationAssets assets = null;
        private readonly XeriValidationAppearance appearance = null;
        private readonly XeriValidationChecklist checklist = null;
        private readonly Button screenHomeButton = null;
        private readonly Button screenPushButton = null;
        private readonly Button screenReplaceButton = null;
        private readonly Button screenPopButton = null;
        private readonly Button screenClearButton = null;
        private readonly Button overlayButton = null;
        private readonly Button spotlightButton = null;
        private readonly Button fadeButton = null;
        private readonly Label status = null;
        private readonly VisualElement spotlightTarget = null;
        private readonly UITKSpotlight spotlight = new();
        private readonly UITKSpotlightElement spotlightDriver = new();

        private ScreenRegistrationHandle homeRegistration = null;
        private ScreenRegistrationHandle detailRegistration = null;
        private CoreScreenSource homeSource = null;
        private CoreScreenSource detailSource = null;
        private Lease<VisualElement> overlayLease = null;
        private Lease spotlightLease = null;
        private IVisualElementScheduledItem revealSchedule = null;
        private bool isDisposed = false;
        private bool ownsFade = false;

    #endregion

    #region 화면과 Source 연결

        // ------------------------------------------------------------
        /// <summary>
        /// Core 조작과 화면 수명을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationCoreLab
        (
            UIRuntime runtime,
            XeriValidationAssets assets,
            XeriValidationAppearance appearance,
            XeriValidationChecklist checklist
        ) : base()
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            this.checklist = checklist;

            Root = XeriValidationUI.CloneRoot
            (
                assets.CoreLabTemplate,
                "CoreLabRoot"
            );
            assets.ApplyLabStyles(Root);
            screenHomeButton = XeriValidationUI.Require<Button>(Root, "CoreScreenHome");
            screenPushButton = XeriValidationUI.Require<Button>(Root, "CoreScreenPush");
            screenReplaceButton = XeriValidationUI.Require<Button>(Root, "CoreScreenReplace");
            screenPopButton = XeriValidationUI.Require<Button>(Root, "CoreScreenPop");
            screenClearButton = XeriValidationUI.Require<Button>(Root, "CoreScreenClear");
            overlayButton = XeriValidationUI.Require<Button>(Root, "CoreOverlay");
            spotlightButton = XeriValidationUI.Require<Button>(Root, "CoreSpotlight");
            fadeButton = XeriValidationUI.Require<Button>(Root, "CoreFade");
            status = XeriValidationUI.Require<Label>(Root, "CoreLabStatus");
            spotlightTarget = XeriValidationUI.Require<VisualElement>(Root, "CoreSpotlightTarget");

            Root.Add(spotlightDriver);
            RegisterScreens();
            screenHomeButton.clicked += OpenHome;
            screenPushButton.clicked += PushDetail;
            screenReplaceButton.clicked += ReplaceDetail;
            screenPopButton.clicked += PopScreen;
            screenClearButton.clicked += ClearScreens;
            overlayButton.clicked += ToggleOverlay;
            spotlightButton.clicked += ToggleSpotlight;
            fadeButton.clicked += RunFade;
            status.text = "Ready · public Core path";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Lab에서 사용할 화면을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RegisterScreens()
        {
            homeSource = new CoreScreenSource
            (
                assets,
                appearance,
                "CORE HOME"
            );
            detailSource = new CoreScreenSource
            (
                assets,
                appearance,
                "CORE DETAIL"
            );
            homeRegistration = runtime.Main.RegisterScreen
            (
                new ScreenOptions
                (
                    XeriValidationIDs.CoreHome,
                    ScreenDuplicatePolicy.Reject,
                    openDuration: 0.12f,
                    closeDuration: 0.1f
                ),
                PresentationTarget.Host(PresentationDestinationID.Application),
                homeSource
            );
            detailRegistration = runtime.Main.RegisterScreen
            (
                new ScreenOptions
                (
                    XeriValidationIDs.CoreDetail,
                    ScreenDuplicatePolicy.Allow,
                    openDuration: 0.12f,
                    closeDuration: 0.1f
                ),
                PresentationTarget.Host(PresentationDestinationID.Application),
                detailSource
            );
        }

    #endregion

    #region Screen 탐색

        // ------------------------------------------------------------
        /// <summary>
        /// Home 화면을 열고 검증 상태를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenHome()
        {
            var response = runtime.Main.Screens.Open(XeriValidationIDs.CoreHome);
            status.text = response.Accepted
                ? "Root Screen · Home opened"
                : "Root Screen · Home rejected";

            if (response.Accepted)
            {
                checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, "Open");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Detail 화면을 스택에 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private void PushDetail()
        {
            var response = runtime.Main.Screens.Open(XeriValidationIDs.CoreDetail);
            status.text = response.Accepted
                ? $"Root Screen · Push depth {runtime.Main.Screens.Count}"
                : "Root Screen · Push rejected";

            if (response.Accepted)
            {
                checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, "Push");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 화면을 Detail로 교체한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReplaceDetail()
        {
            var response = runtime.Main.Screens.Replace(XeriValidationIDs.CoreDetail);
            status.text = response.Accepted
                ? "Root Screen · Top replaced"
                : "Root Screen · Replace rejected";

            if (response.Accepted)
            {
                checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, "Replace");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 최상위 화면을 닫고 이전 화면으로 돌아간다.
        /// </summary>
        // ------------------------------------------------------------
        private void PopScreen()
        {
            var closed = runtime.Main.Screens.Close();
            status.text = closed
                ? $"Root Screen · Pop depth {runtime.Main.Screens.Count}"
                : "Root Screen · Pop rejected";

            if (closed)
            {
                checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, "Pop");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Lab의 화면 스택을 비운다.
        /// </summary>
        // ------------------------------------------------------------
        private void ClearScreens()
        {
            runtime.Main.Screens.Clear();
            status.text = "Root Screen · Cleared";
            checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, "Clear");
        }

    #endregion

    #region Overlay와 Highlight

        // ------------------------------------------------------------
        /// <summary>
        /// Overlay 표시 수명을 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ToggleOverlay()
        {
            if (overlayLease != null && !overlayLease.IsDisposed)
            {
                CloseOverlay();
                return;
            }

            var source = new OverlaySource(assets, appearance, CloseOverlay);
            overlayLease = runtime.Main.AcquirePresentation
            (
                PresentationTarget.Host(PresentationDestinationID.Overlay),
                source
            );
            status.text = "Overlay lease acquired";
            checklist?.Mark(XeriValidationChecklist.Item.Overlay);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Overlay 표시 수명을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CloseOverlay()
        {
            if (overlayLease == null || overlayLease.IsDisposed)
            {
                return;
            }

            var current = overlayLease;
            overlayLease = null;
            current.Dispose();
            status.text = "Overlay lease released";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 다른 조작을 허용하는 강조 표시를 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ToggleSpotlight()
        {
            if (spotlightLease != null && !spotlightLease.IsDisposed)
            {
                spotlightLease.Dispose();
                spotlightLease = null;
                spotlightButton.text = "Show highlight";
                status.text = "Highlight hidden";
                return;
            }

            spotlightLease = spotlight.Show
            (
                spotlightDriver,
                new UITKSpotlightParams
                (
                    new[]
                    {
                        new UITKSpotlightTarget
                        (
                            spotlightTarget,
                            new Vector4(12f, 12f, 12f, 12f)
                        ),
                    },
                    // 시각 강조를 켠 상태에서도 해제와 다른 Lab 조작을 허용한다.
                    blocksOutsideInput: false
                )
            );
            spotlightButton.text = "Hide highlight";
            status.text = "Highlight visible · controls remain available";
        }

    #endregion

    #region 화면 Fade

        // ------------------------------------------------------------
        /// <summary>
        /// 화면을 가렸다가 드러내는 전환을 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RunFade()
        {
            revealSchedule?.Pause();
            revealSchedule = null;

            ownsFade = true;
            runtime.SceneFader.Cover
            (
                runtime.DefaultSceneFadeParams,
                () =>
                {
                    status.text = "Fade covered";
                    checklist?.Mark(XeriValidationChecklist.Item.Fade);
                    revealSchedule = Root.schedule.Execute(RevealFade).StartingIn(450);
                },
                exception =>
                {
                    status.text = $"Fade failed · {exception.GetType().Name}";
                }
            );
            status.text = "Fade covering";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 가려진 화면을 다시 드러낸다.
        /// </summary>
        // ------------------------------------------------------------
        private void RevealFade()
        {
            revealSchedule = null;
            ownsFade = false;
            runtime.SceneFader.Reveal
            (
                runtime.DefaultSceneFadeParams,
                () => status.text = "Fade clear",
                exception => status.text = $"Reveal failed · {exception.GetType().Name}"
            );
        }

    #endregion

    #region 소유 자원 반환

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
            screenHomeButton.clicked -= OpenHome;
            screenPushButton.clicked -= PushDetail;
            screenReplaceButton.clicked -= ReplaceDetail;
            screenPopButton.clicked -= PopScreen;
            screenClearButton.clicked -= ClearScreens;
            overlayButton.clicked -= ToggleOverlay;
            spotlightButton.clicked -= ToggleSpotlight;
            fadeButton.clicked -= RunFade;
            revealSchedule?.Pause();
            revealSchedule = null;

            // 닫힌 Lab의 스케줄에 화면 복원을 맡기지 않는다.
            if (ownsFade && runtime.IsInitialized && !runtime.IsReleasing)
            {
                RevealFade();
            }

            if
            (
                runtime.Main != null &&
                !runtime.Main.IsDisposed &&
                runtime.Main.Screens.Count > 0
            )
            {
                runtime.Main.Screens.Clear();
            }

            detailRegistration?.Dispose();
            homeRegistration?.Dispose();
            detailRegistration = null;
            homeRegistration = null;
            detailSource?.Dispose();
            homeSource?.Dispose();
            detailSource = null;
            homeSource = null;

            CloseOverlay();
            spotlightLease?.Dispose();
            spotlightLease = null;
            Root.RemoveFromHierarchy();
        }

    #endregion

    #region Screen과 Overlay Source

        // ============================================================
        /// <summary>
        /// Core 화면의 생성과 반환을 담당한다.
        /// </summary>
        // ============================================================
        private sealed class CoreScreenSource : IScreenSource, IDisposable
        {

        #region 필드와 상태

            private readonly XeriValidationAssets assets = null;
            private readonly XeriValidationAppearance appearance = null;
            private readonly string title = null;
            private bool isDisposed = false;

        #endregion

        #region Screen Source 연결

            // ------------------------------------------------------------
            /// <summary>
            /// Core 화면의 생성과 반환을 담당한다.
            /// </summary>
            // ------------------------------------------------------------
            internal CoreScreenSource
            (
                XeriValidationAssets assets,
                XeriValidationAppearance appearance,
                string title
            ) : base()
            {
                this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
                this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
                this.title = title ?? throw new ArgumentNullException(nameof(title));
            }

        #endregion

        #region 콘텐츠 획득과 반환

            // ------------------------------------------------------------
            /// <summary>
            /// Presentation에 표시할 콘텐츠를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public ScreenInstance Acquire(ScreenViewScope scope)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(CoreScreenSource));
                }

                if
                (
                    scope?.Layer is not IPresentationLayerDriver<VisualElement> layer ||
                    layer.Root == null
                )
                {
                    throw new InvalidOperationException
                    (
                        "Validation Core Screen Layer가 UITK Root를 제공하지 않습니다."
                    );
                }

                var root = XeriValidationUI.CloneRoot
                (
                    assets.CoreScreenTemplate,
                    "CoreScreenRoot"
                );
                assets.ApplyLabStyles(root);
                XeriValidationUI.Require<Label>(root, "CoreScreenTitle").text = title;
                layer.Root.Add(root);

                var view = new CoreScreenView(root, appearance.Bind(root));
                return new ScreenInstance
                (
                    new UITKScreenDriver(root, view.DefaultFocus),
                    view
                );
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 획득한 콘텐츠를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release(ScreenInstance instance)
            {
                if (instance?.StateHandler is not CoreScreenView view)
                {
                    throw new InvalidOperationException
                    (
                        "Validation Core ScreenInstance의 View Handler가 올바르지 않습니다."
                    );
                }

                view.Dispose();
            }

        #endregion

        #region 소유 자원 반환

            // ------------------------------------------------------------
            /// <summary>
            /// 구독과 소유 수명을 정리한다.
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
        /// 화면 수명 상태와 기본 포커스를 제공한다.
        /// </summary>
        // ============================================================
        private sealed class CoreScreenView :
            IScreenStateHandler,
            IDisposable
        {

        #region 필드와 상태

            // ------------------------------------------------------------
            /// <summary>
            /// 화면 진입 시 포커스를 받을 요소.
            /// </summary>
            // ------------------------------------------------------------
            public VisualElement DefaultFocus => focusButton;
            private readonly Button focusButton = null;

            private readonly VisualElement root = null;
            private readonly Label lifecycle = null;
            private readonly IDisposable appearanceBinding = null;
            private bool isDisposed = false;

        #endregion

        #region 화면 연결

            // ------------------------------------------------------------
            /// <summary>
            /// 화면 수명 상태와 기본 포커스를 제공한다.
            /// </summary>
            // ------------------------------------------------------------
            internal CoreScreenView(VisualElement root, IDisposable appearanceBinding) : base()
            {
                this.root = root ?? throw new ArgumentNullException(nameof(root));
                this.appearanceBinding = appearanceBinding;
                lifecycle = XeriValidationUI.Require<Label>
                (
                    root,
                    "CoreScreenLifecycle"
                );
                focusButton = XeriValidationUI.Require<Button>
                (
                    root,
                    "CoreScreenFocus"
                );
            }

        #endregion

        #region Screen 수명 표시

            // ------------------------------------------------------------
            /// <summary>
            /// 열리는 중인 화면 상태를 표시한다.
            /// </summary>
            // ------------------------------------------------------------
            public void OnOpening(ScreenStateContext context)
            {
                lifecycle.text = "Opening";
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 활성 화면 상태를 표시한다.
            /// </summary>
            // ------------------------------------------------------------
            public void OnOpened(ScreenStateContext context)
            {
                lifecycle.text = "Active";
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 닫히는 중인 화면 상태를 표시한다.
            /// </summary>
            // ------------------------------------------------------------
            public void OnClosing(ScreenStateContext context)
            {
                lifecycle.text = "Closing";
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 닫힌 화면 상태를 표시한다.
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
                appearanceBinding.Dispose();
                root.RemoveFromHierarchy();
            }

        #endregion

        }

        // ============================================================
        /// <summary>
        /// Overlay 알림 콘텐츠를 생성하고 반환한다.
        /// </summary>
        // ============================================================
        private sealed class OverlaySource : IPresentationSource<VisualElement>
        {

        #region 필드와 상태

            private readonly XeriValidationAssets assets = null;
            private readonly XeriValidationAppearance appearance = null;
            private readonly Action close = null;
            private Lease appearanceBinding = null;

        #endregion

        #region Overlay Source 연결

            // ------------------------------------------------------------
            /// <summary>
            /// Overlay 알림 콘텐츠를 생성하고 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            internal OverlaySource
            (
                XeriValidationAssets assets,
                XeriValidationAppearance appearance,
                Action close
            ) : base()
            {
                this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
                this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
                this.close = close ?? throw new ArgumentNullException(nameof(close));
            }

        #endregion

        #region 콘텐츠 획득과 반환

            // ------------------------------------------------------------
            /// <summary>
            /// Presentation에 표시할 콘텐츠를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public VisualElement Acquire(IPresentationLayerDriver layer)
            {
                if (layer is not IPresentationLayerDriver<VisualElement> uitk)
                {
                    throw new InvalidOperationException
                    (
                        "Validation Overlay Layer가 UITK Root를 제공하지 않습니다."
                    );
                }

                var root = new VisualElement
                {
                    name = "XeriValidationOverlayToast",
                };
                root.AddToClassList("xeri-validation-toast");
                appearanceBinding = appearance.Bind(root);

                var signal = new VisualElement();
                signal.AddToClassList("xeri-validation-toast__signal");

                var copy = new VisualElement();
                copy.AddToClassList("xeri-validation-toast__copy");

                var title = new Label("Presentation");
                title.AddToClassList("xeri-validation-toast__title");

                var message = new Label("The transient presentation is active.");
                message.AddToClassList("xeri-validation-toast__message");

                copy.Add(title);
                copy.Add(message);

                var closeButton = new Button(close)
                {
                    text = "×",
                };
                closeButton.AddToClassList("xeri-validation-toast__close");

                root.Add(signal);
                root.Add(copy);
                root.Add(closeButton);
                uitk.Root.Add(root);
                return root;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 획득한 콘텐츠를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release(VisualElement view)
            {
                appearanceBinding?.Dispose();
                appearanceBinding = null;
                view?.RemoveFromHierarchy();
            }

        #endregion

        }

    #endregion

    }
}
