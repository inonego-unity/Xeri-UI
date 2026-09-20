/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenRegistrationHandle.cs
수정일 : 2026-09-17
# 설명
동적 Screen Source 등록 소유권을 해제하되 이미 열린 Session 수명은 건드리지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Screen Source 등록 소유권 Handle.
    /// </summary>
    // ============================================================
    public sealed class ScreenRegistrationHandle : IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 Screen ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ID { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 등록 소유권이 해제됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed => entry == null;

        private ScreenRegistry owner = null;
        private ScreenRegistry.Entry entry = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 등록 Handle을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal ScreenRegistrationHandle
        (
            ScreenRegistry owner,
            ScreenRegistry.Entry entry
        ) : base()
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.entry = entry ?? throw new ArgumentNullException(nameof(entry));
            ID = entry.Options.ID;
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
        /// 새 Open 조회에서 Screen Source 등록을 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (entry == null) return;

            var currentOwner = owner;
            var currentEntry = entry;

            owner = null;
            entry = null;
            currentOwner.Unregister(currentEntry);
        }

    #endregion

    }
}
