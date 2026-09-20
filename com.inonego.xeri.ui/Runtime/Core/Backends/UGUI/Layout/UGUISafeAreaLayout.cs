/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUISafeAreaLayout.cs
수정일 : 2026-09-17
# 설명
화면 크기와 Safe Area 변경을 감지해 명시적으로 연결된 RectTransform 경계를 갱신한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI Safe Area Root의 화면 경계 반영을 소유한다.
    /// </summary>
    // ============================================================
    public sealed class UGUISafeAreaLayout : MonoBehaviour, IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Safe Area RectTransform.
        /// </summary>
        // ------------------------------------------------------------
        public RectTransform SafeAreaRoot => safeAreaRoot;

        [SerializeField]
        private RectTransform safeAreaRoot = null;

        private Rect lastSafeArea = default;
        private Vector2Int lastScreenSize = default;
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
        /// 현재 화면 크기와 Safe Area를 즉시 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Refresh()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(UGUISafeAreaLayout));
            }

            if (safeAreaRoot == null)
            {
                throw new InvalidOperationException("Safe Area Root가 연결되지 않았습니다.");
            }

            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            var area = Screen.safeArea;
            var anchorMin = new Vector2(area.xMin / width, area.yMin / height);
            var anchorMax = new Vector2(area.xMax / width, area.yMax / height);

            safeAreaRoot.anchorMin = anchorMin;
            safeAreaRoot.anchorMax = anchorMax;
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;

            lastSafeArea = area;
            lastScreenSize = new Vector2Int(width, height);

            OnLayoutChanged?.Invoke();
        }

    #endregion

    #region Unity 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 활성화된 첫 Frame의 UI 배치 전에 현재 Safe Area를 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnEnable()
        {
            if (isDisposed) return;

            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실행 중 화면 경계 변경을 감지해 Safe Area를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void LateUpdate()
        {
            if (isDisposed) return;

            var size = new Vector2Int
            (
                Mathf.Max(1, Screen.width),
                Mathf.Max(1, Screen.height)
            );

            if (size == lastScreenSize && Screen.safeArea == lastSafeArea) return;

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

            OnLayoutChanged = null;
            isDisposed = true;
            enabled = false;
        }

    #endregion

    }
}
