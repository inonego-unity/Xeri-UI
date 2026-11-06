/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIRuntime.cs
수정일 : 2026-11-07
# 설명
App 단위 Singleton, Root PresentationSession, PresentationHost, Main UIContext와 Context Authority 조립·역순 해제를 소유한다.
Top-level Layer materialization과 semantic destination Layer 획득을 분리하며 Context lifetime과 Focus/Input authority를 독립적으로 관리한다.
이전 Context 권한 반환 실패 시 소유권 인계를 보류하고, 실제 Focus 권한을 보유한 활성 Context에만 이벤트를 전달한다.
부분 활성화 실패 시 반환 대상을 고정하고, 입력 정책 Batch 중 막힌 권한 복원을 정책 안정화 이후 재시도한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Runtime의 명시적 Composition Root.
    /// </summary>
    // ============================================================
    public sealed class UIRuntime : MonoSingleton<UIRuntime>
    {

    #region 내부 데이터

        private enum State { Created, Initialized, Releasing, Released }

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 초기화가 완료됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsInitialized => state == State.Initialized;

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime이 해제 중인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsReleasing => state == State.Releasing;

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime Core 해제가 완료됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsReleased => state == State.Released;
        private State state = State.Created;

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime이 사용하는 Settings.
        /// </summary>
        // ------------------------------------------------------------
        public UISettingsAsset Settings { get; private set; }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 현재 Root Layout을 materialize한 top-level PresentationSession.
        /// <br/> 수명은 Runtime이 소유하며 직접 Dispose할 수 없다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public PresentationSession RootPresentation { get; private set; }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Root Layer를 app-wide semantic destination으로 노출하는 Presentation Host.
        /// </summary>
        // --------------------------------------------------------------------------------
        public PresentationHost PresentationHost { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 일반 UI와 전체 Child Context Tree의 Root.
        /// </summary>
        // ------------------------------------------------------------
        public UIContext Main { get; private set; }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> PresentationHost의 System destination을 사용하는 Scene Fade 서비스.
        /// <br/> 수명은 Runtime이 소유하며 직접 Dispose할 수 없다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public SceneFader SceneFader { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// Context가 공유할 Presentation Transition backend.
        /// </summary>
        // ------------------------------------------------------------
        internal IPresentationTransitioner Transitioner => transitioner;

        private DOTweenPresentationTransitioner transitioner = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Context가 공유할 실제 Focus backend.
        /// </summary>
        // ------------------------------------------------------------
        internal IFocusDriver FocusDriver => focusDriver;

        [SerializeField]
        private UIFocusDriver focusDriver = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Context의 Screen Session이 공유할 Input backend.
        /// </summary>
        // ------------------------------------------------------------
        internal IScreenInputDriver InputDriver => inputDriver;

        [SerializeField]
        private InputSystemScreenInputDriver inputDriver = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 마지막으로 UI 입력을 수행한 장치.
        /// </summary>
        // ------------------------------------------------------------
        public InputDevice LastInputDevice
        {
            get
            {
                ThrowIfUnavailable();
                return inputDriver.LastInputDevice;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Settings의 기본 Scene Fade 실행 인자.
        /// </summary>
        // ------------------------------------------------------------
        public SceneFadeParams DefaultSceneFadeParams
        {
            get
            {
                ThrowIfUnavailable();
                return new SceneFadeParams(Settings.DefaultFadeColor, Settings.DefaultFadeDuration);
            }
        }

        [SerializeField]
        private Transform presentationRoot = null;

        [SerializeField]
        private UISceneFadeSource sceneFadeSource = null;

        [SerializeField]
        private PresentationLayerHost[] presentationLayerHosts =
            Array.Empty<PresentationLayerHost>();

        [SerializeField]
        private EventSystem eventSystem = null;

        [SerializeField]
        private InputSystemUIInputModule inputModule = null;

        private readonly List<UIContext> contextOverrides = new();
        private readonly List<UIContext> activeContextPath = new();
        private readonly List<UIContext> authorityCleanupContexts = new();
        private readonly List<UIContext> nextContextPath = new();
        private readonly List<UIContext> authorityCleanupCandidates = new();

        private UIContext baseContext = null;
        private bool isRefreshingContextAuthority = false;
        private bool contextAuthorityRefreshRequested = false;
        private bool contextAuthorityRecoveryPending = false;
        private bool sceneLoadedSubscribed = false;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Core와 Root Presentation 조립이 완료된 뒤 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        public event Action<UIRuntime> OnInitialized = null;

        // ----------------------------------------------------------------------
        /// <summary>
        /// 모든 Screen Source 반환 뒤 프로젝트 소유 리소스 해제 직전에 발생한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public event Action<UIRuntime> OnReleasing = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 마지막 UI 입력 장치가 바뀌었을 때 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        public event Action<InputDevice> OnLastInputDeviceChanged
        {
            add
            {
                ThrowIfUnavailable();
                inputDriver.OnLastInputDeviceChanged += value;
            }
            remove
            {
                if (inputDriver != null)
                {
                    inputDriver.OnLastInputDeviceChanged -= value;
                }
            }
        }

    #endregion

    #region 프레젠테이션 전환

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Runtime 소유 공용 backend로 Presentation Transition을 실행한다.
        /// <br/> 외부에는 backend 소유권 대신 취소 Handle만 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public PresentationTransitionHandle PlayPresentationTransition
        (
            PresentationTransitionParams parameters,
            Action onCompleted = null,
            Action<Exception> onFailed = null
        )
        {
            ThrowIfUnavailable();
            return transitioner.Play(parameters, onCompleted, onFailed);
        }

    #endregion

    #region 유니티 생명주기

        // ------------------------------------------------------------
        /// <summary>
        /// Application 종료 fallback 여부를 기록하고 Runtime을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnApplicationQuit()
        {
            ShutdownFallback();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Host 비활성화 시 조립 Component 파괴 순서보다 먼저 Runtime 소유권을 정리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void OnDisable()
        {
            if (!Application.isPlaying || IsReleased) return;

            ShutdownFallback();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 명시적 Shutdown이 누락된 Host 파괴에서 같은 종료 경로를 실행한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void OnDestroy()
        {
            if (!IsReleased)
            {
                ShutdownFallback();
            }

            base.OnDestroy();
        }

    #endregion

    #region 초기화

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Host 참조와 Settings를 검증하고 Core 서비스와 Root Presentation을 조립한다.
        /// <br/> 구독자 실패를 포함한 초기화 실패는 생성된 소유 리소스를 역순 롤백한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public void Initialize(UISettingsAsset settings)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("UI Runtime이 이미 초기화됐습니다.");
            }

            if (IsReleasing || IsReleased)
            {
                throw new InvalidOperationException("해제 중이거나 해제된 UI Runtime은 초기화할 수 없습니다.");
            }

            var coreReady = false;

            try
            {
                // 기본 Slot은 Scope와 무관하게 검사해 중복 Runtime의 조립 부작용을 만들지 않는다.
                if
                (
                    Named.TryGet(DEFAULT_SLOT, out var current) &&
                    current != null &&
                    !ReferenceEquals(current, this)
                )
                {
                    throw new InvalidOperationException
                    (
                        $"다른 UI Runtime '{current.name}'가 기본 Slot을 이미 소유하고 있습니다."
                    );
                }

                ValidateHost(settings);
                Settings = settings;

                transitioner = new DOTweenPresentationTransitioner();
                inputDriver.Initialize(inputModule, settings);
                inputDriver.OnPolicySettled += HandleInputPolicySettled;
                focusDriver.Initialize();
                focusDriver.OnFocusChanged += HandleFocusChanged;
                sceneFadeSource.Initialize();

                var plan = PresentationLayoutResolver.Resolve(settings.DefaultLayout);
                RootPresentation = PresentationSession.CreateTopLevel
                (
                    plan,
                    presentationRoot,
                    settings.UITKPanelSettingsTemplate,
                    settings.UGUIOutputTemplate,
                    focusDriver.BindLayer,
                    presentationLayerHosts,
                    ownerControlsLifetime: true
                );
                PresentationHost = new PresentationHost
                (
                    RootPresentation,
                    settings.DestinationDefinitions
                );

                ValidateSceneFadeSource(sceneFadeSource, PresentationHost);

                SceneFader = new SceneFader
                (
                    PresentationHost,
                    sceneFadeSource,
                    transitioner
                );
                SceneFader.SetOwnerControlledLifetime();

                Main = new UIContext
                (
                    this,
                    null,
                    RootPresentation,
                    transitioner,
                    focusDriver,
                    inputDriver
                );
                baseContext = Main;
                state = State.Initialized;
                RefreshContextAuthority();
                coreReady = true;
                SubscribeSceneValidation();

                // 초기화 완료 구독자가 동기 Scene 전환을 시작해도 Host가 유지되도록 알림 전에 App 수명으로 확정한다.
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }

                // 완전히 조립된 Runtime만 Current로 공개하고 완료 구독자도 같은 인스턴스를 조회하게 한다.
                if (!TryRegister(this))
                {
                    throw new InvalidOperationException
                    (
                        "다른 UI Runtime이 기본 Slot을 이미 소유하고 있습니다."
                    );
                }

                UIPresentationOutputPolicy.Bind(settings);
                InvokeInitializedSubscribers();

                // 완료 알림 중 명시적으로 종료된 Runtime을 초기화 성공 상태로 반환하지 않는다.
                if (!IsInitialized)
                {
                    throw new InvalidOperationException
                    (
                        "OnInitialized 처리 중 UI Runtime이 종료됐습니다."
                    );
                }
            }
            catch (Exception exception)
            {
                // 완료 콜백에서 이미 종료됐다면 Released 상태를 다시 변경하지 않는다.
                var cleanupErrors = Release(coreReady);

                if (cleanupErrors.Count == 0)
                {
                    throw;
                }

                var errors = new ExceptionBuffer();
                errors.Add(exception);
                errors.AddRange(cleanupErrors);
                throw errors.CreateAggregateException("UI Runtime 초기화와 롤백이 실패했습니다.");
            }
        }

    #endregion

    #region 초기화 구성 검증

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Scene Fade Source가 System destination에서 Driver를 획득하는지 검증한다.
        /// <br/> 획득한 Fade Alpha의 유효성도 함께 확인한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private static void ValidateSceneFadeSource
        (
            IPresentationSource<ISceneFadeDriver> source,
            PresentationHost host
        )
        {
            PresentationLayerLease layerLease = null;
            ISceneFadeDriver view = null;
            Exception validationError = null;

            try
            {
                layerLease = host.AcquireLayer(PresentationDestinationID.System);
                view = source.Acquire(layerLease.Layer);
                var alpha = view.Alpha;

                if (alpha == null || !alpha.IsValid)
                {
                    validationError = new InvalidOperationException
                    (
                        "Scene Fade View가 유효한 Alpha State를 제공하지 않습니다."
                    );
                }
                else
                {
                    alpha.Set(0.0f);
                }
            }
            catch (Exception exception)
            {
                validationError = exception;
            }

            if (view != null)
            {
                try
                {
                    source.Release(view);
                }
                catch (Exception releaseException)
                {
                    validationError = validationError == null
                        ? releaseException
                        : new AggregateException(validationError, releaseException);
                }
            }

            try
            {
                layerLease?.Dispose();
            }
            catch (Exception releaseException)
            {
                validationError = validationError == null
                    ? releaseException
                    : new AggregateException(validationError, releaseException);
            }

            if (validationError != null)
            {
                throw new InvalidOperationException
                (
                    "Scene Fade View Provider 또는 Driver 검증이 실패했습니다.",
                    validationError
                );
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Host, EventSystem, Focus Driver와 Scene Fade Source 참조를 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ValidateHost(UISettingsAsset settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Validate();

            if (!gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException("UI Runtime Host Root가 활성 상태가 아닙니다.");
            }

            if (transform != transform.root)
            {
                throw new InvalidOperationException("UIRuntime은 UI Host Root에 있어야 합니다.");
            }

            if (!enabled)
            {
                throw new InvalidOperationException("UI Runtime Component가 비활성 상태입니다.");
            }

            if (presentationRoot == null)
            {
                throw new InvalidOperationException("UI Presentation 부모 Root가 연결되지 않았습니다.");
            }

            if (presentationRoot.root != transform)
            {
                throw new InvalidOperationException("UI Presentation 부모 Root는 Runtime Host 내부에 있어야 합니다.");
            }

            var requiresMixedEventSystem = settings.SupportsBackend(PresentationBackend.UGUI);
            ValidateInputComposition(settings, requiresMixedEventSystem);

            if (focusDriver == null || !focusDriver.enabled)
            {
                throw new InvalidOperationException("활성 UI Focus Driver가 연결되지 않았습니다.");
            }

            var focusDrivers = GetComponents<UIFocusDriver>();

            if (focusDrivers.Length != 1 || focusDrivers[0] != focusDriver)
            {
                throw new InvalidOperationException
                (
                    "UI Runtime Host에는 직렬화 참조와 일치하는 " +
                    "UIFocusDriver가 정확히 하나 필요합니다."
                );
            }

            ValidatePresentationLayerHosts();

            if (sceneFadeSource == null || !sceneFadeSource.enabled)
            {
                throw new InvalidOperationException("활성 Scene Fade Source가 연결되지 않았습니다.");
            }

            var sceneFadeSources = GetComponents<UISceneFadeSource>();

            if (sceneFadeSources.Length != 1 || sceneFadeSources[0] != sceneFadeSource)
            {
                throw new InvalidOperationException
                (
                    "UI Runtime Host에는 직렬화 참조와 일치하는 " +
                    "Scene Fade Source가 정확히 하나 필요합니다."
                );
            }

            if (inputDriver == null || !inputDriver.isActiveAndEnabled)
            {
                throw new InvalidOperationException("활성 Input System Screen Input Driver가 연결되지 않았습니다.");
            }

            if (inputDriver.transform.root != transform)
            {
                throw new InvalidOperationException
                (
                    "Input System Screen Input Driver는 Runtime Host 내부에 있어야 합니다."
                );
            }

            ValidateSceneComposition(requiresMixedEventSystem);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Scene-authored Presentation Layer/Placement Host 참조를 검증한다.
        /// <br/> Host가 현재 Runtime composition에 속하는지도 확인한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ValidatePresentationLayerHosts()
        {
            presentationLayerHosts ??= Array.Empty<PresentationLayerHost>();
            var layerIDs = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < presentationLayerHosts.Length; index++)
            {
                var host = presentationLayerHosts[index];

                if (host == null)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host {index} 참조가 비어 있습니다."
                    );
                }

                if (host.transform.root != transform)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host '{host.name}'은 Runtime Host 내부에 있어야 합니다."
                    );
                }

                if
                (
                    string.IsNullOrWhiteSpace(host.LayerID) ||
                    !layerIDs.Add(host.LayerID)
                )
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host Layer ID '{host.LayerID}'가 비어 있거나 중복됐습니다."
                    );
                }

                var placementHosts = host.PlacementHosts;

                for (var placementIndex = 0; placementIndex < placementHosts.Count; placementIndex++)
                {
                    var placementHost = placementHosts[placementIndex];

                    if (placementHost == null)
                    {
                        throw new InvalidOperationException
                        (
                            $"Layer Host '{host.LayerID}' Placement Host {placementIndex} 참조가 비어 있습니다."
                        );
                    }

                    if (placementHost.transform.root != transform)
                    {
                        throw new InvalidOperationException
                        (
                            $"Presentation Placement Host '{placementHost.name}'은 " +
                            "Runtime Host 내부에 있어야 합니다."
                        );
                    }
                }
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Layout backend 요구에 맞춰 UITK-only 또는 mixed UGUI 입력 구성을 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ValidateInputComposition
        (
            UISettingsAsset settings,
            bool requiresMixedEventSystem
        )
        {
            if (!requiresMixedEventSystem)
            {
                if (settings.UIActionsAsset == null)
                {
                    throw new InvalidOperationException
                    (
                        "UITK-only Runtime은 UISettingsAsset.UIActionsAsset을 명시해야 합니다."
                    );
                }

                return;
            }

            if (eventSystem == null || !eventSystem.enabled)
            {
                throw new InvalidOperationException("활성 EventSystem이 연결되지 않았습니다.");
            }

            if (eventSystem.transform.root != transform)
            {
                throw new InvalidOperationException("EventSystem은 Runtime Host 내부에 있어야 합니다.");
            }

            if (inputModule == null || !inputModule.enabled)
            {
                throw new InvalidOperationException("활성 InputSystemUIInputModule이 연결되지 않았습니다.");
            }

            if (inputModule.GetComponent<EventSystem>() != eventSystem)
            {
                throw new InvalidOperationException
                (
                    "Input Module과 EventSystem이 같은 Host에 연결되지 않았습니다."
                );
            }

            ValidateInputModuleActions(inputModule, settings);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> EventSystem에 필요한 UI Action Reference가 Settings의 UI Map에
        /// <br/> 실제로 연결됐는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ValidateInputModuleActions
        (
            InputSystemUIInputModule inputModule,
            UISettingsAsset settings
        )
        {
            var actionsAsset = inputModule.actionsAsset;

            if (actionsAsset == null)
            {
                throw new InvalidOperationException
                (
                    "InputSystemUIInputModule Actions Asset이 연결되지 않았습니다."
                );
            }

            var uiActionMap = actionsAsset.FindActionMap(settings.UIActionMap, false);

            if (uiActionMap == null)
            {
                throw new InvalidOperationException
                (
                    $"InputSystemUIInputModule에서 UI Action Map '{settings.UIActionMap}'을 찾을 수 없습니다."
                );
            }

            ValidateInputModuleAction(inputModule.point, uiActionMap, "Point");
            ValidateInputModuleAction(inputModule.move, uiActionMap, "Move");
            ValidateInputModuleAction(inputModule.submit, uiActionMap, "Submit");
            ValidateInputModuleAction(inputModule.cancel, uiActionMap, "Cancel");
            ValidateInputModuleAction(inputModule.leftClick, uiActionMap, "Left Click");
            ValidateInputModuleAction(inputModule.scrollWheel, uiActionMap, "Scroll Wheel");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Input Module Action Reference 하나의 UI Map 소속을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ValidateInputModuleAction
        (
            InputActionReference reference,
            InputActionMap uiActionMap,
            string role
        )
        {
            var action = reference?.action;

            if (action == null || !ReferenceEquals(action.actionMap, uiActionMap))
            {
                throw new InvalidOperationException
                (
                    $"InputSystemUIInputModule {role} Action이 Settings UI Map에 연결되지 않았습니다."
                );
            }
        }

    #endregion

    #region 씬 구성과 감시

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Runtime backend 요구에 맞는 Scene 단일 구성을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ValidateSceneComposition()
        {
            var validateEventSystem =
                Settings != null &&
                Settings.SupportsBackend(PresentationBackend.UGUI);
            ValidateSceneComposition(validateEventSystem);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Runtime 중복과 필요할 때만 mixed UGUI EventSystem 중복을 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ValidateSceneComposition(bool validateEventSystem)
        {
            var runtimes = FindObjectsByType<UIRuntime>
            (
                FindObjectsInactive.Include
            );

            for (var i = 0; i < runtimes.Length; i++)
            {
                if (runtimes[i] != this)
                {
                    throw new InvalidOperationException
                    (
                        $"다른 UI Runtime Host '{runtimes[i].name}'가 이미 로드되어 있습니다."
                    );
                }
            }

            if (!validateEventSystem) return;

            var eventSystems = FindObjectsByType<EventSystem>
            (
                FindObjectsInactive.Include
            );

            for (var i = 0; i < eventSystems.Length; i++)
            {
                if (eventSystems[i] != eventSystem)
                {
                    throw new InvalidOperationException
                    (
                        $"다른 EventSystem '{eventSystems[i].name}'가 이미 로드되어 있습니다."
                    );
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 초기화 뒤 Scene 로드 경계 검증을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void SubscribeSceneValidation()
        {
            if (sceneLoadedSubscribed) return;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            sceneLoadedSubscribed = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 종료 전에 Scene 로드 경계 검증을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnsubscribeSceneValidation()
        {
            if (!sceneLoadedSubscribed) return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            sceneLoadedSubscribed = false;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 새 Scene이 추가한 중복 Runtime 또는 EventSystem을 명시적 오류로 보고한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleSceneLoaded
        (
            Scene scene,
            LoadSceneMode mode
        )
        {
            if (!IsInitialized) return;

            try
            {
                ValidateSceneComposition();
            }
            catch (Exception exception)
            {
                Debug.LogException
                (
                    new InvalidOperationException
                    (
                        $"Scene '{scene.name}'의 UI 구성이 유효하지 않습니다.",
                        exception
                    ),
                    this
                );
            }
        }

    #endregion

    #region 종료

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Main Context Tree, 프로젝트 Composition, Fade, Root Presentation과
        /// <br/> 공통 서비스를 역순 해제한다.
        /// <br/> 논리 소유권은 한 번만 정리하고 Runtime은 오류와 관계없이
        /// <br/> Terminal 상태로 끝난다.
        /// <br/> 후속 Shutdown은 상태 변경 전에 거부되어 Runtime 소유권이 남은
        /// <br/> Root PresentationSession만 다시 정리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public void Shutdown()
        {
            var errors = Release(invokeReleasingEvent: true);

            if (errors.Count > 0)
            {
                throw new AggregateException("UI Runtime 종료 중 하나 이상의 정리가 실패했습니다.", errors);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity 종료 callback에서 같은 종료 오류를 기록만 한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ShutdownFallback()
        {
            var errors = Release(invokeReleasingEvent: true);

            for (var i = 0; i < errors.Count; i++)
            {
                if (errors[i] != null)
                {
                    Debug.LogException(errors[i], this);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재까지 생성된 Runtime 소유 리소스를 해제하고 오류를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private IReadOnlyList<Exception> Release(bool invokeReleasingEvent)
        {
            var errors = new ExceptionBuffer();

            if (IsReleasing) return errors.AsReadOnlyList();

            if (IsReleased)
            {
                if (RootPresentation != null && !RootPresentation.IsDisposed)
                {
                    DisposePresentationSession(RootPresentation, ref errors);

                    if (RootPresentation.IsDisposed)
                    {
                        RootPresentation = null;
                    }
                }

                return errors.AsReadOnlyList();
            }

            state = State.Releasing;
            UIPresentationOutputPolicy.Unbind(Settings);
            Unregister(this);
            var main = Main;
            var sceneFader = SceneFader;
            var presentationHost = PresentationHost;
            var rootPresentation = RootPresentation;
            var input = inputDriver;
            var currentTransitioner = transitioner;
            var currentSceneFadeSource = sceneFadeSource;
            var releasingSubscribers = invokeReleasingEvent ? OnReleasing : null;

            UnsubscribeSceneValidation();

            if (focusDriver != null)
            {
                focusDriver.OnFocusChanged -= HandleFocusChanged;
            }

            if (input != null)
            {
                input.OnPolicySettled -= HandleInputPolicySettled;
            }

            if (main != null)
            {
                try
                {
                    errors.AddRange(main.DisposeFromRuntime());
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            InvokeReleasingSubscribers(releasingSubscribers, ref errors);

            DisposeSceneFader(sceneFader, ref errors);
            DisposeOwned(currentSceneFadeSource, ref errors);
            DisposeOwned(input, ref errors);
            DisposeOwned(currentTransitioner, ref errors);
            ReleasePresentationHost(presentationHost, ref errors);
            DisposePresentationSession(rootPresentation, ref errors);

            // 모든 Context와 Layer binding을 반환한 뒤 native Focus 구독을 해제한다.
            if (focusDriver != null)
            {
                try
                {
                    focusDriver.Release();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            Main = null;
            SceneFader = null;
            PresentationHost = null;
            baseContext = null;
            contextOverrides.Clear();
            activeContextPath.Clear();
            authorityCleanupContexts.Clear();
            contextAuthorityRecoveryPending = false;
            // 권한 적용 콜백 안에서 Shutdown이 재진입했다면 작업 목록은
            // 바깥 갱신이 종료될 때 정리해 진행 중인 순회를 변경하지 않는다.
            if (!isRefreshingContextAuthority)
            {
                nextContextPath.Clear();
                authorityCleanupCandidates.Clear();
            }

            inputDriver = null;
            transitioner = null;
            Settings = null;
            OnInitialized = null;
            OnReleasing = null;

            if (rootPresentation == null || rootPresentation.IsDisposed)
            {
                RootPresentation = null;
            }

            state = State.Released;
            return errors.AsReadOnlyList();
        }

    #endregion

    #region 수명 이벤트 전달

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Runtime 초기화 observer를 독립적으로 호출해 한 observer 실패가
        /// <br/> 뒤 observer를 차단하지 않게 한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void InvokeInitializedSubscribers()
        {
            var subscribers = OnInitialized;
            if (subscribers == null) return;

            var errors = new ExceptionBuffer();

            foreach (Action<UIRuntime> subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber.Invoke(this);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                if (IsInitialized) continue;

                errors.Add
                (
                    new InvalidOperationException
                    (
                        "OnInitialized 처리 중 UI Runtime이 종료됐습니다."
                    )
                );
                break;
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException
            (
                "UI Runtime 초기화 observer 처리 중 오류가 발생했습니다."
            );
        }

    #endregion

    #region 소유 자원 해제

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Runtime release observer를 독립적으로 호출해 한 observer 실패가
        /// <br/> 뒤 observer를 차단하지 않게 한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void InvokeReleasingSubscribers
        (
            Action<UIRuntime> subscribers,
            ref ExceptionBuffer errors
        )
        {
            if (subscribers == null) return;

            foreach (Action<UIRuntime> subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber.Invoke(this);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation Host의 소유 상태를 반환하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ReleasePresentationHost
        (
            PresentationHost host,
            ref ExceptionBuffer errors
        )
        {
            if (host == null) return;

            try
            {
                host.Release();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Scene Fader의 소유 상태를 반환하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DisposeSceneFader
        (
            SceneFader sceneFader,
            ref ExceptionBuffer errors
        )
        {
            if (sceneFader == null) return;

            try
            {
                sceneFader.DisposeFromOwner();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation Session의 소유 상태를 반환하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DisposePresentationSession
        (
            PresentationSession session,
            ref ExceptionBuffer errors
        )
        {
            if (session == null) return;

            try
            {
                session.DisposeFromOwner();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 소유 자원의 해제를 시도하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DisposeOwned
        (
            IDisposable owned,
            ref ExceptionBuffer errors
        )
        {
            if (owned == null) return;

            try
            {
                owned.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

    #endregion

    #region 컨텍스트 권한 선택과 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Context를 peer activation의 Base Context로 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SetBaseContext(UIContext context)
        {
            ValidateContextAuthorityTarget(context);

            if (ReferenceEquals(baseContext, context)) return;

            var previous = baseContext;
            baseContext = context;

            try
            {
                RefreshContextAuthority();
            }
            catch (Exception exception)
            {
                // 콜백이 더 새로운 Base를 선택했다면 실패한 이전 요청만 보고한다.
                if (!ReferenceEquals(baseContext, context))
                {
                    throw;
                }

                baseContext = previous;

                try
                {
                    RefreshContextAuthority();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Base Context authority 적용과 topology 롤백이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Context를 Context Override Stack top으로 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        internal Lease PushContextOverride(UIContext context)
        {
            ValidateContextAuthorityTarget(context);

            if (contextOverrides.Contains(context))
            {
                throw new InvalidOperationException("같은 UIContext를 Override Stack에 중복 추가할 수 없습니다.");
            }

            contextOverrides.Add(context);

            try
            {
                RefreshContextAuthority();
            }
            catch (Exception exception)
            {
                contextOverrides.Remove(context);

                try
                {
                    RefreshContextAuthority();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Context Override authority 적용과 topology 롤백이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }

            return new Lease(() => ReleaseContextOverride(context));
        }

        private void ReleaseContextOverride(UIContext context)
        {
            var index = contextOverrides.IndexOf(context);
            if (index < 0) return;

            contextOverrides.RemoveAt(index);
            RefreshContextAuthority();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 종료할 Context subtree를 Base/Override authority에서
        /// <br/> 제거하고 필요하면 살아 있는 Parent를 Base로 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ReleaseContextAuthority
        (
            UIContext context,
            bool restoreAuthority
        )
        {
            if (context == null) return;

            for (var index = contextOverrides.Count - 1; index >= 0; index--)
            {
                if (context.Contains(contextOverrides[index]))
                {
                    contextOverrides.RemoveAt(index);
                }
            }

            if (baseContext != null && context.Contains(baseContext))
            {
                baseContext = restoreAuthority
                    ? FindAuthorityParent(context.Parent)
                    : null;
            }

            RefreshContextAuthority();
        }

        internal bool IsEffectiveContext(UIContext context)
        {
            return context != null && ReferenceEquals(GetActiveContext(), context);
        }

        internal bool IsContextOnActivePath(UIContext context)
        {
            return context != null && activeContextPath.Contains(context);
        }

    #endregion

    #region 컨텍스트 권한 적용과 복구

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 정책이 안정화되면 중단됐던 Context 권한 복원을 재시도한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleInputPolicySettled()
        {
            if (!contextAuthorityRecoveryPending || !IsInitialized) return;

            contextAuthorityRecoveryPending = false;
            RefreshContextAuthority();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Context Authority 변경을 한 번에 하나씩 적용한다.
        /// <br/> 적용 중 콜백이 topology를 바꾸면 중첩 실행 대신 최종 경로를 다시 계산한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void RefreshContextAuthority()
        {
            contextAuthorityRefreshRequested = true;

            if (isRefreshingContextAuthority) return;

            isRefreshingContextAuthority = true;
            var errors = new ExceptionBuffer();

            try
            {
                while (contextAuthorityRefreshRequested && !IsReleased)
                {
                    contextAuthorityRefreshRequested = false;
                    ApplyContextAuthority(ref errors);
                }
            }
            finally
            {
                isRefreshingContextAuthority = false;

                if (IsReleased)
                {
                    nextContextPath.Clear();
                    authorityCleanupCandidates.Clear();
                }
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException
            (
                "UI Runtime Context authority 갱신 중 하나 이상의 적용이 실패했습니다."
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 마지막으로 요청된 Context 경로를 계산하고 이전 권한을 먼저 반환한다.
        /// <br/> 콜백이 topology를 변경했다면 오래된 경로의 적용을 중단한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplyContextAuthority(ref ExceptionBuffer errors)
        {
            nextContextPath.Clear();
            var effective = GetRequestedAuthorityContext();
            effective?.AppendPathTo(nextContextPath);
            var cursorContext = FindCursorPolicyContext(nextContextPath);

            // 새 경로의 입력 기여가 적용 불가능하다면 기존 경로의 권한을 먼저 반환하지 않는다.
            // 최종 실제 적용에서도 검사를 반복해 외부 callback이 만든 상태 변화를 검증한다.
            for (var index = 0; index < nextContextPath.Count; index++)
            {
                if (contextAuthorityRefreshRequested || IsReleased) return;

                try
                {
                    nextContextPath[index].ValidateInputAuthorityActivation();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                    return;
                }
            }

            // 실패했던 반환은 성공할 때까지 추적하고, 이전 논리 경로도 정리 대상으로 합친다.
            authorityCleanupCandidates.Clear();
            AddUniqueContexts(authorityCleanupCandidates, authorityCleanupContexts);
            AddUniqueContexts(authorityCleanupCandidates, activeContextPath);

            for (var index = 0; index < authorityCleanupCandidates.Count; index++)
            {
                if (contextAuthorityRefreshRequested || IsReleased) return;

                var context = authorityCleanupCandidates[index];

                if (nextContextPath.Contains(context))
                {
                    authorityCleanupContexts.Remove(context);
                    continue;
                }

                try
                {
                    context.SetAuthorityState(false, false, false);
                    authorityCleanupContexts.Remove(context);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);

                    if (!context.IsDisposed && !context.IsDisposing)
                    {
                        if (!authorityCleanupContexts.Contains(context))
                        {
                            authorityCleanupContexts.Add(context);
                        }
                    }
                    else
                    {
                        authorityCleanupContexts.Remove(context);
                    }
                }
            }

            if (contextAuthorityRefreshRequested || IsReleased) return;

            // 이전 Context의 권한 반환이 완료되기 전에는 다음 경로에 입력·Focus
            // 소유권을 부여하지 않는다. 실패한 반환은 다음 Refresh에서 먼저 재시도한다.
            if (authorityCleanupContexts.Count > 0) return;

            activeContextPath.Clear();
            activeContextPath.AddRange(nextContextPath);

            for (var index = 0; index < activeContextPath.Count; index++)
            {
                if (contextAuthorityRefreshRequested || IsReleased) return;

                var context = activeContextPath[index];

                try
                {
                    context.SetAuthorityState
                    (
                        true,
                        ReferenceEquals(context, effective),
                        ReferenceEquals(context, cursorContext)
                    );
                }
                catch (Exception exception)
                {
                    errors.Add(exception);

                    // 적용 중 열린 Batch는 지금 롤백까지 막을 수 있으므로 안정화 뒤에 복구한다.
                    if (inputDriver != null && inputDriver.IsPolicyDeferred)
                    {
                        contextAuthorityRecoveryPending = true;
                    }

                    // 일부만 적용된 경로를 정리하고, 실패한 반환은 후속 갱신에 남긴다.
                    AbandonFailedContextActivation(ref errors);
                    return;
                }
            }

            contextAuthorityRecoveryPending = false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패한 활성화 경로의 backend 권한을 반환하고 미완료 반환을 추적한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AbandonFailedContextActivation(ref ExceptionBuffer errors)
        {
            // 종료·입력 콜백이 활성 경로를 변경할 수 있으므로 현재 반환 대상을 고정한다.
            var abandonedPath = new List<UIContext>(activeContextPath);
            AddUniqueContexts(authorityCleanupContexts, abandonedPath);

            // 부분적으로 획득한 Input·Cursor·Focus도 반대 순서로 반환한다.
            for (var index = abandonedPath.Count - 1; index >= 0; index--)
            {
                if (IsReleased) break;

                var context = abandonedPath[index];

                try
                {
                    context.SetAuthorityState(false, false, false);
                    authorityCleanupContexts.Remove(context);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);

                    if (context.IsDisposed || context.IsDisposing)
                    {
                        authorityCleanupContexts.Remove(context);
                    }
                }
            }

            activeContextPath.Clear();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Context reference 목록을 중복 없이 대상 목록에 병합한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AddUniqueContexts
        (
            List<UIContext> target,
            IReadOnlyList<UIContext> source
        )
        {
            for (var index = 0; index < source.Count; index++)
            {
                var context = source[index];
                if (context == null || target.Contains(context)) continue;

                target.Add(context);
            }
        }

    #endregion

    #region 컨텍스트 권한 조회와 검증

        private UIContext GetRequestedAuthorityContext()
        {
            return contextOverrides.Count > 0
                ? contextOverrides[contextOverrides.Count - 1]
                : baseContext;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 경로 끝에서 실제 Focus 권한을 보유한 Context만 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private UIContext GetActiveContext()
        {
            var count = activeContextPath.Count;
            if (count == 0) return null;

            var context = activeContextPath[count - 1];
            return context != null && context.HasFocusAuthority ? context : null;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Active Context path에서 가장 깊은 Screen Stack Cursor owner를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static UIContext FindCursorPolicyContext(IReadOnlyList<UIContext> path)
        {
            for (var index = path.Count - 1; index >= 0; index--)
            {
                var context = path[index];

                if (context != null && context.Screens.HasCursorPolicySource)
                {
                    return context;
                }
            }

            return null;
        }

        private void ValidateContextAuthorityTarget(UIContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThrowIfUnavailable();

            if (context.IsDisposing || context.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(context));
            }

            if (Main == null || !Main.Contains(context))
            {
                throw new InvalidOperationException("현재 UIRuntime Context Tree 밖의 Context에 authority를 줄 수 없습니다.");
            }
        }

        private static UIContext FindAuthorityParent(UIContext current)
        {
            while (current != null)
            {
                if (!current.IsDisposing && !current.IsDisposed)
                {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }

    #endregion

    #region 사용 가능 상태 검증

        // ------------------------------------------------------------
        /// <summary>
        /// Child Context를 생성할 수 있는 Runtime 상태인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ThrowIfContextCreationUnavailable()
        {
            ThrowIfUnavailable();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// native Focus 변경을 권한 인계가 확정된 활성 Context의 Focus containment에 전달한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void HandleFocusChanged(object current)
        {
            if (!IsInitialized) return;

            GetActiveContext()?.HandleNativeFocusChanged(current);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 초기화되지 않았거나 종료 중인 Runtime의 사용을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfUnavailable()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("UI Runtime이 사용 가능한 상태가 아닙니다.");
            }
        }

    #endregion

    }
}
