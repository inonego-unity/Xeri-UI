/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationSystemMonitor.cs
수정일 : 2026-10-19

# 설명
authored Desktop.Status placement에서 Runtime, 출력 정책, Window와 입력 상태를 관찰한다.
Monitor 자체는 Window가 아니며 관찰 때문에 Window focus나 Context authority를 바꾸지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// 실제 Runtime 상태 변경을 구독해 상태를 표시한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationSystemMonitor : IDisposable
    {

    #region 필드와 상태

        private readonly UIRuntime runtime = null;
        private readonly IXeriWindowRegistry registry = null;
        private readonly XeriValidationChecklist checklist = null;
        private readonly UIContext context = null;
        private readonly Dictionary<XeriWindowHandle, XeriWindowController> controllers = new();
        private readonly VisualElement root = null;
        private readonly Label runtimeState = null;
        private readonly Label outputPolicy = null;
        private readonly Label windowCount = null;
        private readonly Label activeWindow = null;
        private readonly Label windowState = null;
        private readonly Label screenCount = null;
        private readonly Label modalCount = null;
        private readonly Label inputDevice = null;

        private Lease<VisualElement> placementLease = null;
        private bool expanded = true;
        private bool isDisposed = false;

    #endregion

    #region 관찰 연결

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 Runtime 상태 변경을 구독해 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationSystemMonitor
        (
            UIContext context,
            UIRuntime runtime,
            IXeriWindowRegistry registry,
            XeriValidationChecklist checklist
        )
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.context = context;
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.checklist = checklist;

            placementLease = context.AcquirePlacement
            (
                XeriValidationIDs.StatusPresentation,
                new XeriValidationPlacementSource()
            );
            root = placementLease.Value;
            runtimeState = XeriValidationUI.Require<Label>(root, "MonitorRuntimeState");
            outputPolicy = XeriValidationUI.Require<Label>(root, "MonitorOutputPolicy");
            windowCount = XeriValidationUI.Require<Label>(root, "MonitorWindowCount");
            activeWindow = XeriValidationUI.Require<Label>(root, "MonitorActiveWindow");
            windowState = XeriValidationUI.Require<Label>(root, "MonitorWindowState");
            screenCount = XeriValidationUI.Require<Label>(root, "MonitorScreenCount");
            modalCount = XeriValidationUI.Require<Label>(root, "MonitorModalCount");
            inputDevice = XeriValidationUI.Require<Label>(root, "MonitorInputDevice");

            registry.OnRegister += OnWindowRegistered;
            registry.OnUnregister += OnWindowUnregistered;
            registry.OnActiveChange += OnActiveChanged;
            context.Screens.OnStackChanged += OnScreensChanged;
            context.Modals.OnStackChanged += OnModalsChanged;
            runtime.OnLastInputDeviceChanged += OnInputChanged;
            foreach (var record in registry.Records)
            {
                if (registry.TryGetHandle(record.ID, out var handle))
                {
                    BindWindow(handle);
                }
            }

            Refresh();
        }

    #endregion

    #region 상태 표시

        // ------------------------------------------------------------
        /// <summary>
        /// 상세 상태 표시를 펼치거나 접는다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SetExpanded(bool expanded)
        {
            this.expanded = expanded;
            if (expanded)
            {
                Refresh();
            }

            root.EnableInClassList
            (
                "xeri-validation-monitor--collapsed",
                !expanded
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 출력 정책과 Core·Window 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Refresh()
        {
            if (isDisposed || !expanded)
            {
                return;
            }

            runtimeState.text = runtime.IsInitialized ? "실행 중" : "중지됨";
            var settings = runtime.Settings;
            outputPolicy.text = settings != null
                ? $"{(settings.HighPrecisionOutput ? "고정밀" : "표준")} · " +
                  (settings.UIPostProcessing == UIPresentationPostProcessing.Unaffected
                      ? "후처리 영향 없음"
                      : "후처리 적용")
                : "—";
            windowCount.text = controllers.Count.ToString();
            screenCount.text = runtime.Main?.Screens.Count.ToString() ?? "—";
            modalCount.text = runtime.Main?.Modals.Count.ToString() ?? "—";

            var device = runtime.LastInputDevice;
            inputDevice.text = device switch
            {
                Mouse => "마우스",
                Keyboard => "키보드",
                Gamepad => "게임패드",
                Touchscreen => "터치스크린",
                Pen => "펜",
                null => "입력 없음",
                _ => device.displayName,
            };

            activeWindow.text = "바탕화면";
            windowState.text = "—";

            var handle = registry.ActiveHandle;
            if (handle != null && registry.TryGetRecord(handle, out var record))
            {
                activeWindow.text = record.Title;
                if (registry.TryGetController(handle, out var controller))
                {
                    windowState.text = FormatWindowState(controller.EffectiveState);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 창 상태의 화면 표시 이름을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string FormatWindowState(XeriWindowState state)
        {
            return state switch
            {
                XeriWindowState.Normal => "일반",
                XeriWindowState.Minimized => "최소화",
                XeriWindowState.Maximized => "최대화",
                XeriWindowState.Closed => "닫힘",
                _ => "—",
            };
        }

    #endregion

    #region 구독과 변경 처리

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 창의 상태 변경을 구독한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BindWindow(XeriWindowHandle handle)
        {
            if (!registry.TryGetController(handle, out var controller)) return;
            controllers.Add(handle, controller);
            controller.OnStateChange += OnWindowStateChanged;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 새 창의 상태 관찰을 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnWindowRegistered(object sender, XeriWindowEventArgs e)
        {
            BindWindow(e.Handle);
            checklist?.Mark(XeriValidationChecklist.Item.WindowOpen, "창 열림");
            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 반환된 창의 상태 구독을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnWindowUnregistered(object sender, XeriWindowEventArgs e)
        {
            if (controllers.Remove(e.Handle, out var controller))
            {
                controller.OnStateChange -= OnWindowStateChanged;
            }

            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 창 변경을 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnActiveChanged(object sender, XeriWindowEventArgs e)
        {
            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 완료된 창 상태 전환을 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnWindowStateChanged(object sender, ValueChangeEventArgs<XeriWindowState> e)
        {
            checklist?.Mark(XeriValidationChecklist.Item.WindowState, FormatWindowState(e.Current));
            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Screen 깊이를 관찰하고 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnScreensChanged()
        {
            checklist?.Mark(XeriValidationChecklist.Item.CoreScreen, $"{context.Screens.Count}개 열림");
            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Modal 깊이를 관찰하고 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnModalsChanged()
        {
            checklist?.Mark(XeriValidationChecklist.Item.Modal, $"{context.Modals.Count}개 열림");
            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 최근 입력 장치 변경을 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnInputChanged(InputDevice device)
        {
            Refresh();
        }

    #endregion

    #region 관찰 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 이벤트 연결과 소유한 수명을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            registry.OnRegister -= OnWindowRegistered;
            registry.OnUnregister -= OnWindowUnregistered;
            registry.OnActiveChange -= OnActiveChanged;
            context.Screens.OnStackChanged -= OnScreensChanged;
            context.Modals.OnStackChanged -= OnModalsChanged;
            runtime.OnLastInputDeviceChanged -= OnInputChanged;
            foreach (var controller in controllers.Values)
            {
                controller.OnStateChange -= OnWindowStateChanged;
            }

            controllers.Clear();
            placementLease?.Dispose();
            placementLease = null;
        }

    #endregion

    }
}
