/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FocusController.cs
수정일 : 2026-09-30

# 설명
Screen과 Focus Scope의 persistent Focus record를 통합해 Base, Primary와 Override 선택을 관리한다.
Registration lifetime과 activation을 분리하고 Context authority가 있을 때만 native Focus를 적용한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// 등록된 Focus Scope record의 persistent lifetime을 소유하는 Handle.
    /// </summary>
    // ======================================================================
    public sealed class FocusScopeHandle : IDisposable
    {

    #region 필드

        internal FocusController.Record Record { get; private set; }
        public bool IsDisposed => Record == null;

        private FocusController owner = null;

    #endregion

    #region 생성자

        internal FocusScopeHandle(FocusController owner, FocusController.Record record)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Record = record ?? throw new ArgumentNullException(nameof(record));
        }

    #endregion

    #region 내부 처리

        internal void MarkDisposed()
        {
            owner = null;
            Record = null;
        }

    #endregion

    #region IDisposable

        public void Dispose()
        {
            if (Record == null) return;

            var currentOwner = owner;
            var currentRecord = Record;
            owner = null;
            Record = null;
            currentOwner.Unregister(currentRecord);
        }

    #endregion

    }

    // ================================================================================
    /// <summary>
    /// UIContext 내부 Focus memory와 Base/Primary/Override selection을 소유한다.
    /// </summary>
    // ================================================================================
    public sealed class FocusController
    {

    #region 내부 데이터

        internal sealed class Record
        {
            public object Owner = null;
            public IFocusScope Scope = null;
            public ScreenSession Screen = null;
            public object Last = null;
            public object Default = null;
            public FocusScopeHandle Handle = null;

            public bool ContainsFocus(object target)
            {
                if (Scope != null)
                {
                    return Scope.ContainsFocus(target);
                }

                return Screen != null && Screen.ContainsFocus(target);
            }
        }

    #endregion

    #region 필드

        private readonly IFocusDriver driver = null;
        private readonly Dictionary<object, Record> records = new();
        private readonly List<Record> overrides = new();

        private Record baseRecord = null;
        private Record primaryRecord = null;
        private bool hasAuthority = true;

    #endregion

    #region 생성자

        public FocusController(IFocusDriver driver)
        {
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

    #endregion

    #region Screen Base Scope

        public void Activate
        (
            ScreenSession session,
            object defaultFocus
        )
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var record = GetOrCreateScreenRecord(session);
            record.Default = defaultFocus;
            SetBaseRecord(record);
        }

        public void Restore(ScreenSession session)
        {
            SetBaseRecord(session != null ? GetOrCreateScreenRecord(session) : null);
        }

        public void Remove(ScreenSession session)
        {
            if (session == null || !records.TryGetValue(session, out var record)) return;

            if (ReferenceEquals(baseRecord, record))
            {
                baseRecord = null;
            }

            records.Remove(session);

            if (hasAuthority)
            {
                RestoreEffective();
            }
        }

        private Record GetOrCreateScreenRecord(ScreenSession session)
        {
            if (!records.TryGetValue(session, out var record))
            {
                record = new Record
                {
                    Owner = session,
                    Screen = session,
                };
                records.Add(session, record);
            }

            return record;
        }

        private void SetBaseRecord(Record record)
        {
            CaptureCurrent();
            baseRecord = record;

            if (hasAuthority)
            {
                RestoreEffective();
            }
        }

    #endregion

    #region Persistent Scope Registration

        public FocusScopeHandle RegisterScope(IFocusScope scope)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (records.ContainsKey(scope))
            {
                throw new InvalidOperationException("같은 Focus Scope를 중복 등록할 수 없습니다.");
            }

            var record = new Record
            {
                Owner = scope,
                Scope = scope,
                Default = scope.DefaultFocus,
            };
            var handle = new FocusScopeHandle(this, record);
            record.Handle = handle;
            records.Add(scope, record);
            return handle;
        }

        public void SetPrimary(FocusScopeHandle handle)
        {
            var record = ResolveHandle(handle);

            if (ReferenceEquals(primaryRecord, record)) return;

            CaptureCurrent();
            primaryRecord = record;

            if (hasAuthority && overrides.Count == 0)
            {
                RestoreEffective();
            }
        }

        public Lease PushOverride(FocusScopeHandle handle)
        {
            var record = ResolveHandle(handle) ??
                throw new ArgumentNullException(nameof(handle));

            if (overrides.Contains(record))
            {
                throw new InvalidOperationException("같은 Focus Scope를 Override Stack에 중복 추가할 수 없습니다.");
            }

            CaptureCurrent();
            overrides.Add(record);

            try
            {
                if (hasAuthority)
                {
                    RestoreEffective();
                }
            }
            catch (Exception exception)
            {
                overrides.Remove(record);

                if (!hasAuthority)
                {
                    throw;
                }

                try
                {
                    RestoreEffective();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Focus Override 적용과 롤백이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }

            return new Lease(() => ReleaseOverride(record));
        }

        private void ReleaseOverride(Record record)
        {
            var index = overrides.IndexOf(record);
            if (index < 0) return;

            var wasTop = index == overrides.Count - 1;

            if (wasTop)
            {
                CaptureCurrent();
            }

            overrides.RemoveAt(index);

            if (wasTop && hasAuthority)
            {
                RestoreEffective();
            }
        }

        internal void Unregister(Record record)
        {
            if (record == null || !records.TryGetValue(record.Owner, out var current)) return;
            if (!ReferenceEquals(record, current)) return;

            var wasEffective = ReferenceEquals(GetEffectiveRecord(), record);

            if (wasEffective)
            {
                CaptureCurrent();
            }

            records.Remove(record.Owner);

            if (ReferenceEquals(primaryRecord, record))
            {
                primaryRecord = null;
            }

            for (var index = overrides.Count - 1; index >= 0; index--)
            {
                if (ReferenceEquals(overrides[index], record))
                {
                    overrides.RemoveAt(index);
                }
            }

            record.Handle?.MarkDisposed();
            record.Handle = null;

            if (wasEffective && hasAuthority)
            {
                RestoreEffective();
            }
        }

        private Record ResolveHandle(FocusScopeHandle handle)
        {
            if (handle == null) return null;

            if (handle.IsDisposed || handle.Record == null)
            {
                throw new ObjectDisposedException(nameof(FocusScopeHandle));
            }

            if (!records.TryGetValue(handle.Record.Owner, out var record) || !ReferenceEquals(record, handle.Record))
            {
                throw new InvalidOperationException("다른 FocusController의 Scope Handle을 사용할 수 없습니다.");
            }

            return record;
        }

    #endregion

    #region Authority / Native Focus

        internal void Suspend()
        {
            if (!hasAuthority) return;

            CaptureCurrent();
            hasAuthority = false;
        }

        internal void Resume()
        {
            if (hasAuthority) return;

            hasAuthority = true;
            RestoreEffective();
        }

        internal void CaptureCurrent(ScreenSession session = null)
        {
            CaptureCurrent();
        }

        private void CaptureCurrent()
        {
            if (!hasAuthority) return;

            var current = driver.Current;
            var record = GetEffectiveRecord();

            if
            (
                record != null &&
                driver.IsValid(current) &&
                record.ContainsFocus(current)
            )
            {
                record.Last = current;
            }
        }

        internal void RecordCurrentFocus(ScreenSession session, object target)
        {
            HandleNativeFocusChanged(target);
        }

        internal void HandleNativeFocusChanged(object target)
        {
            if (!hasAuthority) return;

            var record = GetEffectiveRecord();

            if (record == null || target == null)
            {
                return;
            }

            if (driver.IsValid(target) && record.ContainsFocus(target))
            {
                record.Last = target;
                return;
            }

            RestoreEffective();
        }

        private void RestoreEffective()
        {
            if (!hasAuthority) return;

            var record = GetEffectiveRecord();
            driver.Select(record != null ? Resolve(record) : driver.FindFallback());
        }

        private Record GetEffectiveRecord()
        {
            return overrides.Count > 0
                ? overrides[overrides.Count - 1]
                : primaryRecord ?? baseRecord;
        }

        private object Resolve(Record record)
        {
            if (IsValidFor(record, record.Last))
            {
                return record.Last;
            }

            if (IsValidFor(record, record.Default))
            {
                return record.Default;
            }

            return null;
        }

        private bool IsValidFor(Record record, object target)
        {
            return
                record != null &&
                driver.IsValid(target) &&
                record.ContainsFocus(target);
        }

    #endregion

    #region 전체 정리

        public void Clear()
        {
            CaptureCurrent();

            foreach (var record in records.Values)
            {
                record.Handle?.MarkDisposed();
                record.Handle = null;
            }

            records.Clear();
            overrides.Clear();
            baseRecord = null;
            primaryRecord = null;

            if (hasAuthority)
            {
                driver.Select(driver.FindFallback());
            }
        }

    #endregion

    }
}
