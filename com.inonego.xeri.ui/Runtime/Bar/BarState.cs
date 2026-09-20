/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : BarState.cs
수정일 : 2026-09-17
# 설명
UGUI와 UI Toolkit Bar가 공유하는 방향, Fill 상태 계산, 표시 비율 전이를 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Primitive;

#if DOTWEEN
using DG.Tweening;
#endif

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Bar 값이 증가하는 화면 방향.
    /// </summary>
    // ============================================================
    public enum BarDirection
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom,
    }

    // ============================================================
    /// <summary>
    /// 현재 표시 비율과 목표 비율 사이의 변화 종류.
    /// </summary>
    // ============================================================
    internal enum BarChange
    {
        None,
        Increase,
        Decrease,
    }

    // ============================================================
    /// <summary>
    /// Foreground와 Change Fill이 차지할 정규화 구간.
    /// </summary>
    // ============================================================
    internal readonly struct BarState
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Foreground Fill 시작 비율.
        /// </summary>
        // ------------------------------------------------------------
        public float ForegroundBegin { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Foreground Fill 종료 비율.
        /// </summary>
        // ------------------------------------------------------------
        public float ForegroundEnd { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Change Fill 시작 비율.
        /// </summary>
        // ------------------------------------------------------------
        public float ChangeBegin { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Change Fill 종료 비율.
        /// </summary>
        // ------------------------------------------------------------
        public float ChangeEnd { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 변화 종류.
        /// </summary>
        // ------------------------------------------------------------
        public BarChange Change { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Foreground와 Change Fill 구간 및 변화 종류를 묶어 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public BarState
        (
            float foregroundBegin,
            float foregroundEnd,
            float changeBegin,
            float changeEnd,
            BarChange change
        )
        {
            ForegroundBegin = foregroundBegin;
            ForegroundEnd   = foregroundEnd;
            ChangeBegin     = changeBegin;
            ChangeEnd       = changeEnd;
            Change          = change;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 값과 범위를 0~1 표시 비율로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static float ResolveRatio
        (
            float lowValue,
            float highValue,
            float value
        )
        {
            var range = highValue - lowValue;

            // 유효한 범위가 없으면 Bar를 비운 상태로 고정한다.
            if (range == 0.0f || !range.IsFinite())
            {
                return 0.0f;
            }

            var ratio = (value - lowValue) / range;

            // 비정상 입력이 화면 레이아웃 값으로 전파되지 않게 경계에서 정리한다.
            if (!ratio.IsFinite())
            {
                return 0.0f;
            }

            return Mathf.Clamp01(ratio);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 표시 비율과 목표 비율로 Foreground와 Change Fill 구간을 계산한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static BarState Resolve
        (
            float currentRatio,
            float targetRatio
        )
        {
            currentRatio = Mathf.Clamp01(currentRatio);
            targetRatio  = Mathf.Clamp01(targetRatio);

            // 증가 시 Foreground가 목표를 향해 이동하고 남은 구간을 Change Fill로 표시한다.
            if (currentRatio < targetRatio)
            {
                return new BarState
                (
                    0.0f, currentRatio,
                    currentRatio, targetRatio,
                    BarChange.Increase
                );
            }

            // 감소 시 Foreground는 새 값을 즉시 보여주고 이전 값까지의 차이를 Change Fill로 남긴다.
            if (currentRatio > targetRatio)
            {
                return new BarState
                (
                    0.0f, targetRatio,
                    targetRatio, currentRatio,
                    BarChange.Decrease
                );
            }

            return new BarState
            (
                0.0f, targetRatio,
                targetRatio, targetRatio,
                BarChange.None
            );
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// Bar의 현재 표시 비율을 목표 비율까지 전이한다.
    /// </summary>
    // ============================================================
    internal sealed class BarTransition
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 화면에 남아 있는 표시 비율.
        /// </summary>
        // ------------------------------------------------------------
        public float CurrentRatio => currentRatio;

        private float currentRatio = 0.0f;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 전이가 향하는 목표 비율.
        /// </summary>
        // ------------------------------------------------------------
        public float TargetRatio => targetRatio;

        private float targetRatio = 0.0f;

    #if DOTWEEN
        private Tween currentTween = null;
    #endif

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 초기 표시 비율이 0인 Bar 전이 상태를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public BarTransition() : base()
        {
            // NONE
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 목표 비율을 설정하고 표시 비율 전이를 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Set
        (
            float targetRatio,
            TweenCurve curve,
            bool instant,
            Action<float, float> onUpdate
        )
        {
            if (onUpdate == null)
            {
                throw new ArgumentNullException(nameof(onUpdate));
            }

            // 새 목표가 들어오면 이전 전이는 현재 표시 위치에서 종료하고 새 목표로 이어간다.
            Stop();
            this.targetRatio = Mathf.Clamp01(targetRatio);

        #if DOTWEEN
            if (!instant && Application.isPlaying && curve.Duration > 0.0f)
            {
                StartTween(curve, onUpdate);
                return;
            }
        #endif

            ApplyInstant(onUpdate);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중인 표시 비율 전이를 현재 위치에서 중단한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Stop()
        {
        #if DOTWEEN
            currentTween?.Kill();
            currentTween = null;
        #endif
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 표시 비율을 목표 비율에 즉시 맞추고 한 번 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyInstant(Action<float, float> onUpdate)
        {
            currentRatio = targetRatio;
            onUpdate(currentRatio, targetRatio);
        }

    #if DOTWEEN

        // ------------------------------------------------------------
        /// <summary>
        /// DOTween으로 현재 표시 비율을 목표 비율까지 전이한다.
        /// </summary>
        // ------------------------------------------------------------
        private void StartTween
        (
            TweenCurve curve,
            Action<float, float> onUpdate
        )
        {
            float GetCurrentRatio() => currentRatio;

            void SetCurrentRatio(float value)
            {
                currentRatio = value;
                onUpdate(currentRatio, targetRatio);
            }

            void OnComplete()
            {
                currentRatio = targetRatio;
                currentTween = null;
                onUpdate(currentRatio, targetRatio);
            }

            // Delay 동안에도 현재 값과 새 목표의 차이를 먼저 보여준다.
            onUpdate(currentRatio, targetRatio);

            currentTween = DOTween.To
            (
                GetCurrentRatio,
                SetCurrentRatio,
                targetRatio,
                curve.Duration
            );

            currentTween
                .SetDelay(Mathf.Max(0.0f, curve.Delay))
                .SetEase(curve.Ease);
            currentTween.onComplete = OnComplete;
        }

    #endif

    #endregion

    }
}
