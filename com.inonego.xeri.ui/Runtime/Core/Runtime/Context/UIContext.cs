/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIContext.cs
수정일 : 2026-11-07

# 설명
독립 Screen/Modal/Focus/Input state와 PresentationSession 참조를 소유한다.
Context lifetime과 Base/Override authority를 분리하며 Parent가 Child Context lifetime을 재귀적으로 소유한다.
비활성 Context의 Cursor 소유권을 먼저 반환하고, Focus 복원 실패 시 입력 기여를 잘못 재획득하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// 독립 UI state domain과 재귀 Child Context lifetime을 소유한다.
    /// </summary>
    // ======================================================================
    public sealed class UIContext : IDisposable
    {

    #region 내부 데이터

        private enum State { Active, Disposing, Disposed }

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 소유 상태와 콘텐츠를 반환 중인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposing => state == State.Disposing;

        // ------------------------------------------------------------
        /// <summary>
        /// Context 수명이 종료되었는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed => state == State.Disposed;
        private State state = State.Active;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 입력 권한을 가진 Context인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsEffective => owner.IsEffectiveContext(this);

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 활성 Context 경로에 포함되는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsOnActivePath => owner.IsContextOnActivePath(this);

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 이 Context가 사용하는 Presentation 세션.
        /// <br/> Main Context의 Root Session 수명은 UIRuntime이 소유한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public PresentationSession Presentation { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Context에 등록된 Screen 정의와 Source.
        /// <br/> 이 Registry의 수명은 Context가 소유한다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenRegistry ScreenRegistry { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 Screen 탐색 상태.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenController Screens { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 독립 Modal 스택 상태.
        /// <br/> 이 Controller의 수명은 Context가 소유한다.
        /// </summary>
        // ------------------------------------------------------------
        public ModalController Modals { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 수명을 소유한 부모 Context.
        /// </summary>
        // ------------------------------------------------------------
        internal UIContext Parent => parent;
        private UIContext parent = null;

        internal UIRuntime Runtime => owner;
        private readonly UIRuntime owner = null;

        internal bool HasFocusAuthority => focusController.HasAuthority;

        // ------------------------------------------------------------
        /// <summary>
        /// 살아 있는 자식 Context 수.
        /// </summary>
        // ------------------------------------------------------------
        internal int ChildCount => children.Count;

        private readonly List<UIContext> children = new();
        private readonly List<IDisposable> lifetimes = new();

        private readonly FocusController focusController = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 UI 상태를 생성하고 초기 권한을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal UIContext
        (
            UIRuntime owner,
            UIContext parent,
            PresentationSession presentation,
            IPresentationTransitioner transitioner,
            IFocusDriver focusDriver,
            IScreenInputDriver inputDriver
        )
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.parent = parent;
            Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));

            if (Presentation.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(presentation));
            }

            focusController = new FocusController
            (
                focusDriver ?? throw new ArgumentNullException(nameof(focusDriver))
            );
            ScreenRegistry = new ScreenRegistry();
            ScreenRegistry.SetOwnerControlledLifetime();
            Screens = new ScreenController
            (
                ScreenRegistry,
                AcquireLayer,
                transitioner ?? throw new ArgumentNullException(nameof(transitioner)),
                focusController,
                inputDriver ?? throw new ArgumentNullException(nameof(inputDriver))
            );
            Screens.OnPolicyChanged += HandleScreenStackChanged;
            Modals = new ModalController(this);

            // Runtime이 active path를 계산하기 전까지 새 Child contribution은 global policy에 참여하지 않는다.
            if (parent != null)
            {
                focusController.Suspend();
                Screens.SetInputContributionEnabled(false);
            }

            Screens.Activate();
        }

    #endregion

    #region 콘텐츠 수명

        // ------------------------------------------------------------
        /// <summary>
        /// 자식 Context와 UI 상태보다 먼저 반환할 콘텐츠 수명을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public THandle RegisterChild<THandle>(THandle handle)
        where THandle : class, IDisposable
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            ThrowIfUnavailable();
            lifetimes.Add(handle);
            return handle;
        }

    #endregion

    #region 스크린 등록과 프레젠테이션 획득

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 Context에 Screen 정책과 Source를 등록하고 등록 수명을 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public ScreenRegistrationHandle RegisterScreen
        (
            ScreenOptions options,
            PresentationTarget target,
            IScreenSource source
        )
        {
            ThrowIfUnavailable();
            return ScreenRegistry.Register(options, target, source);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Static Placement에서 View를 획득하고 Source 반환과
        /// <br/> Layer usage 해제를 하나의 Lease로 묶는다.
        /// </summary>
        // ------------------------------------------------------------
        public Lease<TView> AcquirePlacement<TView>
        (
            string presentationID,
            IPresentationSource<TView> source
        )
        where TView : class
        {
            ThrowIfUnavailable();

            if (string.IsNullOrWhiteSpace(presentationID))
            {
                throw new ArgumentException("Presentation ID가 비어 있습니다.", nameof(presentationID));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if
            (
                !Presentation.TryAcquirePlacement
                (
                    presentationID,
                    out var layer,
                    out var usage
                )
            )
            {
                throw new InvalidOperationException
                (
                    $"Static Presentation Placement '{presentationID}'을 획득할 수 없습니다."
                );
            }

            TView view = null;

            try
            {
                view = source.Acquire(layer);

                if (view == null)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Source가 Placement '{presentationID}'에서 null View를 반환했습니다."
                    );
                }

                return new Lease<TView>
                (
                    view,
                    () =>
                    {
                        Exception failure = null;

                        try
                        {
                            source.Release(view);
                        }
                        catch (Exception exception)
                        {
                            failure = exception;
                        }

                        try
                        {
                            usage.Dispose();
                        }
                        catch (Exception exception)
                        {
                            failure = failure == null
                                ? exception
                                : new AggregateException(failure, exception);
                        }

                        if (failure != null)
                        {
                            throw failure;
                        }
                    }
                );
            }
            catch (Exception exception)
            {
                var errors = new ExceptionBuffer(exception);

                if (view != null)
                {
                    try
                    {
                        source.Release(view);
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }
                }

                try
                {
                    usage.Dispose();
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                if (errors.Count == 1)
                {
                    throw;
                }

                throw errors.CreateAggregateException
                (
                    $"Static Presentation Placement '{presentationID}' 획득과 롤백이 실패했습니다."
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Target Layer에서 View를 획득하고
        /// <br/> Source 반환과 Layer Lease 해제를 하나로 묶는다.
        /// </summary>
        // ------------------------------------------------------------
        public Lease<TView> AcquirePresentation<TView>
        (
            PresentationTarget target,
            IPresentationSource<TView> source
        )
        where TView : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var layerLease = AcquireLayer(target);
            TView view = null;

            try
            {
                view = source.Acquire(layerLease.Layer);

                if (view == null)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Source가 Target '{target}'에서 null View를 반환했습니다."
                    );
                }

                return new Lease<TView>
                (
                    view,
                    () =>
                    {
                        Exception failure = null;

                        try
                        {
                            source.Release(view);
                        }
                        catch (Exception exception)
                        {
                            failure = exception;
                        }

                        try
                        {
                            layerLease.Dispose();
                        }
                        catch (Exception exception)
                        {
                            failure = failure == null
                                ? exception
                                : new AggregateException(failure, exception);
                        }

                        if (failure != null)
                        {
                            throw failure;
                        }
                    }
                );
            }
            catch (Exception exception)
            {
                var errors = new ExceptionBuffer(exception);

                if (view != null)
                {
                    try
                    {
                        source.Release(view);
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }
                }

                try
                {
                    layerLease.Dispose();
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                if (errors.Count == 1)
                {
                    throw;
                }

                throw errors.CreateAggregateException
                (
                    $"Presentation Target '{target}' 획득과 롤백이 실패했습니다."
                );
            }
        }

    #endregion

    #region 레이어 획득

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Target을 resolve하고 caller가 소유할 Presentation Layer Lease를 획득한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public PresentationLayerLease AcquireLayer(PresentationTarget target)
        {
            ThrowIfUnavailable();

            if (!target.IsValid)
            {
                throw new ArgumentException
                (
                    "Presentation Target이 유효하지 않습니다.",
                    nameof(target)
                );
            }

            return target.Scope switch
            {
                PresentationTargetScope.Local =>
                    Presentation.AcquireLayer(target.ID),
                PresentationTargetScope.Host =>
                    owner.PresentationHost.AcquireLayer(target.ID),
                _ => throw new ArgumentOutOfRangeException
                (
                    nameof(target),
                    target.Scope,
                    "정의되지 않은 Presentation Target Scope입니다."
                ),
            };
        }

    #endregion

    #region 자식 컨텍스트

        // ------------------------------------------------------------
        /// <summary>
        /// 부모가 수명을 소유하는 자식 Context를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public UIContext CreateChild()
        {
            return CreateChildCore(Presentation);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부모가 수명을 소유하는 자식 Context를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal UIContext CreateChild(PresentationSession presentation)
        {
            if (presentation == null)
            {
                throw new ArgumentNullException(nameof(presentation));
            }

            if (!ReferenceEquals(presentation.Parent, Presentation))
            {
                throw new InvalidOperationException
                (
                    "Child UIContext는 Parent Context Presentation의 직접 Child Session만 사용할 수 있습니다."
                );
            }

            return CreateChildCore(presentation);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 사용 가능한 Presentation으로 자식 상태를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private UIContext CreateChildCore(PresentationSession presentation)
        {
            ThrowIfUnavailable();
            owner.ThrowIfContextCreationUnavailable();

            if (presentation.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(presentation));
            }

            var child = new UIContext
            (
                owner,
                this,
                presentation,
                owner.Transitioner,
                owner.FocusDriver,
                owner.InputDriver
            );
            children.Add(child);

            try
            {
                owner.RefreshContextAuthority();
                return child;
            }
            catch (Exception exception)
            {
                var cleanupErrors = child.DisposeFromParent();

                if (cleanupErrors.Count == 0)
                {
                    throw;
                }

                var errors = new ExceptionBuffer();
                errors.Add(exception);
                errors.AddRange(cleanupErrors);
                throw errors.CreateAggregateException
                (
                    "Child UI Context 생성과 authority 롤백이 모두 실패했습니다."
                );
            }
        }

    #endregion

    #region 컨텍스트 권한

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Context를 peer activation의 Base Context로 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetBaseAuthority()
        {
            ThrowIfUnavailable();
            owner.SetBaseContext(this);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Context를 일시 Context Override Stack top으로 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        public Lease PushAuthorityOverride()
        {
            ThrowIfUnavailable();
            return owner.PushContextOverride(this);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 권한 반환 전에 새 경로의 입력 기여 활성화를 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ValidateInputAuthorityActivation()
        {
            Screens.ValidateInputContributionActivation();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 경로에 따라 Focus와 Input 기여를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SetAuthorityState
        (
            bool onActivePath,
            bool isEffective,
            bool hasCursorAuthority
        )
        {
            if (IsDisposed) return;

            if (IsDisposing)
            {
                if (onActivePath || isEffective || hasCursorAuthority)
                {
                    throw new InvalidOperationException
                    (
                        "종료 중인 UI Context에 새로운 authority를 부여할 수 없습니다."
                    );
                }

                // 종료 중에는 이전 권한으로 rollback하지 않고 독립 반환을 끝까지 시도한다.
                var errors = new ExceptionBuffer();
                TryRestoreAuthority(() => Screens.SetCursorPolicyEnabled(false), ref errors);
                TryRestoreAuthority(() => Screens.SetInputContributionEnabled(false), ref errors);
                TryRestoreAuthority(focusController.Suspend, ref errors);

                if (errors.Count == 1)
                {
                    throw errors[0];
                }

                if (errors.Count > 1)
                {
                    throw errors.CreateAggregateException("UI Context authority 반환이 실패했습니다.");
                }

                return;
            }

            var previousInput = Screens.IsInputContributionEnabled;
            var previousCursor = Screens.IsCursorPolicyEnabled;
            var previousFocus = focusController.HasAuthority;

            try
            {
                // 이전 경로의 Cursor 소유권을 먼저 반환해야 새 경로와 중복되지 않는다.
                if (!onActivePath)
                {
                    Screens.SetCursorPolicyEnabled(false);
                }

                Screens.SetInputContributionEnabled(onActivePath);

                if (onActivePath)
                {
                    Screens.SetCursorPolicyEnabled(hasCursorAuthority);
                }

                if (isEffective)
                {
                    focusController.Resume();
                }
                else
                {
                    focusController.Suspend();
                }
            }
            catch (Exception exception)
            {
                var errors = new ExceptionBuffer(exception);

                TryRestoreAuthority
                (
                    () =>
                    {
                        if (previousFocus)
                        {
                            focusController.Resume();
                        }
                        else
                        {
                            focusController.Suspend();
                        }
                    },
                    ref errors
                );

                // 이전 경로의 Cursor 권한은 다른 복원 실패와 관계없이 되살리지 않는다.
                if (onActivePath)
                {
                    TryRestoreAuthority
                    (
                        () => Screens.SetCursorPolicyEnabled(previousCursor),
                        ref errors
                    );
                }

                // 실제 Focus 복원에도 실패한 유효 Context의 Input 기여는
                // 다시 켜지 않고, Runtime의 권한 반환 재시도 경계로 남긴다.
                var restoreInput = onActivePath || !previousFocus || focusController.HasAuthority
                    ? previousInput
                    : false;

                TryRestoreAuthority
                (
                    () => Screens.SetInputContributionEnabled(restoreInput),
                    ref errors
                );

                if (errors.Count == 1)
                {
                    throw;
                }

                throw errors.CreateAggregateException
                (
                    "UI Context authority 적용과 롤백이 모두 실패했습니다."
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Context authority rollback primitive를 독립적으로 시도한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void TryRestoreAuthority
        (
            Action restore,
            ref ExceptionBuffer errors
        )
        {
            try
            {
                restore();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Screen Stack 변경을 Runtime의 Context/Cursor authority 계산에 반영한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleScreenStackChanged()
        {
            if (state != State.Active) return;

            owner.RefreshContextAuthority();
        }

    #endregion

    #region 포커스 범위

        // ------------------------------------------------------------
        /// <summary>
        /// Context 내부의 포커스 범위를 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public FocusScopeHandle RegisterFocusScope(IFocusScope scope)
        {
            ThrowIfUnavailable();
            return focusController.RegisterScope(scope);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Context의 기본 포커스 범위를 지정한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetPrimaryFocusScope(FocusScopeHandle handle)
        {
            ThrowIfUnavailable();
            focusController.SetPrimary(handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 범위보다 우선하는 포커스 수명을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public Lease PushFocusOverride(FocusScopeHandle handle)
        {
            ThrowIfUnavailable();
            return focusController.PushOverride(handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 네이티브 포커스 변경을 내부 상태에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void HandleNativeFocusChanged(object target)
        {
            if (state != State.Active || !IsEffective) return;

            focusController.HandleNativeFocusChanged(target);
        }

    #endregion

    #region 트리 조회

        // ------------------------------------------------------------
        /// <summary>
        /// 대상이 이 Context의 수명 하위 트리에 포함되는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool Contains(UIContext context)
        {
            var current = context;

            while (current != null)
            {
                if (ReferenceEquals(current, this)) return true;

                current = current.parent;
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 루트부터 현재 Context까지 활성 경로를 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void AppendPathTo(List<UIContext> target)
        {
            if (parent != null)
            {
                parent.AppendPathTo(target);
            }

            target.Add(this);
        }

    #endregion

    #region 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 종료에 맞춰 루트 상태를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal IReadOnlyList<Exception> DisposeFromRuntime()
        {
            return Release(restoreAuthority: false);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부모 종료에 맞춰 자식 상태를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private IReadOnlyList<Exception> DisposeFromParent()
        {
            return Release(restoreAuthority: true);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 콘텐츠와 자식 상태를 반환하고 정리 실패를 모은다.
        /// </summary>
        // ------------------------------------------------------------
        private IReadOnlyList<Exception> Release(bool restoreAuthority)
        {
            var errors = new ExceptionBuffer();

            if (state != State.Active) return errors.AsReadOnlyList();

            state = State.Disposing;

            // 어떤 종료 진입점이든 부모의 소유 목록에서 먼저 분리한다.
            parent?.children.Remove(this);

            try
            {
                owner.ReleaseContextAuthority(this, restoreAuthority);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            // 콘텐츠가 자신의 Screen과 Modal을 정리할 수 있을 때 먼저 반환한다.
            while (lifetimes.Count > 0)
            {
                var index = lifetimes.Count - 1;
                var lifetime = lifetimes[index];
                lifetimes.RemoveAt(index);
                DisposeOwned(lifetime, ref errors);
            }

            while (children.Count > 0)
            {
                var index = children.Count - 1;
                var child = children[index];
                children.RemoveAt(index);
                errors.AddRange(child.DisposeFromParent());
            }

            DisposeModalController(Modals, ref errors);
            Screens.OnPolicyChanged -= HandleScreenStackChanged;

            try
            {
                errors.AddRange(Screens.Shutdown());
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                focusController.Clear();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            DisposeScreenRegistry(ScreenRegistry, ref errors);
            parent = null;
            state = State.Disposed;
            return errors.AsReadOnlyList();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종료 중이거나 종료된 Context의 새 동작을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfUnavailable()
        {
            if (state != State.Active)
            {
                throw new ObjectDisposedException(nameof(UIContext));
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Modal Controller의 소유 상태를 반환하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DisposeModalController
        (
            ModalController controller,
            ref ExceptionBuffer errors
        )
        {
            try
            {
                controller?.DisposeFromOwner();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Registry의 소유 상태를 반환하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DisposeScreenRegistry
        (
            ScreenRegistry registry,
            ref ExceptionBuffer errors
        )
        {
            try
            {
                registry?.DisposeFromOwner();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 개별 정리 실패를 모아 나머지 반환을 계속한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DisposeOwned(IDisposable owned, ref ExceptionBuffer errors)
        {
            try
            {
                owned.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부모 추적에서 분리하고 소유 상태를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (state != State.Active) return;

            if (parent == null)
            {
                throw new InvalidOperationException
                (
                    "Main UI Context는 UIRuntime.Shutdown으로만 종료할 수 있습니다."
                );
            }

            var errors = Release(restoreAuthority: true);

            if (errors.Count > 0)
            {
                throw new AggregateException("UI Context 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
