/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ModalController.cs
수정일 : 2026-09-17
# 설명
Modality Policy Stack을 소유하고 상단 Session만 상호작용 가능하도록 backend 상태를 갱신한다.
각 Session은 Presentation과 상호작용 backend를 분리하며, 선택적 부가 lifetime은 Session이 함께 소유한다.
실패한 정리를 같은 Session으로 다시 시도하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Modal Stack과 상단 상호작용 상태를 소유한다.
    /// </summary>
    // ============================================================
    public sealed class ModalController : IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Modal Stack 항목 수.
        /// </summary>
        // ------------------------------------------------------------
        public int Count => stack.Count;

        private readonly List<ModalSession> stack = new List<ModalSession>();
        private bool isDisposed = false;
        private bool isOpening = false;

    #endregion

    #region 메서드

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Modal Driver를 Stack top에 추가하고 성공 시 소유 lifetime 소유권을 이전받는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public ModalSession Open
        (
            IPresentation presentation,
            IModalInteractionDriver interaction,
            params IDisposable[] ownedLifetimes
        )
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(ModalController));
            }

            if (isOpening)
            {
                throw new InvalidOperationException("다른 Modal Open 명령이 진행 중입니다.");
            }

            if (presentation == null)
            {
                throw new ArgumentNullException(nameof(presentation));
            }

            if (interaction == null)
            {
                throw new ArgumentNullException(nameof(interaction));
            }

            for (var i = 0; i < stack.Count; i++)
            {
                if (ReferenceEquals(stack[i].Interaction, interaction))
                {
                    throw new InvalidOperationException("같은 Modal Interaction backend를 중복으로 열 수 없습니다.");
                }
            }

            isOpening = true;

            try
            {
                return OpenCore(presentation, interaction, ownedLifetimes);
            }
            finally
            {
                isOpening = false;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 검증된 단일 Modal Open 명령을 Stack top에 적용하고,
        /// <br/> 성공 시 소유 lifetime 소유권을 이전받는다.
        /// </summary>
        // ------------------------------------------------------------
        private ModalSession OpenCore
        (
            IPresentation presentation,
            IModalInteractionDriver interaction,
            IDisposable[] ownedLifetimes
        )
        {
            var previous = stack.Count > 0
                ? stack[stack.Count - 1]
                : null;
            var driverSetTopAttempted = false;

            try
            {
                if (previous != null)
                {
                    previous.Interaction.SetTop(false);
                    ValidateOpenState(previous);
                }

                driverSetTopAttempted = true;
                interaction.SetTop(true);
                ValidateOpenState(previous);

                var session = new ModalSession
                (
                    this,
                    presentation,
                    interaction,
                    ownedLifetimes
                );
                stack.Add(session);
                return session;
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };

                if (driverSetTopAttempted)
                {
                    try
                    {
                        interaction.SetTop(false);
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }
                }

                if (!isDisposed && stack.Count > 0)
                {
                    var current = stack[stack.Count - 1];

                    try
                    {
                        current.Interaction.SetTop(true);
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

                throw new AggregateException("Modal 표시 시작과 롤백이 실패했습니다.", errors);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Modal backend 콜백 뒤 Controller와 기존 Stack 소유권이 유지되는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ValidateOpenState(ModalSession previous)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(ModalController));
            }

            var current = stack.Count > 0
                ? stack[stack.Count - 1]
                : null;

            if (!ReferenceEquals(current, previous))
            {
                throw new InvalidOperationException("Modal Open 중 기존 Stack이 변경됐습니다.");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Modal만 제거하고 남은 Stack top 상태를 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Release
        (
            ModalSession handle,
            bool removeFromStack = true
        )
        {
            if (isDisposed && removeFromStack) return;

            var index = stack.IndexOf(handle);

            var wasTop = index == stack.Count - 1;
            var previous = wasTop && index > 0
                ? stack[index - 1]
                : null;

            handle.MarkStackReleased();

            var errors = new List<Exception>();

            try
            {
                handle.Interaction.SetTop(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                handle.ReleaseOwnedLifetimes();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            // Driver와 소유 표시 수명 정리가 끝날 때까지 같은 Driver의 재등록을 막는다.
            if (removeFromStack)
            {
                stack.Remove(handle);
            }

            // 현재 Modal 정리 결과와 관계없이 남은 Stack top의 상호작용 상태를 복원한다.
            if
            (
                previous != null &&
                !isDisposed &&
                stack.Count > 0 &&
                ReferenceEquals(stack[stack.Count - 1], previous)
            )
            {
                try
                {
                    previous.Interaction.SetTop(true);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Modal 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    #region IDisposable

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 모든 Modal을 top부터 각각 한 번 해제한다.
        /// <br/> Stack과 Handle을 먼저 Terminal화하고 실패 항목을 재시도 대상으로 보관하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            var errors = new List<Exception>();

            try
            {
                // 개별 Modal이 Stack을 변경하지 않게 한 뒤 원본 Stack을 상단부터 정리한다.
                for (var i = stack.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        Release(stack[i], removeFromStack: false);
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
                stack.Clear();
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Modal Controller 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
