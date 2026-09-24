/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowTitleBarManipulator.cs
수정일 : 2026-10-03

# 설명
XeriWindowPanel titlebar drag와 double click 상태 전환을 처리하는 wrapper manipulator.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.DragDrop;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Window titlebar 상호작용 wrapper.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowTitleBarManipulator
    {

    #region 필드

        private readonly XeriWindowPanel panel = null;
        private readonly XeriWindowController controller = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 내부 Drag_Drop UITK manipulator.
        /// </summary>
        // ------------------------------------------------------------
        internal UITKDraggableManipulator DragManipulator => dragManipulator;

        private readonly UITKDraggableManipulator dragManipulator = null;

        private const float DEFAULT_TITLE_BAR_HEIGHT = 24f;
        private const float DOUBLE_CLICK_INTERVAL = 0.45f;
        private const float DOUBLE_CLICK_DISTANCE = 8f;

        private Vector2 beginWindowPos = Vector2.zero;
        private Vector2 lastClickPos = Vector2.zero;
        private float lastClickTime = -1f;
        private bool hasPendingTitleBarClick = false;
        private bool hasPreviousTitleBarClick = false;
        private bool isAttached = false;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Window titlebar 상호작용 wrapper를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowTitleBarManipulator
        (
            XeriWindowPanel panel,
            XeriWindowController controller,
            DragDropCoordinator coordinator = null
        ) : base()
        {
            this.panel = panel ?? throw new System.ArgumentNullException(nameof(panel));
            this.controller = controller ?? throw new System.ArgumentNullException(nameof(controller));
            dragManipulator = new UITKDraggableManipulator(coordinator)
            {
                CanMove = false,
                CanDrop = false,
                ForceAbsolutePosition = false,
                CoordinateProvider = new XeriWindowTitleBarCoordinateProvider(),
            };
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar에 drag와 double click callback을 부착한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Attach()
        {
            if (isAttached) return;

            try
            {
                RegisterCallbacks();
                isAttached = true;
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };
                UnregisterCallbacks(errors);
                ResetTitleBarClickState();
                ThrowErrors("Window titlebar callback 연결 rollback", errors);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar에서 drag와 double click callback을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Detach()
        {
            if (!isAttached) return;

            isAttached = false;
            var errors = new List<Exception>();
            UnregisterCallbacks(errors);
            ResetTitleBarClickState();
            ThrowErrors("Window titlebar callback 해제", errors);
        }

    #endregion

    #region 내부 바인딩

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar drag와 click callback 전체를 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RegisterCallbacks()
        {
            panel.TitleBar.RegisterCallback<MouseDownEvent>(OnTitleBarMouseDown);
            panel.TitleBar.RegisterCallback<MouseUpEvent>(OnTitleBarMouseUp);
            dragManipulator.OnDragBegin += OnDragBegin;
            dragManipulator.OnDrag += OnDrag;
            panel.TitleBar.AddManipulator(dragManipulator);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar drag와 click callback 전체를 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnregisterCallbacks(List<Exception> errors)
        {
            TryCleanup
            (
                () => panel.TitleBar.UnregisterCallback<MouseDownEvent>
                (
                    OnTitleBarMouseDown
                ),
                errors
            );
            TryCleanup
            (
                () => panel.TitleBar.UnregisterCallback<MouseUpEvent>
                (
                    OnTitleBarMouseUp
                ),
                errors
            );
            TryCleanup(() => dragManipulator.OnDragBegin -= OnDragBegin, errors);
            TryCleanup(() => dragManipulator.OnDrag -= OnDrag, errors);
            TryCleanup(() => panel.TitleBar.RemoveManipulator(dragManipulator), errors);
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

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Window drag 시작 위치를 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDragBegin(Draggable sender, DragEventArgs e)
        {
            ResetTitleBarClickState();

            if (controller.IsTransitionRunning)
            {
                beginWindowPos = controller.Driver.Pos;
                return;
            }

            if (controller.EffectiveState == XeriWindowState.Maximized)
            {
                RestoreMaximizedWindowForDrag(e);
                return;
            }

            beginWindowPos = controller.Driver.Pos;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag_Drop의 계산 위치 delta를 window 이동 명령으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDrag(Draggable sender, DragEventArgs e)
        {
            if (controller.IsTransitionRunning) return;

            var delta = e.GoalPos - e.OriginPos;

            controller.Move(beginWindowPos + delta);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Maximized window를 titlebar drag 기준점에 맞춰 normal 상태로 복귀시킨다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void RestoreMaximizedWindowForDrag(DragEventArgs e)
        {
            var pointerPos = e.GoalPos - e.Offset;
            var delta = e.GoalPos - e.OriginPos;
            var restoredPos = XeriWindowMaximizedDragCalculator.CalculateRestoredPos
            (
                pointerPos,
                controller.Driver.Size,
                GetMaximizedSize(),
                GetTitleBarHeight()
            );

            var errors = new List<Exception>();

            try
            {
                controller.RequestStateCommand
                (
                    new XeriWindowStateCommandRequest
                    (
                        XeriWindowStateCommandKind.ShowNormal,
                        XeriWindowCommandSource.TitleBar,
                        new Rect(restoredPos, controller.Driver.Size),
                        false
                    )
                );
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (controller.EffectiveState != XeriWindowState.Normal)
            {
                beginWindowPos = controller.Driver.Pos;
                ThrowErrors("Titlebar drag 상태 복원", errors);
                return;
            }

            try
            {
                controller.Move(restoredPos);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            beginWindowPos = controller.Driver.Pos - delta;
            ThrowErrors("Titlebar drag 상태 복원", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity MouseDownEvent로 titlebar click 후보를 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnTitleBarMouseDown(MouseDownEvent evt)
        {
            hasPendingTitleBarClick = CanAcceptTitleBarClick
            (
                evt.target,
                evt.button
            );

            if (hasPendingTitleBarClick)
            {
                evt.StopImmediatePropagation();
                return;
            }

            ClearTitleBarClick();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Drag 없이 끝난 titlebar click 후보를 titlebar 기준 double click으로 확정한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void OnTitleBarMouseUp(MouseUpEvent evt)
        {
            if (!hasPendingTitleBarClick) return;

            hasPendingTitleBarClick = false;

            if (!CanAcceptTitleBarClick(evt.target, evt.button))
            {
                ClearTitleBarClick();
                return;
            }

            if (!RegisterTitleBarClick(evt.mousePosition)) return;

            ToggleMaximize();
            evt.StopImmediatePropagation();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 window 상태에 따라 maximize와 show normal을 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ToggleMaximize()
        {
            var kind = controller.EffectiveState == XeriWindowState.Maximized
                ? XeriWindowStateCommandKind.ShowNormal
                : XeriWindowStateCommandKind.Maximize;

            controller.RequestStateCommand
            (
                new XeriWindowStateCommandRequest
                (
                    kind,
                    XeriWindowCommandSource.TitleBar
                )
            );
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Event target이 title action 영역 내부인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool IsTitleActionTarget(object target)
        {
            var element = target as VisualElement;

            while (element != null)
            {
                if (element == panel.TitleActions) return true;
                if (element == panel.TitleBar) return false;

                element = element.parent;
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Mouse event가 titlebar click 대상인지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool CanAcceptTitleBarClick(object target, int button)
        {
            if (button != 0) return false;
            if (!controller.Options.CanTitleBarDoubleClickMaximize) return false;
            if (IsTitleActionTarget(target)) return false;

            return IsTitleBarTarget(target);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Event target이 titlebar 영역 내부인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool IsTitleBarTarget(object target)
        {
            var element = target as VisualElement;

            while (element != null)
            {
                if (element == panel.TitleBar) return true;

                element = element.parent;
            }

            return false;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Titlebar 기준 click sequence를 갱신하고 double click 성립 여부를 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private bool RegisterTitleBarClick(Vector2 clickPos)
        {
            var now = Time.realtimeSinceStartup;

            if
            (
                hasPreviousTitleBarClick &&
                now - lastClickTime <= DOUBLE_CLICK_INTERVAL &&
                Vector2.Distance(clickPos, lastClickPos) <= DOUBLE_CLICK_DISTANCE
            )
            {
                ClearTitleBarClick();
                return true;
            }

            hasPreviousTitleBarClick = true;
            lastClickTime = now;
            lastClickPos = clickPos;

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar 기준 click sequence를 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ClearTitleBarClick()
        {
            hasPreviousTitleBarClick = false;
            lastClickTime = -1f;
            lastClickPos = Vector2.zero;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Pending titlebar click과 titlebar click sequence를 함께 초기화한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ResetTitleBarClickState()
        {
            hasPendingTitleBarClick = false;
            ClearTitleBarClick();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 수집한 Titlebar 처리 오류를 원래 예외 또는 AggregateException으로 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
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
        /// Maximized 상태에서 window가 차지하는 부모 영역 크기를 구한다.
        /// </summary>
        // ------------------------------------------------------------
        private Vector2 GetMaximizedSize()
        {
            var parent = panel.parent;
            var fallback = panel.worldBound.size;

            if (fallback.x <= 0f)
            {
                fallback.x = controller.Driver.Size.x;
            }

            if (fallback.y <= 0f)
            {
                fallback.y = controller.Driver.Size.y;
            }

            if (parent == null) return fallback;

            var parentWidth = XeriWindowMaximizedDragCalculator.ResolvePositive
            (
                parent.resolvedStyle.width,
                fallback.x
            );
            var parentHeight = XeriWindowMaximizedDragCalculator.ResolvePositive
            (
                parent.resolvedStyle.height,
                fallback.y
            );

            return new Vector2
            (
                parentWidth,
                parentHeight
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar 높이를 구한다.
        /// </summary>
        // ------------------------------------------------------------
        private float GetTitleBarHeight()
        {
            return XeriWindowMaximizedDragCalculator.ResolvePositive
            (
                panel.TitleBar.resolvedStyle.height,
                DEFAULT_TITLE_BAR_HEIGHT
            );
        }

    #endregion

    }
}
