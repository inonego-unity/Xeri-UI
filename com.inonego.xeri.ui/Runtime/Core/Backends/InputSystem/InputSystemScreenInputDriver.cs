/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : InputSystemScreenInputDriver.cs
수정일 : 2026-09-17
# 설명
Screen 입력 정책을 Input System Action Map과 Cursor 상태에 합성하고 입력 해제 장벽을 갱신한다.
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
            /// 같은 우선순위에서 최신 정책을 선택할 획득 순서.
            /// </summary>
            // ------------------------------------------------------------
            public long Sequence { get; }

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
            public SessionState
            (
                ScreenInputSession session,
                long sequence
            ) : base()
            {
                Session = session ?? throw new ArgumentNullException(nameof(session));
                Sequence = sequence;
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// 구성된 UI·Gameplay Action Map에서 가장 최근 수행된 Action의 장치.
        /// </summary>
        // ----------------------------------------------------------------------
        public InputDevice LastInputDevice { get; private set; }

        private readonly List<SessionState> sessions = new List<SessionState>();
        private readonly List<SessionState> sessionsReadyForRelease = new List<SessionState>();
        private readonly List<InputAction> releaseActions = new List<InputAction>();

        private InputSystemUIInputModule inputModule = null;
        private InputActionMap uiActionMap = null;
        private InputActionMap gameplayActionMap = null;

        private bool baselineCaptured = false;
        private bool baselineGameplayEnabled = false;
        private bool[] baselineUIActionsEnabled = null;
        private bool[] baselineGameplayActionsEnabled = null;
        private bool baselineCursorVisible = false;
        private CursorLockMode baselineCursorLockMode = CursorLockMode.None;

        private bool appliedUIEnabled = false;
        private bool appliedGameplayEnabled = false;
        private bool appliedCursorVisible = false;
        private CursorLockMode appliedCursorLockMode = CursorLockMode.None;

        private int batchDepth = 0;
        private bool applyPending = false;
        private long nextSequence = 0;
        private bool isDisposed = false;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 마지막 입력 장치가 바뀌었을 때 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        public event Action<InputDevice> OnLastInputDeviceChanged = null;

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

            this.inputModule = inputModule ?? throw new ArgumentNullException(nameof(inputModule));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var uiActionsAsset = inputModule.actionsAsset;

            if (uiActionsAsset == null)
            {
                throw new InvalidOperationException("InputSystemUIInputModule Actions Asset이 설정되지 않았습니다.");
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
            InputSystem.onActionChange += HandleActionChange;
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
            sessionsReadyForRelease.Clear();

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

                sessionsReadyForRelease.Add(sessionState);
            }

            if (sessionsReadyForRelease.Count == 0) return;

            for (var i = 0; i < sessionsReadyForRelease.Count; i++)
            {
                sessions.Remove(sessionsReadyForRelease[i]);
            }

            try
            {
                RequestApply();
            }
            catch (Exception exception)
            {
                ResetApplyStateAfterFailure();
                var errors = new List<Exception>
                {
                    exception,
                };

                for (var i = 0; i < sessionsReadyForRelease.Count; i++)
                {
                    try
                    {
                        ReleaseSession
                        (
                            sessionsReadyForRelease[i],
                            removeFromSessions: false
                        );
                    }
                    catch (Exception releaseException)
                    {
                        errors.Add(releaseException);
                    }
                }

                sessionsReadyForRelease.Clear();

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "입력 정책 적용과 해제 완료 callback이 모두 실패했습니다.",
                    errors
                );
            }

            List<Exception> releaseErrors = null;

            for (var i = 0; i < sessionsReadyForRelease.Count; i++)
            {
                try
                {
                    ReleaseSession
                    (
                        sessionsReadyForRelease[i],
                        removeFromSessions: false
                    );
                }
                catch (Exception exception)
                {
                    releaseErrors ??= new List<Exception>();
                    releaseErrors.Add(exception);
                }
            }

            sessionsReadyForRelease.Clear();

            if (releaseErrors != null)
            {
                throw new AggregateException
                (
                    "입력 해제 완료 callback 중 하나 이상이 실패했습니다.",
                    releaseErrors
                );
            }
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

    #region IScreenInputDriver

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Options에 맞는 입력 정책 Session을 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenInputSession Acquire(ScreenOptions options)
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

            var session = new ScreenInputSession(options, RequestSessionRelease);
            var sessionState = new SessionState(session, nextSequence++);
            sessions.Add(sessionState);

            try
            {
                RequestApply();
                return session;
            }
            catch (Exception exception)
            {
                sessions.Remove(sessionState);

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
        /// 배치 해제 뒤 최종 입력 상태를 한 번 적용한다.
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

            if (batchDepth == 0 && applyPending)
            {
                ApplyEffectiveState();
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
                sessionState.ReleaseFrame = -1;
                session.MarkAwaitingRelease(retainCursorWhileAwaitingRelease);

                try
                {
                    RequestApply();
                }
                catch
                {
                    sessions.RemoveAt(index);
                    ResetApplyStateAfterFailure();
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

            try
            {
                RequestApply();
            }
            catch
            {
                ResetApplyStateAfterFailure();
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

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 입력 정책 적용 또는 Batch 종료 시점 적용을 요청한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RequestApply()
        {
            applyPending = true;

            if (batchDepth == 0)
            {
                ApplyEffectiveState();
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 실패한 적용 요청을 재시도 대상으로 남기지 않고 다음 독립 작업의 기준 상태를 정리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ResetApplyStateAfterFailure()
        {
            applyPending = false;

            if (sessions.Count == 0)
            {
                baselineCaptured = false;
                baselineUIActionsEnabled = null;
                baselineGameplayActionsEnabled = null;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 모든 입력 Session 소유권을 먼저 분리하고 기준 상태 적용 뒤 역순으로 종결한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private List<Exception> ReleaseAllSessions()
        {
            var errors = new List<Exception>();
            var releasingSessions = new List<SessionState>(sessions.Count);

            // 전체 해제 시작 시점의 Session만 분리해 callback에서 획득한 새 Session을 보존한다.
            for (var i = sessions.Count - 1; i >= 0; i--)
            {
                releasingSessions.Add(sessions[i]);
            }

            sessions.Clear();
            batchDepth = 0;
            applyPending = true;

            try
            {
                ApplyBaselineState();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
                ResetApplyStateAfterFailure();
            }

            // 입력 적용 성공 여부와 관계없이 분리된 Session은 Terminal 완료를 소유자에게 알린다.
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

            return errors;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 생존 Session 정책을 합성해 Action Map과 Cursor에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyEffectiveState()
        {
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

            var blocksGameplay = false;
            var hasReleaseBarrier = false;
            SessionState cursorSession = null;
            SessionState retainedCursorSession = null;

            for (var i = 0; i < sessions.Count; i++)
            {
                var sessionState = sessions[i];

                if (sessionState.Session.IsAwaitingRelease)
                {
                    hasReleaseBarrier = true;

                    if
                    (
                        sessionState.Session.RetainsCursorWhileAwaitingRelease &&
                        (retainedCursorSession == null ||
                        sessionState.Sequence > retainedCursorSession.Sequence)
                    )
                    {
                        retainedCursorSession = sessionState;
                    }

                    continue;
                }

                blocksGameplay |= sessionState.Session.Options.BlocksGameplayInput;

                if
                (
                    cursorSession == null ||
                    sessionState.Session.Options.InputPriority > cursorSession.Session.Options.InputPriority ||
                    (sessionState.Session.Options.InputPriority == cursorSession.Session.Options.InputPriority &&
                    sessionState.Sequence > cursorSession.Sequence)
                )
                {
                    cursorSession = sessionState;
                }
            }

            var effectiveCursorSession = retainedCursorSession ?? cursorSession;
            var cursorVisible = effectiveCursorSession != null
                ? effectiveCursorSession.Session.Options.ShowsCursor
                : baselineCursorVisible;
            var cursorLockMode = effectiveCursorSession != null
                ? effectiveCursorSession.Session.Options.CursorLockMode
                : baselineCursorLockMode;

            ApplyState
            (
                true,
                hasReleaseBarrier || blocksGameplay ? false : baselineGameplayEnabled,
                cursorVisible,
                cursorLockMode
            );

            applyPending = false;
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

            // Map 단위 Enable로 원래 꺼져 있던 Action까지 켜지 않도록 개별 상태를 복원한다.
            RestoreActionStates(uiActionMap, baselineUIActionsEnabled);
            RestoreActionStates(gameplayActionMap, baselineGameplayActionsEnabled);
            appliedUIEnabled = AreAllActionsEnabled(uiActionMap);
            appliedGameplayEnabled = gameplayActionMap.enabled;

            if (baselineCursorVisible != appliedCursorVisible)
            {
                Cursor.visible = baselineCursorVisible;
                appliedCursorVisible = baselineCursorVisible;
            }

            if (baselineCursorLockMode != appliedCursorLockMode)
            {
                Cursor.lockState = baselineCursorLockMode;
                appliedCursorLockMode = baselineCursorLockMode;
            }

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
        /// 캡처한 Action별 활성 상태를 Action Map에 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void RestoreActionStates
        (
            InputActionMap map,
            bool[] states
        )
        {
            map.Disable();
            var actions = map.actions;

            for (var i = 0; i < actions.Count; i++)
            {
                if (states[i])
                {
                    actions[i].Enable();
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 변경된 값만 실제 Action Map과 Cursor에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyState
        (
            bool uiEnabled,
            bool gameplayEnabled,
            bool cursorVisible,
            CursorLockMode cursorLockMode
        )
        {
            if (uiEnabled != appliedUIEnabled)
            {
                SetMapEnabled(uiActionMap, uiEnabled);
                appliedUIEnabled = uiEnabled;
            }

            if (gameplayEnabled != appliedGameplayEnabled)
            {
                if (gameplayEnabled)
                {
                    // 차단 해제 시 Map 전체가 아니라 Session 직전의 Action별 상태를 복원한다.
                    RestoreActionStates(gameplayActionMap, baselineGameplayActionsEnabled);
                }
                else
                {
                    SetMapEnabled(gameplayActionMap, false);
                }

                appliedGameplayEnabled = gameplayEnabled;
            }

            if (cursorVisible != appliedCursorVisible)
            {
                Cursor.visible = cursorVisible;
                appliedCursorVisible = cursorVisible;
            }

            if (cursorLockMode != appliedCursorLockMode)
            {
                Cursor.lockState = cursorLockMode;
                appliedCursorLockMode = cursorLockMode;
            }
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

            appliedUIEnabled = AreAllActionsEnabled(uiActionMap);
            appliedGameplayEnabled = baselineGameplayEnabled;
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
        /// 수행된 Input Action의 장치가 바뀌면 마지막 입력 장치를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleActionChange
        (
            object changedObject,
            InputActionChange change
        )
        {
            if (change != InputActionChange.ActionPerformed || changedObject is not InputAction action)
            {
                return;
            }

            if
            (
                !ReferenceEquals(action.actionMap, uiActionMap) &&
                !ReferenceEquals(action.actionMap, gameplayActionMap)
            )
            {
                return;
            }

            var device = action.activeControl?.device;

            if (device == null || ReferenceEquals(device, LastInputDevice)) return;

            LastInputDevice = device;

            try
            {
                OnLastInputDeviceChanged?.Invoke(device);
            }
            catch (Exception exception)
            {
                // Input System 전역 callback은 구독자 예외를 외부 루프까지 전파하지 않는다.
                Debug.LogException(exception, this);
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

    #region IDisposable

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
            var errors = new List<Exception>();

            if (wasInitialized)
            {
                errors.AddRange(ReleaseAllSessions());
                InputSystem.onActionChange -= HandleActionChange;
            }

            releaseActions.Clear();
            sessions.Clear();
            sessionsReadyForRelease.Clear();
            baselineUIActionsEnabled = null;
            baselineGameplayActionsEnabled = null;
            inputModule = null;
            uiActionMap = null;
            gameplayActionMap = null;
            OnLastInputDeviceChanged = null;
            IsInitialized = false;

            if (errors.Count > 0)
            {
                throw new AggregateException("Input System Screen Input Driver 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
