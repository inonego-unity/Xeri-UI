/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationCoreLab.cs
수정일 : 2026-11-11

# 설명
Root UIContext의 Host Target 기반 Presentation, Spotlight, Scene Fade와 Splash를 실제 public API로 행사하는 instrumented Core Lab.
Scene Fade 실패와 Lab 종료 시 복원 책임을 정리하고, ValidationChecklist는 결과를 관찰만 하며 진행을 제한하지 않는다.
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
        private readonly Button splashButton = null;
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
        private SplashSession splashSession = null;
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
            splashButton = XeriValidationUI.Require<Button>(Root, "CoreSplash");
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
            splashButton.clicked += ShowSplash;
            status.text = "준비됨 · 기본 기능";
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
                "기본 홈"
            );
            detailSource = new CoreScreenSource
            (
                assets,
                appearance,
                "기본 상세"
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
                ? "기본 화면 · 홈 열림"
                : "기본 화면 · 홈 열기 거부됨";

            if (response.Accepted)
            {
                checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, "열기");
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
                ? $"기본 화면 · {runtime.Main.Screens.Count}개 열림"
                : "기본 화면 · 추가 거부됨";

            if (response.Accepted)
            {
                checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, "추가");
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
                ? "기본 화면 · 현재 화면 교체됨"
                : "기본 화면 · 교체 거부됨";

            if (response.Accepted)
            {
                checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, "교체");
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
                ? $"기본 화면 · {runtime.Main.Screens.Count}개 남음"
                : "기본 화면 · 돌아가기 거부됨";

            if (closed)
            {
                checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, "이전 화면");
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
            status.text = "기본 화면 · 모두 닫힘";
            checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, "모두 닫기");
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
            status.text = "알림 표시됨";
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
            status.text = "알림 닫힘";
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
                spotlightButton.text = "강조 표시";
                status.text = "강조 숨겨짐";
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
            spotlightButton.text = "강조 숨기기";
            status.text = "강조 표시 중 · 버튼 조작 가능";
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
            status.text = "화면 전환 중";

            try
            {
                runtime.SceneFader.Cover
                (
                    runtime.DefaultSceneFadeParams,
                    () =>
                    {
                        if (isDisposed) return;

                        status.text = "화면 가려짐";
                        checklist?.Mark(XeriValidationChecklist.Item.Fade);
                        revealSchedule = Root.schedule.Execute(RevealFade).StartingIn(450);
                    },
                    exception => HandleFadeFailure(exception, "화면 전환")
                );
            }
            catch (Exception exception)
            {
                HandleFadeFailure(exception, "화면 전환");
                Debug.LogException(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 가려진 화면을 다시 드러낸다.
        /// </summary>
        // ------------------------------------------------------------
        private void RevealFade()
        {
            revealSchedule = null;

            if (isDisposed) return;

            if (!ownsFade || runtime.SceneFader.State == SceneFadeState.Clear)
            {
                ownsFade = false;
                return;
            }

            try
            {
                runtime.SceneFader.Reveal
                (
                    runtime.DefaultSceneFadeParams,
                    () =>
                    {
                        ownsFade = false;
                        if (!isDisposed)
                        {
                            status.text = "화면 전환 완료";
                        }
                    },
                    exception => HandleFadeFailure(exception, "화면 복원")
                );
            }
            catch (Exception exception)
            {
                HandleFadeFailure(exception, "화면 복원");
                Debug.LogException(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패한 Fade가 복원한 안정 상태에 맞춰 Lab의 해제 책임을 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleFadeFailure(Exception exception, string phase)
        {
            ownsFade = runtime.SceneFader.State != SceneFadeState.Clear;
            if (isDisposed) return;

            status.text = $"{phase} 실패 · {exception.GetType().Name}";
        }

    #endregion

    #region 스플래시 표시와 수명

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 UITK Presentation Layer에서 Splash를 재생한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ShowSplash()
        {
            if (splashSession != null && splashSession.IsActive)
            {
                status.text = "스플래시 · 이미 재생 중";
                return;
            }

            var root = new VisualElement
            {
                name = "ValidationSplash",
            };
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.backgroundColor = new Color(0.035f, 0.045f, 0.075f, 0.98f);

            var logo = new Label("XERI UI");
            logo.style.fontSize = 52f;
            logo.style.unityFontStyleAndWeight = FontStyle.Bold;
            logo.style.color = Color.white;
            root.Add(logo);

            var actions = new VisualElement();
            actions.style.position = Position.Absolute;
            actions.style.bottom = 72f;
            actions.style.flexDirection = FlexDirection.Row;

            actions.Add(CreateSplashButton("Skip", () => splashSession?.Skip()));
            actions.Add(CreateSplashButton("Cancel", () => splashSession?.Cancel()));
            root.Add(actions);

            var session = UITKSplash.Show
            (
                runtime.Main,
                PresentationTarget.Host(XeriValidationIDs.SplashDestination),
                root,
                SplashParams.Default(),
                fadeTarget: root,
                onEnded: result =>
                {
                    splashSession = null;
                    status.text = $"스플래시 · {result}";

                    if (result != SplashResult.Failed)
                    {
                        checklist?.Mark(XeriValidationChecklist.Item.Splash, result.ToString());
                    }
                }
            );

            if (session.IsActive)
            {
                splashSession = session;
                status.text = "스플래시 · 재생 중";
            }
        }

        private static Button CreateSplashButton(string label, Action action)
        {
            var button = new Button(action)
            {
                text = label,
            };
            button.style.width = 120f;
            button.style.height = 40f;
            button.style.marginLeft = 8f;
            button.style.marginRight = 8f;
            return button;
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
            splashButton.clicked -= ShowSplash;
            splashSession?.Dispose();
            splashSession = null;
            revealSchedule?.Pause();
            revealSchedule = null;

            // Lab 종료 뒤에는 스케줄에 의존하지 않고 즉시 화면 복원을 요청한다.
            if (ownsFade && runtime.IsInitialized && !runtime.IsReleasing)
            {
                try
                {
                    if (runtime.SceneFader.State != SceneFadeState.Clear)
                    {
                        var parameters = runtime.DefaultSceneFadeParams;
                        runtime.SceneFader.Reveal
                        (
                            new SceneFadeParams(parameters.Color, 0f),
                            onFailed: exception => Debug.LogException(exception)
                        );
                    }
                }
                catch (Exception exception)
                {
                    // Fade 복원 실패가 Screen과 Overlay 반환까지 중단하지 않도록 기록한다.
                    Debug.LogException(exception);
                }
            }

            ownsFade = false;

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
                lifecycle.text = "여는 중";
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 활성 화면 상태를 표시한다.
            /// </summary>
            // ------------------------------------------------------------
            public void OnOpened(ScreenStateContext context)
            {
                lifecycle.text = "활성";
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 닫히는 중인 화면 상태를 표시한다.
            /// </summary>
            // ------------------------------------------------------------
            public void OnClosing(ScreenStateContext context)
            {
                lifecycle.text = "닫는 중";
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 닫힌 화면 상태를 표시한다.
            /// </summary>
            // ------------------------------------------------------------
            public void OnClosed(ScreenStateContext context)
            {
                lifecycle.text = "닫힘";
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

                var title = new Label("화면 표시");
                title.AddToClassList("xeri-validation-toast__title");

                var message = new Label("임시 알림이 표시되고 있습니다.");
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
