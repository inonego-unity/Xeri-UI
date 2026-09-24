/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenRegistry.cs
수정일 : 2026-10-07
# 설명
Screen Options, PresentationTarget과 Source를 stable string ID로 등록하고 새 Open 조회를 제공한다.
표시 Target은 Screen policy와 분리된 composition 정보로 Open 시 resolve한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Screen 등록과 조회를 소유하는 Registry.
    /// </summary>
    // ============================================================
    public sealed class ScreenRegistry : IDisposable
    {

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// Screen 등록 시점의 Options와 Source 참조를 묶는다.
        /// </summary>
        // ============================================================
        internal sealed class Entry
        {

        #region 필드

            // ------------------------------------------------------------
            /// <summary>
            /// Screen 등록 정책.
            /// </summary>
            // ------------------------------------------------------------
            public ScreenOptions Options { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Screen View를 표시할 local Layer 또는 Host destination.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationTarget Target { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Screen View와 Presenter를 공급하는 Source.
            /// </summary>
            // ------------------------------------------------------------
            public IScreenSource Source { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 이 등록 소유권을 나타내는 Handle.
            /// </summary>
            // ------------------------------------------------------------
            public ScreenRegistrationHandle Handle { get; set; }

        #endregion

        #region 생성자

            // ------------------------------------------------------------
            /// <summary>
            /// Screen 등록 Entry를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public Entry
            (
                ScreenOptions options,
                PresentationTarget target,
                IScreenSource source
            ) : base()
            {
                Options = options ?? throw new ArgumentNullException(nameof(options));

                if (!target.IsValid)
                {
                    throw new ArgumentException
                    (
                        "Screen Presentation Target이 유효하지 않습니다.",
                        nameof(target)
                    );
                }

                Target = target;
                Source = source ?? throw new ArgumentNullException(nameof(source));
            }

        #endregion

        }

    #endregion

    #region 필드

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private bool ownerControlsLifetime = false;
        private bool isDisposed = false;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Options와 Source를 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenRegistrationHandle Register
        (
            ScreenOptions options,
            PresentationTarget target,
            IScreenSource source
        )
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(ScreenRegistry));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (entries.ContainsKey(options.ID))
            {
                throw new InvalidOperationException
                (
                    $"Screen '{options.ID}'가 이미 등록되어 있습니다."
                );
            }

            var entry = new Entry(options, target, source);
            entries.Add(options.ID, entry);
            var handle = new ScreenRegistrationHandle(this, entry);
            entry.Handle = handle;
            return handle;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen ID가 새 Open 조회에 등록되어 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Contains(string id)
        {
            if (isDisposed || string.IsNullOrWhiteSpace(id)) return false;

            return entries.ContainsKey(id);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Controller가 사용할 Screen 등록 Entry를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool TryGet
        (
            string id,
            out Entry entry
        )
        {
            if (isDisposed || string.IsNullOrWhiteSpace(id))
            {
                entry = null;
                return false;
            }

            return entries.TryGetValue(id, out entry);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 등록 Handle이 소유한 Entry를 새 조회에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Unregister(Entry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (isDisposed) return;

            if (entries.TryGetValue(entry.Options.ID, out var current) && ReferenceEquals(current, entry))
            {
                ReleaseRegistration(entry);
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Screen 등록과 Handle의 소유 연결을 종료한다.
        /// <br/> Registry 전체 종료에서는 원본 목록을 순회한 뒤 한 번에 비우도록 등록 제거를 생략한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ReleaseRegistration
        (
            Entry entry,
            bool removeFromEntries = true
        )
        {
            if (removeFromEntries)
            {
                entries.Remove(entry.Options.ID);
            }

            entry.Handle?.MarkDisposed();
            entry.Handle = null;
        }

    #endregion

    #region 수명 해제

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 새 Screen 조회를 모두 제거하되 Source 자체는 해제하지 않는다.
        /// <br/> owner-controlled Registry는 직접 해제할 수 없다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            if (ownerControlsLifetime)
            {
                throw new InvalidOperationException
                (
                    "이 Screen Registry의 수명은 소유자가 관리합니다."
                );
            }

            DisposeCore();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이후 Registry 수명을 조립 owner만 종료하도록 고정한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SetOwnerControlledLifetime()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(ScreenRegistry));
            }

            ownerControlsLifetime = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// owner-controlled Registry를 소유자 종료 경로에서 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void DisposeFromOwner()
        {
            DisposeCore();
        }

        private void DisposeCore()
        {
            if (isDisposed) return;

            isDisposed = true;

            foreach (var entry in entries.Values)
            {
                ReleaseRegistration(entry, removeFromEntries: false);
            }

            entries.Clear();
        }

    #endregion

    }
}
