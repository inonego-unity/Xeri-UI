/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowResizeManipulator.cs
수정일 : 2026-09-20

# 설명
XeriWindowPanel의 resize handle 입력을 controller resize 명령으로 연결한다.
========================================================================= BLOCK_HEADER_END */

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
        // ------------------------------------------------------------
        /// <summary>
        /// Resize cursor 적용자.
        /// </summary>
        // ------------------------------------------------------------
        public IXeriWindowResizeCursorProvider CursorProvider => cursorProvider;

        private readonly IXeriWindowResizeCursorProvider cursorProvider = null;

        private Vector2 beginInputPos = Vector2.zero;
        private Vector2 beginPos = Vector2.zero;
        private Vector2 beginSize = Vector2.zero;
        private XeriWindowResizeMode resizeMode = XeriWindowResizeMode.None;
        private int activeID = -1;
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
            this.panel = panel;
            this.controller = controller;
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
            if (panel == null || controller == null) return;

            RegisterHandle(panel.ResizeLeft, XeriWindowResizeMode.Left);
            RegisterHandle(panel.ResizeTop, XeriWindowResizeMode.Top);
            RegisterHandle(panel.ResizeRight, XeriWindowResizeMode.Right);
            RegisterHandle(panel.ResizeBottom, XeriWindowResizeMode.Bottom);
            RegisterHandle(panel.ResizeTopLeft, XeriWindowResizeMode.TopLeft);
            RegisterHandle(panel.ResizeTopRight, XeriWindowResizeMode.TopRight);
            RegisterHandle(panel.ResizeBottomLeft, XeriWindowResizeMode.BottomLeft);
            RegisterHandle(panel.ResizeBottomRight, XeriWindowResizeMode.BottomRight);

            isAttached = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Resize handle callback을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Detach()
        {
            if (!isAttached) return;
            if (panel == null) return;

            UnregisterHandle(panel.ResizeLeft);
            UnregisterHandle(panel.ResizeTop);
            UnregisterHandle(panel.ResizeRight);
            UnregisterHandle(panel.ResizeBottom);
            UnregisterHandle(panel.ResizeTopLeft);
            UnregisterHandle(panel.ResizeTopRight);
            UnregisterHandle(panel.ResizeBottomLeft);
            UnregisterHandle(panel.ResizeBottomRight);

            cursorProvider.Reset();
            isAttached = false;
        }

    #endregion

    #region 내부 메서드

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
        private void UnregisterHandle(VisualElement handle)
        {
            handle.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            handle.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            handle.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            handle.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            handle.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            handle.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
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
            if (handle != null && activeID >= 0 && handle.HasPointerCapture(activeID))
            {
                handle.ReleasePointer(activeID);
            }

            activeID = -1;
            resizeMode = XeriWindowResizeMode.None;
            cursorProvider.Reset();
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
            resizeMode = mode;
            beginInputPos = evt.position;
            beginPos = controller.Driver.Pos;
            beginSize = controller.Driver.Size;

            cursorProvider.Apply(mode);
            handle.CapturePointer(activeID);
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
            controller.Move(pos);
            controller.Resize(size);
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
