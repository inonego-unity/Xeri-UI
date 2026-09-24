/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowController.cs
수정일 : 2026-10-04

# 설명
Xeri 커스텀 윈도우 상태 전환, 명령, 이벤트를 관리하는 controller.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 상태와 명령을 관리하는 controller.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowController
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Window 위치.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 Pos => driver.Pos;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Window 크기.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 Size => driver.Size;

        // ------------------------------------------------------------
        /// <summary>
        /// 완료된 Window 상태.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowState State => driver.State;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Window 논리 bounds.
        /// </summary>
        // ------------------------------------------------------------
        public Rect Bounds => driver.Bounds;

        // ------------------------------------------------------------
        /// <summary>
        /// Window 내부 표시 backend.
        /// </summary>
        // ------------------------------------------------------------
        internal IXeriWindowDriver Driver => driver;

        private readonly IXeriWindowDriver driver = null;
        private readonly IXeriWindowStateTransitioner transitioner = null;

        private readonly XeriWindowBoundsSnapshot boundsSnapshot = null;
        private XeriWindowState? pendingState = null;
        private bool isDispatchingStateEvent = false;

        // ------------------------------------------------------------
        /// <summary>
        /// 마지막 Normal 상태 bounds.
        /// </summary>
        // ------------------------------------------------------------
        public Rect NormalBounds => boundsSnapshot.NormalBounds;

        // ------------------------------------------------------------
        /// <summary>
        /// Minimized 상태에서 Restore할 완료 상태.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowState MinimizedRestoreState => minimizedRestoreState;

        private XeriWindowState minimizedRestoreState = XeriWindowState.Normal;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 동작 옵션.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowOptions Options => options;

        private readonly XeriWindowOptions options;

        // ----------------------------------------------------------------------
        /// <summary>
        /// 진행 중 전환 목표가 있으면 해당 상태를, 없으면 완료된 driver 상태를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public XeriWindowState EffectiveState => pendingState ?? transitioner.PendingState ?? driver.State;

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 실행 중 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsTransitionRunning => transitioner.IsRunning;

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 전환 목표 상태.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowState? PendingState => pendingState ?? transitioner.PendingState;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 이동 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowEventArgs> OnMove = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 크기 변경 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowEventArgs> OnResize = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 위치 값 변경 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event ValueChangeEventHandler<Vector2> OnPosChange = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 크기 값 변경 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event ValueChangeEventHandler<Vector2> OnSizeChange = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 상태 값 변경 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event ValueChangeEventHandler<XeriWindowState> OnStateChange = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 최소화 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowEventArgs> OnMinimize = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 최소화 요청 전에 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowCancelEventArgs> OnPreMinimize = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 최대화 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowEventArgs> OnMaximize = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 최대화 요청 전에 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowCancelEventArgs> OnPreMaximize = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 normal 표시 복귀 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowEventArgs> OnShowNormal = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 normal 표시 복귀 요청 전에 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowCancelEventArgs> OnPreShowNormal = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 최소화 이전 표시 상태 복구 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowEventArgs> OnRestore = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 최소화 이전 표시 상태 복구 요청 전에 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowCancelEventArgs> OnPreRestore = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 닫기 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowEventArgs> OnClose = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 닫기 요청 전에 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowCancelEventArgs> OnPreClose = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 controller를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowController
        (
            IXeriWindowDriver driver,
            XeriWindowOptions? options = null,
            IXeriWindowStateTransitioner transitioner = null,
            Rect? normalBounds = null,
            XeriWindowState minimizedRestoreState = XeriWindowState.Normal
        ) : base()
        {
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
            this.options = options ?? XeriWindowOptions.Default();
            ValidateOptions(this.options);
            this.transitioner = transitioner ?? new XeriImmediateWindowStateTransitioner();

            var initialNormalBounds = normalBounds ?? this.driver.Bounds;
            ValidateFiniteVector(initialNormalBounds.position, nameof(normalBounds));
            ValidateFiniteVector(initialNormalBounds.size, nameof(normalBounds));
            initialNormalBounds.size = ClampSize(initialNormalBounds.size);
            boundsSnapshot = new XeriWindowBoundsSnapshot(initialNormalBounds);
            this.minimizedRestoreState = NormalizeMinimizedRestoreState(minimizedRestoreState);

            if
            (
                this.driver.State == XeriWindowState.Normal ||
                this.driver.State == XeriWindowState.Minimized
            )
            {
                var previousBounds = this.driver.Bounds;

                try
                {
                    this.driver.ApplyBounds(initialNormalBounds);
                }
                catch (Exception exception)
                {
                    try
                    {
                        this.driver.ApplyBounds(previousBounds);
                    }
                    catch (Exception rollbackException)
                    {
                        throw new AggregateException
                        (
                            "Window Controller 초기 bounds 적용과 rollback이 모두 실패했습니다.",
                            exception,
                            rollbackException
                        );
                    }

                    throw;
                }
            }
        }

    #endregion

    #region 명령

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 이동한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Move(Vector2 pos)
        {
            ValidateFiniteVector(pos, nameof(pos));

            if (!options.CanMove) return;
            if (!CanChangeBounds()) return;

            var previous = driver.Pos;
            if (previous == pos) return;

            try
            {
                driver.Pos = pos;
                boundsSnapshot.UpdateNormalBounds(EffectiveState, driver.Bounds);
            }
            catch (Exception exception)
            {
                try
                {
                    driver.Pos = previous;
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Window 이동 적용과 이전 위치 복원이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }

            var errors = new List<Exception>();
            InvokeValueChangeHandlers
            (
                OnPosChange,
                new ValueChangeEventArgs<Vector2>(previous, pos),
                errors
            );
            InvokeStateHandlers(OnMove, CreateEventArgs(), errors);
            ThrowEventErrors("Window 이동 이벤트 처리", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 크기를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Resize(Vector2 size)
        {
            ValidateFiniteVector(size, nameof(size));

            if (!options.CanResize) return;
            if (!CanChangeBounds()) return;

            var previous = driver.Size;
            var clamped  = ClampSize(size);
            if (previous == clamped) return;

            try
            {
                driver.Size = clamped;
                boundsSnapshot.UpdateNormalBounds(EffectiveState, driver.Bounds);
            }
            catch (Exception exception)
            {
                try
                {
                    driver.Size = previous;
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Window 크기 적용과 이전 크기 복원이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }

            var errors = new List<Exception>();
            InvokeValueChangeHandlers
            (
                OnSizeChange,
                new ValueChangeEventArgs<Vector2>(previous, clamped),
                errors
            );
            InvokeStateHandlers(OnResize, CreateEventArgs(), errors);
            ThrowEventErrors("Window 크기 변경 이벤트 처리", errors);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Resize handle이 계산한 위치와 크기를 하나의 bounds transaction으로 적용한다.
        /// <br/> 위치 보정은 CanMove와 무관하며 Resize capability에 포함된다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void ResizeBounds
        (
            Vector2 pos,
            Vector2 size
        )
        {
            ValidateFiniteVector(pos, nameof(pos));
            ValidateFiniteVector(size, nameof(size));

            if (!options.CanResize) return;
            if (!CanChangeBounds()) return;

            var previous = driver.Bounds;
            var next = new Rect(pos, ClampSize(size));
            if (previous == next) return;

            try
            {
                driver.ApplyBounds(next);
                boundsSnapshot.UpdateNormalBounds(EffectiveState, driver.Bounds);
            }
            catch (Exception exception)
            {
                try
                {
                    driver.ApplyBounds(previous);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Window resize bounds 적용과 이전 bounds 복원이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }

            var errors = new List<Exception>();

            if (previous.position != next.position)
            {
                InvokeValueChangeHandlers
                (
                    OnPosChange,
                    new ValueChangeEventArgs<Vector2>(previous.position, next.position),
                    errors
                );
                InvokeStateHandlers(OnMove, CreateEventArgs(), errors);
            }

            if (previous.size != next.size)
            {
                InvokeValueChangeHandlers
                (
                    OnSizeChange,
                    new ValueChangeEventArgs<Vector2>(previous.size, next.size),
                    errors
                );
                InvokeStateHandlers(OnResize, CreateEventArgs(), errors);
            }

            ThrowEventErrors("Window resize bounds 이벤트 처리", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 최소화한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Minimize()
        {
            RequestStateCommand
            (
                new XeriWindowStateCommandRequest
                (
                    XeriWindowStateCommandKind.Minimize,
                    XeriWindowCommandSource.API
                )
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 최대화한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Maximize()
        {
            RequestStateCommand
            (
                new XeriWindowStateCommandRequest
                (
                    XeriWindowStateCommandKind.Maximize,
                    XeriWindowCommandSource.API
                )
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 normal 상태로 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
        public void ShowNormal()
        {
            RequestStateCommand
            (
                new XeriWindowStateCommandRequest
                (
                    XeriWindowStateCommandKind.ShowNormal,
                    XeriWindowCommandSource.API
                )
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 최소화 이전 표시 상태로 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Restore()
        {
            RequestStateCommand
            (
                new XeriWindowStateCommandRequest
                (
                    XeriWindowStateCommandKind.Restore,
                    XeriWindowCommandSource.API
                )
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 닫는다.
        /// </summary>
        // ------------------------------------------------------------
        public void Close()
        {
            RequestStateCommand
            (
                new XeriWindowStateCommandRequest
                (
                    XeriWindowStateCommandKind.Close,
                    XeriWindowCommandSource.API
                )
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 요청을 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool RequestStateCommand(XeriWindowStateCommandRequest request)
        {
            if (isDispatchingStateEvent) return false;
            if (!CanExecuteStateCommand(request)) return false;

            if (!TryResolveNextState(request, out var nextState))
            {
                return false;
            }

            if (IsCancelled(GetPreStateEvent(request.Kind))) return false;

            return CommitStateTransition(nextState, request);
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 상태와 요청을 기준으로 다음 완료 상태를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool TryResolveNextState
        (
            XeriWindowStateCommandRequest request,
            out XeriWindowState nextState
        )
        {
            var currentState = EffectiveState;
            if (!XeriWindowStateTransitionRule.TryResolveNextState(currentState, request, out nextState))
            {
                return false;
            }

            if (request.Kind == XeriWindowStateCommandKind.Restore && currentState == XeriWindowState.Minimized)
            {
                nextState = minimizedRestoreState;
            }

            return currentState != nextState;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 상태에서 위치와 크기를 변경할 수 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool CanChangeBounds()
        {
            if (isDispatchingStateEvent || IsTransitionRunning) return false;

            return EffectiveState == XeriWindowState.Normal;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 크기 제한 옵션이 유한하고 축별 MinSize가 MaxSize를 넘지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal static void ValidateOptions(XeriWindowOptions options)
        {
            if
            (
                !IsFiniteNonNegative(options.MinSize.x) ||
                !IsFiniteNonNegative(options.MinSize.y) ||
                !IsFiniteNonNegative(options.MaxSize.x) ||
                !IsFiniteNonNegative(options.MaxSize.y) ||
                options.MinSize.x > options.MaxSize.x ||
                options.MinSize.y > options.MaxSize.y ||
                !Enum.IsDefined(typeof(XeriWindowStackLayer), options.StackLayer)
            )
            {
                throw new ArgumentException
                (
                    "Window 크기 제한과 StackLayer 옵션 값이 유효해야 합니다.",
                    nameof(options)
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 값이 유한한 0 이상 수인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsFiniteNonNegative(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                value >= 0f;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Vector2 두 축이 모두 유한한 값인지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ValidateFiniteVector(Vector2 value, string paramName)
        {
            if
            (
                float.IsNaN(value.x) ||
                float.IsNaN(value.y) ||
                float.IsInfinity(value.x) ||
                float.IsInfinity(value.y)
            )
            {
                throw new ArgumentOutOfRangeException
                (
                    paramName,
                    value,
                    "Window 위치와 크기는 유한한 값이어야 합니다."
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 옵션 범위 안으로 크기를 보정한다.
        /// </summary>
        // ------------------------------------------------------------
        private Vector2 ClampSize(Vector2 size)
        {
            return new Vector2
            (
                Mathf.Clamp(size.x, options.MinSize.x, options.MaxSize.x),
                Mathf.Clamp(size.y, options.MinSize.y, options.MaxSize.y)
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 취소 가능한 이벤트를 호출하고 취소 여부를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool IsCancelled(EventHandler<XeriWindowCancelEventArgs> preEvent)
        {
            if (preEvent == null) return false;

            var eventArgs = new XeriWindowCancelEventArgs
            {
                Pos    = driver.Pos,
                Size   = driver.Size,
                State  = EffectiveState,
                Cancel = false,
            };

            var errors = new List<Exception>();
            isDispatchingStateEvent = true;

            try
            {
                foreach
                (
                    EventHandler<XeriWindowCancelEventArgs> handler in
                    preEvent.GetInvocationList()
                )
                {
                    try
                    {
                        handler.Invoke(this, eventArgs);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }
            finally
            {
                isDispatchingStateEvent = false;
            }

            ThrowEventErrors("Window 사전 이벤트 처리", errors);
            return eventArgs.Cancel;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 상태를 변경하고 관련 이벤트를 호출한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool CommitStateTransition
        (
            XeriWindowState state,
            XeriWindowStateCommandRequest request
        )
        {
            var effectivePrevious = EffectiveState;

            if (transitioner.IsRunning)
            {
                try
                {
                    transitioner.Cancel(true);
                }
                catch
                {
                    if (!transitioner.IsRunning)
                    {
                        pendingState = null;
                    }

                    throw;
                }
            }

            var previous = driver.State;
            var previousMinimizedRestoreState = minimizedRestoreState;
            var previousNormalBounds = boundsSnapshot.NormalBounds;
            var previousRestoreBounds = boundsSnapshot.RestoreBounds;
            var targetBounds = default(Rect?);

            CaptureMinimizedRestoreState(effectivePrevious, state);

            if (state == XeriWindowState.Maximized)
            {
                boundsSnapshot.UpdateNormalBounds(previous, driver.Bounds);
                boundsSnapshot.CaptureRestoreBounds();
            }
            else if (state == XeriWindowState.Normal)
            {
                if (request.TargetBounds.HasValue)
                {
                    targetBounds = request.TargetBounds;
                }
                else if (effectivePrevious == XeriWindowState.Maximized)
                {
                    targetBounds = boundsSnapshot.RestoreBounds;
                }
            }

            pendingState = state;
            var transitionCompleted = false;

            try
            {
                var transitionStarted = transitioner.Transition
                (
                    new XeriWindowStateTransitionRequest
                    {
                        Driver = driver,
                        PreviousState = previous,
                        NextState = state,
                        TargetBounds = targetBounds,
                        Animate = request.Animate,
                        InterruptPolicy = XeriWindowTransitionInterruptPolicy.CancelAndReplace,
                        OnComplete = () =>
                        {
                            try
                            {
                                CompleteStateTransition(previous, state, request);
                            }
                            finally
                            {
                                transitionCompleted = true;
                            }
                        },
                        OnCancel = ClearPendingState,
                        OnError = _ =>
                        {
                            RestoreTransitionMetadata
                            (
                                previousMinimizedRestoreState,
                                previousNormalBounds,
                                previousRestoreBounds
                            );
                            ClearPendingState();
                        },
                    }
                );

                if (!transitionStarted)
                {
                    pendingState = null;
                    RestoreTransitionMetadata
                    (
                        previousMinimizedRestoreState,
                        previousNormalBounds,
                        previousRestoreBounds
                    );
                }

                return transitionStarted;
            }
            catch
            {
                var transitionRunning = transitioner.IsRunning;

                // 완료 callback 안의 observer 실패는 이미 완료된 상태 전환을 되돌리지 않는다.
                if (!transitionRunning && !transitionCompleted)
                {
                    RestoreTransitionMetadata
                    (
                        previousMinimizedRestoreState,
                        previousNormalBounds,
                        previousRestoreBounds
                    );
                }

                pendingState = transitionRunning
                    ? transitioner.PendingState
                    : null;
                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Minimized에서 Restore할 때 돌아갈 표시 상태를 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CaptureMinimizedRestoreState
        (
            XeriWindowState previous,
            XeriWindowState next
        )
        {
            if (next != XeriWindowState.Minimized) return;

            minimizedRestoreState = NormalizeMinimizedRestoreState(previous);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패한 상태 전환 전에 보존한 Controller metadata를 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RestoreTransitionMetadata
        (
            XeriWindowState restoreState,
            Rect normalBounds,
            Rect restoreBounds
        )
        {
            minimizedRestoreState = restoreState;
            boundsSnapshot.Restore(normalBounds, restoreBounds);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Minimized 복원 상태를 Normal 또는 Maximized로 정규화한다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriWindowState NormalizeMinimizedRestoreState(XeriWindowState state)
        {
            return state == XeriWindowState.Maximized
                ? XeriWindowState.Maximized
                : XeriWindowState.Normal;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 완료 후 상태 이벤트를 발행한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CompleteStateTransition
        (
            XeriWindowState previous,
            XeriWindowState state,
            XeriWindowStateCommandRequest request
        )
        {
            pendingState = null;

            if (state == XeriWindowState.Normal)
            {
                boundsSnapshot.UpdateNormalBounds(XeriWindowState.Normal, driver.Bounds);
            }

            var eventArgs = CreateEventArgs();
            var errors = new List<Exception>();
            isDispatchingStateEvent = true;

            try
            {
                if (previous != state)
                {
                    InvokeValueChangeHandlers
                    (
                        OnStateChange,
                        new ValueChangeEventArgs<XeriWindowState>(previous, state),
                        errors
                    );
                }

                InvokeStateHandlers(GetStateEvent(request.Kind), eventArgs, errors);
            }
            finally
            {
                isDispatchingStateEvent = false;
            }

            ThrowEventErrors("Window 상태 완료 이벤트 처리", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 값 변경 구독자를 독립적으로 호출하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private void InvokeValueChangeHandlers<T>
        (
            ValueChangeEventHandler<T> handlers,
            ValueChangeEventArgs<T> eventArgs,
            List<Exception> errors
        )
        {
            if (handlers == null) return;

            foreach (ValueChangeEventHandler<T> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, eventArgs);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 상태별 완료 구독자를 독립적으로 호출하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private void InvokeStateHandlers
        (
            EventHandler<XeriWindowEventArgs> handlers,
            XeriWindowEventArgs eventArgs,
            List<Exception> errors
        )
        {
            if (handlers == null) return;

            foreach (EventHandler<XeriWindowEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, eventArgs);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 수집된 이벤트 오류를 원래 예외 또는 AggregateException으로 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ThrowEventErrors
        (
            string message,
            List<Exception> errors
        )
        {
            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                $"{message} 중 하나 이상의 오류가 발생했습니다.",
                errors
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 상태 전환 목표를 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ClearPendingState()
        {
            pendingState = null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 명령의 옵션 허용 여부를 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool CanExecuteStateCommand(XeriWindowStateCommandRequest request)
        {
            return request.Kind switch
            {
                XeriWindowStateCommandKind.Minimize   => options.CanMinimize,
                XeriWindowStateCommandKind.Maximize   => options.CanMaximize,
                XeriWindowStateCommandKind.Close      => options.CanClose,
                XeriWindowStateCommandKind.ShowNormal => true,
                XeriWindowStateCommandKind.Restore    => true,
                _                                      => false,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 명령에 대응하는 사전 이벤트를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private EventHandler<XeriWindowCancelEventArgs> GetPreStateEvent(XeriWindowStateCommandKind kind)
        {
            return kind switch
            {
                XeriWindowStateCommandKind.Minimize   => OnPreMinimize,
                XeriWindowStateCommandKind.Maximize   => OnPreMaximize,
                XeriWindowStateCommandKind.ShowNormal => OnPreShowNormal,
                XeriWindowStateCommandKind.Restore    => OnPreRestore,
                XeriWindowStateCommandKind.Close      => OnPreClose,
                _                                      => null,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 명령에 대응하는 완료 이벤트를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private EventHandler<XeriWindowEventArgs> GetStateEvent(XeriWindowStateCommandKind kind)
        {
            return kind switch
            {
                XeriWindowStateCommandKind.Minimize   => OnMinimize,
                XeriWindowStateCommandKind.Maximize   => OnMaximize,
                XeriWindowStateCommandKind.ShowNormal => OnShowNormal,
                XeriWindowStateCommandKind.Restore    => OnRestore,
                XeriWindowStateCommandKind.Close      => OnClose,
                _                                      => null,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 driver 상태로 이벤트 인자를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowEventArgs CreateEventArgs()
        {
            return new XeriWindowEventArgs
            {
                Pos   = driver.Pos,
                Size  = driver.Size,
                State = EffectiveState,
            };
        }

    #endregion

    }
}
