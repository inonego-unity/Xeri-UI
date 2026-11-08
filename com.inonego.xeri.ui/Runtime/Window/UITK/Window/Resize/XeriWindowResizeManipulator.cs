/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowResizeManipulator.cs
수정일 : 2026-11-08

# 설명
XeriWindowPanel의 resize handle 입력을 controller resize 명령으로 연결한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Window resize handle 상호작용 wrapper.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowResizeManipulator
    {

    #region Resize 세션

        // ============================================================
        /// <summary>
        /// 하나의 resize가 소유하는 pointer와 시작 Bounds의 snapshot.
        /// </summary>
        // ============================================================
        private sealed class ResizeSession
        {
            // ============================================================
            /// <summary>
            /// Resize 세션의 시작·진행·종료 상태.
            /// </summary>
            // ============================================================
            public enum Phase { Starting, Active, Stopping }

            // ------------------------------------------------------------
            /// <summary>
            /// Resize 입력을 소유하는 handle.
            /// </summary>
            // ------------------------------------------------------------
            public VisualElement Handle { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Resize 시작 포인터의 ID.
            /// </summary>
            // ------------------------------------------------------------
            public int PointerID { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 시작 시 결정한 Resize 방향.
            /// </summary>
            // ------------------------------------------------------------
            public XeriWindowResizeMode Mode { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Resize 시작 시 포인터의 위치.
            /// </summary>
            // ------------------------------------------------------------
            public Vector2 InputStart { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Resize 시작 시 Window 위치.
            /// </summary>
            // ------------------------------------------------------------
            public Vector2 WindowStart { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Resize 시작 시 Window 크기.
            /// </summary>
            // ------------------------------------------------------------
            public Vector2 SizeStart { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Resize 세션의 현재 진행 상태.
            /// </summary>
            // ------------------------------------------------------------
            public Phase State { get; private set; } = Phase.Starting;

            // ------------------------------------------------------------
            /// <summary>
            /// Resize 시작 입력의 포인터·핸들·방향을 보관한다.
            /// </summary>
            // ------------------------------------------------------------
            public ResizeSession
            (
                VisualElement handle,
                int pointerID,
                XeriWindowResizeMode mode,
                Vector2 inputStart
            )
            {
                Handle = handle;
                PointerID = pointerID;
                Mode = mode;
                InputStart = inputStart;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Capture 획득 후 시작 Bounds를 고정하고 세션을 활성화한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Activate(Vector2 windowStart, Vector2 sizeStart)
            {
                WindowStart = windowStart;
                SizeStart = sizeStart;
                State = Phase.Active;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 세션 종료를 표시해 재진입 입력을 차단한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Stop()
            {
                State = Phase.Stopping;
            }
        }

    #endregion

    #region 종속성과 입력 수명

        // ============================================================
        /// <summary>
        /// Window 입력 callback의 연결·해제 상태.
        /// </summary>
        // ============================================================
        private enum AttachmentState { Detached, Attached, Detaching }

        private readonly XeriWindowPanel panel = null;
        private readonly XeriWindowController controller = null;
        private readonly IXeriWindowResizeCursorProvider cursorProvider = null;
        private readonly Dictionary<VisualElement, XeriWindowResizeMode> handleModes = new();

        private ResizeSession activeSession = null;
        private AttachmentState attachmentState = AttachmentState.Detached;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Window resize 상호작용 wrapper를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowResizeManipulator
        (
            XeriWindowPanel panel,
            XeriWindowController controller,
            IXeriWindowResizeCursorProvider cursorProvider = null
        )
        {
            this.panel = panel ?? throw new System.ArgumentNullException(nameof(panel));
            this.controller = controller ?? throw new System.ArgumentNullException(nameof(controller));
            this.cursorProvider = cursorProvider ?? new XeriWindowResizeCursorProvider();
        }

    #endregion

    #region 콜백 연결과 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle callback을 부착한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Attach()
        {
            if (attachmentState != AttachmentState.Detached) return;

            try
            {
                RegisterAllHandles();
                attachmentState = AttachmentState.Attached;
            }
            catch (Exception exception)
            {
                var errors = new ExceptionBuffer(exception);
                UnregisterAllHandles(ref errors);
                TryCleanup(cursorProvider.Reset, ref errors);

                ThrowErrors("Window resize callback 연결 rollback", errors);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle callback을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Detach()
        {
            if (attachmentState != AttachmentState.Attached) return;

            // 종료 callback의 재진입은 무시하고 이전 등록을 완전히 해제한다.
            attachmentState = AttachmentState.Detaching;
            var errors = new ExceptionBuffer();

            try
            {
                if (activeSession != null)
                {
                    StopResize(activeSession);
                }
                else
                {
                    cursorProvider.Reset();
                }
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            UnregisterAllHandles(ref errors);
            attachmentState = AttachmentState.Detached;
            ThrowErrors("Window resize callback 해제", errors);
        }

    #endregion

    #region 핸들 등록·해제와 공통 정리

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 resize handle callback을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RegisterAllHandles()
        {
            RegisterHandle(panel.ResizeLeft, XeriWindowResizeMode.Left);
            RegisterHandle(panel.ResizeTop, XeriWindowResizeMode.Top);
            RegisterHandle(panel.ResizeRight, XeriWindowResizeMode.Right);
            RegisterHandle(panel.ResizeBottom, XeriWindowResizeMode.Bottom);
            RegisterHandle(panel.ResizeTopLeft, XeriWindowResizeMode.TopLeft);
            RegisterHandle(panel.ResizeTopRight, XeriWindowResizeMode.TopRight);
            RegisterHandle(panel.ResizeBottomLeft, XeriWindowResizeMode.BottomLeft);
            RegisterHandle(panel.ResizeBottomRight, XeriWindowResizeMode.BottomRight);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 resize handle callback을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnregisterAllHandles(ref ExceptionBuffer errors)
        {
            UnregisterHandle(panel.ResizeLeft, ref errors);
            UnregisterHandle(panel.ResizeTop, ref errors);
            UnregisterHandle(panel.ResizeRight, ref errors);
            UnregisterHandle(panel.ResizeBottom, ref errors);
            UnregisterHandle(panel.ResizeTopLeft, ref errors);
            UnregisterHandle(panel.ResizeTopRight, ref errors);
            UnregisterHandle(panel.ResizeBottomLeft, ref errors);
            UnregisterHandle(panel.ResizeBottomRight, ref errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle callback을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RegisterHandle(VisualElement handle, XeriWindowResizeMode mode)
        {
            handleModes[handle] = mode;
            handle.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            handle.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            handle.RegisterCallback<PointerDownEvent>(OnPointerDown);
            handle.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            handle.RegisterCallback<PointerUpEvent>(OnPointerUp);
            handle.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            handle.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle callback을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnregisterHandle
        (
            VisualElement handle,
            ref ExceptionBuffer errors
        )
        {
            handleModes.Remove(handle);

            TryCleanup
            (
                () => handle.UnregisterCallback<PointerEnterEvent>(OnPointerEnter),
                ref errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave),
                ref errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerDownEvent>(OnPointerDown),
                ref errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerMoveEvent>(OnPointerMove),
                ref errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerUpEvent>(OnPointerUp),
                ref errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerCancelEvent>(OnPointerCancel),
                ref errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut),
                ref errors
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 정리 중 예외를 수집해 나머지 정리 작업을 계속 수행한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void TryCleanup
        (
            Action cleanup,
            ref ExceptionBuffer errors
        )
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 수집한 정리 예외를 단일 예외 또는 집계 예외로 전파한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ThrowErrors
        (
            string message,
            ExceptionBuffer errors
        )
        {
            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException(message);
        }

    #endregion

    #region Bounds 변환

        // ----------------------------------------------------------------------
        /// <summary>
        /// 활성 resize의 시작 Bounds와 입력 delta로 목표 Bounds를 계산한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void CalculateBounds
        (
            ResizeSession session,
            Vector2 inputPos,
            out Vector2 pos,
            out Vector2 size
        )
        {
            var delta = inputPos - session.InputStart;
            var mode = session.Mode;
            pos  = session.WindowStart;
            size = session.SizeStart;

            if (UsesLeftEdge(mode))
            {
                size.x = session.SizeStart.x - delta.x;
            }

            if (UsesRightEdge(mode))
            {
                size.x = session.SizeStart.x + delta.x;
            }

            if (UsesTopEdge(mode))
            {
                size.y = session.SizeStart.y - delta.y;
            }

            if (UsesBottomEdge(mode))
            {
                size.y = session.SizeStart.y + delta.y;
            }

            size = ClampSize(size);

            if (UsesLeftEdge(mode))
            {
                pos.x = session.WindowStart.x + session.SizeStart.x - size.x;
            }

            if (UsesTopEdge(mode))
            {
                pos.y = session.WindowStart.y + session.SizeStart.y - size.y;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window option 범위 안으로 resize 크기를 보정한다.
        /// </summary>
        // ------------------------------------------------------------
        private Vector2 ClampSize(Vector2 size)
        {
            return new Vector2
            (
                Mathf.Clamp(size.x, controller.Options.MinSize.x, controller.Options.MaxSize.x),
                Mathf.Clamp(size.y, controller.Options.MinSize.y, controller.Options.MaxSize.y)
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 resize mode가 왼쪽 경계를 사용하는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool UsesLeftEdge(XeriWindowResizeMode mode)
        {
            return mode == XeriWindowResizeMode.Left ||
                   mode == XeriWindowResizeMode.TopLeft ||
                   mode == XeriWindowResizeMode.BottomLeft;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 resize mode가 오른쪽 경계를 사용하는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool UsesRightEdge(XeriWindowResizeMode mode)
        {
            return mode == XeriWindowResizeMode.Right ||
                   mode == XeriWindowResizeMode.TopRight ||
                   mode == XeriWindowResizeMode.BottomRight;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 resize mode가 위쪽 경계를 사용하는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool UsesTopEdge(XeriWindowResizeMode mode)
        {
            return mode == XeriWindowResizeMode.Top ||
                   mode == XeriWindowResizeMode.TopLeft ||
                   mode == XeriWindowResizeMode.TopRight;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 resize mode가 아래쪽 경계를 사용하는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool UsesBottomEdge(XeriWindowResizeMode mode)
        {
            return mode == XeriWindowResizeMode.Bottom ||
                   mode == XeriWindowResizeMode.BottomLeft ||
                   mode == XeriWindowResizeMode.BottomRight;
        }

    #endregion

    #region Resize 세션 종료

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 세션만 종료하며 capture와 cursor의 정리를 끝까지 시도한다.
        /// </summary>
        // ------------------------------------------------------------
        private void StopResize(ResizeSession session)
        {
            if (session == null || !ReferenceEquals(activeSession, session)) return;
            if (session.State == ResizeSession.Phase.Stopping) return;

            // 외부 callback이 재진입해도 종료 중인 세션을 새 입력이 차지하지 않는다.
            session.Stop();
            var errors = new ExceptionBuffer();

            TryCleanup
            (
                () =>
                {
                    if (session.Handle.HasPointerCapture(session.PointerID))
                    {
                        session.Handle.ReleasePointer(session.PointerID);
                    }
                },
                ref errors
            );
            TryCleanup(cursorProvider.Reset, ref errors);

            // 종료 후에만 새 resize 세션의 시작을 허용한다.
            activeSession = null;
            ThrowErrors("Window resize pointer 정리", errors);
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle 위에 pointer가 올라오면 방향 cursor를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (attachmentState != AttachmentState.Attached || activeSession != null || IsResizeBlocked()) return;
            if (evt.target is not VisualElement handle) return;
            if (!handleModes.TryGetValue(handle, out var mode)) return;

            cursorProvider.Apply(mode);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle에서 pointer가 벗어나면 기본 cursor로 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (attachmentState != AttachmentState.Attached || activeSession != null) return;

            cursorProvider.Reset();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize 시작 정보를 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerDown(PointerDownEvent evt)
        {
            // 새 입력은 Idle에서만 시작한다. Starting과 Stopping도 소유권을 점유한다.
            if (attachmentState != AttachmentState.Attached || activeSession != null || evt.button != 0 || IsResizeBlocked()) return;
            if (evt.target is not VisualElement handle) return;
            if (!handleModes.TryGetValue(handle, out var mode)) return;

            var session = new ResizeSession(handle, evt.pointerId, mode, evt.position);
            activeSession = session;

            try
            {
                // 외부 동작 전에 시작 Bounds를 한 번만 읽는다.
                var windowStart = controller.Driver.Pos;
                var sizeStart = controller.Driver.Size;

                cursorProvider.Apply(mode);
                if (!ReferenceEquals(activeSession, session)) return;

                handle.CapturePointer(session.PointerID);
                if (ReferenceEquals(activeSession, session))
                {
                    // Capture를 실제로 획득한 세션만 입력을 처리할 수 있다.
                    if (handle.HasPointerCapture(session.PointerID))
                    {
                        session.Activate(windowStart, sizeStart);
                    }
                    else
                    {
                        StopResize(session);
                    }
                }
            }
            catch (Exception exception)
            {
                var errors = new ExceptionBuffer(exception);

                try
                {
                    StopResize(session);
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                ThrowErrors("Window resize 시작과 rollback", errors);
            }

            evt.StopPropagation();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Pointer 이동을 resize 명령으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerMove(PointerMoveEvent evt)
        {
            var session = activeSession;
            if (session == null || evt.pointerId != session.PointerID) return;
            if (session.State != ResizeSession.Phase.Active) return;

            // 시작 버튼, capture, Window 상태 중 하나라도 유효하지 않으면 종료한다.
            var startButtonReleased =
                session.PointerID == PointerId.mousePointerId && (evt.pressedButtons & 1) == 0;

            if
            (
                startButtonReleased ||
                !session.Handle.HasPointerCapture(session.PointerID) ||
                IsResizeBlocked()
            )
            {
                StopResize(session);
                evt.StopPropagation();
                return;
            }

            try
            {
                CalculateBounds(session, evt.position, out var pos, out var size);
                controller.ResizeBounds(pos, size);
            }
            catch (Exception exception)
            {
                var errors = new ExceptionBuffer(exception);

                try
                {
                    StopResize(session);
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                ThrowErrors("Window resize 이동과 포인터 정리", errors);
            }

            evt.StopPropagation();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize를 정상 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerUp(PointerUpEvent evt)
        {
            var session = activeSession;
            if (session == null || evt.pointerId != session.PointerID) return;
            if (session.State == ResizeSession.Phase.Stopping) return;

            // 보조 버튼만 해제되고 시작 버튼이 남아 있으면 resize를 계속 소유한다.
            if
            (
                session.PointerID == PointerId.mousePointerId &&
                evt.button != 0 &&
                (evt.pressedButtons & 1) != 0
            )
            {
                evt.StopPropagation();
                return;
            }

            StopResize(session);
            evt.StopPropagation();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize를 취소 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerCancel(PointerCancelEvent evt)
        {
            var session = activeSession;
            if (session == null || evt.pointerId != session.PointerID) return;

            StopResize(session);
            evt.StopPropagation();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 외부에서 pointer capture를 잃으면 진행 중 resize와 cursor를 종료한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            var session = activeSession;
            if (session == null || session.State != ResizeSession.Phase.Active) return;

            // 다른 세션의 지연 통지나 현재 capture가 살아 있는 통지는 무시한다.
            if (evt.pointerId != session.PointerID) return;
            if (!ReferenceEquals(evt.currentTarget, session.Handle)) return;
            if (session.Handle.HasPointerCapture(session.PointerID)) return;

            StopResize(session);
        }

    #endregion

    #region 입력 상태 차단

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 window 상태에서 resize 입력을 막아야 하는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool IsResizeBlocked()
        {
            if (controller == null) return true;
            if (!controller.Options.CanResize) return true;
            if (controller.IsTransitionRunning) return true;

            return controller.EffectiveState != XeriWindowState.Normal;
        }

    #endregion

    }
}
