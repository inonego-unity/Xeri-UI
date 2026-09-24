/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowTraySource.cs
수정일 : 2026-09-20

# 설명
Registry의 최소화된 Xeri 윈도우 목록을 공통 Tray entry 목록으로 공급한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
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
        private bool isRegistryBound = false;
        private bool isDisposed = false;

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
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.mapper = mapper ?? new XeriWindowTrayMapper();

            try
            {
                BindRegistry();
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };
                UnbindRegistry(errors);

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "Window Tray Source Registry 연결과 롤백이 실패했습니다.",
                    errors
                );
            }
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
            ThrowIfDisposed();

            var entries = new List<XeriTrayEntry>();

            SynchronizeOrder();

            foreach (var id in order)
            {
                if (!registry.TryGetHandle(id, out var handle)) continue;
                if (!registry.TryGetRecord(handle, out var record)) continue;
                if (record.State != XeriWindowState.Minimized) continue;

                var entry = mapper.Map(record, handle);

                if (entry == null) continue;

                entry.IsActive = ReferenceEquals(registry.ActiveHandle, handle);

                if (registry.TryGetController(handle, out var controller))
                {
                    entry.CanClose = controller.Options.CanClose;
                }

                entries.Add(entry);
            }

            return entries;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Tray entry payload의 handle로 윈도우를 최소화 이전 표시 상태로 복구한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void Restore(XeriTrayEntry entry)
        {
            ThrowIfDisposed();

            if (entry?.Payload is not XeriWindowHandle handle) return;

            registry.Restore(handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry payload의 handle로 윈도우를 닫는다.
        /// </summary>
        // ------------------------------------------------------------
        public void Close(XeriTrayEntry entry)
        {
            ThrowIfDisposed();

            if (entry?.Payload is not XeriWindowHandle handle) return;

            registry.Close(handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 표시 순서를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        public void MoveEntry(XeriWindowHandle handle, int targetIndex)
        {
            ThrowIfDisposed();
            if (!registry.TryGetRecord(handle, out var record)) return;
            if (record.State != XeriWindowState.Minimized) return;

            SynchronizeOrder();

            var sourceIndex = order.IndexOf(record.ID);

            if (sourceIndex < 0) return;

            targetIndex = ClampIndex(targetIndex, order.Count);
            if (sourceIndex == targetIndex) return;

            order.RemoveAt(sourceIndex);
            order.Insert(targetIndex, record.ID);

            NotifyReloadRequired();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 이벤트 구독을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            var errors = new List<Exception>();
            UnbindRegistry(errors);

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    "Window Tray Source Registry 해제가 실패했습니다.",
                    errors
                );
            }
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
            isRegistryBound = true;
            registry.OnCollectionChange += OnRegistryChange;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 변경 이벤트 연결을 한 번 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnbindRegistry(List<Exception> errors)
        {
            if (!isRegistryBound) return;

            isRegistryBound = false;

            try
            {
                registry.OnCollectionChange -= OnRegistryChange;
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Registry의 minimized window 목록과 Tray 표시 순서를 동기화한다.
        /// </summary>
        // ----------------------------------------------------------------------
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Registry record 순서 기준으로 minimized window ID 목록을 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private List<string> CreateMinimizedIDList()
        {
            var ids = new List<string>();

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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Reload observer를 독립적으로 호출하고 수집된 오류를 한 번 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void NotifyReloadRequired()
        {
            var handlers = OnReloadRequired;
            if (handlers == null) return;

            var errors = new List<Exception>();

            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, EventArgs.Empty);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "Window Tray reload 이벤트 처리 중 오류가 발생했습니다.",
                errors
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종료된 Source 사용을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(XeriWindowTraySource));
            }
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
            if (isDisposed) return;

            NotifyReloadRequired();
        }

    #endregion

    }
}
