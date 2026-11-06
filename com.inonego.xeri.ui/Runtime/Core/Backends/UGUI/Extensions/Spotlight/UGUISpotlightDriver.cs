/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUISpotlightDriver.cs
수정일 : 2026-11-06
# 설명
실제 RectTransform 대상의 Canvas 표시 상태와 RectMask2D 클리핑을 반영해 Spotlight Graphic 구멍을 갱신한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI 기반 Spotlight 표시 backend.
    /// </summary>
    // ============================================================
    public sealed class UGUISpotlightDriver : MonoBehaviour, ISpotlightDriver<UGUISpotlightParams>
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Spotlight Graphic과 표시 Root가 연결됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsValid => root != null && graphic != null;

        [SerializeField]
        private GameObject root = null;

        [SerializeField]
        private UGUISpotlightGraphic graphic = null;

        private readonly List<Rect> holes = new List<Rect>();
        private readonly Vector3[] corners = new Vector3[4];
        private UGUISpotlightParams activeParams = null;

    #endregion

    #region Unity 수명주기

        // ------------------------------------------------------------
        /// <summary>
        /// 이동·Layout 변경된 실제 대상의 Spotlight 구멍을 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void LateUpdate()
        {
            if (activeParams != null)
            {
                Refresh();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// backend가 비활성화될 때 별도 표시 Root의 dim과 입력 차단도 제거한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void OnDisable()
        {
            Hide();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// backend 파괴 시 남은 표시 상태를 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDestroy()
        {
            Hide();
        }

    #endregion

    #region 표시 요청 및 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 실제 RectTransform 대상의 현재 Spotlight 구멍을 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Show(UGUISpotlightParams parameters)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("UGUI Spotlight 참조가 연결되지 않았습니다.");
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            var previous = activeParams;
            activeParams = parameters;

            try
            {
                root.SetActive(true);
                Refresh();
            }
            catch (Exception exception)
            {
                try
                {
                    activeParams = previous;

                    if (previous != null)
                    {
                        root.SetActive(true);
                        Refresh();
                    }
                    else
                    {
                        Hide();
                    }
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        "UGUI Spotlight 표시와 이전 상태 복원이 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Spotlight 구멍과 바깥 입력 차단을 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Hide()
        {
            activeParams = null;
            holes.Clear();
            var errors = new ExceptionBuffer();

            if (graphic != null)
            {
                try
                {
                    graphic.ClearHoles();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (root != null)
            {
                try
                {
                    root.SetActive(false);
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
                "UGUI Spotlight 숨김 처리 중 하나 이상의 정리가 실패했습니다."
            );
        }

    #endregion

    #region 대상 영역 갱신

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 대상의 표시 가능 영역을 구멍으로 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Refresh()
        {
            if (activeParams == null || graphic == null) return;

            holes.Clear();

            for (var i = 0; i < activeParams.Targets.Count; i++)
            {
                if (TryGetVisibleHole(activeParams.Targets[i], out var hole))
                {
                    holes.Add(hole);
                }
            }

            // 표시 가능한 대상이 없다면 dim과 입력 차단도 함께 해제한다.
            if (holes.Count == 0)
            {
                graphic.ClearHoles();
                graphic.enabled = false;
                return;
            }

            graphic.enabled = true;
            graphic.SetHoles(holes, activeParams.BlocksOutsideInput);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 대상·Canvas·RectMask2D의 표시 계약을 적용한다.
        /// <br/> 최종 영역은 Spotlight Graphic의 로컬 사각형으로 제한한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool TryGetVisibleHole(UGUISpotlightTarget target, out Rect hole)
        {
            hole = Rect.zero;
            var rect = target.Target;

            if (rect == null || !rect.gameObject.activeInHierarchy) return false;

            hole = GetLocalRect(rect);
            hole.xMin -= target.Padding.x;
            hole.xMax += target.Padding.y;
            hole.yMax += target.Padding.z;
            hole.yMin -= target.Padding.w;

            // Graphic 자체가 masking을 거부하면 조상 RectMask2D도 적용하지 않는다.
            var clipAncestors =
                !rect.TryGetComponent<MaskableGraphic>(out var targetGraphic) ||
                targetGraphic.maskable;

            for (Transform ancestor = rect; ancestor != null; ancestor = ancestor.parent)
            {
                if
                (
                    ancestor.TryGetComponent<CanvasGroup>(out var group) &&
                    group.isActiveAndEnabled &&
                    group.alpha <= 0.0f
                )
                {
                    return false;
                }

                if (ancestor.TryGetComponent<Canvas>(out var canvas))
                {
                    if (!canvas.isActiveAndEnabled) return false;
                }

                if
                (
                    clipAncestors &&
                    ancestor != rect &&
                    ancestor.TryGetComponent<RectMask2D>(out var mask) &&
                    mask.isActiveAndEnabled
                )
                {
                    var maskCanvas = mask.GetComponentInParent<Canvas>();
                    if (maskCanvas != null)
                    {
                        hole = Clamp
                        (
                            hole,
                            GetLocalMaskRect(mask, maskCanvas.rootCanvas.transform)
                        );

                        if (hole.width <= 0.0f || hole.height <= 0.0f) return false;
                    }
                }

                // overrideSorting은 마스크 상속만 끊고 Canvas 표시 상태 검사는 유지한다.
                if (canvas != null && canvas.overrideSorting)
                {
                    clipAncestors = false;
                }
            }

            hole = Clamp(hole, graphic.rectTransform.rect);
            return hole.width > 0.0f && hole.height > 0.0f;
        }

    #endregion

    #region 좌표 및 마스크 변환

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 RectTransform의 World Corner를 Graphic 로컬 Rect로 변환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private Rect GetLocalRect(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            return GetLocalWorldCornersRect();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// RectMask2D의 Canvas clip과 padding을 Graphic 좌표계로 변환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private Rect GetLocalMaskRect(RectMask2D mask, Transform canvas)
        {
            var clip = mask.canvasRect;
            var padding = mask.padding;
            var xMin = clip.xMin + padding.x;
            var xMax = clip.xMax - padding.z;
            var yMin = clip.yMin + padding.y;
            var yMax = clip.yMax - padding.w;

            if (xMin >= xMax || yMin >= yMax) return Rect.zero;

            corners[0] = canvas.TransformPoint(new Vector3(xMin, yMin));
            corners[1] = canvas.TransformPoint(new Vector3(xMin, yMax));
            corners[2] = canvas.TransformPoint(new Vector3(xMax, yMax));
            corners[3] = canvas.TransformPoint(new Vector3(xMax, yMin));
            return GetLocalWorldCornersRect();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 확보한 World Corner 네 개를 Graphic 로컬 사각형으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private Rect GetLocalWorldCornersRect()
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (var i = 0; i < corners.Length; i++)
            {
                var local = graphic.rectTransform.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Graphic 좌표계에서 대상 Rect를 상위 Mask 영역 안으로 제한한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Rect Clamp(Rect value, Rect bounds)
        {
            var xMin = Mathf.Clamp(value.xMin, bounds.xMin, bounds.xMax);
            var xMax = Mathf.Clamp(value.xMax, bounds.xMin, bounds.xMax);
            var yMin = Mathf.Clamp(value.yMin, bounds.yMin, bounds.yMax);
            var yMax = Mathf.Clamp(value.yMax, bounds.yMin, bounds.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

    #endregion

    }
}
