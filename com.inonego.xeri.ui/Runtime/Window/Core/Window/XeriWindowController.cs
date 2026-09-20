/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowController.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 상태 전환, 명령, 이벤트를 관리하는 controller.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

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
        /// 표시 계층 driver.
        /// </summary>
        // ------------------------------------------------------------
        public IXeriWindowDriver Driver => driver;

        private readonly IXeriWindowDriver driver = null;
        private readonly IXeriWindowStateTransitioner transitioner = null;

        private readonly XeriWindowBoundsSnapshot boundsSnapshot = null;
        private XeriWindowState? pendingState = null;
        private XeriWindowState minimizedRestoreState = XeriWindowState.Normal;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 동작 옵션.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowOptions Options
        {
            get => options;
            set => options = value;
        }

        private XeriWindowOptions options = XeriWindowOptions.Default();

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 전환 목표가 있으면 해당 상태를, 없으면 완료된 driver 상태를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
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

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 포커스 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowEventArgs> OnFocus = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 포커스 해제 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriWindowEventArgs> OnLoseFocus = null;

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
            IXeriWindowStateTransitioner transitioner = null
        ) : base()
        {
            this.driver  = driver ?? throw new ArgumentNullException(nameof(driver));
            this.options = options ?? XeriWindowOptions.Default();
            this.transitioner = transitioner ?? new XeriImmediateWindowStateTransitioner();
            boundsSnapshot = new XeriWindowBoundsSnapshot(this.driver.Bounds);

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
            if (!options.CanMove) return;
            if (!CanChangeBounds()) return;

            var previous = driver.Pos;
            if (previous == pos) return;

            driver.Pos = pos;
            boundsSnapshot.UpdateNormalBounds(EffectiveState, driver.Bounds);

            OnMove?.Invoke(this, CreateEventArgs());
            OnPosChange?.Invoke(this, new ValueChangeEventArgs<Vector2>(previous, pos));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 크기를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Resize(Vector2 size)
        {
            if (!options.CanResize) return;
            if (!CanChangeBounds()) return;

            var previous = driver.Size;
            var clamped  = ClampSize(size);
            if (previous == clamped) return;

            driver.Size = clamped;
            boundsSnapshot.UpdateNormalBounds(EffectiveState, driver.Bounds);

            OnResize?.Invoke(this, CreateEventArgs());
            OnSizeChange?.Invoke(this, new ValueChangeEventArgs<Vector2>(previous, clamped));
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
        /// 윈도우에 포커스를 부여한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Focus()
        {
            if (!options.CanFocus) return;
            if (EffectiveState == XeriWindowState.Closed) return;

            OnFocus?.Invoke(this, CreateEventArgs());
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 포커스를 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void LoseFocus()
        {
            if (EffectiveState == XeriWindowState.Closed) return;

            OnLoseFocus?.Invoke(this, CreateEventArgs());
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 요청을 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool RequestStateCommand(XeriWindowStateCommandRequest request)
        {
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
            return EffectiveState == XeriWindowState.Normal;
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

            preEvent.Invoke(this, eventArgs);

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
            var previous = driver.State;
            if (previous == state) return false;

            var targetBounds = default(Rect?);

            CaptureMinimizedRestoreState(previous, state);

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
                else if (previous == XeriWindowState.Maximized)
                {
                    targetBounds = boundsSnapshot.RestoreBounds;
                }
            }

            pendingState = state;

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
                    OnComplete = () => CompleteStateTransition(previous, state, request),
                    OnCancel = ClearPendingState,
                    OnError = _ => ClearPendingState(),
                }
            );

            if (!transitionStarted)
            {
                pendingState = null;
            }

            return transitionStarted;
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

            minimizedRestoreState = previous == XeriWindowState.Maximized
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

            var eventArgs = CreateEventArgs();

            GetStateEvent(request.Kind)?.Invoke(this, eventArgs);
            OnStateChange?.Invoke(this, new ValueChangeEventArgs<XeriWindowState>(previous, state));
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
