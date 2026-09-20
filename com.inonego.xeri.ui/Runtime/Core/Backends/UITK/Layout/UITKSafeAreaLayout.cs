/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSafeAreaLayout.cs
수정일 : 2026-09-17
# 설명
화면과 Panel 크기 변화를 감지해 명시적으로 지정한 VisualElement에 Safe Area를 반영한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Safe Area Root의 화면 경계 반영을 소유한다.
    /// </summary>
    // ============================================================
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UITKLayerPanel))]
    public sealed class UITKSafeAreaLayout : MonoBehaviour, IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Safe Area VisualElement.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement SafeAreaRoot => FindRoot();

        [SerializeField]
        private string rootName = "";

        private UITKLayerPanel layer = null;
        private VisualElement observedRoot = null;
        private Rect lastSafeArea = default;
        private Vector2Int lastScreenSize = default;
        private Vector2 lastPanelSize = default;
        private bool isDisposed = false;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Safe Area Root가 새 화면 경계에 맞게 갱신됐을 때 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        public event Action OnLayoutChanged = null;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 화면 Safe Area를 지정 VisualElement에 즉시 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Refresh()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(UITKSafeAreaLayout));
            }

            var root = FindRoot();

            if (root == null)
            {
                throw new InvalidOperationException
                (
                    $"UITK Safe Area Root '{rootName}'을 찾을 수 없습니다."
                );
            }

            if (root.panel == null || root.parent == null)
            {
                throw new InvalidOperationException
                (
                    "UITK Safe Area Root는 Panel에 연결된 하위 VisualElement여야 합니다."
                );
            }

            var area = Screen.safeArea;
            var parent = root.parent;
            var panel = root.panel;
            var panelTopLeft = RuntimePanelUtils.ScreenToPanel
            (
                panel,
                new Vector2(area.xMin, Screen.height - area.yMax)
            );
            var panelBottomRight = RuntimePanelUtils.ScreenToPanel
            (
                panel,
                new Vector2(area.xMax, Screen.height - area.yMin)
            );
            var topLeft = parent.WorldToLocal(panelTopLeft);
            var bottomRight = parent.WorldToLocal(panelBottomRight);
            var parentRect = parent.contentRect;

            root.style.position = Position.Absolute;
            root.style.left = topLeft.x - parentRect.xMin;
            root.style.top = topLeft.y - parentRect.yMin;
            root.style.right = parentRect.xMax - bottomRight.x;
            root.style.bottom = parentRect.yMax - bottomRight.y;

            lastSafeArea = area;
            lastScreenSize = new Vector2Int
            (
                Mathf.Max(1, Screen.width),
                Mathf.Max(1, Screen.height)
            );
            lastPanelSize = panel.visualTree.contentRect.size;

            OnLayoutChanged?.Invoke();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// UITK Layer Root에서 명시적으로 지정한 Safe Area Root를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private VisualElement FindRoot()
        {
            CacheLayer();

            if (layer == null || string.IsNullOrWhiteSpace(rootName))
            {
                return null;
            }

            return layer.Root?.Q<VisualElement>(rootName);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 같은 GameObject의 UITK Layer backend를 현재 Layout에 연결한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void CacheLayer()
        {
            if (layer == null)
            {
                layer = GetComponent<UITKLayerPanel>();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Root의 Panel 연결 변경 알림을 구독한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ObserveRoot()
        {
            observedRoot = FindRoot();

            if (observedRoot == null)
            {
                throw new InvalidOperationException
                (
                    $"UITK Safe Area Root '{rootName}'을 찾을 수 없습니다."
                );
            }

            observedRoot.RegisterCallback<AttachToPanelEvent>(HandleAttachedToPanel);

            if (observedRoot.panel != null)
            {
                Refresh();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Root의 Panel 연결 변경 알림을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnobserveRoot()
        {
            if (observedRoot == null) return;

            observedRoot.UnregisterCallback<AttachToPanelEvent>(HandleAttachedToPanel);
            observedRoot = null;
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Root가 Panel에 연결된 시점의 실제 좌표계로 Safe Area를 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleAttachedToPanel(AttachToPanelEvent evt)
        {
            Refresh();
        }

    #endregion

    #region Unity 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 활성화 시 Root 연결과 최초 Safe Area 반영을 준비한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnEnable()
        {
            if (isDisposed) return;

            ObserveRoot();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 비활성화 시 Root의 Panel 연결 알림을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDisable()
        {
            UnobserveRoot();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실행 중 화면 또는 Panel 경계 변경을 감지해 Safe Area를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void LateUpdate()
        {
            if (isDisposed || observedRoot?.panel == null) return;

            var screenSize = new Vector2Int
            (
                Mathf.Max(1, Screen.width),
                Mathf.Max(1, Screen.height)
            );
            var panelSize = observedRoot.panel.visualTree.contentRect.size;

            if
            (
                screenSize == lastScreenSize &&
                Screen.safeArea == lastSafeArea &&
                panelSize == lastPanelSize
            )
            {
                return;
            }

            Refresh();
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// Safe Area 갱신과 Layout 변경 구독을 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            UnobserveRoot();
            OnLayoutChanged = null;
            enabled = false;
        }

    #endregion

    }
}
