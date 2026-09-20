/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayerRegistry.cs
수정일 : 2026-09-17
# 설명
stable string ID로 Presentation Layer를 등록하고 조회하며 활성 소비자 수를 추적한다.
Layer는 placement·order·root와 수명만 제공하며 Presentation State capability를 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Presentation Layer 등록과 조회를 소유하는 Registry.
    /// </summary>
    // ============================================================
    public sealed class PresentationLayerRegistry : IDisposable
    {

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// 외부 backend 적용을 기준으로 한 Layer 등록 단계.
        /// </summary>
        // ============================================================
        internal enum EntryState
        {
            Activating = 0,
            Available = 1,
            Releasing = 2,
            Released = 3,
        }

        // ============================================================
        /// <summary>
        /// 등록된 Layer와 활성 소비자 수를 보관한다.
        /// </summary>
        // ============================================================
        internal sealed class Entry
        {

        #region 필드

            // ------------------------------------------------------------
            /// <summary>
            /// Layer Asset.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationLayerAsset Asset { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Layer backend.
            /// </summary>
            // ------------------------------------------------------------
            public IPresentationLayerDriver Driver { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 활성 소비자 수.
            /// </summary>
            // ------------------------------------------------------------
            public int ConsumerCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 현재 등록이 외부 backend 적용 경계의 어느 단계인지 나타낸다.
            /// </summary>
            // ------------------------------------------------------------
            public EntryState State { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 이 등록 소유권을 나타내는 Handle.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationLayerHandle Handle { get; set; }

        #endregion

        #region 생성자

            // ------------------------------------------------------------
            /// <summary>
            /// Layer 등록 Entry를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public Entry
            (
                PresentationLayerAsset asset,
                IPresentationLayerDriver driver
            ) : base()
            {
                Asset = asset ?? throw new ArgumentNullException(nameof(asset));
                Driver = driver ?? throw new ArgumentNullException(nameof(driver));
                ConsumerCount = 0;
                State = EntryState.Activating;
            }

        #endregion

        #region 메서드

            // ------------------------------------------------------------
            /// <summary>
            /// Layer 소비자 수명을 시작한다.
            /// </summary>
            // ------------------------------------------------------------
            public Lease AcquireUsage()
            {
                ConsumerCount++;
                return new Lease(ReleaseUsage);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Layer 소비자 수명을 종료한다.
            /// </summary>
            // ------------------------------------------------------------
            private void ReleaseUsage()
            {
                ConsumerCount--;
            }

        #endregion

        }

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 소유권이 종료되었는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        internal bool IsDisposed => isDisposed;

        private bool isDisposed = false;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Asset과 backend를 등록하고 소유권 Handle을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationLayerHandle Register
        (
            PresentationLayerAsset asset,
            IPresentationLayerDriver driver
        )
        {
            ThrowIfDisposed();

            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            asset.Validate();

            if (entries.ContainsKey(asset.ID))
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{asset.ID}'가 이미 등록되어 있습니다."
                );
            }

            if (!driver.Validate(asset, out var error))
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{asset.ID}' 구성이 유효하지 않습니다. {error}"
                );
            }

            ValidateOrder(asset);

            var entry = new Entry(asset, driver);
            var handle = new PresentationLayerHandle(this, entry);
            entry.Handle = handle;

            // 외부 backend callback보다 먼저 ID와 Order를 예약하되 소비자에게는 아직 공개하지 않는다.
            entries.Add(asset.ID, entry);

            try
            {
                driver.SetOrder(asset.Order);
                ThrowIfDisposed();

                driver.SetActive(true);
                ThrowIfDisposed();

                // 활성화가 끝난 예약만 조회와 Usage 획득에 공개한다.
                entry.State = EntryState.Available;
                return handle;
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };

                // 아직 이 Register가 소유하는 예약만 종료한다.
                if (entries.TryGetValue(asset.ID, out var current) && ReferenceEquals(current, entry))
                {
                    ReleaseRegistration(entry);
                }

                // Registry 전체 종료가 이미 비활성화한 backend를 중복으로 정리하지 않는다.
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
                    $"Presentation Layer '{asset.ID}' 등록과 롤백이 실패했습니다.",
                    errors
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 조립 검증을 위해 ID에 해당하는 Layer backend를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool TryGet
        (
            string id,
            out IPresentationLayerDriver driver
        )
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(id))
            {
                driver = null;
                return false;
            }

            if (!entries.TryGetValue(id, out var entry) || entry.State != EntryState.Available)
            {
                driver = null;
                return false;
            }

            driver = entry.Driver;
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// ID에 해당하는 Layer가 등록되어 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Contains(string id)
        {
            if (isDisposed || string.IsNullOrWhiteSpace(id)) return false;

            return entries.TryGetValue(id, out var entry) && entry.State == EntryState.Available;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Layer를 사용하는 내부 소비자 수명을 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool TryAcquireUsage
        (
            string id,
            out IPresentationLayerDriver driver,
            out Lease usage
        )
        {
            ThrowIfDisposed();

            if (!entries.TryGetValue(id, out var entry) || entry.State != EntryState.Available)
            {
                driver = null;
                usage = null;
                return false;
            }

            driver = entry.Driver;
            usage = entry.AcquireUsage();
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 등록 Handle이 소유한 Entry를 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Unregister(Entry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (isDisposed) return;

            if (!entries.TryGetValue(entry.Asset.ID, out var current) || !ReferenceEquals(current, entry))
            {
                return;
            }

            if (entry.ConsumerCount > 0)
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{entry.Asset.ID}'에 활성 소비자가 남아 있습니다."
                );
            }

            // 비활성화 callback이 같은 등록을 소비하지 못하도록 먼저 공개 수명을 종료한다.
            entry.State = EntryState.Releasing;
            var errors = new List<Exception>();

            try
            {
                // 비활성화 callback이 같은 ID를 재등록하지 못하도록 적용이 끝날 때까지 ID를 예약한다.
                entry.Driver.SetActive(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
            finally
            {
                // backend 적용 결과와 관계없이 등록 소유권은 한 번만 종료한다.
                ReleaseRegistration(entry);
            }

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    $"Presentation Layer '{entry.Asset.ID}' 해제가 실패했습니다.",
                    errors
                );
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Layer 등록과 Handle의 소유 연결을 종료한다.
        /// <br/> Registry 전체 종료에서는 원본 목록을 순회한 뒤 한 번에 비우도록 등록 제거를 생략한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ReleaseRegistration
        (
            Entry entry,
            bool removeFromEntries = true
        )
        {
            entry.State = EntryState.Released;

            if (removeFromEntries)
            {
                entries.Remove(entry.Asset.ID);
            }

            entry.Handle?.MarkDisposed();
            entry.Handle = null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Order가 기존 등록과 충돌하지 않는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ValidateOrder(PresentationLayerAsset asset)
        {
            foreach (var pair in entries)
            {
                var current = pair.Value;

                if (current.Asset.Order == asset.Order)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer '{asset.ID}'의 Order({asset.Order})가 " +
                        $"'{current.Asset.ID}'와 충돌합니다."
                    );
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 해제된 Registry 사용을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(PresentationLayerRegistry));
            }
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 소비자가 없을 때 모든 Layer 등록을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            // Registry 종료는 사용 중인 Layer를 암묵적으로 파괴하지 않는다.
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
                    // 개별 해제가 이미 비활성화 중이면 Registry 종료가 같은 backend를 다시 호출하지 않는다.
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
