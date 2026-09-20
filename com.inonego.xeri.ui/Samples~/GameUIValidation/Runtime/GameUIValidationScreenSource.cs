/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : GameUIValidationScreenSource.cs
수정일 : 2026-08-05

# 설명
검증용 UXML을 실제 UITK Presentation Layer에 생성하고 Xeri Screen Driver와 상태 훅을
Bind한 뒤 Release에서 이벤트, 예약 작업과 VisualElement를 대칭으로 반환한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine.UIElements;

using inonego.Xeri.UI;

namespace inonego.Xeri.Samples.GameUIValidation
{
    // ============================================================
    /// <summary>
    /// 하나의 UXML Template으로 Dashboard와 Detail Screen 인스턴스를 공급한다.
    /// </summary>
    // ============================================================
    internal sealed class GameUIValidationScreenSource : IScreenSource, IDisposable
    {

    #region 필드

        private readonly GameUIValidationLab owner = null;
        private readonly VisualTreeAsset template = null;
        private readonly StyleSheet styleSheet = null;
        private bool isDisposed = false;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 검증 화면 조립점과 UXML·USS Asset을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public GameUIValidationScreenSource
        (
            GameUIValidationLab owner,
            VisualTreeAsset template,
            StyleSheet styleSheet
        ) : base()
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.styleSheet = styleSheet ?? throw new ArgumentNullException(nameof(styleSheet));
        }

    #endregion

    #region IScreenSource

