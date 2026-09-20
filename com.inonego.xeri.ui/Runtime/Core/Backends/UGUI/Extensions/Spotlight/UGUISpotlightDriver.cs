/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUISpotlightDriver.cs
수정일 : 2026-09-17
# 설명
실제 RectTransform 대상의 현재 World Corner를 UGUI Spotlight Graphic의 여러 로컬 구멍으로 갱신한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

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

    #region Unity 이벤트

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

    #region ISpotlightDriver

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

            if (graphic != null)
            {
                graphic.ClearHoles();
            }

            if (root != null)
            {
                root.SetActive(false);
            }
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 대상 World Corner를 Graphic 로컬 사각 구멍으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Refresh()
        {
            if (activeParams == null || graphic == null) return;

            holes.Clear();

            for (var i = 0; i < activeParams.Targets.Count; i++)
            {
                var target = activeParams.Targets[i];

                if
                (
                    target.Target == null ||
                    !target.Target.gameObject.activeInHierarchy
                )
                {
                    continue;
                }

                target.Target.GetWorldCorners(corners);
                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

                for (var cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    var local = graphic.rectTransform.InverseTransformPoint(corners[cornerIndex]);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }

                min.x -= target.Padding.x;
                max.x += target.Padding.y;
                max.y += target.Padding.z;
                min.y -= target.Padding.w;
                holes.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            }

            // 유효한 대상이 없으면 dim과 Raycast를 모두 비워 전체 입력 잠금을 만들지 않는다.
            if (holes.Count == 0)
            {
                graphic.ClearHoles();
                graphic.enabled = false;
                return;
            }

            graphic.enabled = true;
            graphic.SetHoles(holes, activeParams.BlocksOutsideInput);
        }

    #endregion

    }
}
