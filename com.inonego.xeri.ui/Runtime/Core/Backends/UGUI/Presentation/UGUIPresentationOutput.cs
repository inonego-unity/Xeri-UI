/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIPresentationOutput.cs
수정일 : 2026-10-17

# 설명
Top-level UGUI ScreenOverlay Layer의 Canvas Native Output 경계를 제공한다.
Generated/Borrowed 초기화와 sortingOrder의 scoped lifetime을 UITK Output과 대칭으로 관리한다.
CanvasScaler, GraphicRaycaster 및 authored Canvas 설정은 기존 prefab/Scene이 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UI;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// Screen Space Overlay Canvas의 Native Output 구성과 임시 상태를 소유한다.
    /// </summary>
    // ======================================================================
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class UGUIPresentationOutput : MonoBehaviour
    {

    #region 필드

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 Native Canvas. 기존 Scene-authored Canvas identity를 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public Canvas Canvas => canvas != null ? canvas : GetComponent<Canvas>();

        // ------------------------------------------------------------
        /// <summary>
        /// Native Canvas의 top-level 출력 Root.
        /// </summary>
        // ------------------------------------------------------------
        public RectTransform Root => Canvas != null ? Canvas.transform as RectTransform : null;

        private Canvas canvas = null;
        private bool isInitialized;
        private bool restoreOriginalOrder;
        private int originalSortingOrder;

    #endregion

    #region 출력 수명

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 생성된 Output의 LayerOrder를 적용한다.
        /// <br/> GameObject lifetime은 Materializer가 소유한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Initialize(int layerOrder)
        {
            InitializeCore(layerOrder, borrowed: false);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Scene-authored Canvas를 빌리고 원래 sortingOrder를 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void InitializeBorrowed(int layerOrder)
        {
            InitializeCore(layerOrder, borrowed: true);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Canvas 순서를 설정하고 대여형이면 원래 순서 복원 상태를 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void InitializeCore(int layerOrder, bool borrowed)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException
                (
                    "UGUI Presentation Output이 이미 초기화됐습니다."
                );
            }

            canvas = GetComponent<Canvas>();

            if (!Validate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            originalSortingOrder = canvas.sortingOrder;
            restoreOriginalOrder = borrowed;
            isInitialized = true;

            try
            {
                canvas.sortingOrder = layerOrder;
            }
            catch
            {
                ReleaseOutputState();
                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 빌린 Canvas의 authored sortingOrder를 되돌린다.
        /// <br/> 중복 Release와 Unity destroy callback을 지원한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ReleaseOutputState()
        {
            if (!isInitialized) return;

            var originalOrder = originalSortingOrder;
            var restore = restoreOriginalOrder;
            var currentCanvas = canvas;

            isInitialized = false;
            restoreOriginalOrder = false;
            originalSortingOrder = 0;

            if (restore && currentCanvas != null)
            {
                currentCanvas.sortingOrder = originalOrder;
            }
        }

        private void OnDestroy()
        {
            ReleaseOutputState();
        }

    #endregion

    #region 검증

        // ----------------------------------------------------------------------
        /// <summary>
        /// Canvas 출력에 필요한 Native component와 render mode를 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public bool Validate(out string error)
        {
            if (Canvas == null || Root == null)
            {
                error = "UGUI Presentation Output Canvas Root가 없습니다.";
                return false;
            }

            if (Canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                error = "UGUI Presentation Output은 Screen Space - Overlay여야 합니다.";
                return false;
            }

            if (GetComponent<CanvasScaler>() == null)
            {
                error = "UGUI Presentation Output CanvasScaler가 없습니다.";
                return false;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                error = "UGUI Presentation Output GraphicRaycaster가 없습니다.";
                return false;
            }

            error = "";
            return true;
        }

    #endregion

    }
}
