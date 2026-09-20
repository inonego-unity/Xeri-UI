/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSpotlightElement.cs
수정일 : 2026-09-17
# 설명
UI Toolkit dim을 여러 Spotlight 구멍으로 렌더링하고 구멍 바깥 Pointer 입력만 차단한다.
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
    /// 여러 사각 구멍을 지원하는 UI Toolkit Spotlight Element.
    /// </summary>
    // ============================================================
    public sealed class UITKSpotlightElement : VisualElement, ISpotlightDriver<UITKSpotlightParams>
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Spotlight 구멍 수.
        /// </summary>
        // ------------------------------------------------------------
        public int HoleCount => holes.Count;

        // ------------------------------------------------------------
        /// <summary>
        /// 구멍 바깥에 표시할 dim 색상.
        /// </summary>
        // ------------------------------------------------------------
        public Color DimColor
        {
            get => dimColor;
            set
            {
                dimColor = value;
                MarkDirtyRepaint();
            }
        }

        private Color dimColor = new Color(0.0f, 0.0f, 0.0f, 0.72f);

        // ------------------------------------------------------------
        /// <summary>
        /// Spotlight Element가 현재 Panel 좌표를 사용할 수 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsValid => panel != null;

        private readonly List<Rect> holes = new List<Rect>();
        private readonly List<float> xCoordinates = new List<float>();
        private readonly List<float> yCoordinates = new List<float>();
        private readonly List<VisualElement> observedTargets = new List<VisualElement>();
        private UITKSpotlightParams activeParams = null;
        private bool blocksOutsideInput = false;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 부모 Root 전체를 덮는 Spotlight Element를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKSpotlightElement() : base()
        {
            name = "UITK Spotlight";
            style.position = Position.Absolute;
            style.left = 0.0f;
            style.top = 0.0f;
            style.right = 0.0f;
            style.bottom = 0.0f;
            style.visibility = Visibility.Hidden;
            pickingMode = PickingMode.Ignore;

            generateVisualContent += HandleGenerateVisualContent;
            RegisterCallback<AttachToPanelEvent>(HandleAttachedToPanel);
            RegisterCallback<DetachFromPanelEvent>(HandleDetachedFromPanel);
        }

    #endregion

    #region ISpotlightDriver

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 실제 VisualElement 대상의 현재 Spotlight 구멍을 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Show(UITKSpotlightParams parameters)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException
                (
                    "UITK Spotlight Element가 Panel에 연결되지 않았습니다."
                );
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            ValidateTargets(parameters);

            var previous = activeParams;
            UnobserveTargets();
            activeParams = parameters;

            try
            {
                ObserveTargets(parameters);
                Refresh();
            }
            catch (Exception exception)
            {
                try
                {
                    UnobserveTargets();
                    activeParams = previous;

                    if (previous != null)
                    {
                        ObserveTargets(previous);
                        Refresh();
                    }
                    else
                    {
                        ClearVisualState();
                    }
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        "UITK Spotlight 표시와 이전 상태 복원이 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Spotlight 구멍, 대상 구독과 바깥 입력 차단을 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Hide()
        {
            activeParams = null;
            UnobserveTargets();
            ClearVisualState();
        }

    #endregion

    #region VisualElement

        // ------------------------------------------------------------
        /// <summary>
        /// Pointer가 모든 구멍 바깥에 있을 때만 Picking을 허용한다.
        /// </summary>
        // ------------------------------------------------------------
        public override bool ContainsPoint(Vector2 localPoint)
        {
            return
                blocksOutsideInput &&
                base.ContainsPoint(localPoint) &&
                !ContainsHole(localPoint);
        }

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// 실제 Target 좌표를 Spotlight Element 로컬 사각 구멍으로 변환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void Refresh()
        {
            if (activeParams == null || panel == null)
            {
                ClearVisualState();
                return;
            }

            holes.Clear();

            for (var i = 0; i < activeParams.Targets.Count; i++)
            {
                var target = activeParams.Targets[i];
                var element = target.Target;

                if
                (
                    element.panel != panel ||
                    !element.visible ||
                    element.resolvedStyle.display == DisplayStyle.None
                )
                {
                    continue;
                }

                var hole = element.ChangeCoordinatesTo
                (
                    this,
                    new Rect(Vector2.zero, element.layout.size)
                );

                if (hole.width <= 0.0f || hole.height <= 0.0f) continue;

                hole.xMin -= target.Padding.x;
                hole.xMax += target.Padding.y;
                hole.yMin -= target.Padding.z;
                hole.yMax += target.Padding.w;
                holes.Add(hole);
            }

            // 유효한 대상이 없으면 dim과 Picking을 함께 비워 전체 입력 잠금을 만들지 않는다.
            if (holes.Count == 0)
            {
                ClearVisualState();
                return;
            }

            blocksOutsideInput = activeParams.BlocksOutsideInput;
            pickingMode = blocksOutsideInput ? PickingMode.Position : PickingMode.Ignore;
            style.visibility = Visibility.Visible;
            MarkDirtyRepaint();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 dim과 Picking 상태를 제거하되 활성 요청은 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ClearVisualState()
        {
            holes.Clear();
            blocksOutsideInput = false;
            pickingMode = PickingMode.Ignore;
            style.visibility = Visibility.Hidden;
            MarkDirtyRepaint();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 Target이 현재 Spotlight와 같은 Panel에 연결됐는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ValidateTargets(UITKSpotlightParams parameters)
        {
            for (var i = 0; i < parameters.Targets.Count; i++)
            {
                if (parameters.Targets[i].Target.panel == panel) continue;

                throw new InvalidOperationException
                (
                    $"UITK Spotlight 대상 {i}가 같은 Panel에 연결되지 않았습니다."
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Target의 Geometry와 Panel 연결 변경을 구독한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ObserveTargets(UITKSpotlightParams parameters)
        {
            for (var i = 0; i < parameters.Targets.Count; i++)
            {
                var target = parameters.Targets[i].Target;

                if (observedTargets.Contains(target)) continue;

                observedTargets.Add(target);
                target.RegisterCallback<GeometryChangedEvent>(HandleTargetGeometryChanged);
                target.RegisterCallback<AttachToPanelEvent>(HandleTargetAttachedToPanel);
                target.RegisterCallback<DetachFromPanelEvent>(HandleTargetDetachedFromPanel);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Target의 Geometry와 Panel 연결 변경 구독을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnobserveTargets()
        {
            for (var i = 0; i < observedTargets.Count; i++)
            {
                var target = observedTargets[i];
                target.UnregisterCallback<GeometryChangedEvent>(HandleTargetGeometryChanged);
                target.UnregisterCallback<AttachToPanelEvent>(HandleTargetAttachedToPanel);
                target.UnregisterCallback<DetachFromPanelEvent>(HandleTargetDetachedFromPanel);
            }

            observedTargets.Clear();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 전체 Rect를 구멍 경계 Grid로 나누고 구멍 밖 Cell을 채운다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleGenerateVisualContent(MeshGenerationContext context)
        {
            if (holes.Count == 0) return;

            var painter = context.painter2D;
            var bounds = contentRect;
            BuildCoordinates(bounds);
            painter.fillColor = dimColor;

            for (var x = 0; x < xCoordinates.Count - 1; x++)
            {
                for (var y = 0; y < yCoordinates.Count - 1; y++)
                {
                    var min = new Vector2(xCoordinates[x], yCoordinates[y]);
                    var max = new Vector2(xCoordinates[x + 1], yCoordinates[y + 1]);
                    var center = (min + max) * 0.5f;

                    if (ContainsHole(center)) continue;

                    FillRect(painter, min, max);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 전체 영역과 각 구멍 경계를 정렬된 Grid 축으로 구성한다.
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
                if (Mathf.Approximately(coordinates[i], value)) return;
            }

            coordinates.Add(value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 위치가 하나 이상의 Spotlight 구멍 안에 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool ContainsHole(Vector2 point)
        {
            for (var i = 0; i < holes.Count; i++)
            {
                if (holes[i].Contains(point)) return true;
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 구멍 Rect를 Spotlight 전체 영역 안으로 제한한다.
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
        /// dim Cell 하나를 UI Toolkit 사각형으로 채운다.
        /// </summary>
        // ------------------------------------------------------------
        private static void FillRect
        (
            Painter2D painter,
            Vector2 min,
            Vector2 max
        )
        {
            painter.BeginPath();
            painter.MoveTo(min);
            painter.LineTo(new Vector2(max.x, min.y));
            painter.LineTo(max);
            painter.LineTo(new Vector2(min.x, max.y));
            painter.ClosePath();
            painter.Fill();
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Spotlight가 Panel에 연결되면 현재 Target 좌표를 다시 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleAttachedToPanel(AttachToPanelEvent evt)
        {
            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Spotlight가 Panel에서 분리되면 현재 dim과 Picking을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleDetachedFromPanel(DetachFromPanelEvent evt)
        {
            ClearVisualState();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Target Geometry 변경을 현재 Spotlight 구멍에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleTargetGeometryChanged(GeometryChangedEvent evt)
        {
            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Target의 Panel 연결을 현재 Spotlight 구멍에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleTargetAttachedToPanel(AttachToPanelEvent evt)
        {
            Refresh();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Target의 Panel 분리를 현재 Spotlight 구멍에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleTargetDetachedFromPanel(DetachFromPanelEvent evt)
        {
            Refresh();
        }

    #endregion

    }
}
