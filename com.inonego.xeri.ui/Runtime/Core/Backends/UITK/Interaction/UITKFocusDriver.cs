/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKFocusDriver.cs
수정일 : 2026-09-29

# 설명
등록된 UITK Presentation Layer 범위의 Unity native Focus를 관찰하고 선택하는 얇은 backend bridge.
Layer Root 자체의 Focus event lifetime만 대칭 관리하고 Panel navigation/focus graph는 Unity FocusController에 위임한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    public sealed class UITKFocusDriver : FocusDriverBehaviour
    {

    #region 필드

        public override object Current => IsValid(current) ? current : null;

        [SerializeField]
        private string fallbackName = "";

        private readonly List<VisualElement> layerRoots = new();

        private VisualElement current = null;
        private VisualElement reportedCurrent = null;

    #endregion

    #region 포커스 드라이버 기반 구현

        public override bool CanSelect(object target) => target is VisualElement;

        // ----------------------------------------------------------------------
        /// <summary>
        /// UITK Layer Root 자체에 native Focus 변경 callback을 등록한다.
        /// </summary>
        // ----------------------------------------------------------------------
        protected override void HandleLayerRegistered(IPresentationLayerDriver driver)
        {
            if
            (
                driver is not IPresentationLayerDriver<VisualElement> layer ||
                layer.Root == null ||
                layerRoots.Contains(layer.Root)
            )
            {
                return;
            }

            var root = layer.Root;
            layerRoots.Add(root);
            root.RegisterCallback<FocusInEvent>(HandleFocusIn, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(HandleFocusOut, TrickleDown.TrickleDown);
            root.RegisterCallback<DetachFromPanelEvent>(HandleLayerDetached);

            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            PublishCurrent(ResolveFocusTarget(focused));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Layer registration lifetime이 끝나면 Root callback을 대칭 해제한다.
        /// </summary>
        // ----------------------------------------------------------------------
        protected override void HandleLayerUnregistered(IPresentationLayerDriver driver)
        {
            if
            (
                driver is not IPresentationLayerDriver<VisualElement> layer ||
                layer.Root == null
            )
            {
                return;
            }

            var root = layer.Root;

            if (!layerRoots.Remove(root)) return;

            root.UnregisterCallback<FocusInEvent>(HandleFocusIn, TrickleDown.TrickleDown);
            root.UnregisterCallback<FocusOutEvent>(HandleFocusOut, TrickleDown.TrickleDown);
            root.UnregisterCallback<DetachFromPanelEvent>(HandleLayerDetached);

            if (IsDescendantOf(current, root))
            {
                PublishCurrent(null);
            }
        }

    #endregion

    #region 포커스 드라이버 구현

        public override bool IsValid(object target)
        {
            if
            (
                target is not VisualElement element ||
                element.panel == null ||
                !Owns(element)
            )
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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Unity native FocusController에 대상 선택 또는 명시적 Focus 해제를 요청한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public override void Select(object target)
        {
            if (!IsValid(target))
            {
                ClearNativeFocus();
                PublishCurrent(null);
                return;
            }

            var element = (VisualElement)target;
            element.Focus();
            PublishCurrent(element);
        }

        public override object FindFallback()
        {
            if (string.IsNullOrWhiteSpace(fallbackName)) return null;

            for (var index = layerRoots.Count - 1; index >= 0; index--)
            {
                var fallback = layerRoots[index]?.Q<VisualElement>(fallbackName);

                if (IsValid(fallback))
                {
                    return fallback;
                }
            }

            return null;
        }

    #endregion

    #region 포커스 관찰

        // ----------------------------------------------------------------------
        /// <summary>
        /// 등록 Layer 안에서 새 native Focus가 들어오면 현재 대상으로 반영한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleFocusIn(FocusInEvent eventData)
        {
            PublishCurrent
            (
                ResolveFocusTarget(eventData.target as VisualElement)
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 현재 native Focus가 빠지면 공통 Driver가 같은 Frame의 다음 FocusIn을 판정하게 알린다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void HandleFocusOut(FocusOutEvent eventData)
        {
            var losing = ResolveFocusTarget(eventData.target as VisualElement);

            if
            (
                current == null ||
                ReferenceEquals(current, losing) ||
                !IsValid(current)
            )
            {
                PublishCurrent(null);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Layer가 Panel에서 분리되며 현재 Focus를 잃으면 logical current도 비운다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleLayerDetached(DetachFromPanelEvent eventData)
        {
            if (eventData.currentTarget is not VisualElement root) return;
            if (!IsDescendantOf(current, root)) return;

            PublishCurrent(null);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// focused element 또는 focusable ancestor 중 등록 Layer가 소유하는 대상을 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private VisualElement ResolveFocusTarget(VisualElement focused)
        {
            for (var candidate = focused; candidate != null; candidate = candidate.parent)
            {
                if (IsValid(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// VisualElement가 등록 Layer 중 하나의 subtree에 속하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private bool Owns(VisualElement element)
        {
            if (element == null) return false;

            for (var currentElement = element; currentElement != null; currentElement = currentElement.parent)
            {
                for (var index = 0; index < layerRoots.Count; index++)
                {
                    if (ReferenceEquals(currentElement, layerRoots[index]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Element가 지정 Root subtree에 속하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsDescendantOf
        (
            VisualElement element,
            VisualElement root
        )
        {
            for (var currentElement = element; currentElement != null; currentElement = currentElement.parent)
            {
                if (ReferenceEquals(currentElement, root))
                {
                    return true;
                }
            }

            return false;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 명시적 Select(null) 요청에서 등록 Layer의 현재 native Focus를 해제한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ClearNativeFocus()
        {
            var visitedPanels = new HashSet<IPanel>();

            for (var index = 0; index < layerRoots.Count; index++)
            {
                var panel = layerRoots[index]?.panel;

                if (panel == null || !visitedPanels.Add(panel)) continue;

                if
                (
                    panel.focusController?.focusedElement is VisualElement focused &&
                    Owns(focused)
                )
                {
                    focused.Blur();
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 current 변경만 공통 Focus Driver에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void PublishCurrent(VisualElement next)
        {
            current = IsValid(next) ? next : null;

            if (ReferenceEquals(reportedCurrent, current)) return;

            reportedCurrent = current;
            NotifyFocusChanged();
        }

    #endregion

    #region Unity 이벤트

        private void OnDestroy()
        {
            for (var index = 0; index < layerRoots.Count; index++)
            {
                var root = layerRoots[index];
                if (root == null) continue;

                root.UnregisterCallback<FocusInEvent>(HandleFocusIn, TrickleDown.TrickleDown);
                root.UnregisterCallback<FocusOutEvent>(HandleFocusOut, TrickleDown.TrickleDown);
                root.UnregisterCallback<DetachFromPanelEvent>(HandleLayerDetached);
            }

            layerRoots.Clear();
            current = null;
            reportedCurrent = null;
        }

    #endregion

    }
}
