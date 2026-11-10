/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSafeAreaLayout.cs
수정일 : 2026-11-11
# 설명
화면 Safe Area를 부모 로컬 좌표계로 투영하고 안전한 최대 축 정렬 영역을 배치한다.
부모 Transform과 Native Output Root 재연결을 관찰하며 콘텐츠 Transform은 변경하지 않는다.

# 제약사항
Safe Area Root는 자체 Transform·고정 크기 없이 레이아웃 경계로 사용한다.
자식 Overflow와 조상 Clipping에 의한 최종 시각 영역은 이 레이아웃에서 제어하지 않는다.
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
    [RequireComponent(typeof(UITKPresentationOutput))]
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

        private UITKPresentationOutput output = null;
        private VisualElement observedRoot = null;
        private Rect lastSafeArea = default;
        private Vector2Int lastScreenSize = default;
        private Vector2 lastPanelSize = default;
        private Matrix4x4 lastParentWorldTransform = default;
        private Rect lastParentContentRect = default;
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
            var topLeft = parent.WorldToLocal
            (
                RuntimePanelUtils.ScreenToPanel
                (
                    panel, new Vector2(area.xMin, Screen.height - area.yMax)
                )
            );
            var topRight = parent.WorldToLocal
            (
                RuntimePanelUtils.ScreenToPanel
                (
                    panel, new Vector2(area.xMax, Screen.height - area.yMax)
                )
            );
            var bottomRight = parent.WorldToLocal
            (
                RuntimePanelUtils.ScreenToPanel
                (
                    panel, new Vector2(area.xMax, Screen.height - area.yMin)
                )
            );
            var bottomLeft = parent.WorldToLocal
            (
                RuntimePanelUtils.ScreenToPanel
                (
                    panel, new Vector2(area.xMin, Screen.height - area.yMin)
                )
            );
            var safeRect = FindInscribedRect(topLeft, topRight, bottomRight, bottomLeft);
            var parentRect = parent.contentRect;

            root.style.position = Position.Absolute;
            root.style.left = safeRect.xMin - parentRect.xMin;
            root.style.top = safeRect.yMin - parentRect.yMin;
            root.style.right = parentRect.xMax - safeRect.xMax;
            root.style.bottom = parentRect.yMax - safeRect.yMax;

            lastSafeArea = area;
            lastScreenSize = new Vector2Int
            (
                Mathf.Max(1, Screen.width),
                Mathf.Max(1, Screen.height)
            );
            lastPanelSize = panel.visualTree.contentRect.size;
            lastParentWorldTransform = parent.worldTransform;
            lastParentContentRect = parentRect;

            NotifyLayoutChanged();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 역투영된 Safe Area 안에서 최대 면적의 축 정렬 Rect를 구한다.
        /// <br/> 부모와 콘텐츠의 Transform은 변경하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static Rect FindInscribedRect
        (
            Vector2 topLeft, Vector2 topRight,
            Vector2 bottomRight, Vector2 bottomLeft
        )
        {
            var center = (topLeft + topRight + bottomRight + bottomLeft) * 0.25f;
            var horizontal = (topRight - topLeft + bottomRight - bottomLeft) * 0.25f;
            var vertical = (bottomLeft - topLeft + bottomRight - topRight) * 0.25f;
            var determinant = Math.Abs
            (
                (double)horizontal.x * vertical.y -
                (double)horizontal.y * vertical.x
            );

            if (determinant <= 0d || double.IsNaN(determinant) || double.IsInfinity(determinant))
            {
                throw new InvalidOperationException
                (
                    "부모 좌표계에서 Safe Area의 유효한 면적을 계산할 수 없습니다."
                );
            }

            // 역행렬의 두 축 제약을 만족하는 중심 정렬 직사각형만 후보로 취급한다.
            var widthA = Math.Abs((double)vertical.y);
            var heightA = Math.Abs((double)vertical.x);
            var widthB = Math.Abs((double)horizontal.y);
            var heightB = Math.Abs((double)horizontal.x);
            var bestHalfWidth = 0d;
            var bestHalfHeight = 0d;
            var bestArea = 0d;

            void Consider(double halfWidth, double halfHeight)
            {
                if (halfWidth <= 0d || halfHeight <= 0d) return;

                // 두 축의 제약이 허용하는 범위로 후보를 비례 축소해 내접 조건을 유지한다.
                var constraintScale = Math.Max
                (
                    widthA * halfWidth + heightA * halfHeight,
                    widthB * halfWidth + heightB * halfHeight
                ) / determinant;

                if
                (
                    constraintScale <= 0d ||
                    double.IsNaN(constraintScale) ||
                    double.IsInfinity(constraintScale)
                ) return;

                if (constraintScale > 1d)
                {
                    halfWidth /= constraintScale;
                    halfHeight /= constraintScale;
                }

                var area = halfWidth * halfHeight;
                if (area <= bestArea) return;

                bestArea = area;
                bestHalfWidth = halfWidth;
                bestHalfHeight = halfHeight;
            }

            // 최대 넓이는 각 제약 직선의 접점이나 두 직선의 교점에서 발생한다.
            if (widthA > 0d && heightA > 0d)
            {
                Consider
                (
                    determinant / (2d * widthA),
                    determinant / (2d * heightA)
                );
            }

            if (widthB > 0d && heightB > 0d)
            {
                Consider
                (
                    determinant / (2d * widthB),
                    determinant / (2d * heightB)
                );
            }

            var intersection = widthA * heightB - widthB * heightA;
            if (intersection != 0d)
            {
                Consider
                (
                    determinant * (heightB - heightA) / intersection,
                    determinant * (widthA - widthB) / intersection
                );
            }

            if (bestArea <= 0d)
            {
                throw new InvalidOperationException
                (
                    "Safe Area 안에 배치할 수 있는 로컬 직사각형이 없습니다."
                );
            }

            return Rect.MinMaxRect
            (
                center.x - (float)bestHalfWidth,
                center.y - (float)bestHalfHeight,
                center.x + (float)bestHalfWidth,
                center.y + (float)bestHalfHeight
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Layout observer를 독립적으로 호출해 한 observer 실패가 뒤 알림을 차단하지 않게 한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void NotifyLayoutChanged()
        {
            if (isDisposed) return;

            var handlers = OnLayoutChanged;
            if (handlers == null) return;

            var errors = new ExceptionBuffer();

            foreach (Action handler in handlers.GetInvocationList())
            {
                if (isDisposed) break;

                try
                {
                    handler.Invoke();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException
            (
                "Safe Area Layout 변경 observer 처리 중 오류가 발생했습니다."
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// UITK Native Output Root에서 명시적으로 지정한 Safe Area Root를 찾는다.
        /// </summary>
        // ----------------------------------------------------------------------
        private VisualElement FindRoot()
        {
            CacheOutput();

            if (output == null || string.IsNullOrWhiteSpace(rootName))
            {
                return null;
            }

            return output.Root?.Q<VisualElement>(rootName);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 같은 GameObject의 UITK Native Output을 현재 Layout에 연결한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void CacheOutput()
        {
            if (output == null)
            {
                output = GetComponent<UITKPresentationOutput>();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Root의 Panel 연결 변경 알림을 구독한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ObserveRoot()
        {
            var currentRoot = FindRoot();
            if (ReferenceEquals(observedRoot, currentRoot)) return;

            UnobserveRoot();
            observedRoot = currentRoot;
            if (observedRoot == null) return;

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

        // ------------------------------------------------------------
        /// <summary>
        /// Native Output과 현재 Root의 구독을 함께 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void StopObserving()
        {
            if (output != null)
            {
                output.RootUpdated -= HandleOutputRootUpdated;
            }

            UnobserveRoot();
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
            if (!isActiveAndEnabled || isDisposed) return;

            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Native Root 교체·반환에 맞춰 관찰 대상을 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleOutputRootUpdated(VisualElement root)
        {
            if (!isActiveAndEnabled || isDisposed) return;

            ObserveRoot();
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

            CacheOutput();
            output.RootUpdated += HandleOutputRootUpdated;

            try
            {
                ObserveRoot();
            }
            catch
            {
                StopObserving();
                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 비활성화 시 Root의 Panel 연결 알림을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDisable()
        {
            StopObserving();
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
            var parent = observedRoot.parent;

            if
            (
                parent != null &&
                screenSize == lastScreenSize &&
                Screen.safeArea == lastSafeArea &&
                panelSize == lastPanelSize &&
                parent.worldTransform == lastParentWorldTransform &&
                parent.contentRect == lastParentContentRect
            )
            {
                return;
            }

            Refresh();
        }

    #endregion

    #region 수명 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Safe Area 갱신과 Layout 변경 구독을 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            StopObserving();
            OnLayoutChanged = null;
            enabled = false;
        }

    #endregion

    }
}
