/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIRuntime.cs
수정일 : 2026-09-17
# 설명
App 단위 Singleton 등록, Main UI Context, 공용 Transition, 혼합 Layer Profile과 Scene Fade의 조립·역순 해제를 소유한다.
Render Pipeline별 UI 우회 경로 없이 Unity native UI output을 전제로 동작한다.
Shutdown은 일반 소유 객체를 한 번씩 정리하고, 사전 조건에서 거부된 Profile과 Layer Registry 소유권만 유지한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Runtime의 명시적 Composition Root.
    /// </summary>
    // ============================================================
    public sealed class UIRuntime : MonoSingleton<UIRuntime>
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 초기화가 완료됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsInitialized { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime이 해제 중인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsReleasing { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime Core 해제가 완료됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsReleased { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime이 사용하는 Settings.
        /// </summary>
        // ------------------------------------------------------------
        public UISettingsAsset Settings { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation Layer 등록과 소비자 수명 Registry.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationLayerRegistry LayerRegistry { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 일반 UI와 전체 Child Context Tree의 Root.
        /// </summary>
        // ------------------------------------------------------------
        public UIContext Main { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// App 기본 Profile Layer를 사용하는 Scene Fade 서비스.
        /// </summary>
        // ------------------------------------------------------------
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
        private Transform layerRoot = null;

        [SerializeField]
        private UISceneFadeSource sceneFadeSource = null;

        [SerializeField]
        private EventSystem eventSystem = null;

        [SerializeField]
        private InputSystemUIInputModule inputModule = null;

        private readonly List<UIProfileHandle> profileHandles =
            new List<UIProfileHandle>();

        private PresentationLayerRegistry pendingLayerRegistry = null;
        private UIProfileHandle defaultProfile = null;
        private UIContext focusedContext = null;
        private bool sceneLoadedSubscribed = false;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Core와 기본 Profile 조립이 완료된 뒤 발생한다.
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

    #region Presentation Transition

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

    #region Unity 이벤트

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
        /// <br/> Host 참조와 Settings를 검증하고 Core 서비스와 기본 Profile을 조립한다.
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

                LayerRegistry = new PresentationLayerRegistry();
                transitioner = new DOTweenPresentationTransitioner();
                inputDriver.Initialize(inputModule, settings);
                focusDriver.Initialize();
                focusDriver.OnFocusChanged += HandleFocusChanged;
                sceneFadeSource.Initialize();

                defaultProfile = AcquireProfileInternal(settings.DefaultProfile);

                if (!LayerRegistry.Contains(settings.SceneFadeLayerID))
                {
                    throw new InvalidOperationException
                    (
                        $"기본 Profile에 Scene Fade Layer '{settings.SceneFadeLayerID}'가 없습니다."
                    );
                }

                if (!LayerRegistry.TryGet(settings.SceneFadeLayerID, out var fadeLayer))
                {
                    throw new InvalidOperationException
                    (
                        $"Scene Fade Layer '{settings.SceneFadeLayerID}' Driver를 조회할 수 없습니다."
                    );
                }

                ValidateSceneFadeSource(sceneFadeSource, fadeLayer);

                SceneFader = new SceneFader
                (
                    LayerRegistry,
                    settings.SceneFadeLayerID,
                    sceneFadeSource,
                    transitioner
                );

                Main = new UIContext
                (
                    this,
                    null,
                    LayerRegistry,
                    transitioner,
                    focusDriver,
                    inputDriver
                );
                focusedContext = Main;
                IsInitialized = true;
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

                OnInitialized?.Invoke(this);

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
                IsInitialized = false;
                var errors = Release(coreReady);

                if (errors.Count == 0)
                {
                    throw;
                }

                errors.Insert(0, exception);
                throw new AggregateException("UI Runtime 초기화와 롤백이 실패했습니다.", errors);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// App·Scene·게임 모드 Layer Profile을 명시적으로 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        public UIProfileHandle AcquireProfile(UIProfileAsset profile)
        {
            ThrowIfUnavailable();
            return AcquireProfileInternal(profile);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Provider Layer Root를 모두 준비한 뒤 Registry에 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        private UIProfileHandle AcquireProfileInternal(UIProfileAsset profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            profile.Validate();

            var handle = new UIProfileHandle
            (
                profile,
                HandleProfileReleaseCompleted
            );
            profileHandles.Add(handle);
            var prepared =
                new List<(UIProfileHandle.OwnedLayer Layer, PresentationLayerAsset Asset, IPresentationLayerDriver Driver)>();

            try
            {
                for (var i = 0; i < profile.Count; i++)
                {
                    var asset = profile.GetLayerAsset(i);
                    var provider = profile.GetProvider(i);
                    var previousParent = provider.Parent;
                    GameObject instance = null;
                    UIProfileHandle.OwnedLayer ownedLayer = null;

                    try
                    {
                        provider.Parent = layerRoot;
                        instance = provider.Acquire(false);

                        if (instance != null)
                        {
                            try
                            {
                                // Handle에 기록된 시점부터 후속 Layer 준비 실패를 Profile 정리가 담당한다.
                                ownedLayer = handle.AddLayer(provider, instance);
                            }
                            catch (Exception exception)
                            {
                                try
                                {
                                    // 종료된 Handle이 받지 못한 미확정 인스턴스는 현재 획득 경로가 한 번 반환한다.
                                    provider.Release(instance, false);
                                }
                                catch (Exception cleanupException)
                                {
                                    throw new AggregateException(exception, cleanupException);
                                }

                                throw;
                            }
                        }
                    }
                    finally
                    {
                        provider.Parent = previousParent;
                    }

                    if (instance == null)
                    {
                        throw new InvalidOperationException
                        (
                            $"Profile Layer '{asset.ID}' Provider가 null 인스턴스를 반환했습니다."
                        );
                    }

                    instance.SetActive(false);

                    var driver = GetLayerDriver(instance);

                    prepared.Add((ownedLayer, asset, driver));
                }

                for (var i = 0; i < prepared.Count; i++)
                {
                    var item = prepared[i];

                    if (!item.Driver.Validate(item.Asset, out var error))
                    {
                        throw new InvalidOperationException
                        (
                            $"Profile Layer '{item.Asset.ID}' 구성이 유효하지 않습니다. {error}"
                        );
                    }
                }

                for (var i = 0; i < prepared.Count; i++)
                {
                    var item = prepared[i];
                    var layerHandle = LayerRegistry.Register(item.Asset, item.Driver);
                    handle.AttachLayerHandle(item.Layer, layerHandle);
                    focusDriver.RegisterLayer(item.Driver);
                }

                return handle;
            }
            catch (Exception exception)
            {
                try
                {
                    handle.Dispose();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        $"UI Profile '{profile.name}' 획득과 롤백이 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Terminal Profile Handle을 Runtime 추적에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleProfileReleaseCompleted(UIProfileHandle handle)
        {
            profileHandles.Remove(handle);

            if (ReferenceEquals(defaultProfile, handle))
            {
                defaultProfile = null;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Layer Prefab Root에서 기술과 무관하게
        /// <br/> Presentation Layer Driver 하나를 찾는다.
        /// <br/> Root 소유권이 모호한 0개 또는 복수 구성은 Registry 공개 전에 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static IPresentationLayerDriver GetLayerDriver(GameObject instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var components = instance.GetComponents<MonoBehaviour>();
            IPresentationLayerDriver driver = null;

            for (var i = 0; i < components.Length; i++)
            {
                if (!(components[i] is IPresentationLayerDriver candidate)) continue;

                if (driver != null)
                {
                    throw new InvalidOperationException
                    (
                        $"Profile Layer '{instance.name}' Root에는 " +
                        "IPresentationLayerDriver가 정확히 하나 필요합니다."
                    );
                }

                driver = candidate;
            }

            if (driver == null)
            {
                throw new InvalidOperationException
                (
                    $"Profile Layer '{instance.name}' Root에 IPresentationLayerDriver가 없습니다."
                );
            }

            return driver;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Fade View Provider와 Driver를 초기화 시점에 한 번 획득·반환해 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ValidateSceneFadeSource
        (
            IPresentationSource<ISceneFadeDriver> source,
            IPresentationLayerDriver layer
        )
        {
            ISceneFadeDriver view = null;
            Exception validationError = null;

            try
            {
                view = source.Acquire(layer);
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

            if (layerRoot == null)
            {
                throw new InvalidOperationException("UI Layer 부모 Root가 연결되지 않았습니다.");
            }

            if (layerRoot.root != transform)
            {
                throw new InvalidOperationException("UI Layer 부모 Root는 Runtime Host 내부에 있어야 합니다.");
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
                throw new InvalidOperationException("Input Module과 EventSystem이 같은 Host에 연결되지 않았습니다.");
            }

            ValidateInputModuleActions(inputModule, settings);

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

            if (inputDriver == null || !inputDriver.enabled)
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

            ValidateSceneComposition();
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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 현재 로드된 Scene 전체에서 Runtime Host와 EventSystem의 단일 구성을 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void ValidateSceneComposition()
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
            if (!IsInitialized || IsReleasing || IsReleased) return;

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

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Main Context Tree, 프로젝트 Composition, Fade, Profile과
        /// <br/> 공통 서비스를 역순 해제한다.
        /// <br/> 논리 소유권은 한 번만 정리하고 Runtime은 오류와 관계없이
        /// <br/> Terminal 상태로 끝난다.
        /// <br/> 후속 Shutdown은 상태 변경 전에 거부되어 Runtime 소유권이 남은
        /// <br/> Profile과 Layer Registry만 다시 정리한다.
        /// </summary>
        // ----------------------------------------------------------------------
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
        private List<Exception> Release(bool invokeReleasingEvent)
        {
            var errors = new List<Exception>();

            if (IsReleasing) return errors;

            if (IsReleased)
            {
                // 활성 소비자 사전 조건에서 상태 변경 전에 거부된 소유권만 남는다.
                ReleaseProfileHandles(errors);

                DisposePendingLayerRegistry(errors);
                return errors;
            }

            IsInitialized = false;
            IsReleasing = true;

            // 종료가 시작된 Runtime을 새 UI 작업에서 조회하지 않도록 public Singleton 경계를 먼저 닫는다.
            Unregister(this);
            var main = Main;
            var sceneFader = SceneFader;
            var input = inputDriver;
            var currentTransitioner = transitioner;
            var layerRegistry = LayerRegistry;
            var currentSceneFadeSource = sceneFadeSource;
            var releasingSubscribers =
                invokeReleasingEvent
                    ? OnReleasing
                    : null;

            UnsubscribeSceneValidation();

            if (focusDriver != null)
            {
                focusDriver.OnFocusChanged -= HandleFocusChanged;
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

            if (releasingSubscribers != null)
            {
                try
                {
                    releasingSubscribers.Invoke(this);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            // 종료 구독자가 살아 있는 Runtime 서비스를 확인한 뒤 public 접근 경계를 닫는다.
            Main = null;
            SceneFader = null;
            defaultProfile = null;
            inputDriver = null;
            transitioner = null;
            LayerRegistry = null;
            focusedContext = null;
            Settings = null;
            OnInitialized = null;
            OnReleasing = null;

            // Fader가 보유한 View를 Source에 먼저 반환한 뒤 Source 자체를 종료한다.
            DisposeOwned(sceneFader, errors);
            DisposeOwned(currentSceneFadeSource, errors);
            DisposeOwned(input, errors);

            // 시작된 종료는 결과와 관계없이 Callback이 제거하고 사전 조건에서 거부된 소유권만 보존한다.
            ReleaseProfileHandles(errors);

            DisposeOwned(currentTransitioner, errors);
            pendingLayerRegistry = layerRegistry;
            DisposePendingLayerRegistry(errors);

            IsReleasing = false;
            IsReleased = true;

            return errors;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Runtime 추적에서 Profile 소유권을 하나씩 분리해 종료하고,
        /// <br/> 상태 변경 전 사전 조건에서 거부된 Handle만 다시 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReleaseProfileHandles(List<Exception> errors)
        {
            if (profileHandles.Count == 0) return;

            var retainedProfiles = new List<UIProfileHandle>();

            // 외부 Provider callback이 다른 Profile을 해제해도 현재 종료 대상 인덱스는 변하지 않는다.
            while (profileHandles.Count > 0)
            {
                var index = profileHandles.Count - 1;
                var handle = profileHandles[index];
                profileHandles.RemoveAt(index);
                DisposeOwned(handle, errors);

                if (!handle.IsDisposed)
                {
                    retainedProfiles.Add(handle);
                }
            }

            // 역순으로 분리한 사전 조건 거부 Handle의 기존 획득 순서를 복원한다.
            for (var i = retainedProfiles.Count - 1; i >= 0; i--)
            {
                if (!retainedProfiles[i].IsDisposed)
                {
                    profileHandles.Add(retainedProfiles[i]);
                }
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Terminal 상태로 분리한 IDisposable 서비스를 한 번 해제하고 오류를 수집한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void DisposeOwned
        (
            IDisposable owned,
            List<Exception> errors
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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 활성 소비자 사전 조건에서 거부된 Layer Registry 소유권을 Terminal 전까지만 유지한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void DisposePendingLayerRegistry(List<Exception> errors)
        {
            var registry = pendingLayerRegistry;

            if (registry == null) return;

            DisposeOwned(registry, errors);

            if (registry.IsDisposed)
            {
                pendingLayerRegistry = null;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Child Context를 생성할 수 있는 Runtime 상태인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ThrowIfContextCreationUnavailable()
        {
            ThrowIfUnavailable();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Context에 실제 Focus Driver 적용 권한을 넘긴다.
        /// </summary>
        // ------------------------------------------------------------
        internal void FocusContext(UIContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThrowIfUnavailable();

            if (ReferenceEquals(focusedContext, context)) return;

            SetFocusedContext(context, selectFallbackWhenEmpty: false);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 지정 Context의 Focus 권한을 가장 가까운 살아 있는 Parent에 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void UnfocusContext(UIContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThrowIfUnavailable();

            if (!ReferenceEquals(focusedContext, context)) return;

            var fallback = FindFocusableParent(context.Parent);
            SetFocusedContext(fallback, selectFallbackWhenEmpty: true);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 종료할 Subtree가 현재 Focus를 포함하면 외부 Parent 또는 빈 상태로 권한을 옮긴다.
        /// <br/> Runtime Shutdown에서는 새 Context나 fallback 대상을 선택하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void ReleaseContextFocus
        (
            UIContext context,
            bool restoreFocus
        )
        {
            if (focusedContext == null || !context.Contains(focusedContext)) return;

            SetFocusedContext
            (
                restoreFocus ? FindFocusableParent(context.Parent) : null,
                selectFallbackWhenEmpty: restoreFocus
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 지정 Context가 현재 실제 Focus Driver 적용 권한을 갖는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal bool IsFocusedContext(UIContext context) => ReferenceEquals(focusedContext, context);

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 안정화된 native Focus가 있으면 현재 Context Top Screen의
        /// <br/> 마지막 선택으로 기록하고,
        /// <br/> Focus가 실제로 비었을 때만 기존 복원 정책을 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleFocusChanged(object current)
        {
            if (!IsInitialized || IsReleasing || IsReleased) return;

            var context = focusedContext;

            if (context == null) return;

            if (focusDriver.IsValid(current))
            {
                context.RecordFocus(current);
                return;
            }

            context.RestoreFocus();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종료 중이지 않은 가장 가까운 Parent Context를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static UIContext FindFocusableParent(UIContext current)
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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 이전 Context의 선택을 기록한 뒤 새 Context를 현재 권한자로 먼저 확정하고,
        /// <br/> 새 Top Screen 선택 또는 Runtime fallback을 실제 Driver에 적용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void SetFocusedContext
        (
            UIContext next,
            bool selectFallbackWhenEmpty
        )
        {
            var previous = focusedContext;

            if (ReferenceEquals(previous, next)) return;

            previous?.SuspendFocus();
            focusedContext = next;

            if (next != null)
            {
                next.ResumeFocus();
                return;
            }

            if (selectFallbackWhenEmpty && focusDriver != null)
            {
                focusDriver.Select(focusDriver.FindFallback());
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 초기화 완료 상태에서만 public Runtime 서비스를 사용하게 한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfUnavailable()
        {
            if (!IsInitialized || IsReleasing || IsReleased)
            {
                throw new InvalidOperationException("UI Runtime이 사용 가능한 상태가 아닙니다.");
            }
        }

    #endregion

    }
}
