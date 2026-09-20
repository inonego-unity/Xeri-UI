/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIFocusDriver.cs
수정일 : 2026-09-17
# 설명
같은 Host의 Focus Driver Component를 하나의 Runtime Focus 계약으로 조립한다.
backend의 native Focus 변경을 모아 실제 Focus 유실만 Runtime에 전달한다.
========================================================================= BLOCK_HEADER_END */

using System;

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

        // ------------------------------------------------------------
        /// <summary>
        /// 동일 Host의 Focus Driver Component를 수집하고 구성을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Initialize()
        {
            if (!enabled)
            {
                throw new InvalidOperationException("UI Focus Driver가 비활성 상태입니다.");
            }

            CollectDrivers();

            if (drivers.Length == 0)
            {
                throw new InvalidOperationException
                (
                    "UI Focus Host에 Focus Driver Component가 없습니다."
                );
            }

            for (var i = 0; i < drivers.Length; i++)
            {
                drivers[i].ValidateConfiguration();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Host의 Focus Driver Component를 수집하고 native 변경 알림을 연결한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void CollectDrivers()
        {
            for (var i = 0; i < drivers.Length; i++)
            {
                if (drivers[i] != null)
                {
                    drivers[i].OnFocusChanged -= HandleFocusChanged;
                }
            }

            drivers = GetComponents<FocusDriverBehaviour>();

            for (var i = 0; i < drivers.Length; i++)
            {
                drivers[i].OnFocusChanged -= HandleFocusChanged;
                drivers[i].OnFocusChanged += HandleFocusChanged;
            }
        }

    #endregion

    #region Layer 연결

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation Layer를 관련 Focus Driver에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void RegisterLayer(IPresentationLayerDriver driver)
        {
            if (drivers.Length == 0)
            {
                CollectDrivers();
            }

            for (var i = 0; i < drivers.Length; i++)
            {
                drivers[i].RegisterLayer(driver);
            }
        }

    #endregion

    #region IFocusDriver

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
        /// 대상을 다루는 Driver만 남기고 나머지 native Focus를 비운다.
        /// </summary>
        // ------------------------------------------------------------
        public void Select(object target)
        {
            var next = target != null ? FindDriver(target) : null;

            // 권한을 먼저 확정해 native callback의 재진입이 이전 Driver를 복구하지 않게 한다.
            currentDriver = next;
            focusLossEvaluationRequested = false;
            ClearDrivers(next);

            next?.Select(target);
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

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 Driver를 제외한 native Focus를 비운다.
        /// </summary>
        // ------------------------------------------------------------
        private void ClearDrivers(FocusDriverBehaviour except)
        {
            for (var i = 0; i < drivers.Length; i++)
            {
                var driver = drivers[i];

                if (ReferenceEquals(driver, except)) continue;

                driver.Select(null);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// native Focus 이동이 종료된 뒤 유효한 대상이 없는지 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        private void EvaluateFocusLoss()
        {
            focusLossEvaluationRequested = false;

            // 명시적으로 Focus를 비운 Context는 native 복구 대상이 아니다.
            if (currentDriver == null) return;

            var current = currentDriver.Current;

            if (currentDriver.IsValid(current))
            {
                OnFocusChanged?.Invoke(current);
                return;
            }

            OnFocusChanged?.Invoke(null);
        }

    #endregion

    #region 이벤트 핸들러

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 유효한 native Focus가 들어온 Driver를 현재 Driver로 확정하고
        /// <br/> 유실은 같은 Frame의 FocusOut·FocusIn이 끝난 뒤 판정한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleFocusChanged(FocusDriverBehaviour driver)
        {
            var current = driver.Current;

            if (driver.IsValid(current))
            {
                // 실제 Focus를 얻은 Driver가 권한을 소유하며 다른 native 선택은 남기지 않는다.
                currentDriver = driver;
                focusLossEvaluationRequested = false;
                ClearDrivers(driver);
                OnFocusChanged?.Invoke(current);
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
        /// Host의 Focus Driver Component를 native 변경 추적 대상으로 수집한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void Awake()
        {
            CollectDrivers();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 같은 Frame의 native Focus 이동이 끝난 뒤 공통 Focus 유실을 판정한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void LateUpdate()
        {
            if (!focusLossEvaluationRequested || Time.frameCount <= focusChangeFrame) return;

            EvaluateFocusLoss();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Host 파괴 시 Focus Driver 구독과 Runtime 구독자를 분리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDestroy()
        {
            for (var i = 0; i < drivers.Length; i++)
            {
                if (drivers[i] != null)
                {
                    drivers[i].OnFocusChanged -= HandleFocusChanged;
                }
            }

            drivers = Array.Empty<FocusDriverBehaviour>();
            currentDriver = null;
            focusLossEvaluationRequested = false;
            OnFocusChanged = null;
        }

    #endregion

    }
}
