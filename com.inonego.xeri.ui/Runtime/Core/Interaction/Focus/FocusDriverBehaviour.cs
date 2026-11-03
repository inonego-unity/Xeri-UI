/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FocusDriverBehaviour.cs
수정일 : 2026-11-04
# 설명
Unity Focus backend의 선택 가능 시점과 Layer 등록·대칭 해제를 공통 Driver에 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Unity Focus backend의 공통 Component 계약.
    /// </summary>
    // ============================================================
    public abstract class FocusDriverBehaviour : MonoBehaviour, IFocusDriver
    {

    #region 필드

        private readonly Dictionary<IPresentationLayerDriver, int> layerRegistrations = new();

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// backend의 native Focus 대상이 바뀌었을 때 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        internal event Action<FocusDriverBehaviour> OnFocusChanged = null;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 대상이 이 backend이 다루는 Focus 타입인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract bool CanSelect(object target);

        // ------------------------------------------------------------
        /// <summary>
        /// Native 선택 트랜잭션이 진행 중인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        internal virtual bool IsSelectionBlocked => false;

        // ------------------------------------------------------------
        /// <summary>
        /// backend의 Host 구성을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ValidateConfiguration()
        {
            if (!enabled)
            {
                throw new InvalidOperationException
                (
                    $"{GetType().Name} Focus Driver가 비활성 상태입니다."
                );
            }

            ValidateBackendConfiguration();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation Layer가 등록되었음을 backend에 알린다.
        /// </summary>
        // ------------------------------------------------------------
        internal IDisposable RegisterLayer(IPresentationLayerDriver driver)
        {
            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            if (layerRegistrations.TryGetValue(driver, out var count))
            {
                if (count == 0)
                {
                    throw new InvalidOperationException("Focus Layer 등록이 아직 진행 중입니다.");
                }

                layerRegistrations[driver] = checked(count + 1);
                return new Lease(() => ReleaseLayerRegistration(driver));
            }

            layerRegistrations.Add(driver, 0);
            try
            {
                HandleLayerRegistered(driver);
                layerRegistrations[driver] = 1;
            }
            catch (Exception exception)
            {
                layerRegistrations.Remove(driver);

                try
                {
                    HandleLayerUnregistered(driver);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Focus backend Layer 등록과 자체 rollback이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }

            return new Lease(() => ReleaseLayerRegistration(driver));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 마지막 등록 Lease를 반환할 때만 backend 구독을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReleaseLayerRegistration(IPresentationLayerDriver driver)
        {
            if (!layerRegistrations.TryGetValue(driver, out var count)) return;

            if (count > 1)
            {
                layerRegistrations[driver] = count - 1;
                return;
            }

            layerRegistrations.Remove(driver);
            HandleLayerUnregistered(driver);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// backend별 Host 구성을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        protected virtual void ValidateBackendConfiguration()
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// backend이 필요한 Presentation Layer 정보를 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        protected virtual void HandleLayerRegistered(IPresentationLayerDriver driver)
        {
            // NONE
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Presentation Layer registration lifetime이 끝났음을 backend에 알린다.
        /// </summary>
        // ----------------------------------------------------------------------
        protected virtual void HandleLayerUnregistered(IPresentationLayerDriver driver)
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// native Focus 변경을 공통 Driver에 알린다.
        /// </summary>
        // ------------------------------------------------------------
        protected void NotifyFocusChanged()
        {
            OnFocusChanged?.Invoke(this);
        }

    #endregion

    #region 포커스 드라이버 구현

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 선택된 Focus 대상.
        /// </summary>
        // ------------------------------------------------------------
        public abstract object Current { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 대상이 현재 선택 가능한지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract bool IsValid(object target);

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 대상을 현재 Focus로 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract void Select(object target);

        // ------------------------------------------------------------
        /// <summary>
        /// 명시적 대상이 없을 때 사용할 대체 Focus를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract object FindFallback();

    #endregion

    }
}
