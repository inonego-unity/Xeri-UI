/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowTraySource.cs
수정일 : 2026-10-03

# 설명
Registry의 live Xeri Window를 상태 필터에 따라 공통 Tray entry 목록으로 공급한다.
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

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Source가 Tray entry로 포함하는 live Window 상태 집합.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowTrayStateMask IncludedStates => includedStates;

        private readonly XeriWindowTrayStateMask includedStates = XeriWindowTrayStateMask.All;
        private readonly IXeriWindowRegistry registry = null;
        private readonly XeriWindowTrayMapper mapper = null;
        private readonly List<string> order = new();
        private bool isCollectionBound = false;
        private bool isActiveBound = false;
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
            XeriWindowTrayStateMask includedStates = XeriWindowTrayStateMask.All,
            XeriWindowTrayMapper mapper = null
        ) : base()
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            ValidateIncludedStates(includedStates);
            this.includedStates = includedStates;
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// 설정된 상태 집합에 포함되는 live Window를 Tray entry 목록으로 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public IReadOnlyList<XeriTrayEntry> GetEntries()
        {
            ThrowIfDisposed();

            var entries = new List<XeriTrayEntry>();

            SynchronizeOrder();

            foreach (var id in order)
            {
                if (!registry.TryGetHandle(id, out var handle)) continue;
                if (!registry.TryGetRecord(handle, out var record)) continue;
                if (!Includes(record.State)) continue;

                var entry = mapper.Map(record, handle);

                if (entry == null) continue;

                entry.IsActive = ReferenceEquals(registry.ActiveHandle, handle);
                entry.IsVisible = record.State == XeriWindowState.Normal ||
                                  record.State == XeriWindowState.Maximized;

                if (registry.TryGetController(handle, out var controller))
                {
                    entry.CanClose = controller.Options.CanClose;
                }

                entries.Add(entry);
            }

            return entries;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Tray entry payload의 Window를 활성화한다.
        /// <br/> Minimized Window는 이전 표시 상태로 복구한 뒤 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Activate(XeriTrayEntry entry)
        {
            ThrowIfDisposed();

            if (entry?.Payload is not XeriWindowHandle handle) return;
            if (!registry.TryGetController(handle, out var controller)) return;

            if (controller.EffectiveState == XeriWindowState.Minimized)
            {
                registry.Restore(handle);
                return;
            }

            if (controller.EffectiveState == XeriWindowState.Closed) return;

            registry.Focus(handle);
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
            if (!Includes(record.State)) return;

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
            // custom event accessor가 handler를 등록한 뒤 throw해도 constructor rollback이 제거를 시도할 수 있게 먼저 표시한다.
            isCollectionBound = true;
            registry.OnCollectionChange += OnRegistryCollectionChange;

            isActiveBound = true;
            registry.OnActiveChange += OnRegistryActiveChange;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 변경 이벤트 연결을 한 번씩 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnbindRegistry(List<Exception> errors)
        {
            if (isActiveBound)
            {
                isActiveBound = false;

                try
                {
                    registry.OnActiveChange -= OnRegistryActiveChange;
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (isCollectionBound)
            {
                isCollectionBound = false;

                try
                {
                    registry.OnCollectionChange -= OnRegistryCollectionChange;
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry의 포함 대상 Window 목록과 Tray 표시 순서를 동기화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void SynchronizeOrder()
        {
            var includedIDs = CreateIncludedIDList();

            for (var i = order.Count - 1; i >= 0; i--)
            {
                if (includedIDs.Contains(order[i])) continue;

                order.RemoveAt(i);
            }

            foreach (var id in includedIDs)
            {
                if (order.Contains(id)) continue;

                order.Add(id);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Registry record 순서 기준으로 포함 대상 Window ID 목록을 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private List<string> CreateIncludedIDList()
        {
            var ids = new List<string>();

            foreach (var record in registry.Records)
            {
                if (!Includes(record.State)) continue;

                ids.Add(record.ID);
            }

            return ids;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window 상태가 이 Source의 포함 대상인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool Includes(XeriWindowState state)
        {
            return state switch
            {
                XeriWindowState.Normal =>
                    (includedStates & XeriWindowTrayStateMask.Normal) != XeriWindowTrayStateMask.None,
                XeriWindowState.Minimized =>
                    (includedStates & XeriWindowTrayStateMask.Minimized) != XeriWindowTrayStateMask.None,
                XeriWindowState.Maximized =>
                    (includedStates & XeriWindowTrayStateMask.Maximized) != XeriWindowTrayStateMask.None,
                _ => false,
            };
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Tray 상태 집합이 지원하는 live Window 상태 비트만 포함하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ValidateIncludedStates(XeriWindowTrayStateMask includedStates)
        {
            var invalidStates = includedStates & ~XeriWindowTrayStateMask.All;

            if (invalidStates != XeriWindowTrayStateMask.None)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(includedStates),
                    includedStates,
                    "Window Tray 상태 집합에는 Normal, Minimized, Maximized만 포함할 수 있습니다."
                );
            }
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
        /// Registry collection 변경을 Tray reload 요청으로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnRegistryCollectionChange(object sender, EventArgs e)
        {
            if (isDisposed) return;

            NotifyReloadRequired();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry active Window 변경을 Tray reload 요청으로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnRegistryActiveChange(object sender, XeriWindowEventArgs e)
        {
            if (isDisposed) return;

            NotifyReloadRequired();
        }

    #endregion

    }
}
