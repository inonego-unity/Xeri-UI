/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUISpotlightGraphic.cs
수정일 : 2026-09-17
# 설명
UGUI dim 영역을 여러 Spotlight 구멍으로 분할 렌더링하고 구멍 바깥 Raycast만 차단한다.
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
    /// 여러 사각 구멍을 지원하는 UGUI Spotlight Graphic.
    /// </summary>
    // ============================================================
    public sealed class UGUISpotlightGraphic : MaskableGraphic, ICanvasRaycastFilter
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Spotlight 구멍 수.
        /// </summary>
        // ------------------------------------------------------------
        public int HoleCount => holes.Count;

        private readonly List<Rect> holes = new List<Rect>();
        private readonly List<float> xCoordinates = new List<float>();
        private readonly List<float> yCoordinates = new List<float>();
        private bool blocksOutsideInput = false;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 사각 구멍과 바깥 입력 차단 상태를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetHoles
        (
            IReadOnlyList<Rect> nextHoles,
            bool blocksOutsideInput
        )
        {
            holes.Clear();

            if (nextHoles != null)
            {
                for (var i = 0; i < nextHoles.Count; i++)
                {
                    holes.Add(nextHoles[i]);
                }
            }

            this.blocksOutsideInput = blocksOutsideInput;
            raycastTarget = blocksOutsideInput;
            SetVerticesDirty();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 구멍과 입력 차단 상태를 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ClearHoles()
        {
            holes.Clear();
            blocksOutsideInput = false;
            raycastTarget = false;
            SetVerticesDirty();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Pointer가 모든 구멍 바깥에 있을 때만 Raycast를 유효하게 한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsRaycastLocationValid
        (
            Vector2 screenPoint,
            Camera eventCamera
        )
        {
            if (!blocksOutsideInput) return false;

            if
            (
                !RectTransformUtility.ScreenPointToLocalPointInRectangle
                (
                    rectTransform,
                    screenPoint,
                    eventCamera,
                    out var localPoint
                )
            )
            {
                return true;
            }

            return !Contains(localPoint);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 전체 Rect를 구멍 경계 Grid로 나누고 구멍 밖 Cell만 Quad로 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            var bounds = rectTransform.rect;
            BuildCoordinates(bounds);

            for (var x = 0; x < xCoordinates.Count - 1; x++)
            {
                for (var y = 0; y < yCoordinates.Count - 1; y++)
                {
                    var min = new Vector2(xCoordinates[x], yCoordinates[y]);
                    var max = new Vector2(xCoordinates[x + 1], yCoordinates[y + 1]);
                    var center = (min + max) * 0.5f;

                    if (Contains(center)) continue;

                    AddQuad(vertexHelper, min, max);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 전체 영역과 각 구멍 경계 좌표를 정렬된 Grid 축으로 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BuildCoordinates(Rect bounds)
        {
            xCoordinates.Clear();
            yCoordinates.Clear();
            xCoordinates.Add(bounds.xMin);
            xCoordinates.Add(bounds.xMax);
            yCoordinates.Add(bounds.yMin);
            yCoordinates.Add(bounds.yMax);

            for (var i = 0; i < holes.Count; i++)
            {
                var hole = Clamp(holes[i], bounds);
                AddCoordinate(xCoordinates, hole.xMin);
                AddCoordinate(xCoordinates, hole.xMax);
                AddCoordinate(yCoordinates, hole.yMin);
                AddCoordinate(yCoordinates, hole.yMax);
            }

            xCoordinates.Sort();
            yCoordinates.Sort();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 중복되지 않은 Grid 좌표를 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AddCoordinate
        (
            List<float> coordinates,
            float value
        )
        {
            for (var i = 0; i < coordinates.Count; i++)
            {
                if (Mathf.Approximately(coordinates[i], value))
                {
                    return;
                }
            }

            coordinates.Add(value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 위치가 하나 이상의 Spotlight 구멍 안에 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool Contains(Vector2 point)
        {
            for (var i = 0; i < holes.Count; i++)
            {
                if (holes[i].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 구멍 Rect를 Graphic 전체 영역 안으로 제한한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Rect Clamp
        (
            Rect value,
            Rect bounds
        )
        {
            var xMin = Mathf.Clamp(value.xMin, bounds.xMin, bounds.xMax);
            var xMax = Mathf.Clamp(value.xMax, bounds.xMin, bounds.xMax);
            var yMin = Mathf.Clamp(value.yMin, bounds.yMin, bounds.yMax);
            var yMax = Mathf.Clamp(value.yMax, bounds.yMin, bounds.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Dim Cell 하나를 UGUI Quad로 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AddQuad
        (
            VertexHelper vertexHelper,
            Vector2 min,
            Vector2 max
        )
        {
            var start = vertexHelper.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = new Vector3(min.x, min.y);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(min.x, max.y);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(max.x, max.y);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(max.x, min.y);
            vertexHelper.AddVert(vertex);

            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

    #endregion

    }
}
