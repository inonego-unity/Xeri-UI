/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKFocusDriver.cs
수정일 : 2026-09-17
# 설명
UI Toolkit Panel의 VisualElement Focus 선택, 유효성 검사와 native Focus 변경 보고를 수행한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Panel Focus backend.
    /// </summary>
    // ============================================================
    public sealed class UITKFocusDriver : FocusDriverBehaviour
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 마지막으로 Focus가 이동한 Panel의 현재 Focus Element.
        /// </summary>
        // ------------------------------------------------------------
        public override object Current
        {
            get
            {
                if (currentPanelRoot == null || currentPanelRoot.panel == null) return null;

                var focused = currentPanelRoot.panel.focusController?.focusedElement as VisualElement;
                return ResolveFocusTarget(focused);
            }
        }

        [SerializeField]
        private UITKLayerPanel fallbackLayer = null;

        [SerializeField]
        private string fallbackName = "";

        private readonly List<VisualElement> panelRoots = new List<VisualElement>();
        private VisualElement currentPanelRoot = null;
        private VisualElement pendingPanelRoot = null;
        private VisualElement reportedCurrent = null;
        private bool focusEvaluationRequested = false;

    #endregion

    #region FocusDriverBehaviour

        // ------------------------------------------------------------
        /// <summary>
        /// VisualElement Focus 대상을 다룬다.
        /// </summary>
        // ------------------------------------------------------------
        public override bool CanSelect(object target) => target is VisualElement;

        // ------------------------------------------------------------
        /// <summary>
        /// UI Toolkit Layer Panel을 사용자 Focus 추적 범위에 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void HandleLayerRegistered(IPresentationLayerDriver driver)
        {
            if (driver is IPresentationLayerDriver<VisualElement> layer)
            {
                RegisterPanel(layer.Root);
            }
        }

    #endregion

    #region IFocusDriver

        // ------------------------------------------------------------
        /// <summary>
        /// VisualElement가 현재 Panel에서 Focus를 받을 수 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public override bool IsValid(object target)
        {
            if (!(target is VisualElement element) || element.panel == null)
            {
                return false;
            }

            return
                element.focusable &&
                element.canGrabFocus &&
                element.enabledInHierarchy &&
                element.resolvedStyle.display != DisplayStyle.None &&
                element.resolvedStyle.visibility == Visibility.Visible;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 유효한 VisualElement에 Focus를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Select(object target)
        {
            if (!IsValid(target))
            {
                ClearTrackedFocus(null);
                currentPanelRoot = null;
                pendingPanelRoot = null;
                reportedCurrent = null;
                focusEvaluationRequested = false;
                return;
            }

            var element = (VisualElement)target;
            RegisterPanel(element);
            var panelRoot = element.panel.visualTree;
            ClearTrackedFocus(element.panel);
            currentPanelRoot = panelRoot;
            element.Focus();
            RequestFocusEvaluation(panelRoot);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 직렬화한 fallback Element가 유효하면 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public override object FindFallback()
        {
            if (fallbackLayer == null || string.IsNullOrWhiteSpace(fallbackName))
            {
                return null;
            }

            var fallback = fallbackLayer.Root?.Q<VisualElement>(fallbackName);
            return IsValid(fallback) ? fallback : null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Focus 대상이 속한 Panel의 실제 Focus 이동을 추적한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void RegisterPanel(VisualElement element)
        {
            if (element == null)
            {
                throw new System.ArgumentNullException(nameof(element));
            }

            var panelRoot = element.panel?.visualTree;

            if (panelRoot == null)
            {
                throw new System.InvalidOperationException
                (
                    "Focus 추적 대상이 UI Toolkit Panel에 연결되지 않았습니다."
                );
            }

            if (panelRoots.Contains(panelRoot)) return;

            panelRoots.Add(panelRoot);
            panelRoot.RegisterCallback<FocusInEvent>
            (
                HandleFocusIn,
                TrickleDown.TrickleDown
            );
            panelRoot.RegisterCallback<FocusOutEvent>
            (
                HandleFocusOut,
                TrickleDown.TrickleDown
            );
            panelRoot.RegisterCallback<DetachFromPanelEvent>(HandlePanelDetached);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Panel을 제외한 추적 Panel의 native Focus를 비운다.
        /// </summary>
        // ------------------------------------------------------------
        private void ClearTrackedFocus(IPanel exceptPanel)
        {
            for (var i = 0; i < panelRoots.Count; i++)
            {
                var panel = panelRoots[i].panel;

                if (panel == null || ReferenceEquals(panel, exceptPanel)) continue;

                if (panel.focusController?.focusedElement is VisualElement focused)
                {
                    focused.Blur();
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> native focusedElement를 다시 선택 가능한
        /// <br/> 가장 가까운 logical Focus 대상으로 정규화한다.
        /// </summary>
        // ------------------------------------------------------------
        private VisualElement ResolveFocusTarget(VisualElement focused)
        {
            for (var current = focused; current != null; current = current.parent)
            {
                if (IsValid(current)) return current;
            }

            return null;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Focus dispatch가 끝난 뒤 Panel의 최종 focusedElement를 평가하도록 요청한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void RequestFocusEvaluation(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            pendingPanelRoot = panelRoot;
            focusEvaluationRequested = true;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// FocusIn dispatch 중의 stale focusedElement를 읽지 않고 Panel 평가만 예약한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void HandleFocusIn(FocusInEvent eventData)
        {
            RequestFocusEvaluation(eventData.currentTarget as VisualElement);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// FocusOut도 dispatch 종료 후 같은 안정화 경로에서 최종 상태를 판정한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleFocusOut(FocusOutEvent eventData)
        {
            RequestFocusEvaluation(eventData.currentTarget as VisualElement);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 예약한 Panel의 안정화된 Focus를 현재 대상으로 확정하고 변경을 한 번 보고한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void EvaluateFocus()
        {
            focusEvaluationRequested = false;
            var candidateRoot = pendingPanelRoot;
            pendingPanelRoot = null;

            if (candidateRoot != null && candidateRoot.panel != null)
            {
                var focused = candidateRoot.panel.focusController?.focusedElement as VisualElement;
                var candidate = ResolveFocusTarget(focused);

                if (candidate != null)
                {
                    currentPanelRoot = candidateRoot;
                    ClearTrackedFocus(candidateRoot.panel);
                    PublishCurrentIfChanged(candidate);
                    return;
                }
            }

            var current = Current as VisualElement;

            if (IsValid(current)) return;

            currentPanelRoot = candidateRoot != null && candidateRoot.panel != null
                ? candidateRoot
                : currentPanelRoot;
            PublishCurrentIfChanged(null);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 동일 Focus의 중복 보고 없이 안정화된 현재 대상만 공통 Driver에 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void PublishCurrentIfChanged(VisualElement current)
        {
            if (ReferenceEquals(reportedCurrent, current)) return;

            reportedCurrent = current;
            NotifyFocusChanged();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종료된 Panel의 Focus 추적 callback을 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandlePanelDetached(DetachFromPanelEvent eventData)
        {
            if (!(eventData.target is VisualElement panelRoot)) return;

            var index = panelRoots.IndexOf(panelRoot);

            if (index < 0) return;

            panelRoots.RemoveAt(index);
            panelRoot.UnregisterCallback<FocusInEvent>
            (
                HandleFocusIn,
                TrickleDown.TrickleDown
            );
            panelRoot.UnregisterCallback<FocusOutEvent>
            (
                HandleFocusOut,
                TrickleDown.TrickleDown
            );
            panelRoot.UnregisterCallback<DetachFromPanelEvent>(HandlePanelDetached);

            if (ReferenceEquals(pendingPanelRoot, panelRoot))
            {
                pendingPanelRoot = null;
                focusEvaluationRequested = false;
            }

            if (ReferenceEquals(currentPanelRoot, panelRoot))
            {
                currentPanelRoot = null;
                PublishCurrentIfChanged(null);
            }
        }

    #endregion

    #region Unity 이벤트

        // --------------------------------------------------------------------------------
        /// <summary>
        /// UI Toolkit Focus dispatch가 끝난 Frame 후반에 안정화된 Panel Focus를 확정한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void LateUpdate()
        {
            if (focusEvaluationRequested)
            {
                EvaluateFocus();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Host 종료 시 추적 중인 Panel callback을 모두 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDestroy()
        {
            for (var i = panelRoots.Count - 1; i >= 0; i--)
            {
                var panelRoot = panelRoots[i];
                panelRoots.RemoveAt(i);
                panelRoot.UnregisterCallback<FocusInEvent>
                (
                    HandleFocusIn,
                    TrickleDown.TrickleDown
                );
                panelRoot.UnregisterCallback<FocusOutEvent>
                (
                    HandleFocusOut,
                    TrickleDown.TrickleDown
                );
                panelRoot.UnregisterCallback<DetachFromPanelEvent>(HandlePanelDetached);
            }

            currentPanelRoot = null;
            pendingPanelRoot = null;
            reportedCurrent = null;
            focusEvaluationRequested = false;
        }

    #endregion

    }
}
