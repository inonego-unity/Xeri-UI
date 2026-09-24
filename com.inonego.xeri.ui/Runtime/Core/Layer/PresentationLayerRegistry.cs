/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayerRegistry.cs
수정일 : 2026-09-23

# 설명
PresentationSession scope에서 stable LayerID와 runtime backend를 등록하고 활성 소비자 수명을 추적한다.
Ordering과 authoring asset identity는 Registry 책임에 포함하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    internal sealed class PresentationLayerRegistry : IDisposable
    {

    #region 내부 데이터

        internal enum EntryState
        {
            Activating = 0,
            Available = 1,
            Releasing = 2,
            Released = 3,
        }

        internal sealed class Entry
        {
            public string ID { get; }
            public IPresentationLayerDriver Driver { get; }
            public int ConsumerCount { get; private set; }
            public EntryState State { get; set; }
            public PresentationLayerHandle Handle { get; set; }

            public Entry(string id, IPresentationLayerDriver driver)
            {
                ID = id;
                Driver = driver;
                State = EntryState.Activating;
            }

            public Lease AcquireUsage()
            {
                ConsumerCount++;
                return new Lease(ReleaseUsage);
            }

            private void ReleaseUsage()
            {
                ConsumerCount--;
            }
        }

    #endregion

    #region 필드

        internal bool IsDisposed => isDisposed;

        private bool isDisposed = false;

        internal bool HasConsumers
        {
            get
            {
                foreach (var entry in entries.Values)
                {
                    if (entry.ConsumerCount > 0) return true;
                }

                return false;
            }
        }

        private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    #endregion

    #region 등록

        internal PresentationLayerHandle Register
        (
            string layerID,
            IPresentationLayerDriver driver
        )
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(layerID))
            {
                throw new ArgumentException("Presentation Layer ID가 비어 있습니다.", nameof(layerID));
            }

            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            if (entries.ContainsKey(layerID))
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{layerID}'가 이미 등록되어 있습니다."
                );
            }

            if (!driver.Validate(out var error))
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{layerID}' 구성이 유효하지 않습니다. {error}"
                );
            }

            var entry = new Entry(layerID, driver);
            var handle = new PresentationLayerHandle(this, entry);
            entry.Handle = handle;
            entries.Add(layerID, entry);

            try
            {
                driver.SetActive(true);
                ThrowIfDisposed();
                entry.State = EntryState.Available;
                return handle;
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };

                if (entries.TryGetValue(layerID, out var current) && ReferenceEquals(current, entry))
                {
                    ReleaseRegistration(entry);
                }

                if (!isDisposed)
                {
                    try
                    {
                        driver.SetActive(false);
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

                throw new AggregateException
                (
                    $"Presentation Layer '{layerID}' 등록과 롤백이 실패했습니다.",
                    errors
                );
            }
        }

        internal bool TryGet(string layerID, out IPresentationLayerDriver driver)
        {
            ThrowIfDisposed();

            if
            (
                string.IsNullOrWhiteSpace(layerID) ||
                !entries.TryGetValue(layerID, out var entry) ||
                entry.State != EntryState.Available
            )
            {
                driver = null;
                return false;
            }

            driver = entry.Driver;
            return true;
        }

        public bool Contains(string layerID)
        {
            return
                !isDisposed &&
                !string.IsNullOrWhiteSpace(layerID) &&
                entries.TryGetValue(layerID, out var entry) &&
                entry.State == EntryState.Available;
        }

        internal bool TryAcquireUsage
        (
            string layerID,
            out IPresentationLayerDriver driver,
            out Lease usage
        )
        {
            ThrowIfDisposed();

            if
            (
                !entries.TryGetValue(layerID, out var entry) ||
                entry.State != EntryState.Available
            )
            {
                driver = null;
                usage = null;
                return false;
            }

            driver = entry.Driver;
            usage = entry.AcquireUsage();
            return true;
        }

        internal void Unregister(Entry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (isDisposed) return;

            if (!entries.TryGetValue(entry.ID, out var current) || !ReferenceEquals(current, entry))
            {
                return;
            }

            if (entry.ConsumerCount > 0)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{entry.ID}'에 활성 소비자가 남아 있습니다."
                );
            }

            entry.State = EntryState.Releasing;
            Exception failure = null;

            try
            {
                entry.Driver.SetActive(false);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                ReleaseRegistration(entry);
            }

            if (failure != null)
            {
                throw new AggregateException
                (
                    $"Presentation Layer '{entry.ID}' 해제가 실패했습니다.",
                    failure
                );
            }
        }

        private void ReleaseRegistration(Entry entry, bool removeFromEntries = true)
        {
            entry.State = EntryState.Released;

            if (removeFromEntries)
            {
                entries.Remove(entry.ID);
            }

            entry.Handle?.MarkDisposed();
            entry.Handle = null;
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(PresentationLayerRegistry));
            }
        }

    #endregion

    #region IDisposable

        public void Dispose()
        {
            if (isDisposed) return;

            foreach (var pair in entries)
            {
                if (pair.Value.ConsumerCount > 0)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer '{pair.Key}'에 활성 소비자가 남아 있습니다."
                    );
                }
            }

            isDisposed = true;
            var errors = new List<Exception>();

            try
            {
                foreach (var entry in entries.Values)
                {
                    var shouldDeactivate = entry.State != EntryState.Releasing;
                    ReleaseRegistration(entry, removeFromEntries: false);

                    if (!shouldDeactivate) continue;

                    try
                    {
                        entry.Driver.SetActive(false);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }
            finally
            {
                entries.Clear();
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Presentation Layer Registry 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
