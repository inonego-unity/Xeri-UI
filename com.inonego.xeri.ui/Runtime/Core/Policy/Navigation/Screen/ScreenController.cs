/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenController.cs
수정일 : 2026-09-17
# 설명
Screen Open·Close·Replace·Clear 명령과 Stack, 상태 훅, Transition과 대칭 수명을 중재한다.
Screen lifecycle Alpha는 PresentationAlpha Base로 전환해 외부 Modifier와 독립적으로 합성한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Screen 명령, Stack과 실행 Session 수명을 소유하는 Controller.
    /// </summary>
    // ============================================================
    public sealed class ScreenController
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Stack top Screen.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenSession Top
        {
            get
            {
                return stack.Count > 0 ? stack[stack.Count - 1] : null;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Screen Stack 항목 수.
        /// </summary>
        // ------------------------------------------------------------
        public int Count => stack.Count;

        // ------------------------------------------------------------
        /// <summary>
        /// 공개 Screen 명령을 받을 수 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsAvailable => isActive && !isReleasing && !isReleased;

        private readonly ScreenRegistry screenRegistry = null;
        private readonly PresentationLayerRegistry layerRegistry = null;
        private readonly IPresentationTransitioner transitioner = null;
        private readonly FocusController focusController = null;
        private readonly IScreenInputDriver inputDriver = null;

        private readonly List<ScreenSession> stack = new List<ScreenSession>();
        private readonly List<ScreenSession> liveSessions = new List<ScreenSession>();

        private bool isActive = false;
        private bool isReleasing = false;
        private bool isReleased = false;
        private bool isOpening = false;
        private int hookDepth = 0;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 실행에 필요한 Registry와 backend를 명시적으로 주입한다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenController
        (
            ScreenRegistry screenRegistry,
            PresentationLayerRegistry layerRegistry,
            IPresentationTransitioner transitioner,
            FocusController focusController,
            IScreenInputDriver inputDriver
        ) : base()
        {
            this.screenRegistry = screenRegistry ?? throw new ArgumentNullException(nameof(screenRegistry));
            this.layerRegistry = layerRegistry ?? throw new ArgumentNullException(nameof(layerRegistry));
            this.transitioner = transitioner ?? throw new ArgumentNullException(nameof(transitioner));
            this.focusController = focusController ?? throw new ArgumentNullException(nameof(focusController));
            this.inputDriver = inputDriver ?? throw new ArgumentNullException(nameof(inputDriver));
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 조립이 끝난 뒤 공개 Screen 명령을 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Activate()
        {
            if (isReleased)
            {
                throw new ObjectDisposedException(nameof(ScreenController));
            }

            if (isActive) return;

            isActive = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 새 Screen을 Stack top에 연다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenOpenResponse Open
        (
            string id,
            ScreenOpenParams parameters = default
        )
        {
            return OpenInternal(id, parameters, false);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Active top을 새 Screen으로 교체한다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenOpenResponse Replace
        (
            string id,
            ScreenOpenParams parameters = default
        )
        {
            return OpenInternal(id, parameters, true);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Stack top Screen 하나를 취소 가능한 경로로 닫는다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Close()
        {
            return Close(Top);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Session이 현재 Stack top일 때 취소 가능한 닫기를 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool Close(ScreenSession session)
        {
            if (hookDepth > 0) return false;
            if (isOpening) return false;
            if (!IsAvailable || session == null || !ReferenceEquals(Top, session)) return false;

            if (session.State != ScreenState.Opening && session.State != ScreenState.Active)
            {
                return false;
            }

            return BeginClose(session, true, true, true, false, true);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 생존 Screen을 최신 획득부터 애니메이션 없이 강제 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Clear()
        {
            if (hookDepth > 0)
            {
                throw new InvalidOperationException("Screen 상태 훅 안에서는 Clear를 호출할 수 없습니다.");
            }

            if (isOpening)
            {
                throw new InvalidOperationException("Screen Open 계열 명령이 진행 중일 때는 Clear를 호출할 수 없습니다.");
            }

            if (!IsAvailable || liveSessions.Count == 0) return;

            // 전체 정리 중 새 Screen 명령이 Live 목록을 변경하지 않도록 Controller를 일시적으로 닫는다.
            isReleasing = true;
            List<Exception> errors;

            try
            {
                errors = ForceClear();
            }
            finally
            {
                isReleasing = false;
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Screen Clear 중 하나 이상의 정리가 실패했습니다.", errors);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 공개 명령을 중지하고 모든 Screen Source 반환을 강제 완료한다.
        /// </summary>
        // ------------------------------------------------------------
        internal List<Exception> Shutdown()
        {
            var errors = new List<Exception>();

            if (isReleased || isReleasing) return errors;

            isReleasing = true;
            errors.AddRange(ForceClear());

            isActive = false;
            isReleasing = false;
            isReleased = true;
            return errors;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Screen 사전 조건을 검증하고 Source·Layer·입력 수명을 준비한 뒤,
        /// <br/> Stack에 공개하고 열기 Transition을 시작한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private ScreenOpenResponse OpenInternal
        (
            string id,
            ScreenOpenParams parameters,
            bool replace
        )
        {
            if (hookDepth > 0)
            {
                return ScreenOpenResponse.Reject("Screen 상태 훅 안에서는 Open 계열 명령을 호출할 수 없습니다.");
            }

            if (isOpening)
            {
                return ScreenOpenResponse.Reject("다른 Screen Open 계열 명령이 진행 중입니다.");
            }

            if (!IsAvailable)
            {
                return ScreenOpenResponse.Reject("UI Runtime이 Screen 명령을 받을 수 없는 상태입니다.");
            }

            isOpening = true;

            try
            {
                return OpenCore(id, parameters, replace);
            }
            finally
            {
                isOpening = false;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 검증된 단일 Open 명령의 Source·Layer·입력 수명을 준비한 뒤,
        /// <br/> Stack에 공개하고 열기 Transition을 시작한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private ScreenOpenResponse OpenCore
        (
            string id,
            ScreenOpenParams parameters,
            bool replace
        )
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return ScreenOpenResponse.Reject("Screen ID가 비어 있습니다.");
            }

            var previous = Top;

            if (replace && (previous == null || previous.State != ScreenState.Active))
            {
                return ScreenOpenResponse.Reject("Replace는 Active top Screen에서만 시작할 수 있습니다.");
            }

            if (!replace && previous != null && previous.State != ScreenState.Active)
            {
                return ScreenOpenResponse.Reject("현재 top Screen이 전환 중입니다.");
            }

            if (!screenRegistry.TryGet(id, out var registration))
            {
                return ScreenOpenResponse.Reject($"Screen '{id}'가 등록되어 있지 않습니다.");
            }

            if (registration.Options.DuplicatePolicy == ScreenDuplicatePolicy.Reject && HasLiveScreen(id))
            {
                return ScreenOpenResponse.Reject($"Screen '{id}'의 중복 Open이 거부됐습니다.");
            }

            if
            (
                !layerRegistry.TryAcquireUsage
                (
                    registration.Options.LayerID,
                    out var layerDriver,
                    out var layerUsage
                )
            )
            {
                return ScreenOpenResponse.Reject
                (
                    $"Screen '{id}'의 Layer '{registration.Options.LayerID}'가 등록되어 있지 않습니다."
                );
            }

            var session = new ScreenSession
            (
                this,
                registration.Options,
                parameters,
                registration.Source,
                layerUsage
            )
            {
                ReplacedSession = replace ? previous : null,
            };

            liveSessions.Add(session);

            try
            {
                // 외부 Source와 훅이 EventSystem 선택을 바꾸기 전에 이전 화면의 실제 선택을 기록한다.
                if (previous != null)
                {
                    focusController.CaptureCurrent(previous);

                    if (IsOpenInterrupted(session))
                    {
                        return ScreenOpenResponse.Reject
                        (
                            $"Screen '{id}' Focus 기록 중 Runtime이 종료됐습니다."
                        );
                    }
                }

                var scope = new ScreenViewScope
                (
                    id,
                    parameters,
                    session,
                    registration.Options.LayerID,
                    layerDriver
                );

                if (!session.Resources.TryAcquireSource(scope))
                {
                    return ScreenOpenResponse.Reject
                    (
                        $"Screen '{id}' Source 획득 중 Runtime이 종료됐습니다."
                    );
                }
            }
            catch (Exception exception)
            {
                if (IsOpenInterrupted(session))
                {
                    return ScreenOpenResponse.SourceFailure
                    (
                        $"Screen '{id}' Source 획득 중 Runtime 종료가 실패했습니다.",
                        exception
                    );
                }

                var failure = CleanupUnacceptedFailure
                (
                    session,
                    previous,
                    exception
                );

                return ScreenOpenResponse.SourceFailure
                (
                    $"Screen '{id}' Source 획득·Bind가 실패했습니다.",
                    failure
                );
            }

            try
            {
                if (session.Resources.Instance == null)
                {
                    throw new InvalidOperationException("Screen Source가 null ScreenInstance를 반환했습니다.");
                }

                if (!session.Resources.Instance.Driver.IsValid)
                {
                    throw new InvalidOperationException("Screen Driver가 유효하지 않습니다.");
                }
            }
            catch (Exception exception)
            {
                var failure = CleanupUnacceptedFailure
                (
                    session,
                    previous,
                    exception
                );

                return ScreenOpenResponse.SourceFailure
                (
                    $"Screen '{id}' Source 획득·Bind가 실패했습니다.",
                    failure
                );
            }

            var driver = session.Resources.Instance.Driver;

            try
            {
                if (driver.Alpha == null || driver.Visibility == null)
                {
                    throw new InvalidOperationException
                    (
                        $"Screen '{id}' Driver가 필수 Presentation State를 제공하지 않습니다."
                    );
                }

                // Source가 만든 Presentation을 먼저 표시한 뒤 Navigation 정책의 초기 Alpha를 적용한다.
                driver.Visibility.Set(true);

                if (IsOpenInterrupted(session))
                {
                    return ScreenOpenResponse.Reject
                    (
                        $"Screen '{id}' Driver 표시 중 Runtime이 종료됐습니다."
                    );
                }

                driver.SetInteractable(false);
                session.Alpha = driver.Alpha;
                session.Alpha.Set(0.0f);
            }
            catch (Exception exception)
            {
                if (IsOpenInterrupted(session))
                {
                    return ScreenOpenResponse.SourceFailure
                    (
                        $"Screen '{id}' Driver 준비 중 Runtime이 종료됐습니다.",
                        exception
                    );
                }

                var failure = CleanupUnacceptedFailure(session, previous, exception);
                return ScreenOpenResponse.SourceFailure
                (
                    $"Screen '{id}' Driver 준비가 실패했습니다.",
                    failure
                );
            }

            // OnOpening만 예외를 호출자에게 다시 전달하고 준비 Session을 정리한다.
            ScreenStateContext openingContext;

            try
            {
                openingContext = InvokeOpening(session);

                if (IsOpenInterrupted(session))
                {
                    return ScreenOpenResponse.Reject
                    (
                        $"Screen '{id}' OnOpening 중 Runtime이 종료됐습니다."
                    );
                }
            }
            catch (Exception exception)
            {
                if (IsOpenInterrupted(session))
                {
                    throw;
                }

                try
                {
                    ReleaseUnacceptedAndRestorePrevious(session, previous);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(exception, cleanupException);
                }

                throw;
            }

            if (openingContext.IsCancelled)
            {
                ReleaseUnacceptedAndRestorePrevious(session, previous);
                return ScreenOpenResponse.Cancel($"Screen '{id}'의 OnOpening에서 취소됐습니다.");
            }

            try
            {
                if (!session.Resources.TryAcquireInput(inputDriver, session.Options))
                {
                    return ScreenOpenResponse.Reject
                    (
                        $"Screen '{id}' 입력 획득 중 Runtime이 종료됐습니다."
                    );
                }
            }
            catch (Exception exception)
            {
                if (IsOpenInterrupted(session))
                {
                    throw;
                }

                throw CleanupUnacceptedFailure(session, previous, exception);
            }

            try
            {
                // 동기 완료 callback도 이전 화면이 이미 Covered인 상태만 관찰하게 먼저 Stack을 갱신한다.
                if (previous != null)
                {
                    previous.State = ScreenState.Covered;
                    previous.Resources.Instance.Driver.SetInteractable(false);
                }

                stack.Add(session);
                session.IsAccepted = true;

                // Stack이 소유권을 수락한 뒤에는 동기 완료 callback도 일반 공개 명령을 사용할 수 있다.
                isOpening = false;
                StartOpening(session);
            }
            catch (Exception exception)
            {
                Exception failure = exception;

                try
                {
                    RollbackOpen(session, previous);
                }
                catch (Exception cleanupException)
                {
                    failure = new AggregateException(exception, cleanupException);
                }

                return ScreenOpenResponse.TransitionFailure
                (
                    $"Screen '{id}' 열기 Transition 시작이 실패했습니다.",
                    failure
                );
            }

            return ScreenOpenResponse.Accept(session);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 외부 콜백 중 현재 Open 작업의 준비 Session 소유권이 종료됐는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private bool IsOpenInterrupted(ScreenSession session)
        {
            return !IsAvailable ||
                session.State == ScreenState.Closed ||
                !liveSessions.Contains(session);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// ID와 같은 미종결 Screen이 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool HasLiveScreen(string id)
        {
            for (var i = 0; i < liveSessions.Count; i++)
            {
                if
                (
                    liveSessions[i].State != ScreenState.Closed &&
                    string.Equals(liveSessions[i].ID, id, StringComparison.Ordinal)
                )
                {
                    return true;
                }
            }

            return false;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Screen 열기 Transition을 시작하고 유효한 callback만 Session에 반영한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void StartOpening(ScreenSession session)
        {
            InvalidateTransition(session);
            var generation = ++session.TransitionGeneration;
            var parameters = new PresentationTransitionParams
            (
                session.Alpha,
                0.0f,
                1.0f,
                session.Options.OpenDuration,
                session.Options.UsesUnscaledTime
            );

            var handle = transitioner.Play
            (
                parameters,
                () => CompleteOpening(session, generation),
                exception =>
                {
                    if (!IsExpected(session, generation, ScreenState.Opening)) return;

                    Debug.LogException(exception);

                    try
                    {
                        session.Alpha.Set(1.0f);
                    }
                    catch (Exception applyException)
                    {
                        Debug.LogException(applyException);
                    }

                    CompleteOpening(session, generation);
                }
            );

            if (IsExpected(session, generation, ScreenState.Opening) && handle.IsPending)
            {
                session.Transition = handle;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 열기 Transition 완료와 OnOpened를 확정하고 Replace 이전 Screen을 닫는다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void CompleteOpening
        (
            ScreenSession session,
            int generation
        )
        {
            if (!IsExpected(session, generation, ScreenState.Opening)) return;

            session.Transition = null;

            try
            {
                session.Alpha.Set(1.0f);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            try
            {
                session.Resources.Instance.Driver.SetInteractable(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            session.State = ScreenState.Active;

            try
            {
                focusController.Activate
                (
                    session,
                    session.Options.DefaultFocus,
                    session.Resources.Instance.Driver.DefaultFocus
                );
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            // Focus callback에서 새 Screen이 덮었으면 Replace 이전 Session을 확정하고 현재 top Focus를 다시 적용한다.
            if (!IsExpected(session, generation, ScreenState.Active))
            {
                if (session.State == ScreenState.Covered)
                {
                    FinishReplace(session);
                }

                var activeTop = Top;

                if (activeTop != null && activeTop.State == ScreenState.Active)
                {
                    RestorePrevious(activeTop);
                }

                return;
            }

            InvokeNonCancellingHook(session, ScreenState.Active, HookKind.Opened);

            FinishReplace(session);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Replace로 대체된 이전 Session과 아직 확정되지 않은 교체 대상을 순서대로 닫는다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void FinishReplace(ScreenSession session)
        {
            var replaced = session.ReplacedSession;
            session.ReplacedSession = null;

            while (replaced != null)
            {
                var next = replaced.ReplacedSession;
                replaced.ReplacedSession = null;

                if (replaced.State == ScreenState.Covered)
                {
                    BeginClose(replaced, false, true, false, true, false);
                }

                replaced = next;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 닫기 훅과 Transition을 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool BeginClose
        (
            ScreenSession session,
            bool canCancel,
            bool animate,
            bool restorePrevious,
            bool detachFromStack,
            bool retainCursorWhileAwaitingRelease
        )
        {
            if (session == null || session.State == ScreenState.Closed) return false;
            if (session.State == ScreenState.Closing) return false;

            var stateBeforeClosing = session.State;
            ScreenStateContext closingContext;

            // 취소 판단 전 상태는 유지하되 강제 종료 재진입이 같은 Closing 훅을 반복하지 않게 표시한다.
            session.IsClosingHookRunning = true;

            try
            {
                closingContext = InvokeClosing(session, canCancel);
            }
            finally
            {
                session.IsClosingHookRunning = false;
            }

            // OnClosing에서 Runtime 종료가 Screen을 이미 정리했으면 바깥 닫기 흐름을 재개하지 않는다.
            if (session.State == ScreenState.Closed || !liveSessions.Contains(session))
            {
                return true;
            }

            if (!IsAvailable || session.State != stateBeforeClosing)
            {
                return false;
            }

            if (canCancel && closingContext.IsCancelled)
            {
                return false;
            }

            session.State = ScreenState.Closing;
            session.RestorePreviousOnClose = restorePrevious;
            session.RetainCursorOnClose = retainCursorWhileAwaitingRelease;
            InvalidateTransition(session);

            // Replace 이전 Screen은 새 top을 건드리지 않도록 닫기 시작 시 Stack에서 분리한다.
            if (detachFromStack)
            {
                stack.Remove(session);
                focusController.Remove(session);
            }

            try
            {
                session.Resources.Instance.Driver.SetInteractable(false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (!animate)
            {
                try
                {
                    session.Alpha.Set(0.0f);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                FinishClose
                (
                    session,
                    restorePrevious,
                    true,
                    retainCursorWhileAwaitingRelease,
                    null
                );
                return true;
            }

            try
            {
                StartClosing(session);
            }
            catch (Exception exception)
            {
                // 닫기 시작 실패는 화면을 중간 상태로 남기지 않고 즉시 최종 정리한다.
                Debug.LogException(exception);

                try
                {
                    session.Alpha.Set(0.0f);
                }
                catch (Exception applyException)
                {
                    Debug.LogException(applyException);
                }

                FinishClose
                (
                    session,
                    restorePrevious,
                    true,
                    retainCursorWhileAwaitingRelease,
                    null
                );
            }

            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 닫기 Transition을 시작하고 유효한 callback만 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void StartClosing(ScreenSession session)
        {
            var generation = ++session.TransitionGeneration;
            var parameters = new PresentationTransitionParams
            (
                session.Alpha,
                session.Alpha.Base,
                0.0f,
                session.Options.CloseDuration,
                session.Options.UsesUnscaledTime
            );

            var handle = transitioner.Play
            (
                parameters,
                () => FinishClosingTransition(session, generation),
                exception =>
                {
                    if (!IsExpected(session, generation, ScreenState.Closing)) return;

                    Debug.LogException(exception);

                    try
                    {
                        session.Alpha.Set(0.0f);
                    }
                    catch (Exception applyException)
                    {
                        Debug.LogException(applyException);
                    }

                    FinishClosingTransition(session, generation);
                }
            );

            if (IsExpected(session, generation, ScreenState.Closing) && handle.IsPending)
            {
                session.Transition = handle;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 닫기 Transition 완료 뒤 Screen Source와 하위 수명을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void FinishClosingTransition
        (
            ScreenSession session,
            int generation
        )
        {
            if (!IsExpected(session, generation, ScreenState.Closing)) return;

            session.Transition = null;
            FinishClose
            (
                session,
                session.RestorePreviousOnClose,
                true,
                session.RetainCursorOnClose,
                null
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 하위 Handle, OnClosed, Source, Layer와 입력 수명을 순서대로 한 번씩 정리한다.
        /// <br/> 실패한 소유권은 다시 보관하지 않고 Session을 Terminal 상태로 확정한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private bool FinishClose
        (
            ScreenSession session,
            bool restorePrevious,
            bool waitForInputRelease,
            bool retainCursorWhileAwaitingRelease,
            List<Exception> collectedErrors,
            bool removeFromLiveSessions = true
        )
        {
            var errors = new List<Exception>();
            var resources = session.Resources;

            // 늦게 도착한 외부 획득 결과가 종료 Session에 다시 연결되지 않게 먼저 닫는다.
            resources.BeginRelease();
            resources.ReleaseChildren(errors);

            if (session.Resources.Instance != null)
            {
                try
                {
                    session.Resources.Instance.Driver.Visibility?.Set(false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (!session.ClosedHookCalled)
            {
                session.ClosedHookCalled = true;
                InvokeNonCancellingHook(session, ScreenState.Closing, HookKind.Closed);
            }

            resources.ReleaseSource(errors);

            // Source 반환 뒤에는 닫힌 Session이 표시 backend를 더 이상 보유하지 않게 Alpha 연결도 종료한다.
            session.Alpha = null;
            resources.ReleaseLayer(errors);

            var detached = stack.Remove(session);
            focusController.Remove(session);
            var inputReleaseSucceeded = false;
            var restoreRequested = false;
            ScreenSession previous = null;
            Action restoreFocus = null;

            if (detached && restorePrevious)
            {
                previous = Top;
                restoreFocus = () =>
                {
                    restoreRequested = true;

                    if (inputReleaseSucceeded)
                    {
                        RestorePrevious(previous);
                    }
                };
            }

            var inputSession = resources.TakeInputSession();

            try
            {
                if (inputSession != null)
                {
                    inputSession.Release
                    (
                        waitForInputRelease,
                        retainCursorWhileAwaitingRelease,
                        restoreFocus
                    );
                }
                else
                {
                    restoreRequested = restoreFocus != null;
                }

                inputReleaseSucceeded = true;

                if (restoreRequested)
                {
                    RestorePrevious(previous);
                }
            }
            catch (Exception exception)
            {
                errors.Add(exception);

                if (restoreFocus != null)
                {
                    RestorePrevious(previous);
                }
            }

            session.State = ScreenState.Closed;

            if (removeFromLiveSessions)
            {
                liveSessions.Remove(session);
            }

            ReportErrors(errors, collectedErrors);
            return errors.Count == 0;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 생존 Session을 최신 획득부터 즉시 강제 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        private List<Exception> ForceClear()
        {
            var errors = new List<Exception>();
            var batchStarted = false;

            try
            {
                inputDriver.BeginBatch();
                batchStarted = true;
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                // 개별 Session이 Live 목록을 변경하지 않게 한 뒤 원본 목록을 생성 역순으로 정리한다.
                for (var i = liveSessions.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        ForceCloseImmediate
                        (
                            liveSessions[i],
                            errors,
                            removeFromLiveSessions: false
                        );
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }
            finally
            {
                liveSessions.Clear();
                stack.Clear();
            }

            if (batchStarted)
            {
                try
                {
                    inputDriver.EndBatch();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            try
            {
                focusController.Clear();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            return errors;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 입력 해제 장벽 뒤에도 같은 Covered Screen이 top일 때만 상호작용과 Focus를 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void RestorePrevious(ScreenSession previous)
        {
            if (!IsAvailable || !ReferenceEquals(Top, previous)) return;

            try
            {
                if (previous != null && previous.State == ScreenState.Covered)
                {
                    previous.State = ScreenState.Active;
                    previous.Resources.Instance.Driver.SetInteractable(true);
                }

                focusController.Restore(previous);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 한 생존 Session의 Transition을 취소하고 강제 종료 훅과 정리를 수행한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ForceCloseImmediate
        (
            ScreenSession session,
            List<Exception> errors,
            bool removeFromLiveSessions = true
        )
        {
            if (session == null) return;

            if (session.State == ScreenState.Closed)
            {
                FinishClose
                (
                    session,
                    false,
                    true,
                    true,
                    errors,
                    removeFromLiveSessions
                );
                return;
            }

            if (!session.IsAccepted)
            {
                try
                {
                    ReleaseUnaccepted
                    (
                        session,
                        removeFromLiveSessions
                    );
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                return;
            }

            if (session.State != ScreenState.Closing)
            {
                if (!session.IsClosingHookRunning)
                {
                    InvokeClosing(session, false);
                }

                session.State = ScreenState.Closing;
            }

            try
            {
                InvalidateTransition(session);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            stack.Remove(session);
            session.RestorePreviousOnClose = false;

            try
            {
                session.Resources.Instance.Driver.SetInteractable(false);
                session.Alpha.Set(0.0f);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            FinishClose
            (
                session,
                false,
                true,
                true,
                errors,
                removeFromLiveSessions
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Transition 시작 실패 후 새 Session을 제거하고 이전 top 상태를 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void RollbackOpen
        (
            ScreenSession session,
            ScreenSession previous
        )
        {
            var errors = new List<Exception>();

            try
            {
                InvalidateTransition(session);
                stack.Remove(session);
                session.IsAccepted = false;

                try
                {
                    ReleaseUnaccepted(session);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
            finally
            {
                // 정리 실패와 무관하게 이전 top의 관찰 가능한 상태는 복원한다.
                if (previous != null)
                {
                    previous.State = ScreenState.Active;

                    try
                    {
                        previous.Resources.Instance.Driver.SetInteractable(true);
                        focusController.Restore(previous);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Screen Open 롤백이 실패했습니다.", errors);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Stack에 수락되지 않은 준비 Session의 Source와 Layer 수명을 정리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ReleaseUnaccepted
        (
            ScreenSession session,
            bool removeFromLiveSessions = true
        )
        {
            var errors = new List<Exception>();
            var resources = session.Resources;

            // 하위 Handle 해제 콜백이 새 자식을 등록하지 못하도록 먼저 종료 상태를 공개한다.
            session.State = ScreenState.Closing;
            resources.BeginRelease();
            resources.ReleaseChildren(errors);

            if (session.Resources.Instance != null)
            {
                try
                {
                    session.Resources.Instance.Driver.Visibility?.Set(false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            resources.ReleaseSource(errors);

            // Source 반환 뒤에는 닫힌 Session이 표시 backend를 더 이상 보유하지 않게 Alpha 연결도 종료한다.
            session.Alpha = null;
            resources.ReleaseLayer(errors);

            var inputSession = resources.TakeInputSession();

            try
            {
                inputSession?.Release(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (removeFromLiveSessions)
            {
                liveSessions.Remove(session);
            }

            session.State = ScreenState.Closed;

            if (errors.Count > 0)
            {
                throw new AggregateException("수락되지 않은 Screen 정리가 실패했습니다.", errors);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 수락 전 실패와 정리 실패를 하나의 Open 실패 Exception으로 결합한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private Exception CleanupUnacceptedFailure
        (
            ScreenSession session,
            ScreenSession previous,
            Exception failure
        )
        {
            try
            {
                ReleaseUnacceptedAndRestorePrevious(session, previous);
                return failure;
            }
            catch (Exception cleanupException)
            {
                return new AggregateException(failure, cleanupException);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 수락되지 않은 Screen 자원을 정리하고,
        /// <br/> 외부 Source 호출 전에 기록한 이전 top Focus를 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReleaseUnacceptedAndRestorePrevious
        (
            ScreenSession session,
            ScreenSession previous
        )
        {
            var errors = new List<Exception>();

            try
            {
                ReleaseUnaccepted(session);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            // 이전 top이 없을 때도 반환된 첫 View의 무효 선택 대신 fallback을 복원한다.
            try
            {
                focusController.Restore(previous);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("수락되지 않은 Screen 정리와 이전 Focus 복원이 실패했습니다.", errors);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// OnOpening 훅을 동기로 호출하고 취소 Context를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private ScreenStateContext InvokeOpening(ScreenSession session)
        {
            var context = new ScreenStateContext(session, ScreenState.Opening, true);
            var handler = session.Resources.Instance.StateHandler;

            if (handler == null) return context;

            hookDepth++;

            try
            {
                handler.OnOpening(context);
            }
            finally
            {
                hookDepth--;
            }

            return context;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// OnClosing 훅을 동기로 호출하고 예외를 기록한 뒤 Context를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private ScreenStateContext InvokeClosing
        (
            ScreenSession session,
            bool canCancel
        )
        {
            var context = new ScreenStateContext(session, session.State, canCancel);
            var handler = session.Resources.Instance.StateHandler;

            if (handler == null) return context;

            hookDepth++;

            try
            {
                handler.OnClosing(context);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                hookDepth--;
            }

            return context;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 취소 불가능한 OnOpened 또는 OnClosed 훅을 호출하고 예외를 기록한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void InvokeNonCancellingHook
        (
            ScreenSession session,
            ScreenState state,
            HookKind kind
        )
        {
            var handler = session.Resources.Instance.StateHandler;

            if (handler == null) return;

            var context = new ScreenStateContext(session, state, false);
            hookDepth++;

            try
            {
                if (kind == HookKind.Opened)
                {
                    handler.OnOpened(context);
                }
                else
                {
                    handler.OnClosed(context);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                hookDepth--;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Session의 이전 Transition callback을 무효화한 뒤 실행을 취소한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void InvalidateTransition(ScreenSession session)
        {
            session.TransitionGeneration++;

            var current = session.Transition;

            if (current == null) return;

            session.Transition = null;
            current.Cancel();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Transition callback의 세대값과 예상 Session 상태가 일치하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private bool IsExpected
        (
            ScreenSession session,
            int generation,
            ScreenState state
        )
        {
            return session.TransitionGeneration == generation &&
                session.State == state;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 한 정리 예외를 수집하거나 즉시 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReportError
        (
            Exception exception,
            List<Exception> collectedErrors
        )
        {
            if (collectedErrors != null)
            {
                collectedErrors.Add(exception);
            }
            else
            {
                Debug.LogException(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 정리 예외를 수집하거나 즉시 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReportErrors
        (
            List<Exception> errors,
            List<Exception> collectedErrors
        )
        {
            for (var i = 0; i < errors.Count; i++)
            {
                ReportError(errors[i], collectedErrors);
            }
        }

    #endregion

    #region 내부 데이터

        private enum HookKind
        {
            Opened = 0,
            Closed = 1,
        }

    #endregion

    }
}
