/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SceneFader.cs
수정일 : 2026-11-08
# 설명
PresentationHost의 System destination Layer Lease를 Cover부터 Reveal 또는 종료까지 소유하는 상태 머신이다.
Fade Source lifetime과 PresentationAlpha를 결합해 Presentation 수명 Core 위에 구성한다.

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

        private readonly PresentationHost presentationHost = null;
        private readonly IPresentationSource<ISceneFadeDriver> source = null;
        private readonly IPresentationTransitioner transitioner = null;

        private PresentationLayerLease presentationLayerLease = null;
        private ISceneFadeDriver presentationDriver = null;
        private PresentationAlpha alpha = null;
        private bool isAcquiringPresentation = false;
        private FadeRequest activeRequest = null;
        private SceneFadeState stableState = SceneFadeState.Clear;
        private Color stableColor = Color.black;
        private bool ownerControlsLifetime = false;
        private bool isDisposed = false;

    #endregion

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// 하나의 Fade 요청과 그 backend Transition 소유권을 연결한다.
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
        /// Scene Fade 의존성을 명시적으로 주입해 상태 머신을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public SceneFader
        (
            PresentationHost presentationHost,
            IPresentationSource<ISceneFadeDriver> source,
            IPresentationTransitioner transitioner
        ) : base()
        {
            this.presentationHost = presentationHost ??
                throw new ArgumentNullException(nameof(presentationHost));
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
            FadeRequest request = null;

            try
            {
                AcquirePresentation();
                driver = presentationDriver;

                // 새 요청은 기존 실행을 끝낸 뒤 색상과 상태를 함께 교체한다.
                request = new FadeRequest();
                if (!ReplaceRequest(request)) return;

                driver.SetColor(parameters.Color);
                if (isDisposed || !ReferenceEquals(activeRequest, request)) return;

                State = SceneFadeState.Covering;
                LastFailure = null;

                Play
                (
                    alpha.Base,
                    1.0f,
                    parameters.Duration,
                    request,
                    () => CompleteCover(parameters.Color, onCompleted),
                    onFailed
                );
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(exception, LastFailure) || isDisposed)
                {
                    throw;
                }

                if (request != null && !ReferenceEquals(activeRequest, request))
                {
                    throw;
                }

                if (driver == null)
                {
                    LastFailure = exception;
                    throw;
                }

                throw RollbackToStable(exception, request);
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

            if (presentationLayerLease == null || presentationDriver == null || alpha == null)
            {
                throw new InvalidOperationException("Reveal할 초기화된 Fade Presentation가 없습니다.");
            }

            var driver = presentationDriver;
            FadeRequest request = null;

            try
            {
                // Covering 또는 Covered 상태의 같은 Presentation를 재사용한다.
                request = new FadeRequest();
                if (!ReplaceRequest(request)) return;

                driver.SetColor(parameters.Color);
                if (isDisposed || !ReferenceEquals(activeRequest, request)) return;

                State = SceneFadeState.Revealing;
                LastFailure = null;

                Play
                (
                    alpha.Base,
                    0.0f,
                    parameters.Duration,
                    request,
                    () => CompleteReveal(onCompleted),
                    onFailed
                );
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(exception, LastFailure) || isDisposed)
                {
                    throw;
                }

                if (request != null && !ReferenceEquals(activeRequest, request))
                {
                    throw;
                }

                throw RollbackToStable(exception, request);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Fade Presentation가 없으면 지정 Layer에서 한 번 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AcquirePresentation()
        {
            if
            (
                presentationLayerLease != null &&
                presentationDriver != null &&
                alpha != null
            )
            {
                return;
            }

            if (isAcquiringPresentation)
            {
                throw new InvalidOperationException("Scene Fade Presentation을 획득하는 중 재진입할 수 없습니다.");
            }

            // 외부 callback이 종료를 재진입하더라도 획득 중인 리소스의 소유권을 유지한다.
            isAcquiringPresentation = true;
            PresentationLayerLease acquiredLayer = null;
            ISceneFadeDriver acquiredDriver = null;

            try
            {
                acquiredLayer = presentationHost.AcquireLayer(PresentationDestinationID.System);
                ThrowIfDisposed();

                acquiredDriver = source.Acquire(acquiredLayer.Layer);
                if (acquiredDriver == null)
                {
                    throw new InvalidOperationException("Scene Fade Source가 null Driver를 반환했습니다.");
                }

                ThrowIfDisposed();

                var acquiredAlpha = acquiredDriver.Alpha;
                if (acquiredAlpha == null || !acquiredAlpha.IsValid)
                {
                    throw new InvalidOperationException
                    (
                        "Scene Fade Presentation이 유효한 Alpha State를 제공하지 않습니다."
                    );
                }

                acquiredAlpha.Set(0.0f);
                ThrowIfDisposed();

                // 완전히 초기화된 경우에만 현재 SceneFader 소유로 이전한다.
                presentationLayerLease = acquiredLayer;
                presentationDriver = acquiredDriver;
                alpha = acquiredAlpha;
                acquiredLayer = null;
                acquiredDriver = null;
            }
            catch (Exception exception)
            {
                var errors = new ExceptionBuffer(exception);

                if (acquiredDriver != null)
                {
                    try
                    {
                        source.Release(acquiredDriver);
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }
                }

                if (acquiredLayer != null)
                {
                    try
                    {
                        acquiredLayer.Dispose();
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }
                }

                if (errors.Count == 1)
                {
                    throw;
                }

                throw errors.CreateAggregateException
                (
                    "Scene Fade Presentation 초기화와 반환이 실패했습니다."
                );
            }
            finally
            {
                isAcquiringPresentation = false;
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
            FadeRequest request,
            Action onCompleted,
            Action<Exception> onFailed
        )
        {
            alpha.Set(startValue);
            if (isDisposed || !ReferenceEquals(activeRequest, request)) return;

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
                if (!ReferenceEquals(activeRequest, request)) return;

                // 실패한 요청의 복구가 끝나기 전에는 새 요청의 상태를 덮어쓰지 않는다.
                if (!playReturned)
                {
                    synchronousFailure = RollbackToStable(failure, request);
                    return;
                }

                HandleAsyncFailure(failure, request, onFailed);
            }

            void HandleCompletion()
            {
                if (!ReferenceEquals(activeRequest, request)) return;

                try
                {
                    onCompleted();
                }
                catch (Exception exception)
                {
                    if (!ReferenceEquals(activeRequest, request))
                    {
                        // 완료 처리 중 소유권이 이전됐다면 새 요청을 복구하지 않고
                        // 이전 요청의 반환 실패만 원래 호출자에게 전달한다.
                        if (!playReturned)
                        {
                            synchronousFailure = exception;
                        }
                        else if (onFailed != null)
                        {
                            try
                            {
                                onFailed(exception);
                            }
                            catch (Exception callbackException)
                            {
                                Debug.LogException(callbackException);
                            }
                        }
                        else
                        {
                            Debug.LogException(exception);
                        }

                        return;
                    }

                    HandleFailure(exception);
                    return;
                }

                if (ReferenceEquals(activeRequest, request))
                {
                    activeRequest = null;
                }
            }

            var handle = transitioner.Play
            (
                parameters,
                HandleCompletion,
                HandleFailure
            );
            playReturned = true;

            if (synchronousFailure != null)
            {
                throw synchronousFailure;
            }

            // Play 반환 전에 교체된 요청의 backend 실행은 현재 소유자가 아니다.
            if (!ReferenceEquals(activeRequest, request))
            {
                if (handle.IsPending)
                {
                    handle.Cancel();
                }

                return;
            }

            if (handle.IsCancelled)
            {
                throw RollbackToStable
                (
                    new OperationCanceledException("Scene Fade Transition이 취소됐습니다."),
                    request
                );
            }

            if (handle.IsFailed)
            {
                throw RollbackToStable
                (
                    new InvalidOperationException("Scene Fade Transition이 실패 원인 없이 종료됐습니다."),
                    request
                );
            }

            if (handle.IsPending)
            {
                request.Handle = handle;
                handle.OnCancelled += () =>
                {
                    if (!ReferenceEquals(activeRequest, request)) return;

                    HandleAsyncFailure
                    (
                        new OperationCanceledException("Scene Fade Transition이 외부에서 취소됐습니다."),
                        request,
                        onFailed
                    );
                };
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 새 요청을 먼저 소유한 후 이전 backend 실행을 취소한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool ReplaceRequest(FadeRequest next)
        {
            var previous = activeRequest;
            activeRequest = next;
            previous?.Handle?.Cancel();

            // 취소 콜백의 재진입으로 대체됐다면 새 요청을 다시 시작하지 않는다.
            return ReferenceEquals(activeRequest, next);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Fade 요청을 종료한 뒤 backend 실행을 취소한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CancelTransition()
        {
            var previous = activeRequest;
            activeRequest = null;
            previous?.Handle?.Cancel();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Cover 상태를 확정한 뒤 현재 요청의 완료 callback을 호출한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CompleteCover
        (
            Color color,
            Action onCompleted
        )
        {
            stableColor = color;
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
            LastFailure = null;

            // 반환 중 발생하는 재진입이 새 요청의 상태를 변경할 수 있으므로 먼저 확정한다.
            ReleasePresentation();

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
        private Exception RollbackToStable(Exception failure, FadeRequest request)
        {
            var errors = new ExceptionBuffer(failure);
            var stableAlpha = stableState == SceneFadeState.Covered ? 1.0f : 0.0f;

            // 반환·복원 중 콜백이 재진입할 수 있으므로 논리 상태를 먼저 확정한다.
            State = stableState;

            // Covered 안정 상태는 요청이 바꾼 색상도 마지막 성공 상태로 함께 복원한다.
            if (stableState == SceneFadeState.Covered && presentationDriver != null)
            {
                try
                {
                    presentationDriver.SetColor(stableColor);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            // 각 backend 호출이 재진입할 수 있으므로 실패한 요청이 여전히 소유자일 때만 복원한다.
            if (!isDisposed && ReferenceEquals(activeRequest, request) && alpha != null)
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

            if (!isDisposed && ReferenceEquals(activeRequest, request) && stableState == SceneFadeState.Clear)
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

            var reportedFailure = errors.Count == 1
                ? failure
                : errors.CreateAggregateException("Scene Fade 실패 롤백이 실패했습니다.");

            if (ReferenceEquals(activeRequest, request))
            {
                activeRequest = null;
                if (!isDisposed)
                {
                    LastFailure = reportedFailure;
                }
            }

            return reportedFailure;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 비동기 Fade 실패를 안정 상태로 복원하고 현재 요청의 실패 callback에 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleAsyncFailure
        (
            Exception failure,
            FadeRequest request,
            Action<Exception> onFailed
        )
        {
            var reportedFailure = RollbackToStable(failure, request);

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
            if (presentationLayerLease == null && presentationDriver == null) return;

            var currentLayerLease = presentationLayerLease;
            var currentDriver = presentationDriver;
            presentationLayerLease = null;
            presentationDriver = null;
            alpha = null;
            var errors = new ExceptionBuffer();

            if (currentDriver != null)
            {
                try
                {
                    source.Release(currentDriver);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (currentLayerLease != null)
            {
                try
                {
                    currentLayerLease.Dispose();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            if (errors.Count > 1)
            {
                throw errors.CreateAggregateException
                (
                    "Scene Fade Presentation 반환이 실패했습니다."
                );
            }
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

    #region 수명 해제

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 진행 중 Fade를 취소하고 보유 Presentation를 반환한다.
        /// <br/> owner-controlled Scene Fader는 직접 해제할 수 없다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            if (ownerControlsLifetime)
            {
                throw new InvalidOperationException
                (
                    "이 Scene Fader의 수명은 소유자가 관리합니다."
                );
            }

            DisposeCore();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이후 Scene Fader 수명을 조립 owner만 종료하도록 고정한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SetOwnerControlledLifetime()
        {
            ThrowIfDisposed();
            ownerControlsLifetime = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// owner-controlled Scene Fader를 소유자 종료 경로에서 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void DisposeFromOwner()
        {
            DisposeCore();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Fade 상태를 초기화하고 Transition과 Presentation 소유권을 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void DisposeCore()
        {
            if (isDisposed) return;

            isDisposed = true;
            State = SceneFadeState.Clear;
            stableState = SceneFadeState.Clear;
            LastFailure = null;

            var errors = new ExceptionBuffer();

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
                throw errors.CreateAggregateException("Scene Fader 해제가 실패했습니다.");
            }
        }

    #endregion

    }
}
