/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ModalSession.cs
수정일 : 2026-10-05
# 설명
한 Presentation에 적용된 Modality Policy의 Stack 등록과 소유 lifetime을 묶는다.
Presentation 자체의 표현 상태와 Modal 상호작용 backend는 분리해 보관한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 한 Modal Policy 적용의 살아 있는 실행 수명.
    /// </summary>
    // ============================================================
    public sealed class ModalSession : IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Modal Policy가 적용되는 Presentation.
        /// </summary>
        // ------------------------------------------------------------
        public IPresentation Presentation { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Modal Stack 소유권이 종료되었는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed => owner == null;

        // ------------------------------------------------------------
        /// <summary>
        /// Modal 상호작용 top 상태를 적용할 backend.
        /// </summary>
        // ------------------------------------------------------------
        internal IModalInteractionDriver Interaction { get; }

        private ModalController owner = null;
        private readonly List<IDisposable> ownedLifetimes = new List<IDisposable>();
        private readonly List<IDisposable> focusLifetimes = new List<IDisposable>();

    #endregion

    #region 생성자

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Presentation, 상호작용 backend와 함께 종료할 lifetime을 Modal Session으로 묶는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal ModalSession
        (
            ModalController owner,
            IPresentation presentation,
            IModalInteractionDriver interaction,
            IEnumerable<IDisposable> ownedLifetimes
        ) : base()
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            Interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));

            if (ownedLifetimes == null) return;

            foreach (var lifetime in ownedLifetimes)
            {
                if (lifetime != null)
                {
                    this.ownedLifetimes.Add(lifetime);
                }
            }
        }

    #endregion

    #region 소유 수명

        // ------------------------------------------------------------
        /// <summary>
        /// Modal과 함께 반환할 콘텐츠 수명을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public THandle RegisterChild<THandle>(THandle handle)
        where THandle : class, IDisposable
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ModalSession));
            }

            AddOwnedLifetime(handle);
            return handle;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이전 Modal 활성화 뒤 반환할 Focus 수명을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void AddFocusLifetime(IDisposable lifetime)
        {
            AddLifetime(focusLifetimes, lifetime);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Modal과 함께 반환할 부가 수명을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void AddOwnedLifetime(IDisposable lifetime)
        {
            AddLifetime(ownedLifetimes, lifetime);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종료된 Session으로 전달된 수명도 즉시 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AddLifetime(List<IDisposable> lifetimes, IDisposable lifetime)
        {
            if (lifetime == null) return;

            if (owner != null)
            {
                lifetimes.Add(lifetime);
                return;
            }

            var ownershipFailure = new ObjectDisposedException(nameof(ModalSession));

            try
            {
                lifetime.Dispose();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException
                (
                    "종료된 Modal Session에 전달된 lifetime 반환이 실패했습니다.",
                    ownershipFailure,
                    cleanupException
                );
            }

            throw ownershipFailure;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Modal과 함께 소유한 lifetime을 등록 역순으로 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ReleaseOwnedLifetimes()
        {
            ReleaseLifetimes(ownedLifetimes);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이전 Modal이 입력 가능한 상태에서 Focus 수명을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ReleaseFocusLifetimes()
        {
            ReleaseLifetimes(focusLifetimes);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패한 항목과 무관하게 소유 수명을 역순으로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ReleaseLifetimes(List<IDisposable> lifetimes)
        {
            var errors = new List<Exception>();

            // 해제 실패도 terminal이므로 호출 전에 소유 목록에서 제거한다.
            for (var index = lifetimes.Count - 1; index >= 0; index--)
            {
                var lifetime = lifetimes[index];
                lifetimes.RemoveAt(index);

                try
                {
                    lifetime.Dispose();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    "Modal Session 소유 lifetime 해제가 실패했습니다.",
                    errors
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Modal의 Stack 소유권을 단일 해제 작업으로 이전한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool TryReleaseStack()
        {
            if (owner == null) return false;

            owner = null;
            return true;
        }

    #endregion

    #region 수명 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Modality Stack 등록과 함께 소유한 lifetime을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (owner == null) return;

            owner.Release(this);
        }

    #endregion

    }
}
