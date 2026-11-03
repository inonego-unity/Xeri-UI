/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIFocusDriver.cs
수정일 : 2026-11-04
# 설명
같은 Host의 Focus Driver Component를 하나의 Runtime Focus 계약으로 조립한다.
명시적 선택, native Focus 변경과 유실 알림을 직렬화하여 최신 상태를 Runtime에 전달한다.
Presentation Layer binding은 disposable registration lifetime으로 대칭 관리한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 동일 Host의 Focus Driver를 조립하는 공통 Driver.
    /// </summary>
    // ============================================================
    public sealed class UIFocusDriver : MonoBehaviour, IFocusDriver
    {

    #region 내부 데이터

        private enum FocusChangeKind { None, Selection, Native, FocusLoss }

        private sealed class LayerBinding : IDisposable
        {
            private readonly List<IDisposable> handles = null;

            public LayerBinding(List<IDisposable> handles)
            {
                this.handles = handles ?? throw new ArgumentNullException(nameof(handles));
            }

            public void Dispose()
            {
                var errors = new ExceptionBuffer();

                for (var index = handles.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        handles[index]?.Dispose();
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }

                handles.Clear();

                if (errors.Count > 0)
                {
                    throw errors.CreateAggregateException("Focus Layer binding 해제가 실패했습니다.");
                }
            }
        }

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 활성 Focus Driver에서 선택된 대상.
        /// </summary>
        // ------------------------------------------------------------
        public object Current
        {
            get
            {
                if (currentDriver == null) return null;

                var current = currentDriver.Current;
                return currentDriver.IsValid(current) ? current : null;
            }
        }

        private FocusDriverBehaviour[] drivers = Array.Empty<FocusDriverBehaviour>();
        private FocusDriverBehaviour currentDriver = null;
        private bool isInitialized = false;
        private bool isProcessingFocusChange = false;
        private FocusChangeKind pendingFocusChange = FocusChangeKind.None;
        private FocusDriverBehaviour pendingNativeDriver = null;
        private object pendingFocusTarget = null;
        private bool focusLossEvaluationRequested = false;
        private int focusChangeFrame = -1;

    #endregion

    #region 이벤트

        // ----------------------------------------------------------------------
        /// <summary>
        /// backend Focus 이동이 안정화된 뒤 유효한 현재 대상 또는 null을 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal event Action<object> OnFocusChanged = null;

    #endregion

    #region 초기화

        // ----------------------------------------------------------------------
        /// <summary>
        /// Host의 Focus backend 구성을 검증한 뒤 native 변경 알림을 구독한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void Initialize()
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("UI Focus Driver가 이미 초기화됐습니다.");
            }

            if (isProcessingFocusChange)
            {
                throw new InvalidOperationException("UI Focus Driver의 이전 Focus 변경이 아직 종료되지 않았습니다.");
            }

            if (!enabled)
            {
                throw new InvalidOperationException("UI Focus Driver가 비활성 상태입니다.");
            }

            var availableDrivers = GetComponents<FocusDriverBehaviour>();

            if (availableDrivers.Length == 0)
            {
                throw new InvalidOperationException
                (
                    "UI Focus Host에 Focus Driver Component가 없습니다."
                );
            }

            // 구성 검증이 실패하면 아직 어떤 native 이벤트도 구독하지 않는다.
            for (var index = 0; index < availableDrivers.Length; index++)
            {
                availableDrivers[index].ValidateConfiguration();
            }

            drivers = availableDrivers;

            for (var index = 0; index < drivers.Length; index++)
            {
                drivers[index].OnFocusChanged += HandleFocusChanged;
            }

            isInitialized = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 수명이 소유한 native Focus 구독과 현재 선택을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Release()
        {
            isInitialized = false;
            pendingFocusChange = FocusChangeKind.None;
            pendingNativeDriver = null;
            pendingFocusTarget = null;

            for (var index = 0; index < drivers.Length; index++)
            {
                if (drivers[index] != null)
                {
                    drivers[index].OnFocusChanged -= HandleFocusChanged;
                }
            }

            drivers = Array.Empty<FocusDriverBehaviour>();
            currentDriver = null;
            focusLossEvaluationRequested = false;
            focusChangeFrame = -1;
            OnFocusChanged = null;
        }

    #endregion

    #region 레이어 연결

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Presentation Layer를 관련 Focus backend에 등록하고 대칭 해제 binding을 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal IDisposable BindLayer(IPresentationLayerDriver driver)
        {
            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            if (!isInitialized)
            {
                throw new InvalidOperationException("UI Focus Driver가 초기화되지 않았습니다.");
            }

            var handles = new List<IDisposable>(drivers.Length);

            try
            {
                for (var index = 0; index < drivers.Length; index++)
                {
                    handles.Add(drivers[index].RegisterLayer(driver));
                }

                return new LayerBinding(handles);
            }
            catch (Exception exception)
            {
                var errors = new ExceptionBuffer(exception);

                for (var index = handles.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        handles[index]?.Dispose();
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
                    "Focus Layer binding과 등록 롤백이 모두 실패했습니다."
                );
            }
        }

    #endregion

    #region 포커스 드라이버 구현

        // ------------------------------------------------------------
        /// <summary>
        /// 대상을 다루는 Driver에서 현재 선택 가능 여부를 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsValid(object target)
        {
            var driver = FindDriver(target);
            return driver != null && driver.IsValid(target);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Native 변경 알림과 같은 순서로 명시적 Focus 선택을 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Select(object target)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("UI Focus Driver가 초기화되지 않았습니다.");
            }

            QueueFocusChange(FocusChangeKind.Selection, null, target);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 Driver를 우선하고 연결된 Driver의 유효한 대체 Focus를 찾는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public object FindFallback()
        {
            var fallback = currentDriver?.FindFallback();

            if (currentDriver != null && currentDriver.IsValid(fallback))
            {
                return fallback;
            }

            for (var i = 0; i < drivers.Length; i++)
            {
                var driver = drivers[i];

                if (ReferenceEquals(driver, currentDriver)) continue;

                fallback = driver.FindFallback();

                if (driver.IsValid(fallback))
                {
                    return fallback;
                }
            }

            return null;
        }

    #endregion

    #region 메서드

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 명시적 선택, Native 변경 및 Focus 유실을 하나의 처리 흐름으로 직렬화한다.
        /// <br/> 콜백이 새 변경을 요청하면 이전 처리의 나머지 작업보다 최신 요청을 우선한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void QueueFocusChange
        (
            FocusChangeKind kind,
            FocusDriverBehaviour nativeDriver,
            object target
        )
        {
            if (!isInitialized) return;

            pendingFocusChange = kind;
            pendingNativeDriver = nativeDriver;
            pendingFocusTarget = target;

            ProcessPendingFocusChange();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Native backend의 선택 트랜잭션이 진행 중이면 공통 변경을 보류한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool IsNativeSelectionBlocked()
        {
            for (var index = 0; index < drivers.Length; index++)
            {
                if (drivers[index].IsSelectionBlocked) return true;
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 미완료 Focus 요청을 Native 선택 종료 뒤 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ProcessPendingFocusChange()
        {
            if (isProcessingFocusChange) return;

            isProcessingFocusChange = true;
            var errors = new ExceptionBuffer();

            try
            {
                while (isInitialized && pendingFocusChange != FocusChangeKind.None)
                {
                    // Unity의 Native 선택이 끝나기 전에는 완료된 것으로 처리하지 않는다.
                    if
                    (
                        pendingFocusChange != FocusChangeKind.FocusLoss &&
                        IsNativeSelectionBlocked()
                    )
                    {
                        break;
                    }

                    var change = pendingFocusChange;
                    var driver = pendingNativeDriver;
                    var requestedTarget = pendingFocusTarget;
                    pendingFocusChange = FocusChangeKind.None;
                    pendingNativeDriver = null;
                    pendingFocusTarget = null;

                    try
                    {
                        switch (change)
                        {
                            case FocusChangeKind.Selection:
                                ApplyRequestedSelection(requestedTarget);
                                break;

                            case FocusChangeKind.Native:
                                ApplyNativeFocus(driver);
                                break;

                            case FocusChangeKind.FocusLoss:
                                EvaluateFocusLoss();
                                break;
                        }
                    }
                    catch (AggregateException exception)
                    {
                        errors.AddRange(exception.InnerExceptions);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }
            finally
            {
                isProcessingFocusChange = false;

                if (!isInitialized)
                {
                    pendingFocusChange = FocusChangeKind.None;
                    pendingNativeDriver = null;
                    pendingFocusTarget = null;
                }
            }

            ThrowFocusErrors("공통 Focus 변경 처리", errors);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 새 선택을 적용하고 실패 시 이전 Native Focus를 복원한다.
        /// <br/> 별도 최신 요청이나 종료가 생긴 경우 오래된 선택으로 복원하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplyRequestedSelection(object target)
        {
            var next = target != null ? FindDriver(target) : null;
            var previous = currentDriver;
            var previousTarget = Current;

            if (!isInitialized || pendingFocusChange != FocusChangeKind.None) return;

            currentDriver = next;
            focusLossEvaluationRequested = false;

            try
            {
                ApplySelection(next, target);
            }
            catch (Exception exception)
            {
                var errors = new ExceptionBuffer(exception);

                // 실패한 선택과 같은 Native 알림은 새로운 선택 요청이 아니다.
                if
                (
                    pendingFocusChange == FocusChangeKind.Native &&
                    ReferenceEquals(pendingNativeDriver, next) &&
                    ReferenceEquals(pendingFocusTarget, target)
                )
                {
                    pendingFocusChange = FocusChangeKind.None;
                    pendingNativeDriver = null;
                    pendingFocusTarget = null;
                }

                if (!isInitialized || pendingFocusChange != FocusChangeKind.None)
                {
                    throw;
                }

                currentDriver = previous;
                focusLossEvaluationRequested = false;

                try
                {
                    ApplySelection(previous, previousTarget);
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                if (errors.Count == 1)
                {
                    throw;
                }

                throw errors.CreateAggregateException
                (
                    "공통 Focus 적용과 Native rollback이 모두 실패했습니다."
                );
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 최신 Native 선택만 남기도록 다른 backend를 정리한 뒤 변경을 알린다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplyNativeFocus(FocusDriverBehaviour driver)
        {
            if (driver == null) return;

            // 다른 콜백이 변경한 Native 상태를 이전 이벤트의 snapshot으로 덮어쓰지 않는다.
            var current = driver.Current;
            var valid = driver.IsValid(current);

            if (!isInitialized || pendingFocusChange != FocusChangeKind.None) return;

            if (!valid)
            {
                // 중첩 알림이 이미 무효해졌다면 다음 Frame에 실제 Focus를 다시 판정한다.
                focusLossEvaluationRequested = true;
                focusChangeFrame = Time.frameCount;
                return;
            }

            currentDriver = driver;
            focusLossEvaluationRequested = false;
            var errors = new ExceptionBuffer();

            ClearDrivers(driver, ref errors);

            try
            {
                if (isInitialized && pendingFocusChange == FocusChangeKind.None)
                {
                    InvokeFocusChanged(current);
                }
            }
            catch (AggregateException exception)
            {
                errors.AddRange(exception.InnerExceptions);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            ThrowFocusErrors("Native Focus 변경 처리", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 대상 타입을 다루는 Focus Driver를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private FocusDriverBehaviour FindDriver(object target)
        {
            for (var i = 0; i < drivers.Length; i++)
            {
                if (drivers[i].CanSelect(target))
                {
                    return drivers[i];
                }
            }

            return null;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 선택 Driver만 남도록 다른 backend를 비운 뒤 목표 Focus를 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplySelection
        (
            FocusDriverBehaviour selected,
            object target
        )
        {
            var errors = new ExceptionBuffer();
            ClearDrivers(selected, ref errors);

            if (selected != null && isInitialized && pendingFocusChange == FocusChangeKind.None)
            {
                try
                {
                    selected.Select(target);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            ThrowFocusErrors("native Focus backend 적용", errors);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 명시적 선택과 Native 변경이 공유하는 backend 정리.
        /// <br/> 실패한 backend가 있어도 나머지를 처리하고 결과를 호출자에게 누적한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ClearDrivers
        (
            FocusDriverBehaviour except,
            ref ExceptionBuffer errors
        )
        {
            for (var i = 0; i < drivers.Length; i++)
            {
                if (!isInitialized || pendingFocusChange != FocusChangeKind.None) break;

                var driver = drivers[i];

                if (ReferenceEquals(driver, except)) continue;

                try
                {
                    driver.Select(null);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Runtime Focus observer를 독립적으로 호출하고 실패를 한 번 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void InvokeFocusChanged(object target)
        {
            var handlers = OnFocusChanged;
            if (handlers == null) return;

            var errors = new ExceptionBuffer();

            foreach (Action<object> handler in handlers.GetInvocationList())
            {
                if (!isInitialized || pendingFocusChange != FocusChangeKind.None) break;

                try
                {
                    handler.Invoke(target);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            ThrowFocusErrors("공통 Focus 변경 observer 처리", errors);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 수집한 Focus 오류를 원래 예외 또는 AggregateException으로 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ThrowFocusErrors
        (
            string message,
            ExceptionBuffer errors
        )
        {
            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException(message);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Native Focus 이동이 끝난 뒤 마지막 유효 선택 또는 유실을 알린다.
        /// </summary>
        // ------------------------------------------------------------
        private void EvaluateFocusLoss()
        {
            focusLossEvaluationRequested = false;

            // 명시적으로 비운 Focus는 Native 유실로 재해석하지 않는다.
            var driver = currentDriver;
            if (driver == null) return;

            var current = driver.Current;
            var valid = driver.IsValid(current);

            if
            (
                !isInitialized ||
                pendingFocusChange != FocusChangeKind.None ||
                !ReferenceEquals(currentDriver, driver)
            )
            {
                return;
            }

            InvokeFocusChanged(valid ? current : null);
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 유효한 Native Focus 변경은 공통 처리 흐름에 등록한다.
        /// <br/> FocusOut은 같은 Frame의 후속 FocusIn이 끝난 뒤 판정한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleFocusChanged(FocusDriverBehaviour driver)
        {
            if (!isInitialized) return;

            // 처리 중 들어온 명시적 선택도 이전 Native 피드백이 덮지 않는다.
            if (pendingFocusChange == FocusChangeKind.Selection)
            {
                ProcessPendingFocusChange();
                return;
            }

            var current = driver.Current;
            var valid = driver.IsValid(current);

            if (!isInitialized) return;

            if (valid)
            {
                QueueFocusChange(FocusChangeKind.Native, driver, current);
                return;
            }

            // 비활성 Driver의 FocusOut은 현재 Context의 Focus 유실이 아니다.
            if (!ReferenceEquals(currentDriver, driver)) return;

            focusLossEvaluationRequested = true;
            focusChangeFrame = Time.frameCount;
        }

    #endregion

    #region Unity 이벤트

        // ----------------------------------------------------------------------
        /// <summary>
        /// 같은 Frame의 native Focus 이동이 끝난 뒤 공통 Focus 유실을 판정한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void LateUpdate()
        {
            if (pendingFocusChange != FocusChangeKind.None)
            {
                ProcessPendingFocusChange();
                if (pendingFocusChange != FocusChangeKind.None) return;
            }

            if (!focusLossEvaluationRequested || Time.frameCount <= focusChangeFrame) return;

            QueueFocusChange(FocusChangeKind.FocusLoss, null, null);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Host 파괴 시 Focus Driver 구독과 Runtime 구독자를 분리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDestroy()
        {
            Release();
        }

    #endregion

    }
}
