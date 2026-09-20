/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SceneFader.cs
수정일 : 2026-09-17
# 설명
App 기본 Layer의 Scene Fade Presentation을 Cover부터 Reveal 또는 종료까지 소유하는 상태 머신이다.
Fade lifecycle은 일반 Presentation acquisition과 PresentationAlpha를 사용해 Presentation 전용 Core 계약 없이 동작한다.

# 시간 정책
Scene 전환은 게임 시간 정지와 독립적이어야 하므로 항상 Unscaled 시간으로 재생한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// App 수명의 Scene Fade 상태와 Presentation를 소유한다.
    /// </summary>
    // ============================================================
    public sealed class SceneFader : IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Scene Fade 상태.
        /// </summary>
        // ------------------------------------------------------------
        public SceneFadeState State { get; private set; } = SceneFadeState.Clear;

        // ------------------------------------------------------------
        /// <summary>
        /// 마지막 비동기 Fade 실패와 정리 실패를 함께 보존한 예외.
        /// </summary>
        // ------------------------------------------------------------
        public Exception LastFailure { get; private set; }

        private readonly PresentationLayerRegistry layerRegistry = null;
        private readonly string layerID = "";
        private readonly IPresentationSource<ISceneFadeDriver> source = null;
        private readonly IPresentationTransitioner transitioner = null;

        private Lease<ISceneFadeDriver> presentationLease = null;
        private PresentationAlpha alpha = null;
        private bool presentationInitialized = false;
        private PresentationTransitionHandle transition = null;
        private SceneFadeState stableState = SceneFadeState.Clear;
        private int generation = 0;
        private bool isDisposed = false;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Scene Fade 의존성을 명시적으로 주입해 상태 머신을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public SceneFader
        (
            PresentationLayerRegistry layerRegistry,
            string layerID,
            IPresentationSource<ISceneFadeDriver> source,
            IPresentationTransitioner transitioner
        ) : base()
        {
            this.layerRegistry = layerRegistry ?? throw new ArgumentNullException(nameof(layerRegistry));

            if (string.IsNullOrWhiteSpace(layerID))
            {
                throw new ArgumentException("Scene Fade Layer ID가 비어 있습니다.", nameof(layerID));
            }

            this.layerID = layerID;
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.transitioner = transitioner ?? throw new ArgumentNullException(nameof(transitioner));
        }

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Fade Presentation를 불투명하게 전환하고 Covered 상태로 유지한다.
        /// <br/> 완료와 비동기 실패는 이 요청에 전달된 callback으로만 알린다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void Cover
        (
            SceneFadeParams parameters,
            Action onCompleted = null,
            Action<Exception> onFailed = null
        )
        {
            ThrowIfDisposed();
            ISceneFadeDriver driver = null;

            try
            {
                AcquirePresentation();
                driver = presentationLease.Value;

                // 새 요청은 기존 실행을 끝낸 뒤 색상과 상태를 함께 교체한다.
                CancelTransition();
                driver.SetColor(parameters.Color);
                var currentGeneration = ++generation;
                State = SceneFadeState.Covering;
                LastFailure = null;

                Play
                (
                    alpha.Base,
                    1.0f,
                    parameters.Duration,
                    currentGeneration,
                    () => CompleteCover(onCompleted),
                    onFailed
                );
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(exception, LastFailure))
                {
                    throw;
                }

                if (driver == null)
                {
                    LastFailure = exception;
                    throw;
                }

                throw RollbackToStable(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 보유한 Fade Presentation를 투명하게 전환한 뒤 반환한다.
        /// <br/> 완료와 비동기 실패는 이 요청에 전달된 callback으로만 알린다.
        /// </summary>
        // ------------------------------------------------------------
        public void Reveal
        (
            SceneFadeParams parameters,
            Action onCompleted = null,
            Action<Exception> onFailed = null
        )
        {
            ThrowIfDisposed();

            if (presentationLease == null || !presentationInitialized)
            {
                throw new InvalidOperationException("Reveal할 초기화된 Fade Presentation가 없습니다.");
            }

            var driver = presentationLease.Value;

            try
            {
                // Covering 또는 Covered 상태의 같은 Presentation를 재사용한다.
                CancelTransition();
                driver.SetColor(parameters.Color);
                var currentGeneration = ++generation;
                State = SceneFadeState.Revealing;
                LastFailure = null;

                Play
                (
                    alpha.Base,
                    0.0f,
                    parameters.Duration,
                    currentGeneration,
                    () => CompleteReveal(onCompleted),
                    onFailed
                );
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(exception, LastFailure))
                {
                    throw;
                }

                throw RollbackToStable(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Fade Presentation가 없으면 지정 Layer에서 한 번 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AcquirePresentation()
        {
            if (presentationLease != null && presentationInitialized) return;

            if (presentationLease == null)
            {
                presentationLease = PresentationLease.Acquire
                (
                    layerRegistry,
                    layerID,
                    source
                );
            }

            try
            {
                var driver = presentationLease.Value;
                alpha = driver.Alpha;

                if (alpha == null || !alpha.IsValid)
                {
                    throw new InvalidOperationException
                    (
                        "Scene Fade Presentation이 유효한 Alpha State를 제공하지 않습니다."
                    );
                }

                // 새 Fade 수명은 투명 상태에서 시작하고 이후 Transition이 Base Alpha를 소유한다.
                alpha.Set(0.0f);
                presentationInitialized = true;
            }
            catch (Exception exception)
            {
                try
                {
                    ReleasePresentation();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        "Scene Fade Presentation 초기화와 반환이 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Fade 요청의 Transition을 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Play
        (
            float startValue,
            float endValue,
            float duration,
            int currentGeneration,
            Action onCompleted,
            Action<Exception> onFailed
        )
        {
            alpha.Set(startValue);

            var parameters = new PresentationTransitionParams
            (
                alpha,
                startValue,
                endValue,
                duration,
                true
            );

            var playReturned = false;
            Exception synchronousFailure = null;

            void HandleFailure(Exception failure)
            {
                transition = null;

                if (!playReturned)
                {
                    synchronousFailure = RollbackToStable(failure);
                    return;
                }

                HandleAsyncFailure(failure, onFailed);
            }

            var handle = transitioner.Play
            (
                parameters,
                () =>
                {
                    if (currentGeneration != generation) return;

                    transition = null;

                    try
                    {
                        onCompleted();
                    }
                    catch (Exception exception)
                    {
                        HandleFailure(exception);
                    }
                },
                exception =>
                {
                    if (currentGeneration != generation) return;

                    HandleFailure(exception);
                }
            );
            playReturned = true;

            if (synchronousFailure != null)
            {
                throw synchronousFailure;
            }

            // 동기 완료 backend는 callback에서 이미 상태를 확정했으므로 Handle을 보관하지 않는다.
            if
            (
                currentGeneration == generation &&
                (State == SceneFadeState.Covering || State == SceneFadeState.Revealing) &&
                handle.IsPending
            )
            {
                transition = handle;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 진행 중 Transition callback을 무효화하고 backend 실행을 취소한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void CancelTransition()
        {
            generation++;

            var current = transition;
            transition = null;
            current?.Cancel();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Cover 상태를 확정한 뒤 현재 요청의 완료 callback을 호출한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CompleteCover(Action onCompleted)
        {
            stableState = SceneFadeState.Covered;
            State = SceneFadeState.Covered;
            LastFailure = null;

            // 확정된 Fade 상태를 소비자 callback 예외로 되돌리지 않는다.
            try
            {
                onCompleted?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Reveal 상태와 Presentation 반환을 확정한 뒤 현재 요청의 완료 callback을 호출한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void CompleteReveal(Action onCompleted)
        {
            stableState = SceneFadeState.Clear;
            State = SceneFadeState.Clear;
            ReleasePresentation();
            LastFailure = null;

            // Presentation 반환까지 끝난 요청을 소비자 callback 예외로 실패 처리하지 않는다.
            try
            {
                onCompleted?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 실패한 요청이 바꾼 Alpha와 Presentation 소유권을 마지막 완료 상태로 되돌리고,
        /// <br/> 최초 실패와 롤백 실패를 함께 보존한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private Exception RollbackToStable(Exception failure)
        {
            var errors = new List<Exception>
            {
                failure,
            };
            var stableAlpha = stableState == SceneFadeState.Covered ? 1.0f : 0.0f;

            // Presentation 반환이 이미 terminal 처리된 경우에는 마지막 적용값을 유지하고 backend를 다시 건드리지 않는다.
            if (alpha != null)
            {
                try
                {
                    alpha.Set(stableAlpha);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (stableState == SceneFadeState.Clear && transition == null)
            {
                try
                {
                    ReleasePresentation();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            State = stableState;
            LastFailure = errors.Count == 1
                ? failure
                : new AggregateException("Scene Fade 실패 롤백이 실패했습니다.", errors);
            return LastFailure;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 비동기 Fade 실패를 안정 상태로 복원하고 현재 요청의 실패 callback에 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleAsyncFailure
        (
            Exception failure,
            Action<Exception> onFailed
        )
        {
            var reportedFailure = RollbackToStable(failure);

            // 소비자 callback 예외가 원래 Fade 실패를 대체하지 않게 기록만 한다.
            try
            {
                onFailed?.Invoke(reportedFailure);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 보유한 Fade Presentation를 정확히 한 번 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReleasePresentation()
        {
            if (presentationLease == null) return;

            var current = presentationLease;
            presentationLease = null;
            alpha = null;
            presentationInitialized = false;
            current.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 해제된 SceneFader 사용을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(SceneFader));
            }
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// 진행 중 Fade를 취소하고 보유 Presentation를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            State = SceneFadeState.Clear;
            stableState = SceneFadeState.Clear;
            LastFailure = null;

            var errors = new List<Exception>();

            try
            {
                CancelTransition();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                ReleasePresentation();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Scene Fader 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
