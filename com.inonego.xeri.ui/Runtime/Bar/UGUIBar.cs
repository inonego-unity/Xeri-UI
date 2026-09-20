/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIBar.cs
수정일 : 2026-09-17
# 설명
값 범위를 Background, Change, Foreground Image 계층으로 표시하는 UGUI Bar 컴포넌트.

# 특이사항, 제약사항
Gameplay 상태 원본을 소유하지 않고 전달받은 값과 범위만 시각화한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UI;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI Image 계층으로 값을 표시하는 Bar.
    /// </summary>
    // ============================================================
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public sealed class UGUIBar : MonoBehaviour
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 범위의 하한값.
        /// </summary>
        // ------------------------------------------------------------
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

        [SerializeField]
        private float lowValue = 0.0f;

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 범위의 상한값.
        /// </summary>
        // ------------------------------------------------------------
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

        [SerializeField]
        private float highValue = 1.0f;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 목표값.
        /// </summary>
        // ------------------------------------------------------------
        public float Value
        {
            get => value;
            set => SetValue(value);
        }

        [SerializeField]
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

        [SerializeField]
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

        [SerializeField]
    #if DOTWEEN
        private TweenCurve changeCurve = new TweenCurve(0.35f, 0.08f, DG.Tweening.Ease.OutQuad);
    #else
        private TweenCurve changeCurve = new TweenCurve();
    #endif

        // ------------------------------------------------------------
        /// <summary>
        /// Bar의 바탕을 표시하는 Image.
        /// </summary>
        // ------------------------------------------------------------
        public Image BackgroundImage => backgroundImage;

        [SerializeField]
        private Image backgroundImage = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 증가·감소 차이 구간을 표시하는 Image.
        /// </summary>
        // ------------------------------------------------------------
        public Image ChangeImage => changeImage;

        [SerializeField]
        private Image changeImage = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 값을 표시하는 Foreground Image.
        /// </summary>
        // ------------------------------------------------------------
        public Image ForegroundImage => foregroundImage;

        [SerializeField]
        private Image foregroundImage = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Foreground 기본 색상.
        /// </summary>
        // ------------------------------------------------------------
        public Color ForegroundColor
        {
            get => foregroundColor;
            set
            {
                foregroundColor = value;
                ApplyCurrentState();
            }
        }

        [SerializeField]
        private Color foregroundColor = Color.white;

        // ------------------------------------------------------------
        /// <summary>
        /// 값 증가 구간의 Change Fill 색상.
        /// </summary>
        // ------------------------------------------------------------
        public Color IncreaseColor
        {
            get => increaseColor;
            set
            {
                increaseColor = value;
                ApplyCurrentState();
            }
        }

        [SerializeField]
        private Color increaseColor = Color.green;

        // ------------------------------------------------------------
        /// <summary>
        /// 값 감소 구간의 Change Fill 색상.
        /// </summary>
        // ------------------------------------------------------------
        public Color DecreaseColor
        {
            get => decreaseColor;
            set
            {
                decreaseColor = value;
                ApplyCurrentState();
            }
        }

        [SerializeField]
        private Color decreaseColor = Color.red;

        private readonly BarTransition transition = new BarTransition();

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
            // 비활성 상태에서는 보이지 않는 전이를 만들지 않고 최신 값으로 즉시 맞춘다.
            var applyInstant = instant || !isActiveAndEnabled;
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
        /// 현재 전이 상태를 유지한 채 방향과 색상 변경을 다시 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyCurrentState()
        {
            ApplyRatio(transition.CurrentRatio, transition.TargetRatio);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 비율과 목표 비율을 UGUI Image 계층에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyRatio
        (
            float currentRatio,
            float targetRatio
        )
        {
            var state = BarState.Resolve(currentRatio, targetRatio);

            // Foreground는 항상 기본 색을 유지하고 Change Fill만 변화 의미를 담당한다.
            if (foregroundImage != null)
            {
                foregroundImage.color = foregroundColor;
            }

            if (changeImage != null)
            {
                changeImage.color = ResolveChangeColor(state.Change);
            }

            SetFillRange
            (
                foregroundImage,
                state.ForegroundBegin,
                state.ForegroundEnd
            );
            SetFillRange
            (
                changeImage,
                state.ChangeBegin,
                state.ChangeEnd
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 변화 종류에 대응하는 Change Fill 색상을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private Color ResolveChangeColor(BarChange change)
        {
            switch (change)
            {
                case BarChange.Increase:
                    return increaseColor;

                case BarChange.Decrease:
                    return decreaseColor;

                case BarChange.None:
                default:
                    return foregroundColor;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Image RectTransform을 지정한 정규화 구간에 맞춘다.
        /// </summary>
        // ------------------------------------------------------------
        private void SetFillRange
        (
            Image image,
            float beginRatio,
            float endRatio
        )
        {
            if (image == null) return;

            var rectTransform = image.rectTransform;
            Vector2 anchorMin;
            Vector2 anchorMax;
            Vector2 pivot;

            // 방향별 anchor 구간만 달리하고 offset 초기화는 공통 처리한다.
            switch (direction)
            {
                case BarDirection.RightToLeft:
                    anchorMin = new Vector2(1.0f - endRatio, 0.0f);
                    anchorMax = new Vector2(1.0f - beginRatio, 1.0f);
                    pivot     = new Vector2(1.0f, 0.5f);
                    break;

                case BarDirection.BottomToTop:
                    anchorMin = new Vector2(0.0f, beginRatio);
                    anchorMax = new Vector2(1.0f, endRatio);
                    pivot     = new Vector2(0.5f, 0.0f);
                    break;

                case BarDirection.TopToBottom:
                    anchorMin = new Vector2(0.0f, 1.0f - endRatio);
                    anchorMax = new Vector2(1.0f, 1.0f - beginRatio);
                    pivot     = new Vector2(0.5f, 1.0f);
                    break;

                case BarDirection.LeftToRight:
                default:
                    anchorMin = new Vector2(beginRatio, 0.0f);
                    anchorMax = new Vector2(endRatio, 1.0f);
                    pivot     = new Vector2(0.0f, 0.5f);
                    break;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot     = pivot;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

    #endregion

    #region Unity 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 활성화 시 직렬화된 값을 즉시 화면에 동기화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnEnable()
        {
            Refresh(true);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 비활성화 시 진행 중인 표시 비율 전이를 중단한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDisable()
        {
            transition.Stop();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Inspector 변경을 에디터 미리보기에 즉시 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            Refresh(true);
        }

    #endregion

    }
}
