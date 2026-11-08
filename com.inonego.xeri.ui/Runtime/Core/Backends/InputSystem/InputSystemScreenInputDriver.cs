/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : InputSystemScreenInputDriver.cs
수정일 : 2026-11-08
# 설명
Screen 입력 정책을 active Context contribution만 사용해 Action Map과 Cursor 상태에 합성하고 입력 해제 장벽을 갱신한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Input System 기반 Screen 입력 정책 backend.
    /// </summary>
    // ============================================================
    public sealed class InputSystemScreenInputDriver : MonoBehaviour, IScreenInputDriver
    {

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// 획득 순서와 입력 해제 안정화 상태를 함께 보관한다.
        /// </summary>
        // ============================================================
        private sealed class SessionState
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Screen 입력 정책 Session.
            /// </summary>
            // ------------------------------------------------------------
            public ScreenInputSession Session { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 입력 해제 후 복원을 허용할 최초 Frame.
            /// </summary>
            // ------------------------------------------------------------
            public int ReleaseFrame { get; set; } = -1;

            // ------------------------------------------------------------
            /// <summary>
            /// 입력 Session 상태를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public SessionState(ScreenInputSession session)
            {
                Session = session ?? throw new ArgumentNullException(nameof(session));
            }
        }

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// backend 초기화가 끝났는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsInitialized { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 입력 정책이 적용 중이거나 Batch가 열려 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        internal bool IsPolicyDeferred => isApplyingPolicy || batchDepth > 0;

        // ----------------------------------------------------------------------
        /// <summary>
        /// 구성된 UI·Gameplay Action Map에서 가장 최근 수행된 Action의 장치.
        /// </summary>
        // ----------------------------------------------------------------------
        public InputDevice LastInputDevice { get; private set; }

        private readonly List<SessionState> sessions = new List<SessionState>();
        private readonly List<InputAction> releaseActions = new List<InputAction>();

        private InputActionMap uiActionMap = null;
        private InputActionMap gameplayActionMap = null;

        private bool baselineCaptured = false;
        private bool baselineGameplayEnabled = false;
        private bool[] baselineUIActionsEnabled = null;
        private bool[] baselineGameplayActionsEnabled = null;
        private bool baselineCursorVisible = false;
        private CursorLockMode baselineCursorLockMode = CursorLockMode.None;

        private bool appliedCursorVisible = false;
        private CursorLockMode appliedCursorLockMode = CursorLockMode.None;

        private int batchDepth = 0;
        private bool applyPending = false;
        private bool isApplyingPolicy = false;
        private int policyRevision = 0;
        private ScreenInputSession retainedCursorOwner = null;
        private bool isDisposed = false;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 마지막 입력 장치가 바뀌었을 때 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        public event Action<InputDevice> OnLastInputDeviceChanged = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 입력 정책 적용과 Batch가 안정화된 시점을 통지한다.
        /// </summary>
        // ------------------------------------------------------------
        internal event Action OnPolicySettled = null;

    #endregion

    #region 초기화

        // ------------------------------------------------------------
        /// <summary>
        /// Host Input Module과 Settings의 Action Map 구성을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Initialize
        (
            InputSystemUIInputModule inputModule,
            UISettingsAsset settings
        )
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("Input System Screen Input Driver가 이미 초기화됐습니다.");
            }

            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(InputSystemScreenInputDriver));
            }

            // 비활성 컴포넌트는 입력 해제 장벽을 Update에서 처리할 수 없다.
            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException
                (
                    "활성 Input System Screen Input Driver만 초기화할 수 있습니다."
                );
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var uiActionsAsset = settings.UIActionsAsset ?? inputModule?.actionsAsset;

            if (uiActionsAsset == null)
            {
                throw new InvalidOperationException
                (
                    "UI Action Asset이 설정되지 않았습니다. " +
                    "UISettingsAsset.UIActionsAsset 또는 mixed UGUI InputModule actionsAsset이 필요합니다."
                );
            }

            var gameplayActionsAsset = settings.GameplayActionsAsset;

            if (gameplayActionsAsset == null)
            {
                throw new InvalidOperationException("Gameplay Input Action Asset이 설정되지 않았습니다.");
            }

            uiActionMap = FindMap(uiActionsAsset, settings.UIActionMap, "UI");
            gameplayActionMap = FindMap
            (
                gameplayActionsAsset,
                settings.GameplayActionMap,
                "Gameplay"
            );

            if (ReferenceEquals(uiActionMap, gameplayActionMap))
            {
                throw new InvalidOperationException("UI와 Gameplay Action Map은 서로 달라야 합니다.");
            }

            BuildReleaseActions(settings.ReleaseActionNames);
            SubscribeInputDeviceTracking(uiActionMap);
            SubscribeInputDeviceTracking(gameplayActionMap);
            IsInitialized = true;
        }

    #endregion

    #region Unity 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 해제 대기 Session과 한 Frame 안정화 장벽을 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Update()
        {
            if (!IsInitialized || isDisposed || sessions.Count == 0) return;

            var isReleaseInputPressed = IsReleaseInputPressed();
            List<SessionState> readySessions = null;

            for (var i = sessions.Count - 1; i >= 0; i--)
            {
                var sessionState = sessions[i];

                if (!sessionState.Session.IsAwaitingRelease) continue;

                if (isReleaseInputPressed)
                {
                    sessionState.ReleaseFrame = -1;
                    continue;
                }

                if (sessionState.ReleaseFrame < 0)
                {
                    sessionState.ReleaseFrame = Time.frameCount + 1;
                    continue;
                }

                if (Time.frameCount < sessionState.ReleaseFrame) continue;

                // Dispose가 재진입해도 이번 Update의 완료 대상은 지역 목록으로 보존한다.
                (readySessions ??= new List<SessionState>()).Add(sessionState);
            }

            if (readySessions == null) return;

            for (var i = 0; i < readySessions.Count; i++)
            {
                var sessionState = readySessions[i];
                sessions.Remove(sessionState);

                if (ReferenceEquals(retainedCursorOwner, sessionState.Session))
                {
                    retainedCursorOwner = null;
                }
            }

            try
            {
                RequestApply();
            }
            catch (Exception exception)
            {
                applyPending = false;
                var errors = new ExceptionBuffer(exception);

                for (var i = 0; i < readySessions.Count; i++)
                {
                    try
                    {
                        ReleaseSession
                        (
                            readySessions[i],
                            removeFromSessions: false
                        );
                    }
                    catch (Exception releaseException)
                    {
                        errors.Add(releaseException);
                    }
                }

                if (errors.Count == 1)
                {
                    throw;
                }

                throw errors.CreateAggregateException
                (
                    "입력 정책 적용과 해제 완료 callback이 모두 실패했습니다."
                );
            }

            List<Exception> releaseErrors = null;

            for (var i = 0; i < readySessions.Count; i++)
            {
                try
                {
                    ReleaseSession
                    (
                        readySessions[i],
                        removeFromSessions: false
                    );
                }
                catch (Exception exception)
                {
                    releaseErrors ??= new List<Exception>();
                    releaseErrors.Add(exception);
                }
            }

            if (releaseErrors != null)
            {
                throw new AggregateException
                (
                    "입력 해제 완료 callback 중 하나 이상이 실패했습니다.",
                    releaseErrors
                );
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 비활성화된 Driver가 입력 해제 대기 Session을 유지하지 않도록 수명을 종료한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void OnDisable()
        {
            if (!IsInitialized || isDisposed) return;

            Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity 파괴 시 명시적 해제가 누락된 backend 구독을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDestroy()
        {
            Dispose();
        }

    #endregion

    #region 스크린 입력 드라이버 구현

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Options에 맞는 입력 정책 Session을 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenInputSession Acquire
        (
            ScreenOptions options,
            bool contributionEnabled = true
        )
        {
            ThrowIfUnavailable();

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!baselineCaptured)
            {
                CaptureBaselineState();
            }

            var session = new ScreenInputSession
            (
                options,
                RequestSessionRelease,
                HandleContributionChanged,
                contributionEnabled,
                policyPending: true,
                validateContributionEnable: ValidateContributionActivation
            );
            var sessionState = new SessionState(session);
            sessions.Add(sessionState);

            try
            {
                RequestApply();
                // callback에서 전체 반환한 Session도 정상 획득 결과가 될 수 없다.
                ThrowIfUnavailable();

                if (session.IsReleased)
                {
                    throw new InvalidOperationException
                    (
                        "입력 정책 획득 중 Screen Input Session 소유권이 반환됐습니다."
                    );
                }

                return session;
            }
            catch (Exception exception)
            {
                sessions.Remove(sessionState);
                session.MarkReleased(invokeCompletionCallback: false);

                // Dispose가 이미 전체 소유권과 baseline을 종료했으면 획득 롤백으로 다시 적용하지 않는다.
                if (isDisposed)
                {
                    throw;
                }

                try
                {
                    RequestApply();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Screen 입력 정책 획득과 이전 유효 상태 복원이 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 Session 해제 중 중간 입력 상태 적용을 보류한다.
        /// </summary>
        // ------------------------------------------------------------
        public void BeginBatch()
        {
            ThrowIfUnavailable();
            batchDepth++;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Batch 종료 시 정책 적용과 보류된 Session 완료를 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        public void EndBatch()
        {
            ThrowIfUnavailable();

            if (batchDepth <= 0)
            {
                throw new InvalidOperationException("입력 정책 Batch가 시작되지 않았습니다.");
            }

            batchDepth--;

            if (batchDepth == 0)
            {
                // 정책 자체가 이미 적용됐더라도 Batch에서 보류한 Session 완료를 재개한다.
                ApplyPendingPolicy();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 해제를 기다리지 않고 모든 Session을 역순 강제 종결한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ForceReleaseAll()
        {
            if (!IsInitialized || isDisposed) return;

            var errors = ReleaseAllSessions();

            if (errors.Count > 0)
            {
                throw new AggregateException("입력 정책 전체 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    #region 입력 정책

        // ------------------------------------------------------------
        /// <summary>
        /// Session 반환 요청을 즉시 종료하거나 입력 해제 대기 상태로 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RequestSessionRelease
        (
            ScreenInputSession session,
            bool waitForInputRelease,
            bool retainCursorWhileAwaitingRelease
        )
        {
            var index = FindSessionIndex(session);

            if (index < 0)
            {
                session.MarkReleased();
                return;
            }

            var sessionState = sessions[index];

            if (waitForInputRelease && IsReleaseInputPressed())
            {
                if
                (
                    retainCursorWhileAwaitingRelease &&
                    retainedCursorOwner != null &&
                    !ReferenceEquals(retainedCursorOwner, session)
                )
                {
                    throw new InvalidOperationException
                    (
                        "입력 해제 대기 Cursor owner는 동시에 하나만 존재할 수 있습니다."
                    );
                }

                sessionState.ReleaseFrame = -1;
                session.MarkAwaitingRelease(retainCursorWhileAwaitingRelease);

                if (retainCursorWhileAwaitingRelease)
                {
                    retainedCursorOwner = session;
                }
                else if (ReferenceEquals(retainedCursorOwner, session))
                {
                    retainedCursorOwner = null;
                }

                try
                {
                    RequestApply();
                }
                catch
                {
                    if (isDisposed)
                    {
                        throw;
                    }

                    sessions.Remove(sessionState);

                    if (ReferenceEquals(retainedCursorOwner, session))
                    {
                        retainedCursorOwner = null;
                    }

                    applyPending = false;
                    ReleaseSession
                    (
                        sessionState,
                        invokeCompletionCallback: false,
                        removeFromSessions: false
                    );
                    throw;
                }

                return;
            }

            sessions.RemoveAt(index);

            if (ReferenceEquals(retainedCursorOwner, session))
            {
                retainedCursorOwner = null;
            }

            try
            {
                RequestApply();
            }
            catch
            {
                // 목록에서 먼저 분리한 Session은 Dispose의 snapshot에 포함되지 않는다.
                // 현재 Release 작업이 종료 책임을 끝까지 가진다.
                applyPending = false;
                ReleaseSession
                (
                    sessionState,
                    invokeCompletionCallback: false,
                    removeFromSessions: false
                );
                throw;
            }

            ReleaseSession(sessionState, removeFromSessions: false);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Session 상태를 최종 해제하고 필요하면 소유 목록에서 제거한다.
        /// <br/> 전체 해제에서는 먼저 분리한 Session 목록을 순회하므로 소유 목록 제거를 생략한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ReleaseSession
        (
            SessionState sessionState,
            bool invokeCompletionCallback = true,
            bool removeFromSessions = true
        )
        {
            if (removeFromSessions)
            {
                sessions.Remove(sessionState);
            }

            sessionState.Session.MarkReleased(invokeCompletionCallback);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Session의 현재 인덱스를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private int FindSessionIndex(ScreenInputSession session)
        {
            for (var i = 0; i < sessions.Count; i++)
            {
                if (ReferenceEquals(sessions[i].Session, session))
                {
                    return i;
                }
            }

            return -1;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Screen Session contribution 변경을 현재 effective input state에 반영한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void HandleContributionChanged(ScreenInputSession session)
        {
            if (!IsInitialized || isDisposed || session == null || session.IsReleased) return;

            RequestApply();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 기여를 재활성화하는 Context 권한 변경은 현재 정책의 적용 완료가 필요하다.
        /// <br/> 재진입이나 Batch 중이면 상위 권한 트랜잭션이 롤백할 수 있도록 즉시 거부한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ValidateContributionActivation()
        {
            ThrowIfUnavailable();

            if (IsPolicyDeferred)
            {
                throw new InvalidOperationException
                (
                    "입력 정책 적용 중에는 기존 Screen Session의 Context 입력 기여를 활성화할 수 없습니다."
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 입력 정책 적용 또는 Batch 종료 시점 적용을 요청한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RequestApply()
        {
            // 외부 Input Action callback이 정책 소유권을 바꾸면 이전 적용 결과는 폐기한다.
            unchecked
            {
                policyRevision++;
            }

            applyPending = true;
            ApplyPendingPolicy();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 외부 Action callback이 다시 정책을 요청해도 현재 적용을 끝낸 뒤 최종 정책만 반영한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ApplyPendingPolicy()
        {
            if (isApplyingPolicy || batchDepth > 0) return;

            isApplyingPolicy = true;
            var failed = true;

            try
            {
                while (applyPending && batchDepth == 0 && !isDisposed)
                {
                    applyPending = false;
                    ApplyEffectiveState();
                }

                failed = false;
            }
            finally
            {
                isApplyingPolicy = false;

                // 중첩 획득은 마지막 정책이 실제 backend에 적용된 뒤에만 열기를 재개한다.
                if (failed)
                {
                    ResolvePendingAcquisitions(false);
                }
                else if (!isDisposed && batchDepth == 0 && !applyPending)
                {
                    ResolvePendingAcquisitions(true);
                }

                // Context 권한 전환이 중간에 실패했다면 확정된 정책 이후에만 복원을 재시도한다.
                if (!failed && !isDisposed && batchDepth == 0 && !applyPending)
                {
                    OnPolicySettled?.Invoke();
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 정책 적용이 확정된 Session의 대기 중인 Screen 열기를 재개한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ResolvePendingAcquisitions(bool applied)
        {
            // 열기 callback이 새 Session을 획득해도 이번 완료 대상만 처리한다.
            List<ScreenInputSession> pendingSessions = null;

            for (var index = 0; index < sessions.Count; index++)
            {
                var session = sessions[index].Session;
                if (session.IsReleased || !session.IsPolicyPending) continue;

                (pendingSessions ??= new List<ScreenInputSession>()).Add(session);
            }

            if (pendingSessions == null) return;

            for (var index = 0; index < pendingSessions.Count; index++)
            {
                if (isDisposed) return;
                if (applied && (applyPending || batchDepth > 0)) return;

                var session = pendingSessions[index];
                if (session.IsReleased || !session.IsPolicyPending) continue;

                try
                {
                    session.MarkPolicyApplied(applied);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 Session 소유권을 분리하고 역순으로 종결한다.
        /// </summary>
        // ------------------------------------------------------------
        private IReadOnlyList<Exception> ReleaseAllSessions()
        {
            // Dispose뿐 아니라 ForceReleaseAll도 진행 중인 정책 적용의 세대를 종료한다.
            unchecked
            {
                policyRevision++;
            }

            var errors = new ExceptionBuffer();
            var releasingSessions = new List<SessionState>(sessions.Count);

            // 전체 해제 시작 시점의 Session만 분리해 callback에서 획득한 새 Session을 보존한다.
            for (var i = sessions.Count - 1; i >= 0; i--)
            {
                releasingSessions.Add(sessions[i]);
            }

            sessions.Clear();
            retainedCursorOwner = null;
            batchDepth = 0;
            applyPending = true;

            // 이미 진행 중인 정책 적용이 있으면 여기서 Action Map을 중첩 복원하지 않는다.
            // Dispose는 남은 적용을 이어받을 호출자가 없으므로 직접 복원한다.
            if (!isApplyingPolicy || isDisposed)
            {
                try
                {
                    ApplyBaselineState();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                    applyPending = false;
                }
            }

            // 완료 callback은 Session 수명의 종료를 의미한다.
            // 재진입 시 Action Map은 진행 중인 정책 적용이 끝난 뒤 기준 상태로 복원된다.
            for (var i = 0; i < releasingSessions.Count; i++)
            {
                try
                {
                    ReleaseSession
                    (
                        releasingSessions[i],
                        removeFromSessions: false
                    );
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            return errors.AsReadOnlyList();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 생존 Session 정책을 합성해 Action Map과 Cursor에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyEffectiveState()
        {
            var revision = policyRevision;

            if (!baselineCaptured)
            {
                applyPending = false;
                return;
            }

            if (sessions.Count == 0)
            {
                ApplyBaselineState();
                return;
            }

            var hasContribution = false;
            var blocksGameplay = false;
            var hasReleaseBarrier = false;
            ScreenInputSession cursorOwner = null;

            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i].Session;

                if (!session.IsContributionEnabled) continue;

                hasContribution = true;

                if (session.IsAwaitingRelease)
                {
                    hasReleaseBarrier = true;
                    continue;
                }

                blocksGameplay |= session.Options.BlocksGameplayInput;

                if (!session.IsCursorPolicyEnabled) continue;

                if (cursorOwner != null && !ReferenceEquals(cursorOwner, session))
                {
                    throw new InvalidOperationException
                    (
                        "동시에 둘 이상의 Screen이 Cursor policy owner로 활성화됐습니다."
                    );
                }

                cursorOwner = session;
            }

            if (!hasContribution)
            {
                ApplyCapturedBaselineState();

                if (revision == policyRevision)
                {
                    applyPending = false;
                }

                return;
            }

            var retainedOwner = retainedCursorOwner;

            if
            (
                retainedOwner != null &&
                (
                    retainedOwner.IsReleased ||
                    !retainedOwner.IsAwaitingRelease ||
                    !retainedOwner.RetainsCursorWhileAwaitingRelease ||
                    !retainedOwner.IsContributionEnabled
                )
            )
            {
                retainedOwner = null;
                retainedCursorOwner = null;
            }

            var effectiveCursorOwner = retainedOwner ?? cursorOwner;
            var cursorVisible = effectiveCursorOwner != null
                ? effectiveCursorOwner.Options.ShowsCursor
                : baselineCursorVisible;
            var cursorLockMode = effectiveCursorOwner != null
                ? effectiveCursorOwner.Options.CursorLockMode
                : baselineCursorLockMode;

            if
            (
                ApplyState
                (
                    hasReleaseBarrier || blocksGameplay ? false : baselineGameplayEnabled,
                    cursorVisible,
                    cursorLockMode,
                    revision
                )
            )
            {
                applyPending = false;
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 캡처한 baseline을 적용하되 살아 있는 suspended Session을 위해 snapshot은 유지한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ApplyCapturedBaselineState()
        {
            var errors = new ExceptionBuffer();
            var wasDisposing = isDisposed;
            var revision = policyRevision;

            try
            {
                RestoreActionStates
                (
                    uiActionMap,
                    baselineUIActionsEnabled,
                    revision
                );
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            // 일반 정책 복원 중 Dispose가 재진입했다면 이후 기준 상태는 Dispose가 소유한다.
            // Dispose 자신이 시작한 복원은 끝까지 진행하며, 수집한 오류는 아래에서 보고한다.
            if ((wasDisposing || !isDisposed) && revision == policyRevision)
            {
                try
                {
                    RestoreActionStates
                    (
                        gameplayActionMap,
                        baselineGameplayActionsEnabled,
                        revision
                    );
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                if ((wasDisposing || !isDisposed) && revision == policyRevision)
                {
                    if (baselineCursorVisible != appliedCursorVisible)
                    {
                        try
                        {
                            Cursor.visible = baselineCursorVisible;
                            appliedCursorVisible = baselineCursorVisible;
                        }
                        catch (Exception exception)
                        {
                            errors.Add(exception);
                        }
                    }

                    if (baselineCursorLockMode != appliedCursorLockMode)
                    {
                        try
                        {
                            Cursor.lockState = baselineCursorLockMode;
                            appliedCursorLockMode = baselineCursorLockMode;
                        }
                        catch (Exception exception)
                        {
                            errors.Add(exception);
                        }
                    }
                }
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException
            (
                "Input baseline 상태 복원 중 하나 이상의 적용이 실패했습니다."
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 첫 Session 획득 전에 캡처한 Action Map과 Cursor 상태를 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplyBaselineState()
        {
            if (!baselineCaptured)
            {
                applyPending = false;
                return;
            }

            var revision = policyRevision;
            ApplyCapturedBaselineState();

            // 중첩 callback의 신규 소유권이 기준 상태를 다시 캡처했다면 보존한다.
            if (revision != policyRevision) return;

            baselineCaptured = false;
            baselineUIActionsEnabled = null;
            baselineGameplayActionsEnabled = null;
            applyPending = false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Action Map의 현재 Action별 활성 상태를 캡처한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool[] CaptureActionStates(InputActionMap map)
        {
            var actions = map.actions;
            var states = new bool[actions.Count];

            for (var i = 0; i < actions.Count; i++)
            {
                states[i] = actions[i].enabled;
            }

            return states;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Action Map의 모든 Action이 활성 상태인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool AreAllActionsEnabled(InputActionMap map)
        {
            var actions = map.actions;

            for (var i = 0; i < actions.Count; i++)
            {
                if (!actions[i].enabled) return false;
            }

            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Gameplay Action별 상태가 캡처한 기준값과 일치하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool MatchesActionStates(InputActionMap map, bool[] states)
        {
            var actions = map.actions;

            for (var i = 0; i < actions.Count; i++)
            {
                if (actions[i].enabled != states[i]) return false;
            }

            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 캡처한 Action별 활성 상태를 Action Map에 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RestoreActionStates
        (
            InputActionMap map,
            bool[] states,
            int revision
        )
        {
            var errors = new ExceptionBuffer();

            try
            {
                map.Disable();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            var actions = map.actions;

            for (var i = 0; i < actions.Count; i++)
            {
                // 재진입 정책이 생겼다면 이전 복원 작업은 남은 Action을 건드리지 않는다.
                if (revision != policyRevision) break;
                if (!states[i]) continue;

                try
                {
                    actions[i].Enable();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException
            (
                $"Input Action Map '{map.name}' 상태 복원 중 하나 이상의 Action 적용이 실패했습니다."
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 변경된 값만 실제 Action Map과 Cursor에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool ApplyState
        (
            bool gameplayEnabled,
            bool cursorVisible,
            CursorLockMode cursorLockMode,
            int revision
        )
        {
            var errors = new ExceptionBuffer();
            var superseded = false;

            // 개별 Action 활성 상태가 진실의 원천이다. 이전 정책의 bool cache를 신뢰하지 않는다.
            if (!AreAllActionsEnabled(uiActionMap))
            {
                try
                {
                    SetMapEnabled(uiActionMap, true);
                    ThrowIfUnavailable();
                }
                catch (Exception exception)
                {
                    if (isDisposed)
                    {
                        throw;
                    }

                    errors.Add(exception);
                }

                superseded = revision != policyRevision;
            }

            if
            (
                !superseded &&
                (
                    gameplayEnabled
                        ? !MatchesActionStates(gameplayActionMap, baselineGameplayActionsEnabled)
                        : gameplayActionMap.enabled
                )
            )
            {
                try
                {
                    if (gameplayEnabled)
                    {
                        // 차단 해제 시 Map 전체가 아니라 Session 직전의 Action별 상태를 복원한다.
                        RestoreActionStates
                        (
                            gameplayActionMap,
                            baselineGameplayActionsEnabled,
                            revision
                        );
                    }
                    else
                    {
                        SetMapEnabled(gameplayActionMap, false);
                    }

                    ThrowIfUnavailable();
                }
                catch (Exception exception)
                {
                    if (isDisposed)
                    {
                        throw;
                    }

                    errors.Add(exception);
                }

                superseded = revision != policyRevision;
            }

            if (!superseded && cursorVisible != appliedCursorVisible)
            {
                try
                {
                    Cursor.visible = cursorVisible;
                    appliedCursorVisible = cursorVisible;
                }
                catch (Exception exception)
                {
                    appliedCursorVisible = !cursorVisible;
                    errors.Add(exception);
                }
            }

            if (!superseded && cursorLockMode != appliedCursorLockMode)
            {
                try
                {
                    Cursor.lockState = cursorLockMode;
                    appliedCursorLockMode = cursorLockMode;
                }
                catch (Exception exception)
                {
                    appliedCursorLockMode = Cursor.lockState;
                    errors.Add(exception);
                }
            }

            if (errors.Count == 0) return !superseded;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException
            (
                "Screen 입력 정책 적용 중 하나 이상의 backend 변경이 실패했습니다."
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Action Map의 활성 상태를 중복 호출 없이 맞춘다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SetMapEnabled
        (
            InputActionMap map,
            bool enabled
        )
        {
            if (enabled)
            {
                map.Enable();
            }
            else if (map.enabled)
            {
                map.Disable();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 첫 Session 직전 복원 기준 상태를 캡처한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CaptureBaselineState()
        {
            baselineGameplayEnabled = gameplayActionMap.enabled;
            baselineUIActionsEnabled = CaptureActionStates(uiActionMap);
            baselineGameplayActionsEnabled = CaptureActionStates(gameplayActionMap);
            baselineCursorVisible = Cursor.visible;
            baselineCursorLockMode = Cursor.lockState;

            appliedCursorVisible = baselineCursorVisible;
            appliedCursorLockMode = baselineCursorLockMode;
            baselineCaptured = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 설정된 닫기 입력 중 하나라도 유지 중인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool IsReleaseInputPressed()
        {
            for (var i = 0; i < releaseActions.Count; i++)
            {
                var controls = releaseActions[i].controls;

                // 비활성 Action도 실제 Control 상태는 유지하므로 Gameplay Map 차단 중 해제를 관찰할 수 있다.
                for (var controlIndex = 0; controlIndex < controls.Count; controlIndex++)
                {
                    if (controls[controlIndex].IsPressed())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

    #endregion

    #region 설정

        // ------------------------------------------------------------
        /// <summary>
        /// Actions Asset에서 필수 Action Map을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static InputActionMap FindMap
        (
            InputActionAsset asset,
            string mapName,
            string role
        )
        {
            if (string.IsNullOrWhiteSpace(mapName))
            {
                throw new InvalidOperationException($"{role} Action Map 이름이 비어 있습니다.");
            }

            var map = asset.FindActionMap(mapName, false);

            if (map == null)
            {
                throw new InvalidOperationException
                (
                    $"{role} Action Map '{mapName}'을 Input Actions Asset에서 찾을 수 없습니다."
                );
            }

            return map;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// UI와 Gameplay Map에서 입력 해제 장벽 Action을 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BuildReleaseActions(IReadOnlyList<string> actionNames)
        {
            releaseActions.Clear();

            if (actionNames == null) return;

            for (var i = 0; i < actionNames.Count; i++)
            {
                var actionName = actionNames[i];

                if (string.IsNullOrWhiteSpace(actionName)) continue;

                var uiAction = uiActionMap.FindAction(actionName, false);
                var gameplayAction = gameplayActionMap.FindAction(actionName, false);

                if (uiAction == null && gameplayAction == null)
                {
                    throw new InvalidOperationException
                    (
                        $"입력 해제 Action '{actionName}'을 UI 또는 Gameplay Map에서 찾을 수 없습니다."
                    );
                }

                if (uiAction != null && !releaseActions.Contains(uiAction))
                {
                    releaseActions.Add(uiAction);
                }

                if (gameplayAction != null && !releaseActions.Contains(gameplayAction))
                {
                    releaseActions.Add(gameplayAction);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Action Map의 수행 입력을 마지막 입력 장치 추적에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void SubscribeInputDeviceTracking(InputActionMap map)
        {
            var actions = map.actions;

            for (var i = 0; i < actions.Count; i++)
            {
                actions[i].performed += HandleActionPerformed;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Action Map의 마지막 입력 장치 추적 구독을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnsubscribeInputDeviceTracking(InputActionMap map)
        {
            if (map == null) return;

            var actions = map.actions;

            for (var i = 0; i < actions.Count; i++)
            {
                actions[i].performed -= HandleActionPerformed;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 구성된 Action 수행 장치가 바뀌면 마지막 입력 장치를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleActionPerformed(InputAction.CallbackContext context)
        {
            var device = context.control?.device;

            if (device == null || ReferenceEquals(device, LastInputDevice)) return;

            LastInputDevice = device;
            var handlers = OnLastInputDeviceChanged;
            if (handlers == null) return;

            foreach (Action<InputDevice> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(device);
                }
                catch (Exception exception)
                {
                    // 한 observer 실패가 뒤 observer와 Input System update를 차단하지 않게 격리한다.
                    Debug.LogException(exception, this);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 초기화·해제 상태가 유효한지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfUnavailable()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(InputSystemScreenInputDriver));
            }

            if (!IsInitialized)
            {
                throw new InvalidOperationException("Input System Screen Input Driver가 초기화되지 않았습니다.");
            }
        }

    #endregion

    #region 수명 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 입력 Session과 backend 구독을 해제하고 기준 상태를 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            var wasInitialized = IsInitialized;
            isDisposed = true;
            var errors = new ExceptionBuffer();

            if (wasInitialized)
            {
                errors.AddRange(ReleaseAllSessions());
                UnsubscribeInputDeviceTracking(uiActionMap);
                UnsubscribeInputDeviceTracking(gameplayActionMap);
            }

            releaseActions.Clear();
            sessions.Clear();
            baselineUIActionsEnabled = null;
            baselineGameplayActionsEnabled = null;
            uiActionMap = null;
            gameplayActionMap = null;
            OnLastInputDeviceChanged = null;
            OnPolicySettled = null;
            IsInitialized = false;

            if (errors.Count > 0)
            {
                throw errors.CreateAggregateException("Input System Screen Input Driver 해제가 실패했습니다.");
            }
        }

    #endregion

    }
}
