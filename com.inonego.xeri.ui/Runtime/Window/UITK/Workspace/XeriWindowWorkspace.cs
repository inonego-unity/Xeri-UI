/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowWorkspace.cs
수정일 : 2026-09-28

# 설명
UI Core Child Context의 단일 Screen 위에 Xeri Window Container와 Window Session을 조립한다.
Simple Window는 persistent Focus record를 재사용하고 Application Window는 Child PresentationSession + Child UIContext를 소유한다.
Window z-order와 Context/Focus authority는 별도 계약으로 유지한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.Window
{
    // ======================================================================
    /// <summary>
    /// UI Core Context 안에서 여러 Xeri Window 수명을 소유하는 Workspace.
    /// </summary>
    // ======================================================================
    public sealed class XeriWindowWorkspace : IDisposable
    {

    #region 내부 데이터

        // ======================================================================
        /// <summary>
        /// Core Screen Source와 UITK Window Container 획득·반환을 연결한다.
        /// </summary>
        // ======================================================================
        private sealed class ContainerScreenSource : IScreenSource
        {
            public XeriWindowContainer Container { get; private set; }

            private readonly IXeriWindowRegistry registry = null;

            public ContainerScreenSource(IXeriWindowRegistry registry)
            {
                this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            }

            public ScreenInstance Acquire(ScreenViewScope scope)
            {
                if (Container != null)
                {
                    throw new InvalidOperationException("Window Container가 이미 획득되어 있습니다.");
                }

                if (scope.Layer is not IPresentationLayerDriver<VisualElement> layer)
                {
                    throw new InvalidOperationException
                    (
                        $"Window Workspace Presentation '{scope.ScreenID}'가 UI Toolkit Root를 제공하지 않습니다."
                    );
                }

                var container = new XeriWindowContainer(registry);

                try
                {
                    layer.Root.Add(container);
                    var instance = new ScreenInstance(new UITKScreenDriver(container));
                    Container = container;
                    return instance;
                }
                catch (Exception exception)
                {
                    var errors = new List<Exception>
                    {
                        exception,
                    };

                    try
                    {
                        container.Dispose();
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }

                    try
                    {
                        container.RemoveFromHierarchy();
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }

                    if (errors.Count == 1)
                    {
                        throw;
                    }

                    throw new AggregateException
                    (
                        "Window Container 획득과 롤백이 실패했습니다.",
                        errors
                    );
                }
            }

            public void Release(ScreenInstance instance)
            {
                var container = Container;
                Container = null;

                if (container == null) return;

                var errors = new List<Exception>();

                try
                {
                    container.Dispose();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                try
                {
                    container.RemoveFromHierarchy();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                if (errors.Count > 0)
                {
                    throw new AggregateException
                    (
                        "Window Container 반환이 실패했습니다.",
                        errors
                    );
                }
            }
        }

    #endregion

    #region 필드

        private readonly UIContext context = null;

        private readonly IXeriWindowRegistry registry = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Core Screen에 획득된 UITK Container.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowContainer Container => containerSource.Container;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 살아 있는 Window Session 수.
        /// </summary>
        // ------------------------------------------------------------
        public int Count => sessions.Count;

        // ------------------------------------------------------------
        /// <summary>
        /// Workspace가 종료됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed => isDisposed;

        private bool isDisposed = false;

        private readonly ScreenOptions screenOptions = null;
        private readonly IXeriUIViewResolver viewResolver = null;
        private readonly IXeriWindowDragFactory dragFactory = null;
        private readonly XeriWindowAnimationOptions animationOptions;
        private readonly ContainerScreenSource containerSource = null;
        private readonly Dictionary<XeriWindowHandle, XeriWindowSession> sessions = new();
        private readonly List<XeriWindowSession> sessionOrder = new();

        private ScreenRegistrationHandle registration = null;
        private ScreenSession screenSession = null;
        private XeriWindowSession focusedSession = null;
        private bool isRegistryActiveBound = false;
        private bool isClosingScreen = false;

    #endregion

    #region 생성자

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Parent Context에 독립 Child Context를 만들고 Workspace Screen을 등록한다.
        /// <br/> 실제 Container와 입력 정책은 첫 Window를 열 때 Core Screen과 함께 획득한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public XeriWindowWorkspace
        (
            UIContext parentContext,
            string presentationID,
            IXeriWindowRegistry registry = null,
            IXeriUIViewResolver viewResolver = null,
            IXeriWindowDragFactory dragFactory = null,
            XeriWindowAnimationOptions? animationOptions = null
        ) : this
        (
            parentContext,
            CreateWorkspaceScreenOptions(presentationID),
            registry,
            viewResolver,
            dragFactory,
            animationOptions
        )
        {
            // NONE
        }

        internal XeriWindowWorkspace
        (
            UIContext parentContext,
            ScreenOptions screenOptions,
            IXeriWindowRegistry registry = null,
            IXeriUIViewResolver viewResolver = null,
            IXeriWindowDragFactory dragFactory = null,
            XeriWindowAnimationOptions? animationOptions = null
        )
        {
            if (parentContext == null)
            {
                throw new ArgumentNullException(nameof(parentContext));
            }

            this.screenOptions = screenOptions ??
                throw new ArgumentNullException(nameof(screenOptions));
            this.registry = registry ?? new XeriWindowRegistry();

            if (this.registry.Records.Count > 0)
            {
                throw new ArgumentException
                (
                    "Window Workspace에는 비어 있는 전용 Registry를 전달해야 합니다.",
                    nameof(registry)
                );
            }

            this.viewResolver = viewResolver ?? new XeriUIViewResolver();
            this.dragFactory = dragFactory ?? new XeriWindowDragFactory();
            this.animationOptions = animationOptions ?? XeriWindowAnimationOptions.Immediate();
            containerSource = new ContainerScreenSource(this.registry);

            context = parentContext.CreateChild();

            try
            {
                registration = context.RegisterScreen
                (
                    this.screenOptions,
                    containerSource
                );
                isRegistryActiveBound = true;
                this.registry.OnActiveChange += OnRegistryActiveChange;
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };

                if (isRegistryActiveBound)
                {
                    isRegistryActiveBound = false;

                    try
                    {
                        this.registry.OnActiveChange -= OnRegistryActiveChange;
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }
                }

                try
                {
                    context.Dispose();
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "Window Workspace 조립과 롤백이 실패했습니다.",
                    errors
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Workspace Container host에 사용할 고정 Screen 정책을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static ScreenOptions CreateWorkspaceScreenOptions(string presentationID)
        {
            return new ScreenOptions
            (
                presentationID,
                blocksGameplayInput: false,
                showsCursor: true,
                cursorLockMode: CursorLockMode.None,
                openDuration: 0.0f,
                closeDuration: 0.0f
            );
        }

    #endregion

    #region Window 열기

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 직접 전달한 View로 Window를 열고 Core Workspace Screen의 자식 수명으로 등록한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public XeriWindowSession OpenWindow
        (
            string id,
            string title,
            VisualElement view,
            Vector2 pos,
            Vector2 size,
            XeriWindowOptions? options = null
        )
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var windowOptions = options ?? XeriWindowOptions.Default();
            var record = new XeriWindowRecord
            {
                ID = id ?? string.Empty,
                Title = title ?? id ?? string.Empty,
                Pos = pos,
                Size = size,
                NormalPos = pos,
                NormalSize = size,
                StackLayer = windowOptions.StackLayer,
            };

            return OpenWindowInternal(record, view, windowOptions, false, null);
        }
        // ------------------------------------------------------------
        /// <summary>
        /// Record의 ViewSourceID를 해석해 Window를 연다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowSession OpenWindow
        (
            XeriWindowRecord record,
            XeriWindowOptions? options = null
        )
        {
            return OpenWindowInternal
            (
                record,
                null,
                ResolveRecordOptions(record, options),
                true,
                null
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Record와 caller-owned View를 사용해 Window를 연다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowSession OpenWindow
        (
            XeriWindowRecord record,
            VisualElement view,
            XeriWindowOptions? options = null
        )
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return OpenWindowInternal
            (
                record,
                view,
                ResolveRecordOptions(record, options),
                false,
                null
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 Screen/Modal stack을 가진 Application Window를 연다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowSession OpenApplicationWindow
        (
            string id,
            string title,
            Vector2 pos,
            Vector2 size,
            XeriWindowApplicationOptions applicationOptions,
            XeriWindowOptions? options = null
        )
        {
            if (applicationOptions == null)
            {
                throw new ArgumentNullException(nameof(applicationOptions));
            }

            var windowOptions = options ?? XeriWindowOptions.Default();
            var record = new XeriWindowRecord
            {
                ID = id ?? string.Empty,
                Title = title ?? id ?? string.Empty,
                Pos = pos,
                Size = size,
                NormalPos = pos,
                NormalSize = size,
                StackLayer = windowOptions.StackLayer,
            };

            return OpenWindowInternal
            (
                record,
                null,
                windowOptions,
                false,
                applicationOptions
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Workspace Screen을 확보한 뒤 Window View를 조립하고,
        /// <br/> Registry 등록과 View 수명을 하나의 Session으로 묶는다.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowSession OpenWindowInternal
        (
            XeriWindowRecord record,
            VisualElement directView,
            XeriWindowOptions options,
            bool resolveViewSource,
            XeriWindowApplicationOptions applicationOptions
        )
        {
            ThrowIfDisposed();

            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (string.IsNullOrWhiteSpace(record.ID))
            {
                throw new ArgumentException
                (
                    "Window ID가 비어 있습니다.",
                    nameof(record)
                );
            }

            if (!Enum.IsDefined(typeof(XeriWindowState), record.State))
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(record),
                    record.State,
                    "정의되지 않은 Window State입니다."
                );
            }

            if
            (
                record.State == XeriWindowState.Minimized &&
                record.MinimizedRestoreState != XeriWindowState.Normal &&
                record.MinimizedRestoreState != XeriWindowState.Maximized
            )
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(record),
                    record.MinimizedRestoreState,
                    "MinimizedRestoreState는 Normal 또는 Maximized여야 합니다."
                );
            }

            if (registry.TryGetHandle(record.ID, out var exists))
            {
                if (sessions.TryGetValue(exists, out var current))
                {
                    return current;
                }

                throw new InvalidOperationException
                (
                    $"Window '{record.ID}'가 Workspace 밖에서 이미 등록되어 있습니다."
                );
            }

            if (record.State == XeriWindowState.Closed)
            {
                throw new InvalidOperationException
                (
                    $"Closed Window record는 다시 열 수 없습니다. ID: {record.ID}"
                );
            }

            XeriWindowController.ValidateOptions(options);
            var normalBounds = ResolveRecordNormalBounds(record);
            IXeriUIViewSource viewSource = null;

            if (applicationOptions != null)
            {

                if (resolveViewSource || directView != null)
                {
                    throw new InvalidOperationException
                    (
                        "Application Window은 direct View 또는 ViewSource를 동시에 사용할 수 없습니다."
                    );
                }
            }
            else if (resolveViewSource)
            {
                viewSource = ResolveViewSource(record);
            }
            else
            {
                ValidateViewForAttach(directView);
            }

            EnsureScreenOpen();

            var container = Container ??
                throw new InvalidOperationException("Window Container가 준비되지 않았습니다.");
            XeriWindowPanel panel = null;
            XeriUIViewScope viewScope = null;
            var view = directView;
            XeriWindowController controller = null;
            XeriWindowHandle handle = null;
            XeriWindowSession session = null;
            var isContainerAttached = false;
            var isRegistryOwned = false;

            try
            {
                panel = container.CreatePanel(record, options);

                if (resolveViewSource)
                {
                    viewScope = CreateViewScope(record);
                    viewSource.LoadSession(viewScope);
                    view = viewSource.AcquireView(viewScope);

                    if (view == null)
                    {
                        throw new InvalidOperationException
                        (
                            $"ViewSource가 null view를 반환했습니다. ID: {record.ViewSourceID}"
                        );
                    }

                    if (!string.IsNullOrEmpty(record.ViewDataKey))
                    {
                        view.viewDataKey = record.ViewDataKey;
                    }
                }

                if (applicationOptions == null)
                {
                    ValidateViewForAttach(view);
                    panel.AttachView(view);
                }

                if (registry.TryGetHandle(record.ID, out _))
                {
                    throw new InvalidOperationException
                    (
                        $"Window '{record.ID}'가 View 조립 중 다른 경로에서 등록되었습니다."
                    );
                }

                if (options.StackLayer != record.StackLayer)
                {
                    record.StackLayer = options.StackLayer;
                }

                var driver = new UITKWindowDriver(panel);
                driver.ApplyBounds(normalBounds);
                ApplyRecordState(driver, record.State);

                controller = new XeriWindowController
                (
                    driver,
                    options,
                    CreateTransitioner(panel),
                    normalBounds,
                    record.MinimizedRestoreState
                );

                try
                {
                    handle = registry.Register(record.ID, controller, record);
                }
                catch
                {
                    if
                    (
                        registry.TryGetHandle(record.ID, out var registeredHandle) &&
                        registry.TryGetController(registeredHandle, out var registeredController) &&
                        ReferenceEquals(registeredController, controller)
                    )
                    {
                        handle = registeredHandle;
                        isRegistryOwned = true;
                    }

                    throw;
                }

                isRegistryOwned = true;

                if (!registry.TryGetController(handle, out var currentController) ||
                    !ReferenceEquals(controller, currentController))
                {
                    throw new InvalidOperationException
                    (
                        $"Window '{record.ID}' Registry 등록 소유권이 다른 Controller와 충돌했습니다."
                    );
                }

                container.AttachWindow(handle, panel);
                isContainerAttached = true;

                session = new XeriWindowSession
                (
                    this,
                    handle,
                    controller,
                    panel,
                    viewSource,
                    viewScope,
                    view,
                    context,
                    applicationOptions,
                    dragFactory
                );

                sessions.Add(handle, session);
                sessionOrder.Add(session);
                registry.Focus(handle);

                return session;
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };

                try
                {
                    if (session != null)
                    {
                        session.Dispose();
                    }
                    else
                    {
                        CleanupFailedOpen
                        (
                            container,
                            registry,
                            handle,
                            panel,
                            viewSource,
                            viewScope,
                            view,
                            isContainerAttached,
                            isRegistryOwned
                        );
                    }
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                if (sessions.Count == 0 && screenSession != null)
                {
                    try
                    {
                        CloseScreen();
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }
                }

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException("Window 조립과 롤백이 실패했습니다.", errors);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Window가 새로 소유할 View가 다른 hierarchy에 연결되지 않았는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal static void ValidateViewForAttach(VisualElement view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (view.parent != null)
            {
                throw new InvalidOperationException
                (
                    "Window View는 다른 VisualElement hierarchy에 연결되지 않은 상태여야 합니다."
                );
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 살아 있는 View Session을 저장하고 현재 표시 순서의 Record 복사본을 반환한다.
        /// <br/> UISession은 Source가 갱신한 동일 reference를 유지한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public IReadOnlyList<XeriWindowRecord> CaptureRecords()
        {
            ThrowIfDisposed();

            var snapshots = new List<XeriWindowRecord>(registry.Records);
            var errors = new List<Exception>();
            var currentSessions = new List<XeriWindowSession>(sessionOrder);

            foreach (var session in currentSessions)
            {
                if (session == null || session.IsDisposed) continue;

                try
                {
                    session.SaveViewSession();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    "Window View Session 저장이 실패했습니다.",
                    errors
                );
            }

            return snapshots;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 현재 Workspace Registry를 사용하는 Tray Source를 생성한다.
        /// <br/> 반환된 Source 수명은 호출자가 소유한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public XeriWindowTraySource CreateTraySource(XeriWindowTrayMapper mapper = null)
        {
            ThrowIfDisposed();
            return new XeriWindowTraySource(registry, mapper);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Record 기반 Open에서 명시 옵션이 없으면 저장된 StackLayer를 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal static XeriWindowOptions ResolveRecordOptions
        (
            XeriWindowRecord record,
            XeriWindowOptions? options
        )
        {
            var resolved = options ?? XeriWindowOptions.Default();

            if (!options.HasValue && record != null)
            {
                resolved.StackLayer = record.StackLayer;
            }

            return resolved;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Record 상태 복원에 사용할 마지막 Normal bounds를 해석한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static Rect ResolveRecordNormalBounds(XeriWindowRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            var hasSavedNormal =
                record.NormalSize.x > 0f &&
                record.NormalSize.y > 0f &&
                !float.IsNaN(record.NormalSize.x) &&
                !float.IsNaN(record.NormalSize.y) &&
                !float.IsInfinity(record.NormalSize.x) &&
                !float.IsInfinity(record.NormalSize.y);

            var bounds = record.State != XeriWindowState.Normal && hasSavedNormal
                ? new Rect(record.NormalPos, record.NormalSize)
                : new Rect(record.Pos, record.Size);

            if
            (
                !IsFinite(bounds.position) ||
                !IsFinite(bounds.size)
            )
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(record),
                    "Window Record bounds는 유한한 값이어야 합니다."
                );
            }

            return bounds;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Vector2 두 축이 모두 유한한 값인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsFinite(Vector2 value)
        {
            return
                !float.IsNaN(value.x) &&
                !float.IsNaN(value.y) &&
                !float.IsInfinity(value.x) &&
                !float.IsInfinity(value.y);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 완료 상태를 animation 없이 Driver primitive에 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static void ApplyRecordState
        (
            IXeriWindowDriver driver,
            XeriWindowState state
        )
        {
            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            if (!Enum.IsDefined(typeof(XeriWindowState), state))
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(state),
                    state,
                    "정의되지 않은 Window State입니다."
                );
            }

            if (state == XeriWindowState.Closed)
            {
                throw new InvalidOperationException
                (
                    "Closed 상태는 live Window Driver에 복원할 수 없습니다."
                );
            }

            driver.CommitState(state);

            if (state == XeriWindowState.Maximized)
            {
                driver.ApplyMaximizedBounds();
            }

            driver.Visibility.Set(state != XeriWindowState.Minimized);
        }

    #endregion

    #region 조회와 Focus

        // ------------------------------------------------------------
        /// <summary>
        /// Window Handle에 대응하는 Workspace Session을 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool TryGetSession
        (
            XeriWindowHandle handle,
            out XeriWindowSession session
        )
        {
            if (handle != null && sessions.TryGetValue(handle, out session))
            {
                return true;
            }

            session = null;
            return false;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Session Window를 Registry active 순서와 Core Context Focus에 반영한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void Focus(XeriWindowSession session)
        {
            if (!Owns(session)) return;
            if (!session.Options.CanFocus) return;

            registry.Focus(session.Handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Session Window를 Focus 변경 없이 같은 layer의 앞으로 이동한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void BringToFront(XeriWindowSession session)
        {
            if (!Owns(session)) return;

            registry.BringToFront(session.Handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Session Window를 Focus 변경 없이 같은 layer의 뒤로 이동한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SendToBack(XeriWindowSession session)
        {
            if (!Owns(session)) return;

            registry.SendToBack(session.Handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Session Window의 화면 정렬 layer를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SetStackLayer
        (
            XeriWindowSession session,
            XeriWindowStackLayer stackLayer
        )
        {
            if (!Owns(session)) return;

            registry.SetStackLayer(session.Handle, stackLayer);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Session이 현재 Workspace 소유인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool Owns(XeriWindowSession session)
        {
            return
                !isDisposed &&
                session != null &&
                !session.IsDisposed &&
                sessions.TryGetValue(session.Handle, out var current) &&
                ReferenceEquals(current, session);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Minimized Window가 active이면 다음 표시 Window 또는
        /// <br/> Parent Context로 Focus를 넘긴다.
        /// </summary>
        // ------------------------------------------------------------
        internal void HandleStateChanged
        (
            XeriWindowSession session,
            XeriWindowState state
        )
        {
            if (isDisposed || session == null) return;

            if
            (
                state == XeriWindowState.Minimized &&
                (
                    ReferenceEquals(registry.ActiveHandle, session.Handle) ||
                    ReferenceEquals(focusedSession, session)
                )
            )
            {
                registry.Deactivate(session.Handle);
                RestoreFocus(session.Handle);
            }
        }
        // --------------------------------------------------------------------------------
        /// <summary>
        /// Registry active Window 변경을 Workspace Core Context Focus로 연결한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void OnRegistryActiveChange(object sender, XeriWindowEventArgs e)
        {
            if (isDisposed || e?.Handle == null) return;
            if (!sessions.TryGetValue(e.Handle, out var session)) return;
            if (!session.Options.CanFocus) return;

            var state = session.Controller.EffectiveState;

            if (state == XeriWindowState.Minimized || state == XeriWindowState.Closed)
            {
                return;
            }

            ApplyFocus(session);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Simple Window는 persistent Primary Scope, Application
        /// <br/> Window는 Child Context Base authority를 활성화한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplyFocus(XeriWindowSession session)
        {
            if (session.IsApplication)
            {
                context.SetPrimaryFocusScope(null);
                session.Context.SetBaseAuthority();
                focusedSession = session;
                return;
            }

            if (session.FocusScope == null || session.FocusScope.IsDisposed)
            {
                throw new InvalidOperationException
                (
                    $"Window '{session.Handle.ID}' Focus Scope registration이 유효하지 않습니다."
                );
            }

            context.SetPrimaryFocusScope(session.FocusScope);
            context.SetBaseAuthority();
            focusedSession = session;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Workspace Window activation을 비우고 살아 있는 Parent
        /// <br/> Context로 Base authority를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ClearFocus()
        {
            context.SetPrimaryFocusScope(null);
            focusedSession = null;

            var parent = context.Parent;

            if (parent != null && !parent.IsDisposing && !parent.IsDisposed)
            {
                parent.SetBaseAuthority();
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 제외 Window를 건너뛰고 가장 앞의 표시 가능한 Window 또는 Parent Context를 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void RestoreFocus(XeriWindowHandle excluded)
        {
            var records = registry.Records;

            for (var index = records.Count - 1; index >= 0; index--)
            {
                var record = records[index];

                if (!registry.TryGetHandle(record.ID, out var handle)) continue;
                if (ReferenceEquals(handle, excluded)) continue;
                if (!sessions.TryGetValue(handle, out var session)) continue;
                if (!session.Options.CanFocus) continue;

                var state = session.Controller.EffectiveState;

                if (state == XeriWindowState.Minimized || state == XeriWindowState.Closed)
                {
                    continue;
                }

                registry.Focus(handle);
                return;
            }

            ClearFocus();
        }

    #endregion

    #region Window 해제

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Window Session을 Workspace 추적에서 먼저 제거하고,
        /// <br/> Application UI, View와 Registry 수명을 한 번 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Release(XeriWindowSession session)
        {
            if (session == null) return;
            if (!sessions.TryGetValue(session.Handle, out var current) ||
                !ReferenceEquals(current, session))
            {
                return;
            }

            sessions.Remove(session.Handle);
            sessionOrder.Remove(session);
            var shouldRestoreFocus =
                ReferenceEquals(registry.ActiveHandle, session.Handle) ||
                ReferenceEquals(focusedSession, session);
            var errors = new List<Exception>();

            if (shouldRestoreFocus && !isDisposed && !isClosingScreen)
            {
                try
                {
                    RestoreFocus(session.Handle);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
            else if (ReferenceEquals(focusedSession, session))
            {
                try
                {
                    ClearFocus();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            errors.AddRange(session.Release(Container, registry));

            if (!isClosingScreen && sessions.Count == 0)
            {
                try
                {
                    CloseScreen();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Window Session 해제가 실패했습니다.", errors);
            }
        }
        // --------------------------------------------------------------------------------
        /// <summary>
        /// Workspace Screen이 없으면 Core Screen을 열어 Container와 Layer Usage를 획득한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void EnsureScreenOpen()
        {
            if (screenSession != null) return;

            var response = context.Screens.Open(screenOptions.ID);

            if (!response.Accepted)
            {
                if (response.Exception != null)
                {
                    throw new InvalidOperationException(response.Error, response.Exception);
                }

                throw new InvalidOperationException(response.Error);
            }

            var openedSession = response.Session ??
                throw new InvalidOperationException("Window Workspace Screen Session이 없습니다.");
            screenSession = openedSession;

            try
            {
                openedSession.RegisterChild(new Lease(ReleaseSessionsFromScreen));
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(screenSession, openedSession))
                {
                    screenSession = null;
                }

                Exception cleanupException = null;

                try
                {
                    context.Screens.Clear();
                }
                catch (Exception failure)
                {
                    cleanupException = failure;
                }

                if (cleanupException == null)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "Window Workspace Screen 자식 등록과 롤백이 실패했습니다.",
                    exception,
                    cleanupException
                );
            }

            if (Container == null)
            {
                screenSession = null;
                var error = new InvalidOperationException
                (
                    "Window Workspace Screen이 Container를 획득하지 못했습니다."
                );

                try
                {
                    context.Screens.Clear();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        "Window Workspace Screen 획득과 롤백이 실패했습니다.",
                        error,
                        cleanupException
                    );
                }

                throw error;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 열린 Window가 없으면 Workspace Screen과 Core Layer Usage를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void CloseScreen()
        {
            if (screenSession == null || isClosingScreen) return;

            isClosingScreen = true;
            var errors = new List<Exception>();

            try
            {
                try
                {
                    ClearFocus();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                try
                {
                    context.Screens.Clear();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

            }
            finally
            {
                screenSession = null;
                isClosingScreen = false;
            }

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    "Window Workspace Screen 해제가 실패했습니다.",
                    errors
                );
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Core Screen 종료에서 살아 있는 Window Session을 한 번씩 정리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ReleaseSessionsFromScreen()
        {
            var errors = new List<Exception>();
            var wasClosingScreen = isClosingScreen;
            isClosingScreen = true;

            try
            {
                while (sessionOrder.Count > 0)
                {
                    var index = sessionOrder.Count - 1;
                    var session = sessionOrder[index];
                    sessionOrder.RemoveAt(index);

                    if (session == null) continue;

                    try
                    {
                        session.Dispose();
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }

                if (sessions.Count > 0)
                {
                    sessions.Clear();
                }
            }
            finally
            {
                isClosingScreen = wasClosingScreen;
            }

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    "Window Workspace Screen 자식 해제가 실패했습니다.",
                    errors
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window Panel에 사용할 상태 전환 구현을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private IXeriWindowStateTransitioner CreateTransitioner(XeriWindowPanel panel)
        {
            if (!animationOptions.Enabled || animationOptions.Duration <= 0f)
            {
                return new XeriImmediateWindowStateTransitioner();
            }

            return new XeriUITKWindowStateTransitioner
            (
                new XeriUITKWindowStateAnimator(panel, animationOptions)
            );
        }

    #endregion

    #region View Source

        // ------------------------------------------------------------
        /// <summary>
        /// Record의 stable ID에 대응하는 View Source를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        private IXeriUIViewSource ResolveViewSource(XeriWindowRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.ViewSourceID))
            {
                throw new InvalidOperationException
                (
                    "ViewSourceID가 비어 있어 Window view를 생성할 수 없습니다."
                );
            }

            if (!viewResolver.TryGetViewSource(record.ViewSourceID, out var viewSource) ||
                viewSource == null)
            {
                throw new InvalidOperationException
                (
                    $"등록되지 않은 ViewSourceID입니다. ID: {record.ViewSourceID}"
                );
            }

            return viewSource;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window View Source에 전달할 runtime scope를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private XeriUIViewScope CreateViewScope(XeriWindowRecord record)
        {
            return new XeriUIViewScope
            (
                record.ViewSourceID,
                record.ViewDataKey,
                record.UISession
            );
        }
        // ----------------------------------------------------------------------
        /// <summary>
        /// Window 조립 실패 시 Workspace가 획득한 자원만 생성 역순으로 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CleanupFailedOpen
        (
            XeriWindowContainer container,
            IXeriWindowRegistry registry,
            XeriWindowHandle handle,
            XeriWindowPanel panel,
            IXeriUIViewSource viewSource,
            XeriUIViewScope viewScope,
            VisualElement view,
            bool isContainerAttached,
            bool isRegistryOwned
        )
        {
            var errors = new List<Exception>();

            if (isContainerAttached && handle != null)
            {
                try
                {
                    container?.DetachWindow(handle);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (isRegistryOwned && handle != null)
            {
                try
                {
                    registry?.Unregister(handle);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            try
            {
                panel?.AttachView(null);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (viewSource != null && viewScope != null && view != null)
            {
                try
                {
                    viewSource.ReleaseView(viewScope, view);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Window 조립 롤백이 실패했습니다.", errors);
            }
        }

    #endregion

    #region 검증

        // ------------------------------------------------------------
        /// <summary>
        /// 종료된 Workspace 사용을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(XeriWindowWorkspace));
            }
        }

    #endregion

    #region IDisposable

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Registry 이벤트를 끊고 Core Screen을 통해 Window 자식을 먼저 반환한다.
        /// <br/> Screen 등록과 Child Context를 생성 역순으로 해제한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            var errors = new List<Exception>();

            if (isRegistryActiveBound)
            {
                isRegistryActiveBound = false;

                try
                {
                    registry.OnActiveChange -= OnRegistryActiveChange;
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            try
            {
                CloseScreen();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                registration?.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            registration = null;

            try
            {
                context.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            sessions.Clear();
            sessionOrder.Clear();

            if (errors.Count > 0)
            {
                throw new AggregateException("Window Workspace 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
