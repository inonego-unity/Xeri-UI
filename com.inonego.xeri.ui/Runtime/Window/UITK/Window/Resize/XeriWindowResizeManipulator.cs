/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowResizeManipulator.cs
수정일 : 2026-10-03

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

    #region 필드

        private readonly XeriWindowPanel panel = null;
        private readonly XeriWindowController controller = null;
        private readonly IXeriWindowResizeCursorProvider cursorProvider = null;

        private Vector2 beginInputPos = Vector2.zero;
        private Vector2 beginPos = Vector2.zero;
        private Vector2 beginSize = Vector2.zero;
        private XeriWindowResizeMode resizeMode = XeriWindowResizeMode.None;
        private int activeID = -1;
        private VisualElement activeHandle = null;
        private bool isAttached = false;

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
        ) : base()
        {
            this.panel = panel ?? throw new System.ArgumentNullException(nameof(panel));
            this.controller = controller ?? throw new System.ArgumentNullException(nameof(controller));
            this.cursorProvider = cursorProvider ?? new XeriWindowResizeCursorProvider();
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle callback을 부착한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Attach()
        {
            if (isAttached) return;

            try
            {
                RegisterAllHandles();
                isAttached = true;
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };
                UnregisterAllHandles(errors);
                TryCleanup(cursorProvider.Reset, errors);
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
            if (!isAttached) return;

            isAttached = false;
            var errors = new List<Exception>();

            try
            {
                ClearResizeState(activeHandle);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            UnregisterAllHandles(errors);
            ThrowErrors("Window resize callback 해제", errors);
        }

    #endregion

    #region 내부 메서드

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
        private void UnregisterAllHandles(List<Exception> errors)
        {
            UnregisterHandle(panel.ResizeLeft, errors);
            UnregisterHandle(panel.ResizeTop, errors);
            UnregisterHandle(panel.ResizeRight, errors);
            UnregisterHandle(panel.ResizeBottom, errors);
            UnregisterHandle(panel.ResizeTopLeft, errors);
            UnregisterHandle(panel.ResizeTopRight, errors);
            UnregisterHandle(panel.ResizeBottomLeft, errors);
            UnregisterHandle(panel.ResizeBottomRight, errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle callback을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RegisterHandle(VisualElement handle, XeriWindowResizeMode mode)
        {
            handle.userData = mode;
            handle.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            handle.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            handle.RegisterCallback<PointerDownEvent>(OnPointerDown);
            handle.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            handle.RegisterCallback<PointerUpEvent>(OnPointerUp);
            handle.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle callback을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnregisterHandle
        (
            VisualElement handle,
            List<Exception> errors
        )
        {
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerEnterEvent>(OnPointerEnter),
                errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave),
                errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerDownEvent>(OnPointerDown),
                errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerMoveEvent>(OnPointerMove),
                errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerUpEvent>(OnPointerUp),
                errors
            );
            TryCleanup
            (
                () => handle.UnregisterCallback<PointerCancelEvent>(OnPointerCancel),
                errors
            );
        }

        private static void TryCleanup
        (
            Action cleanup,
            List<Exception> errors
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

        private static void ThrowErrors
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

            throw new AggregateException(message, errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 delta를 resize mode에 맞는 위치와 크기로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CalculateBounds
        (
            Vector2 inputPos,
            out Vector2 pos,
            out Vector2 size
        )
        {
            var delta = inputPos - beginInputPos;
            pos  = beginPos;
            size = beginSize;

            if (UsesLeftEdge())
            {
                size.x = beginSize.x - delta.x;
            }

            if (UsesRightEdge())
            {
                size.x = beginSize.x + delta.x;
            }

            if (UsesTopEdge())
            {
                size.y = beginSize.y - delta.y;
            }

            if (UsesBottomEdge())
            {
                size.y = beginSize.y + delta.y;
            }

            size = ClampSize(size);

            if (UsesLeftEdge())
            {
                pos.x = beginPos.x + beginSize.x - size.x;
            }

            if (UsesTopEdge())
            {
                pos.y = beginPos.y + beginSize.y - size.y;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize 상태를 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ClearResizeState(VisualElement handle)
        {
            var currentHandle = handle ?? activeHandle;
            var currentID = activeID;
            var errors = new List<Exception>();

            // 외부 cleanup 전에 logical pointer ownership을 먼저 terminalize한다.
            activeID = -1;
            activeHandle = null;
            resizeMode = XeriWindowResizeMode.None;

            if (currentHandle != null && currentID >= 0)
            {
                TryCleanup
                (
                    () =>
                    {
                        if (currentHandle.HasPointerCapture(currentID))
                        {
                            currentHandle.ReleasePointer(currentID);
                        }
                    },
                    errors
                );
            }

            TryCleanup(cursorProvider.Reset, errors);
            ThrowErrors("Window resize pointer 정리", errors);
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
        private bool UsesLeftEdge()
        {
            return resizeMode == XeriWindowResizeMode.Left ||
                   resizeMode == XeriWindowResizeMode.TopLeft ||
                   resizeMode == XeriWindowResizeMode.BottomLeft;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 resize mode가 오른쪽 경계를 사용하는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool UsesRightEdge()
        {
            return resizeMode == XeriWindowResizeMode.Right ||
                   resizeMode == XeriWindowResizeMode.TopRight ||
                   resizeMode == XeriWindowResizeMode.BottomRight;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 resize mode가 위쪽 경계를 사용하는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool UsesTopEdge()
        {
            return resizeMode == XeriWindowResizeMode.Top ||
                   resizeMode == XeriWindowResizeMode.TopLeft ||
                   resizeMode == XeriWindowResizeMode.TopRight;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 resize mode가 아래쪽 경계를 사용하는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool UsesBottomEdge()
        {
            return resizeMode == XeriWindowResizeMode.Bottom ||
                   resizeMode == XeriWindowResizeMode.BottomLeft ||
                   resizeMode == XeriWindowResizeMode.BottomRight;
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
            if (IsResizeBlocked()) return;
            if (evt.target is not VisualElement handle) return;
            if (handle.userData is not XeriWindowResizeMode mode) return;

            cursorProvider.Apply(mode);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle에서 pointer가 벗어나면 기본 cursor로 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (activeID >= 0) return;

            cursorProvider.Reset();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize 시작 정보를 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (IsResizeBlocked()) return;
            if (evt.target is not VisualElement handle) return;
            if (handle.userData is not XeriWindowResizeMode mode) return;

            activeID = evt.pointerId;
            activeHandle = handle;
            resizeMode = mode;
            beginInputPos = evt.position;
            beginPos = controller.Driver.Pos;
            beginSize = controller.Driver.Size;

            try
            {
                cursorProvider.Apply(mode);
                handle.CapturePointer(activeID);
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };

                try
                {
                    ClearResizeState(handle);
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
            if (evt.pointerId != activeID) return;
            if (IsResizeBlocked())
            {
                ClearResizeState(evt.target as VisualElement);
                evt.StopPropagation();
                return;
            }

            if (resizeMode == XeriWindowResizeMode.None) return;

            CalculateBounds(evt.position, out var pos, out var size);
            controller.ResizeBounds(pos, size);
            evt.StopPropagation();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize를 정상 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != activeID) return;

            ClearResizeState(evt.target as VisualElement);
            evt.StopPropagation();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize를 취소 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId != activeID) return;

            ClearResizeState(evt.target as VisualElement);
            evt.StopPropagation();
        }

    #endregion

    #region 상태

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
