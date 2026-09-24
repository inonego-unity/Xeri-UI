/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriUITKWindowStateAnimator.cs
수정일 : 2026-09-28

# 설명
Xeri window 상태 전환의 Alpha는 UI Core Presentation에 적용하고 bounds는 UITK style로 보간한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// UITK 기반 Xeri window 상태 전환 animator.
    /// </summary>
    // ============================================================
    public sealed class XeriUITKWindowStateAnimator : IXeriWindowStateAnimator
    {

    #region 필드

        private readonly VisualElement target = null;
        private readonly XeriWindowPanel panel = null;
        private readonly IXeriWindowAnimationTargetProvider targetProvider = null;
        private readonly XeriWindowAnimationOptions options;

        private IVisualElementScheduledItem scheduledItem = null;
        private XeriWindowStateTransitionContext context;
        private Action onComplete = null;
        private Action<Exception> onError = null;
        private float startTime = 0f;

        // ------------------------------------------------------------
        /// <summary>
        /// animation 실행 중 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsRunning => isRunning;

        private bool isRunning = false;
        private bool isContentInputBlocked = false;
        private PickingMode previousContentPickingMode = PickingMode.Position;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// UITK window animator를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriUITKWindowStateAnimator
        (
            VisualElement target,
            XeriWindowAnimationOptions options,
            IXeriWindowAnimationTargetProvider targetProvider = null
        ) : base()
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            panel = target as XeriWindowPanel;
            this.options = options;
            this.targetProvider = targetProvider ?? new XeriDefaultWindowAnimationTargetProvider(target);
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 visual animation을 실행한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Play
        (
            XeriWindowStateTransitionContext context,
            Action onComplete,
            Action<Exception> onError
        )
        {
            if (isRunning || scheduledItem != null)
            {
                Cancel(true);
            }

            this.context = context;
            this.onComplete = onComplete;
            this.onError = onError;
            BlockContentInput();

            if (!options.Enabled || options.Duration <= 0f || target.panel == null)
            {
                ApplyProgressVisual(1f);
                Complete();
                return;
            }

            isRunning = true;
            startTime = Time.realtimeSinceStartup;
            ApplyProgressVisual(0f);

            scheduledItem = target.schedule.Execute(UpdateAnimation).Every(16);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 visual animation을 취소한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Cancel(bool restoreVisual)
        {
            if (scheduledItem != null)
            {
                scheduledItem.Pause();
                scheduledItem = null;
            }

            if (restoreVisual)
            {
                RestorePreviousVisual();
            }

            RestoreContentInput();
            isRunning = false;
            context = default;
            onComplete = null;
            onError = null;
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// schedule tick에서 animation 진행률을 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UpdateAnimation()
        {
            try
            {
                var elapsed = Time.realtimeSinceStartup - startTime;
                var progress = Mathf.Clamp01(elapsed / options.Duration);

                ApplyProgressVisual(Smooth(progress));

                if (progress >= 1f)
                {
                    Complete();
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 진행률에 맞춰 opacity와 bounds를 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyProgressVisual(float progress)
        {
            ApplyOpacity(progress);
            ApplyBounds(progress);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 방향에 맞춰 opacity를 보간한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyOpacity(float progress)
        {
            if (context.NextState == XeriWindowState.Minimized || context.NextState == XeriWindowState.Closed)
            {
                context.Driver.Alpha.Set(Mathf.Lerp(1f, options.HiddenOpacity, progress));
                return;
            }

            if
            (
             context.PreviousState == XeriWindowState.Minimized ||
             context.PreviousState == XeriWindowState.Closed
            )
            {
                context.Driver.Alpha.Set(Mathf.Lerp(options.HiddenOpacity, 1f, progress));
                return;
            }

            context.Driver.Alpha.Set(1f);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Maximize와 restore 전환의 bounds를 보간한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyBounds(float progress)
        {
            var from = ResolveAnimationStartBounds();
            var to = context.TargetBounds ?? targetProvider.GetTargetBounds(context.NextState, from);

            if
            (
             context.NextState != XeriWindowState.Maximized &&
             !(context.NextState == XeriWindowState.Normal && context.TargetBounds.HasValue)
            )
            {
                return;
            }

            target.style.left = Mathf.Lerp(from.x, to.x, progress);
            target.style.top = Mathf.Lerp(from.y, to.y, progress);
            target.style.right = StyleKeyword.Auto;
            target.style.bottom = StyleKeyword.Auto;
            target.style.width = Mathf.Lerp(from.width, to.width, progress);
            target.style.height = Mathf.Lerp(from.height, to.height, progress);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이전 완료 상태의 실제 UITK visual bounds를 animation 시작점으로 해석한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private Rect ResolveAnimationStartBounds()
        {
            if (context.PreviousState == XeriWindowState.Maximized)
            {
                return targetProvider.GetTargetBounds
                (
                    XeriWindowState.Maximized,
                    context.PreviousBounds
                );
            }

            return context.PreviousBounds;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Animation 진행률에 ease-out 보정을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private static float Smooth(float progress)
        {
            return 1f - Mathf.Pow(1f - progress, 3f);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Animation을 완료하고 visual 보조 값을 기본 상태로 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
        private void Complete()
        {
            if (scheduledItem != null)
            {
                scheduledItem.Pause();
                scheduledItem = null;
            }

            isRunning = false;
            ResetAnimationAlpha();
            RestoreContentInput();

            var complete = onComplete;
            context = default;
            onComplete = null;
            onError = null;

            complete?.Invoke();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Animation 실패 시 visual을 복구하고 실패 callback을 호출한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Fail(Exception exception)
        {
            var error = onError;

            Cancel(true);
            error?.Invoke(exception);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Animation용 임시 Alpha를 완료 상태 기본값으로 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
        private void ResetAnimationAlpha()
        {
            context.Driver?.Alpha.Set(1f);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 취소 시 이전 완료 상태의 class, bounds, visibility와 Alpha를 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void RestorePreviousVisual()
        {
            var driver = context.Driver;
            if (driver == null) return;

            driver.Alpha.Set(1f);
            driver.ApplyVisualState(context.PreviousState);

            if (context.PreviousState == XeriWindowState.Maximized)
            {
                driver.ApplyMaximizedBounds();
            }
            else
            {
                driver.ApplyBounds(context.PreviousBounds);
            }

            driver.Visibility.Set
            (
                context.PreviousState != XeriWindowState.Minimized &&
                context.PreviousState != XeriWindowState.Closed
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Animation 중 content 입력이 중간 상태에 개입하지 않도록 막는다.
        /// </summary>
        // ------------------------------------------------------------
        private void BlockContentInput()
        {
            if (panel == null) return;
            if (isContentInputBlocked) return;

            previousContentPickingMode = panel.ContentRoot.pickingMode;
            panel.ContentRoot.pickingMode = PickingMode.Ignore;
            isContentInputBlocked = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Animation 종료 경로에서 content 입력 상태를 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RestoreContentInput()
        {
            if (panel == null) return;
            if (!isContentInputBlocked) return;

            panel.ContentRoot.pickingMode = previousContentPickingMode;
            isContentInputBlocked = false;
        }

    #endregion

    }
}