        // ----------------------------------------------------------------------
        /// <summary>
        /// UXML Root를 UITK Layer에 추가하고 Driver, Focus와 상태 Handler를 묶는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public ScreenInstance Acquire(ScreenViewScope scope)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(GameUIValidationScreenSource));
            }

            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (!(scope.Layer is IPresentationLayerDriver<VisualElement> layer))
            {
                throw new InvalidOperationException
                (
                    $"Screen '{scope.ScreenID}'의 Layer가 UITK Root를 제공하지 않습니다."
                );
            }

            var templateContainer = template.Instantiate();
            var root = templateContainer.Q<VisualElement>("ScreenRoot");

            if (root == null)
            {
                throw new InvalidOperationException
                (
                    "검증용 UXML에 'ScreenRoot' VisualElement가 없습니다."
                );
            }

            root.RemoveFromHierarchy();
            root.styleSheets.Add(styleSheet);
            layer.Root.Add(root);

            GameUIValidationScreenView view = null;

            try
            {
                view = new GameUIValidationScreenView
                (
                    owner,
                    root,
                    scope
                );
                var driver = new UITKScreenDriver(root, view.DefaultFocus);
                return new ScreenInstance(driver, view);
            }
            catch
            {
                view?.Dispose();
                root.RemoveFromHierarchy();
                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen View의 이벤트와 Visual Tree 연결을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Release(ScreenInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!(instance.StateHandler is GameUIValidationScreenView view))
            {
                throw new InvalidOperationException
                (
                    "검증용 ScreenInstance에 대응하는 View Handler가 없습니다."
                );
            }

            view.Dispose();
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// 새 Screen 획득을 중지한다. 열린 View는 Screen Session이 먼저 반환한다.
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
    /// 검증용 Screen의 UI 이벤트, 상태 훅과 표시 텍스트를 관리한다.
    /// </summary>
    // ============================================================
    internal sealed class GameUIValidationScreenView : IScreenStateHandler, IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Controller가 선택할 기본 Focus Button.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement DefaultFocus { get; private set; }

        private readonly GameUIValidationLab owner = null;
        private readonly VisualElement root = null;
        private readonly VisualElement layerRoot = null;
        private readonly ScreenSession session = null;
        private readonly bool isDashboard = false;
        private readonly GameUIValidationScreenPayload payload = default;

        private readonly VisualElement dashboardView = null;
        private readonly VisualElement detailView = null;
        private readonly Label lifecycleState = null;
        private readonly Label detailLifecycleState = null;
        private readonly Label stackCount = null;
        private readonly Label topScreen = null;
        private readonly Label modalCount = null;
        private readonly Label fadeState = null;
        private readonly Label inputDevice = null;
        private readonly Label gammaState = null;
        private readonly Label runtimeMode = null;
        private readonly Label activityText = null;
        private readonly Label detailIndex = null;
        private readonly Label detailTitle = null;
        private readonly Label detailCopy = null;
        private readonly Label detailDepth = null;
        private readonly Label detailTopScreen = null;
        private readonly Label detailMode = null;
        private readonly UITKSpotlightElement spotlightElement = null;

        private readonly Button pushButton = null;
        private readonly Button modalButton = null;
        private readonly Button spotlightButton = null;
        private readonly Button fadeButton = null;
        private readonly Button overlayButton = null;
        private readonly Button clearButton = null;
        private readonly Button pushDeeperButton = null;
        private readonly Button replaceButton = null;
        private readonly Button backButton = null;

        private IVisualElementScheduledItem statusSchedule = null;
        private bool isDisposed = false;

    #endregion

    #region 생성자

        // ----------------------------------------------------------------------
        /// <summary>
        /// 생성된 UXML 인스턴스와 현재 Screen Session을 연결하고 UI 이벤트를 Bind한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public GameUIValidationScreenView
        (
            GameUIValidationLab owner,
            VisualElement root,
            ScreenViewScope scope
        ) : base()
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.root = root ?? throw new ArgumentNullException(nameof(root));

            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            session = scope.Session;

            if (!(scope.Layer is IPresentationLayerDriver<VisualElement> typedLayer))
            {
                throw new InvalidOperationException
                (
                    "검증용 Screen Layer가 UITK Root를 제공하지 않습니다."
                );
            }

            layerRoot = typedLayer.Root;
            isDashboard = scope.ScreenID == GameUIValidationLab.DASHBOARD_SCREEN_ID;
            payload = scope.OpenParams.Payload is GameUIValidationScreenPayload value ? value : new GameUIValidationScreenPayload(1, false);

            dashboardView = Require<VisualElement>(root, "DashboardView");
            detailView = Require<VisualElement>(root, "DetailView");
            lifecycleState = Require<Label>(root, "LifecycleState");
            detailLifecycleState = Require<Label>(root, "DetailLifecycleState");
            stackCount = Require<Label>(root, "StackCount");
            topScreen = Require<Label>(root, "TopScreen");
            modalCount = Require<Label>(root, "ModalCount");
            fadeState = Require<Label>(root, "FadeState");
            inputDevice = Require<Label>(root, "InputDevice");
            gammaState = Require<Label>(root, "GammaState");
            runtimeMode = Require<Label>(root, "RuntimeMode");
            activityText = Require<Label>(root, "ActivityText");
            detailIndex = Require<Label>(root, "DetailIndex");
            detailTitle = Require<Label>(root, "DetailTitle");
            detailCopy = Require<Label>(root, "DetailCopy");
            detailDepth = Require<Label>(root, "DetailDepth");
            detailTopScreen = Require<Label>(root, "DetailTopScreen");
            detailMode = Require<Label>(root, "DetailMode");
            spotlightElement = new UITKSpotlightElement();
            root.Add(spotlightElement);

            pushButton = Require<Button>(root, "PushButton");
            modalButton = Require<Button>(root, "ModalButton");
            spotlightButton = Require<Button>(root, "SpotlightButton");
            fadeButton = Require<Button>(root, "FadeButton");
            overlayButton = Require<Button>(root, "OverlayButton");
            clearButton = Require<Button>(root, "ClearButton");
            pushDeeperButton = Require<Button>(root, "PushDeeperButton");
            replaceButton = Require<Button>(root, "ReplaceButton");
            backButton = Require<Button>(root, "BackButton");

            ConfigureView();
            BindButtons();
            RefreshStatus();
            statusSchedule = root.schedule.Execute(RefreshStatus).Every(100);
        }

    #endregion

    #region 구성

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 UXML 인스턴스를 Dashboard 또는 Detail 표현으로 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ConfigureView()
        {
            dashboardView.style.display = isDashboard ? DisplayStyle.Flex : DisplayStyle.None;
            detailView.style.display = isDashboard ? DisplayStyle.None : DisplayStyle.Flex;

            if (isDashboard)
            {
                fadeButton.SetEnabled(owner.SupportsSceneFade);
                DefaultFocus = pushButton;
                return;
            }

            var mode = payload.IsReplacement ? "REPLACE" : "PUSH";
            detailIndex.text = $"SCREEN {payload.Depth:00}";
            detailTitle.text = payload.IsReplacement ? "Top session replaced." : "A new layer of state.";
            detailCopy.text = payload.IsReplacement
                ? "The previous top completed its close path while this new session acquired the same registered Screen source."
                : "This instance owns its view, layer usage, input session and transition until the top is popped or replaced.";
            detailDepth.text = payload.Depth.ToString();
            detailMode.text = mode;
            DefaultFocus = pushDeeperButton;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 화면의 모든 검증 Button을 현재 View 인스턴스 명령에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BindButtons()
        {
            pushButton.clicked += OnPushClicked;
            modalButton.clicked += OnModalClicked;
            spotlightButton.clicked += OnSpotlightClicked;
            fadeButton.clicked += OnFadeClicked;
            overlayButton.clicked += OnOverlayClicked;
            clearButton.clicked += OnClearClicked;
            pushDeeperButton.clicked += OnPushClicked;
            replaceButton.clicked += OnReplaceClicked;
            backButton.clicked += OnBackClicked;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 화면의 모든 검증 Button에서 현재 View 인스턴스 명령을 분리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnbindButtons()
        {
            pushButton.clicked -= OnPushClicked;
            modalButton.clicked -= OnModalClicked;
            spotlightButton.clicked -= OnSpotlightClicked;
            fadeButton.clicked -= OnFadeClicked;
            overlayButton.clicked -= OnOverlayClicked;
            clearButton.clicked -= OnClearClicked;
            pushDeeperButton.clicked -= OnPushClicked;
            replaceButton.clicked -= OnReplaceClicked;
            backButton.clicked -= OnBackClicked;
        }

    #endregion

    #region Button 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 화면 위에 다음 Detail Screen을 Push한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPushClicked()
        {
            owner.PushDetail(isDashboard ? 1 : payload.Depth);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Screen Session이 소유하는 Modal을 연다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnModalClicked()
        {
            owner.OpenModal(session, layerRoot);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Screen의 실제 UITK 대상 Spotlight를 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnSpotlightClicked()
        {
            owner.ToggleSpotlight(session, spotlightElement, spotlightButton);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 Scene Fade Cover와 Reveal 검증을 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnFadeClicked()
        {
            owner.RunFade();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Screen Session이 소유하는 Toast Overlay를 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnOverlayClicked()
        {
            owner.ToggleOverlay(session);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 전체 Stack 강제 정리와 Dashboard 재획득을 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnClearClicked()
        {
            owner.ClearAndRestore();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Detail top을 같은 등록의 새 Session으로 Replace한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnReplaceClicked()
        {
            owner.ReplaceDetail(payload.Depth);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Detail top 하나를 일반 Close 경로로 Pop한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnBackClicked()
        {
            owner.PopScreen();
        }

    #endregion

    #region 상태 표시

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime의 현재 Stack, Modal, Fade와 Input 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshStatus()
        {
            if (isDisposed) return;

            stackCount.text = owner.ScreenCount.ToString();
            topScreen.text = GameUIValidationLab.ShortScreenID(owner.TopScreenID);
            modalCount.text = owner.ModalCount.ToString();
            fadeState.text = owner.FadeState;
            inputDevice.text = owner.InputDeviceName.ToUpperInvariant();
            gammaState.text = owner.GammaDescription;
            runtimeMode.text = owner.RuntimeMode;
            activityText.text = owner.LastActivity;
            detailDepth.text = owner.ScreenCount.ToString();
            detailTopScreen.text = GameUIValidationLab.ShortScreenID(owner.TopScreenID).ToUpperInvariant();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Screen 상태를 두 화면 형식의 상태 Label에 함께 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void SetLifecycle(ScreenState state)
        {
            var text = state.ToString().ToUpperInvariant();
            lifecycleState.text = text;
            detailLifecycleState.text = text;
            owner.RecordLifecycle(session.ID, state);
            RefreshStatus();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// UXML에서 필수 이름과 타입이 일치하는 요소를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static T Require<T>
        (
            VisualElement searchRoot,
            string name
        )
        where T : VisualElement
        {
            var element = searchRoot.Q<T>(name);

            if (element == null)
            {
                throw new InvalidOperationException
                (
                    $"검증용 UXML에 '{name}' {typeof(T).Name} 요소가 없습니다."
                );
            }

            return element;
        }

    #endregion

    #region IScreenStateHandler

        // ------------------------------------------------------------
        /// <summary>
        /// 열기 Transition 직전 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnOpening(ScreenStateContext context)
        {
            SetLifecycle(ScreenState.Opening);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 열기 Transition 완료 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnOpened(ScreenStateContext context)
        {
            SetLifecycle(ScreenState.Active);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 닫기 Transition 직전 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnClosing(ScreenStateContext context)
        {
            SetLifecycle(ScreenState.Closing);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 닫기 Transition과 하위 표시 정리 완료 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnClosed(ScreenStateContext context)
        {
            SetLifecycle(ScreenState.Closed);
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// UI 이벤트와 예약 갱신을 분리하고 Screen Root를 Visual Tree에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            statusSchedule?.Pause();
            statusSchedule = null;
            UnbindButtons();
            root.RemoveFromHierarchy();
        }

    #endregion

    }
}
