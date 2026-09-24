/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIContext.cs
수정일 : 2026-10-07

# 설명
독립 Screen/Modal/Focus/Input state와 PresentationSession 참조를 소유한다.
Context lifetime과 Base/Override authority를 분리하며 Parent가 Child Context lifetime을 재귀적으로 소유한다.
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

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 소유 상태와 콘텐츠를 반환 중인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposing { get; private set; }
        // ------------------------------------------------------------
        /// <summary>
        /// Context 수명이 종료되었는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed { get; private set; }
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

        // ------------------------------------------------------------
        /// <summary>
        /// 살아 있는 자식 Context 수.
        /// </summary>
        // ------------------------------------------------------------
        internal int ChildCount => children.Count;

        private readonly List<UIContext> children = new();
        private readonly List<IDisposable> lifetimes = new();

        private readonly UIRuntime owner = null;
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
                var errors = new List<Exception>
                {
                    exception,
                };

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

                throw new AggregateException
                (
                    $"Static Presentation Placement '{presentationID}' 획득과 롤백이 실패했습니다.",
                    errors
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
                var errors = new List<Exception>
                {
                    exception,
                };

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

                throw new AggregateException
                (
                    $"Presentation Target '{target}' 획득과 롤백이 실패했습니다.",
                    errors
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
                children.Remove(child);
                var errors = child.DisposeFromParent();

                if (errors.Count == 0)
                {
                    throw;
                }

                errors.Insert(0, exception);
                throw new AggregateException
                (
                    "Child UI Context 생성과 authority 롤백이 모두 실패했습니다.",
                    errors
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
            if (IsDisposing || IsDisposed) return;

            var previousInput = Screens.IsInputContributionEnabled;
            var previousCursor = Screens.IsCursorPolicyEnabled;
            var previousFocus = focusController.HasAuthority;

            try
            {
                Screens.SetInputContributionEnabled(onActivePath);
                Screens.SetCursorPolicyEnabled(hasCursorAuthority);

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
                var errors = new List<Exception>
                {
                    exception,
                };

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
                    errors
                );
                TryRestoreAuthority
                (
                    () => Screens.SetCursorPolicyEnabled(previousCursor),
                    errors
                );
                TryRestoreAuthority
                (
                    () => Screens.SetInputContributionEnabled(previousInput),
                    errors
                );

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "UI Context authority 적용과 롤백이 모두 실패했습니다.",
                    errors
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
            List<Exception> errors
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
            if (IsDisposing || IsDisposed) return;

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
            if (IsDisposing || IsDisposed || !IsEffective) return;

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
        internal List<Exception> DisposeFromRuntime()
        {
            return Release
            (
                removeFromParent: false,
                allowRoot: true,
                restoreAuthority: false
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부모 종료에 맞춰 자식 상태를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private List<Exception> DisposeFromParent()
        {
            return Release
            (
                removeFromParent: false,
                allowRoot: false,
                restoreAuthority: true
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 콘텐츠와 자식 상태를 반환하고 정리 실패를 모은다.
        /// </summary>
        // ------------------------------------------------------------
        private List<Exception> Release
        (
            bool removeFromParent,
            bool allowRoot,
            bool restoreAuthority
        )
        {
            var errors = new List<Exception>();

            if (IsDisposed || IsDisposing) return errors;

            if (parent == null && !allowRoot)
            {
                throw new InvalidOperationException
                (
                    "Main UI Context는 UIRuntime.Shutdown으로만 종료할 수 있습니다."
                );
            }

            IsDisposing = true;

            if (removeFromParent)
            {
                parent.children.Remove(this);
            }

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
                DisposeOwned(lifetime, errors);
            }

            while (children.Count > 0)
            {
                var index = children.Count - 1;
                var child = children[index];
                children.RemoveAt(index);
                errors.AddRange(child.DisposeFromParent());
            }

            DisposeModalController(Modals, errors);
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

            DisposeScreenRegistry(ScreenRegistry, errors);
            parent = null;
            IsDisposing = false;
            IsDisposed = true;
            return errors;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종료 중이거나 종료된 Context의 새 동작을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfUnavailable()
        {
            if (IsDisposing || IsDisposed)
            {
                throw new ObjectDisposedException(nameof(UIContext));
            }
        }

        // ------------------------------------------------------------
        private static void DisposeModalController
        (
            ModalController controller,
            ICollection<Exception> errors
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
        private static void DisposeScreenRegistry
        (
            ScreenRegistry registry,
            ICollection<Exception> errors
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
        private static void DisposeOwned(IDisposable owned, ICollection<Exception> errors)
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

    #endregion

    #region 수명 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 부모 추적에서 분리하고 소유 상태를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (IsDisposed || IsDisposing) return;

            if (parent == null)
            {
                throw new InvalidOperationException
                (
                    "Main UI Context는 UIRuntime.Shutdown으로만 종료할 수 있습니다."
                );
            }

            var errors = Release
            (
                removeFromParent: true,
                allowRoot: false,
                restoreAuthority: true
            );

            if (errors.Count > 0)
            {
                throw new AggregateException("UI Context 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
