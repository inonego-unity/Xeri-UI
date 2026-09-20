/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowTraySource.cs
수정일 : 2026-09-20

# 설명
Registry의 최소화된 Xeri 윈도우 목록을 공통 Tray entry 목록으로 공급한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using inonego.Xeri.UI.Tray;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Registry 기반 Window Tray source.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowTraySource : IXeriTraySource, IDisposable
    {

    #region 필드

        private readonly IXeriWindowRegistry registry = null;
        private readonly XeriWindowTrayMapper mapper = null;
        private readonly List<string> order = new();

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 목록 재조회가 필요한 시점에 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler OnReloadRequired = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Window Tray source를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowTraySource
        (
            IXeriWindowRegistry registry,
            XeriWindowTrayMapper mapper = null
        ) : base()
        {
            this.registry = registry;
            this.mapper   = mapper ?? new XeriWindowTrayMapper();

            BindRegistry();
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 최소화된 윈도우를 Tray entry 목록으로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<XeriTrayEntry> GetEntries()
        {
            var entries = new List<XeriTrayEntry>();

            if (registry == null) return entries;

            SynchronizeOrder();

            foreach (var id in order)
            {
                if (!registry.TryGetHandle(id, out var handle)) continue;
                if (!registry.TryGetRecord(handle, out var record)) continue;
                if (record.State != XeriWindowState.Minimized) continue;

                entries.Add(mapper.Map(record, handle));
            }

            return entries;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry payload의 handle로 윈도우를 최소화 이전 표시 상태로 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Restore(XeriTrayEntry entry)
        {
            if (entry?.Payload is not XeriWindowHandle handle) return;

            registry?.Restore(handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry payload의 handle로 윈도우를 닫는다.
        /// </summary>
        // ------------------------------------------------------------
        public void Close(XeriTrayEntry entry)
        {
            if (entry?.Payload is not XeriWindowHandle handle) return;

            registry?.Close(handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 표시 순서를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        public void MoveEntry(XeriWindowHandle handle, int targetIndex)
        {
            if (registry == null) return;
            if (!registry.TryGetRecord(handle, out var record)) return;
            if (record.State != XeriWindowState.Minimized) return;

            SynchronizeOrder();

            var sourceIndex = order.IndexOf(record.ID);

            if (sourceIndex < 0) return;

            targetIndex = ClampIndex(targetIndex, order.Count);
            if (sourceIndex == targetIndex) return;

            order.RemoveAt(sourceIndex);
            order.Insert(targetIndex, record.ID);

            OnReloadRequired?.Invoke(this, EventArgs.Empty);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 이벤트 구독을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            UnbindRegistry();
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 변경 이벤트를 reload 요청으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BindRegistry()
        {
            if (registry == null) return;

            registry.OnCollectionChange += OnRegistryChange;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 변경 이벤트 연결을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnbindRegistry()
        {
            if (registry == null) return;

            registry.OnCollectionChange -= OnRegistryChange;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry의 minimized window 목록과 Tray 표시 순서를 동기화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void SynchronizeOrder()
        {
            var minimizedIDs = CreateMinimizedIDList();

            for (var i = order.Count - 1; i >= 0; i--)
            {
                if (minimizedIDs.Contains(order[i])) continue;

                order.RemoveAt(i);
            }

            foreach (var id in minimizedIDs)
            {
                if (order.Contains(id)) continue;

                order.Add(id);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry record 순서 기준으로 minimized window ID 목록을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private List<string> CreateMinimizedIDList()
        {
            var ids = new List<string>();

            if (registry == null) return ids;

            foreach (var record in registry.Records)
            {
                if (record.State != XeriWindowState.Minimized) continue;

                ids.Add(record.ID);
            }

            return ids;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Index를 현재 entry 목록 범위 안으로 보정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int ClampIndex(int index, int count)
        {
            if (count <= 0) return 0;
            if (index < 0) return 0;
            if (index >= count) return count - 1;

            return index;
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 변경을 Tray reload 요청으로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnRegistryChange(object sender, EventArgs e)
        {
            OnReloadRequired?.Invoke(this, EventArgs.Empty);
        }

    #endregion

    }
}
