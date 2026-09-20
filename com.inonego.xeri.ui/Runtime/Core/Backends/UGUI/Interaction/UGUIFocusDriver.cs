/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIFocusDriver.cs
수정일 : 2026-09-17
# 설명
명시적으로 연결한 EventSystem으로 Screen Focus 선택, 유효성 검사와 native 선택 변경 보고를 수행한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI EventSystem Focus backend.
    /// </summary>
    // ============================================================
    public sealed class UGUIFocusDriver : FocusDriverBehaviour
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 EventSystem 선택 GameObject.
        /// </summary>
        // ------------------------------------------------------------
        public override object Current
        {
            get
            {
                if (eventSystem == null) return null;

                var selected = eventSystem.currentSelectedGameObject;
                return Owns(selected) ? selected : null;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Focus 선택에 사용하는 EventSystem.
        /// </summary>
        // ------------------------------------------------------------
        public EventSystem EventSystem => eventSystem;

        [SerializeField]
        private EventSystem eventSystem = null;

        [SerializeField]
        private GameObject fallback = null;

        private readonly List<RectTransform> layerRoots = new List<RectTransform>();
        private GameObject observedSelection = null;
        private bool observedSelectionValid = false;

    #endregion

    #region FocusDriverBehaviour

        // ------------------------------------------------------------
        /// <summary>
        /// GameObject Focus 대상을 다룬다.
        /// </summary>
        // ------------------------------------------------------------
        public override bool CanSelect(object target) => target is GameObject;

        // ------------------------------------------------------------
        /// <summary>
        /// UGUI Presentation Layer Root를 Focus 소유 범위로 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void HandleLayerRegistered(IPresentationLayerDriver driver)
        {
            if (!(driver is IPresentationLayerDriver<RectTransform> layer) || layer.Root == null)
            {
                return;
            }

            if (!layerRoots.Contains(layer.Root))
            {
                layerRoots.Add(layer.Root);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Focus에 사용할 EventSystem 연결을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void ValidateBackendConfiguration()
        {
            if (eventSystem == null || !eventSystem.enabled)
            {
                throw new System.InvalidOperationException
                (
                    "활성 EventSystem이 UGUI Focus Driver에 연결되지 않았습니다."
                );
            }

            if (eventSystem.transform.root != transform.root)
            {
                throw new System.InvalidOperationException
                (
                    "UGUI Focus Driver와 EventSystem은 같은 Host에 있어야 합니다."
                );
            }
        }

    #endregion

    #region IFocusDriver

        // ------------------------------------------------------------
        /// <summary>
        /// GameObject가 활성 상태이고 선택 가능한지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public override bool IsValid(object target)
        {
            if (!(target is GameObject gameObject) || gameObject == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!Owns(gameObject)) return false;

            var selectable = gameObject.GetComponent<Selectable>();
            return selectable == null ||
                (selectable.isActiveAndEnabled && selectable.IsInteractable());
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 유효한 GameObject를 EventSystem 현재 선택으로 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Select(object target)
        {
            // EventSystem은 선택 callback 안의 중첩 선택을 거부하므로 바깥 선택이 끝난 뒤 다시 요청하게 둔다.
            if (eventSystem == null || eventSystem.alreadySelecting) return;

            if (IsValid(target))
            {
                eventSystem.SetSelectedGameObject((GameObject)target);
                return;
            }

            var current = eventSystem.currentSelectedGameObject;

            if (Owns(current))
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 직렬화한 fallback이 유효하면 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public override object FindFallback()
        {
            return IsValid(fallback) ? fallback : null;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// GameObject가 등록된 UGUI Presentation Layer hierarchy에 속하는지 확인한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private bool Owns(GameObject gameObject)
        {
            if (gameObject == null) return false;

            var transform = gameObject.transform;

            for (var i = layerRoots.Count - 1; i >= 0; i--)
            {
                var root = layerRoots[i];

                if (root == null)
                {
                    layerRoots.RemoveAt(i);
                    continue;
                }

                if (transform == root || transform.IsChildOf(root))
                {
                    return true;
                }
            }

            return false;
        }

    #endregion

    #region Unity 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 활성화 시 현재 EventSystem 선택을 변경 감지 기준으로 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnEnable()
        {
            observedSelection = Current as GameObject;
            observedSelectionValid = IsValid(observedSelection);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> EventSystem이 전역 선택 변경 이벤트를 제공하지 않으므로
        /// <br/> Frame 종료마다 현재 선택을 비교한다.
        /// <br/> 같은 대상의 비활성화도 Focus 유실로 판정할 수 있도록 유효성 변경을 함께 보고한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void LateUpdate()
        {
            var selection = Current as GameObject;
            var selectionValid = IsValid(selection);

            if
            (
                ReferenceEquals(observedSelection, selection) &&
                observedSelectionValid == selectionValid
            )
            {
                return;
            }

            observedSelection = selection;
            observedSelectionValid = selectionValid;
            NotifyFocusChanged();
        }

    #endregion

    }
}
