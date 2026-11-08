/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SplashSession.cs
수정일 : 2026-11-08

# 설명
기존 Presentation Alpha Transition과 Unscaled Hold를 조합해
한 번의 Splash 실행, Skip/Cancel 및 UI 연결 수명을 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Splash 한 번의 실행 상태와 Presentation 수명을 소유한다.
    /// </summary>
    // ============================================================
    public sealed class SplashSession : IDisposable
    {

    #region 상태

        public SplashPhase Phase { get; private set; } = SplashPhase.Entering;
        public SplashResult? Result { get; private set; }
        public Exception LastFailure { get; private set; }
        public bool IsActive => Phase != SplashPhase.Finished;

        private readonly UIRuntime runtime;
        private readonly PresentationAlpha alpha;
        private readonly float visibleAlpha;
        private readonly SplashParams parameters;
        private readonly IDisposable restoration;
        private readonly IDisposable attachment;
        private readonly IDisposable layerLease;
        private readonly Action<SplashResult> onEnded;

        private FadeRequest activeRequest;
        private Coroutine hold;
        private bool skipped;
        private bool started;

    #endregion

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// 하나의 Splash 단계에서 실행하는 Transition의 소유권.
        /// </summary>
        // ============================================================
        private sealed class FadeRequest
        {
            internal PresentationTransitionHandle Handle;
        }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 조립된 View의 표현 상태와 세 가지 반환 수명을 소유한다.
        /// </summary>
        // ------------------------------------------------------------
        internal SplashSession
        (
            UIRuntime runtime,
            PresentationAlpha alpha,
            SplashParams parameters,
            IDisposable restoration,
            IDisposable attachment,
            IDisposable layerLease,
            Action<SplashResult> onEnded
        )
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.alpha = alpha ?? throw new ArgumentNullException(nameof(alpha));
            visibleAlpha = parameters.VisibleAlpha;
            this.parameters = parameters;
            this.restoration = restoration ?? throw new ArgumentNullException(nameof(restoration));
            this.attachment = attachment ?? throw new ArgumentNullException(nameof(attachment));
            this.layerLease = layerLease ?? throw new ArgumentNullException(nameof(layerLease));
            this.onEnded = onEnded;
        }

    #endregion

    #region 재생

        // ------------------------------------------------------------
        /// <summary>
        /// 전달된 View를 투명 상태에서 표시하기 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Start()
        {
            if (started)
            {
                throw new InvalidOperationException("Splash가 이미 시작되었습니다.");
            }

            started = true;

            try
            {
                alpha.Base = 0f;

                if (Phase == SplashPhase.Entering && !skipped)
                {
                    PlayAlpha(visibleAlpha, parameters.FadeInDuration, SplashPhase.Entering, BeginHold);
                }
            }
            catch (Exception exception)
            {
                Finish(SplashResult.Failed, true, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 진입이 끝나면 정해진 유지 시간을 기다린다.
        /// </summary>
        // ------------------------------------------------------------
        private void BeginHold()
        {
            if (!IsActive)
            {
                return;
            }

            Phase = SplashPhase.Holding;

            if (parameters.HoldDuration <= 0f)
            {
                BeginExit();
                return;
            }

            hold = runtime.StartCoroutine(WaitHold());
        }

        private IEnumerator WaitHold()
        {
            yield return new WaitForSecondsRealtime(parameters.HoldDuration);
            hold = null;

            if (Phase == SplashPhase.Holding)
            {
                BeginExit();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Alpha에서 퇴장시키고 끝나면 결과를 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BeginExit()
        {
            StopHold();
            PlayAlpha
            (
                0f,
                parameters.FadeOutDuration,
                SplashPhase.Exiting,
                () => Finish(skipped ? SplashResult.Skipped : SplashResult.Completed, true)
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이미 실행 중인 Transition을 무효화한 뒤 새 Alpha 전환을 실행한다.
        /// </summary>
        // ------------------------------------------------------------
        private void PlayAlpha
        (
            float target,
            float duration,
            SplashPhase phase,
            Action onCompleted
        )
        {
            if (!IsActive) return;

            // 새로운 단계가 먼저 Transition 소유권을 획득한다.
            var previous = activeRequest;
            var request = new FadeRequest();
            activeRequest = request;

            try
            {
                previous?.Handle?.Cancel();

                if (!ReferenceEquals(activeRequest, request) || !IsActive)
                {
                    return;
                }

                Phase = phase;

                void HandleFailure(Exception failure)
                {
                    if (!ReferenceEquals(activeRequest, request) || !IsActive)
                    {
                        return;
                    }

                    // 완료 시점과 관계없이 실행 실패는 세션이 종결한다.
                    Finish(SplashResult.Failed, true, failure);
                }

                void HandleCompletion()
                {
                    if
                    (
                        !ReferenceEquals(activeRequest, request) ||
                        !IsActive
                    )
                    {
                        return;
                    }

                    try
                    {
                        onCompleted();
                    }
                    catch (Exception exception)
                    {
                        HandleFailure(exception);
                        return;
                    }

                    if (ReferenceEquals(activeRequest, request))
                    {
                        activeRequest = null;
                    }
                }

                var handle = runtime.PlayPresentationTransition
                (
                    new PresentationTransitionParams
                    (
                        alpha,
                        alpha.Base,
                        target,
                        duration,
                        usesUnscaledTime: true
                    ),
                    HandleCompletion,
                    HandleFailure
                );

                if (!ReferenceEquals(activeRequest, request) || !IsActive)
                {
                    if (handle.IsPending)
                    {
                        handle.Cancel();
                    }

                    return;
                }

                if (handle.IsCancelled)
                {
                    throw new OperationCanceledException("Splash Transition이 시작 중 취소됐습니다.");
                }

                if (handle.IsFailed)
                {
                    throw new InvalidOperationException("Splash Transition이 실패 원인 없이 종료됐습니다.");
                }

                if (handle.IsPending)
                {
                    request.Handle = handle;
                    handle.OnCancelled += () =>
                    {
                        if (!ReferenceEquals(activeRequest, request) || !IsActive)
                        {
                            return;
                        }

                        Finish
                        (
                            SplashResult.Failed,
                            true,
                            new OperationCanceledException("Splash Transition이 외부에서 취소됐습니다.")
                        );
                    };
                }
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(activeRequest, request) && IsActive)
                {
                    Finish(SplashResult.Failed, true, exception);
                }
            }
        }

    #endregion

    #region 외부 제어

        // ------------------------------------------------------------
        /// <summary>
        /// 남은 진입 또는 유지 단계를 생략하고 퇴장을 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Skip()
        {
            if (!IsActive || skipped)
            {
                return;
            }

            if (Phase == SplashPhase.Exiting)
            {
                return;
            }

            skipped = true;
            BeginExit();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 애니메이션 없이 취소하고 모든 표시 수명을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Cancel()
        {
            Finish(SplashResult.Canceled, true);
        }

    #endregion

    #region 종료

        private void StopTransition()
        {
            var current = activeRequest;
            activeRequest = null;
            current?.Handle?.Cancel();
        }

        private void StopHold()
        {
            var current = hold;
            hold = null;

            if (current != null)
            {
                runtime.StopCoroutine(current);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 수명을 한 번만 반환하고 외부에 최종 결과를 알린다.
        /// </summary>
        // ------------------------------------------------------------
        private void Finish
        (
            SplashResult result,
            bool notify,
            Exception failure = null
        )
        {
            if (!IsActive)
            {
                return;
            }

            Phase = SplashPhase.Finished;
            Result = result;
            LastFailure = failure;
            var errors = new ExceptionBuffer();

            Release(() => StopHold(), ref errors);
            Release(() => StopTransition(), ref errors);
            Release(() => attachment.Dispose(), ref errors);
            Release(() => restoration.Dispose(), ref errors);
            Release(() => layerLease.Dispose(), ref errors);

            if (errors.Count > 0)
            {
                var cleanup = errors.CreateAggregateException("Splash 표시 리소스 반환 중 오류가 발생했습니다.");
                LastFailure = failure == null ? cleanup : new AggregateException(failure, cleanup);
                Result = SplashResult.Failed;
            }

            if (notify)
            {
                try
                {
                    onEnded?.Invoke(Result.Value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            if (LastFailure != null)
            {
                Debug.LogException(LastFailure);
            }
        }

        private static void Release(Action action, ref ExceptionBuffer errors)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 소유자의 종료에서는 별도 완료 콜백 없이 수명을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            Finish(SplashResult.Canceled, false);
        }

    #endregion

    }
}
