/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKBar.cs
수정일 : 2026-09-17
# 설명
ProgressBar와 같은 값 범위 API를 제공하고 3계층 Fill을 직접 소유하는 UI Toolkit Bar.

# 특이사항, 제약사항
Unity ProgressBar 내부 Visual Tree에 의존하지 않고 Xeri Bar의 변화 표현과 4방향을 직접 구성한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit VisualElement 계층으로 값을 표시하는 Bar.
    /// </summary>
    // ============================================================
    [UxmlElement]
    public partial class UITKBar : VisualElement
    {

    #region 내부 데이터

        private const string STYLE_SHEET_PATH = "Xeri/Bar/UITKBar";
        private const string ROOT_CLASS = "xeri-bar";
        private const string BACKGROUND_CLASS = "xeri-bar__background";
        private const string CHANGE_CLASS = "xeri-bar__change";
        private const string FOREGROUND_CLASS = "xeri-bar__foreground";
        private const string INCREASE_CLASS = "xeri-bar--increase";
        private const string DECREASE_CLASS = "xeri-bar--decrease";

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 범위의 하한값.
        /// </summary>
        // ------------------------------------------------------------
        [UxmlAttribute("low-value")]
        public float LowValue
        {
            get => lowValue;
            set
            {
                if (lowValue == value) return;

                lowValue = value;
                Refresh();
            }
        }

        private float lowValue = 0.0f;

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 범위의 상한값.
        /// </summary>
        // ------------------------------------------------------------
        [UxmlAttribute("high-value")]
        public float HighValue
        {
            get => highValue;
            set
            {
                if (highValue == value) return;

                highValue = value;
                Refresh();
            }
        }

        private float highValue = 1.0f;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 목표값.
        /// </summary>
        // ------------------------------------------------------------
        [UxmlAttribute("value")]
        public float Value
        {
            get => value;
            set => SetValue(value);
        }

        private float value = 1.0f;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 목표값이 범위에서 차지하는 0~1 비율.
        /// </summary>
        // ------------------------------------------------------------
        public float Ratio => BarState.ResolveRatio(lowValue, highValue, value);

        // ------------------------------------------------------------
        /// <summary>
        /// 값이 증가할 때 Fill이 진행하는 화면 방향.
        /// </summary>
        // ------------------------------------------------------------
        [UxmlAttribute("direction")]
        public BarDirection Direction
        {
            get => direction;
            set
            {
                if (direction == value) return;

                direction = value;
                ApplyCurrentState();
            }
        }

        private BarDirection direction = BarDirection.LeftToRight;

        // ------------------------------------------------------------
        /// <summary>
        /// 값 변화 표시 비율에 적용할 전이 곡선.
        /// </summary>
        // ------------------------------------------------------------
        public TweenCurve ChangeCurve
        {
            get => changeCurve;
            set => changeCurve = value;
        }

    #if DOTWEEN
        private TweenCurve changeCurve = new TweenCurve(0.35f, 0.08f, DG.Tweening.Ease.OutQuad);
    #else
        private TweenCurve changeCurve = new TweenCurve();
    #endif

        // ------------------------------------------------------------
        /// <summary>
        /// Bar의 바탕 VisualElement.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Background => background;

        private readonly VisualElement background = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 증가·감소 차이 구간을 표시하는 VisualElement.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Change => change;

        private readonly VisualElement change = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 값을 표시하는 Foreground VisualElement.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Foreground => foreground;

        private readonly VisualElement foreground = null;

        private readonly BarTransition transition = new BarTransition();

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 VisualElement 계층과 스타일을 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKBar() : base()
        {
            name = ROOT_CLASS;
            pickingMode = PickingMode.Ignore;
            AddToClassList(ROOT_CLASS);
            LoadStyleSheet();

            background = CreatePart("background", BACKGROUND_CLASS);
            change     = CreatePart("change", CHANGE_CLASS);
            foreground = CreatePart("foreground", FOREGROUND_CLASS);

            hierarchy.Add(background);
            hierarchy.Add(change);
            hierarchy.Add(foreground);
            Refresh(true);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 범위를 한 번에 설정하고 Bar를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetRange
        (
            float lowValue,
            float highValue,
            bool instant = false
        )
        {
            this.lowValue  = lowValue;
            this.highValue = highValue;
            Refresh(instant);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 목표값을 설정하고 Bar를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetValue
        (
            float value,
            bool instant = false
        )
        {
            this.value = value;
            Refresh(instant);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 값과 범위를 다시 계산해 표시 비율 전이를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Refresh(bool instant = false)
        {
            // Panel 밖에서는 scheduler/tween 수명을 만들지 않고 최신 상태만 유지한다.
            var applyInstant = instant || panel == null;
            transition.Set
            (
                Ratio,
                changeCurve,
                applyInstant,
                ApplyRatio
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 전이 상태를 유지한 채 방향 변경을 다시 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyCurrentState()
        {
            ApplyRatio(transition.CurrentRatio, transition.TargetRatio);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 비율과 목표 비율을 UI Toolkit Fill 계층에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyRatio
        (
            float currentRatio,
            float targetRatio
        )
        {
            var state = BarState.Resolve(currentRatio, targetRatio);

            ApplyChangeClass(state.Change);
            SetFillRange
            (
                foreground,
                state.ForegroundBegin,
                state.ForegroundEnd
            );
            SetFillRange
            (
                change,
                state.ChangeBegin,
                state.ChangeEnd
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 변화 종류에 맞는 상태 class를 Root에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyChangeClass(BarChange barChange)
        {
            RemoveFromClassList(INCREASE_CLASS);
            RemoveFromClassList(DECREASE_CLASS);

            if (barChange == BarChange.Increase)
            {
                AddToClassList(INCREASE_CLASS);
                return;
            }

            if (barChange == BarChange.Decrease)
            {
                AddToClassList(DECREASE_CLASS);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// VisualElement를 지정한 정규화 구간에 맞춰 배치한다.
        /// </summary>
        // ------------------------------------------------------------
        private void SetFillRange
        (
            VisualElement element,
            float beginRatio,
            float endRatio
        )
        {
            beginRatio = Mathf.Clamp01(beginRatio);
            endRatio   = Mathf.Clamp01(endRatio);

            var beginPercent = Length.Percent(beginRatio * 100.0f);
            var endPercent   = Length.Percent((1.0f - endRatio) * 100.0f);

            // 방향에 따라 진행축의 시작·끝 inset만 바꾸고 반대축은 전체를 사용한다.
            switch (direction)
            {
                case BarDirection.RightToLeft:
                    element.style.left   = endPercent;
                    element.style.right  = beginPercent;
                    element.style.top    = 0.0f;
                    element.style.bottom = 0.0f;
                    break;

                case BarDirection.BottomToTop:
                    element.style.left   = 0.0f;
                    element.style.right  = 0.0f;
                    element.style.bottom = beginPercent;
                    element.style.top    = endPercent;
                    break;

                case BarDirection.TopToBottom:
                    element.style.left   = 0.0f;
                    element.style.right  = 0.0f;
                    element.style.top    = beginPercent;
                    element.style.bottom = endPercent;
                    break;

                case BarDirection.LeftToRight:
                default:
                    element.style.left   = beginPercent;
                    element.style.right  = endPercent;
                    element.style.top    = 0.0f;
                    element.style.bottom = 0.0f;
                    break;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 UITK Bar 스타일을 Resources에서 로드한다.
        /// </summary>
        // ------------------------------------------------------------
        private void LoadStyleSheet()
        {
            var styleSheet = Resources.Load<StyleSheet>(STYLE_SHEET_PATH);

            if (styleSheet == null)
            {
                throw new InvalidOperationException
                (
                    $"UITKBar USS를 로드할 수 없습니다. Path: {STYLE_SHEET_PATH}"
                );
            }

            styleSheets.Add(styleSheet);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 입력을 받지 않는 Bar 내부 VisualElement를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static VisualElement CreatePart
        (
            string name,
            string className
        )
        {
            var element = new VisualElement
            {
                name = name,
                pickingMode = PickingMode.Ignore,
            };
            element.AddToClassList(className);
            return element;
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Panel에서 분리될 때 진행 중인 표시 비율 전이를 중단한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            transition.Stop();
        }

    #endregion

    }
}
