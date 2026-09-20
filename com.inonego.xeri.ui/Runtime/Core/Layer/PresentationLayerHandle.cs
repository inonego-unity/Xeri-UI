/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayerHandle.cs
수정일 : 2026-09-17
# 설명
Presentation Layer 등록 소유권과 활성 소비자 수명을 연결하는 Handle을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Presentation Layer 등록 소유권 Handle.
    /// </summary>
    // ============================================================
    public sealed class PresentationLayerHandle : IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 Layer ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ID { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Handle이 등록 소유권을 해제했는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed => entry == null;

        // ------------------------------------------------------------
        /// <summary>
        /// Layer를 사용하는 활성 소비자가 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool HasConsumers => entry != null && entry.ConsumerCount > 0;

        private PresentationLayerRegistry owner = null;
        private PresentationLayerRegistry.Entry entry = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Layer 등록 Handle을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal PresentationLayerHandle
        (
            PresentationLayerRegistry owner,
            PresentationLayerRegistry.Entry entry
        ) : base()
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.entry = entry ?? throw new ArgumentNullException(nameof(entry));
            ID = entry.Asset.ID;
        }

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// Registry가 등록 전체를 종료할 때 Handle을 Terminal 상태로 전환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void MarkDisposed()
        {
            owner = null;
            entry = null;
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 소비자가 없을 때 Layer 등록을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (entry == null) return;

            // 소비 중인 Layer를 제거하면 View 수명이 끊기므로 상태 변경 전에 거부한다.
            if (entry.ConsumerCount > 0)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{ID}'에 활성 소비자가 남아 있습니다."
                );
            }

            var currentOwner = owner;
            var currentEntry = entry;

            owner = null;
            entry = null;
            currentOwner.Unregister(currentEntry);
        }

    #endregion

    }
}
