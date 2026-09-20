/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ModalSession.cs
수정일 : 2026-09-17
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

    #region 소유 lifetime

        // ------------------------------------------------------------
        /// <summary>
        /// Modal과 함께 소유한 lifetime을 등록 역순으로 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ReleaseOwnedLifetimes()
        {
            var errors = new List<Exception>();

            for (var index = ownedLifetimes.Count - 1; index >= 0; index--)
            {
                var lifetime = ownedLifetimes[index];
                ownedLifetimes.RemoveAt(index);

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
        /// Modal의 공개 Stack 소유권이 종료됐음을 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void MarkStackReleased()
        {
            owner = null;
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// Modality Stack 등록과 함께 소유한 lifetime을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (owner == null) return;

            var current = owner;
            owner = null;
            current.Release(this);
        }

    #endregion

    }
}
