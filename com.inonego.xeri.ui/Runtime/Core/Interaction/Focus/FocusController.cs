/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FocusController.cs
수정일 : 2026-10-03

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

    #region 수명 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Scope 등록을 해제하고 Handle을 무효화한다.
        /// </summary>
        // ------------------------------------------------------------
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

        internal bool HasAuthority => hasAuthority;

        private bool hasAuthority = true;

    #endregion

    #region 생성자

        public FocusController(IFocusDriver driver)
        {
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

    #endregion

    #region 스크린 기본 범위

        // ------------------------------------------------------------
        /// <summary>
        /// Screen의 Base Scope와 기본 Focus를 설정한다.
        /// </summary>
        // ------------------------------------------------------------
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

        // ------------------------------------------------------------
        /// <summary>
        /// Screen의 Scope를 Base로 복원하며 null이면 Base를 비운다.
        /// </summary>
        // ------------------------------------------------------------
        public void Restore(ScreenSession session)
        {
            SetBaseRecord(session != null ? GetOrCreateScreenRecord(session) : null);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen의 Scope를 제거하고 다음 유효한 Focus를 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Remove(ScreenSession session)
        {
            if (session == null || !records.TryGetValue(session, out var record)) return;

            var wasEffective = ReferenceEquals(GetEffectiveRecord(), record);

            if (ReferenceEquals(baseRecord, record))
            {
                baseRecord = null;
            }

            records.Remove(session);

            if (wasEffective && hasAuthority)
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
            var previous = baseRecord;
            var previousEffective = GetEffectiveRecord();

            // Primary/Override가 effective이면 Base 교체는 native Focus를 건드리지 않는다.
            if (!ReferenceEquals(previousEffective, baseRecord))
            {
                baseRecord = record;
                return;
            }

            CaptureCurrent();
            baseRecord = record;

            if (!hasAuthority) return;

            try
            {
                RestoreEffective();
            }
            catch (Exception exception)
            {
                baseRecord = previous;

                try
                {
                    RestoreEffective();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Focus Base 적용과 롤백이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }
        }

    #endregion

    #region 영속 범위 등록

        // ------------------------------------------------------------
        /// <summary>
        /// Scope를 등록하고 등록 수명을 소유하는 Handle을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
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

        // ------------------------------------------------------------
        /// <summary>
        /// <br/>Primary Scope를 선택하고 실패 시 이전 선택을 복원한다.
        /// <br/>Override가 있으면 native Focus는 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetPrimary(FocusScopeHandle handle)
        {
            var record = ResolveHandle(handle);

            if (ReferenceEquals(primaryRecord, record)) return;

            CaptureCurrent();
            var previous = primaryRecord;
            primaryRecord = record;

            if (!hasAuthority || overrides.Count > 0) return;

            try
            {
                RestoreEffective();
            }
            catch (Exception exception)
            {
                primaryRecord = previous;

                try
                {
                    RestoreEffective();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Primary Focus 적용과 롤백이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/>Override를 추가하고 해제 시 제거하는 Lease를 반환한다.
        /// <br/>Focus 적용에 실패하면 신규 등록을 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
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
            var errors = new List<Exception>();

            if (wasTop)
            {
                try
                {
                    CaptureCurrent();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            // Lease는 이미 terminal이므로 내부 Override 소유권도 반드시 제거한다.
            overrides.RemoveAt(index);

            if (wasTop && hasAuthority)
            {
                try
                {
                    RestoreEffective();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            ThrowErrors("Focus Override 해제", errors);
        }

        internal void Unregister(Record record)
        {
            if (record == null || !records.TryGetValue(record.Owner, out var current)) return;
            if (!ReferenceEquals(record, current)) return;

            var wasEffective = ReferenceEquals(GetEffectiveRecord(), record);
            var errors = new List<Exception>();

            if (wasEffective)
            {
                try
                {
                    CaptureCurrent();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            // Handle은 이미 terminal일 수 있으므로 Registry 소유권은 반드시 제거한다.
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
                try
                {
                    RestoreEffective();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            ThrowErrors("Focus Scope 등록 해제", errors);
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

    #region 권한과 네이티브 포커스

        internal void Suspend()
        {
            if (!hasAuthority) return;

            Exception failure = null;

            try
            {
                CaptureCurrent();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                // authority 반환은 Focus 기억 저장 실패와 무관하게 terminal로 확정한다.
                hasAuthority = false;
            }

            if (failure != null)
            {
                throw failure;
            }
        }

        internal void Resume()
        {
            if (hasAuthority) return;

            hasAuthority = true;

            try
            {
                RestoreEffective();
            }
            catch
            {
                // native 적용 실패를 성공한 authority 획득으로 고정하지 않아 다음 Refresh가 재시도할 수 있게 한다.
                hasAuthority = false;
                throw;
            }
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/>모든 Scope와 Override를 지우고 Handle을 무효화한다.
        /// <br/>authority가 있으면 fallback을 적용하고 실패를 모아 보고한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void Clear()
        {
            var errors = new List<Exception>();

            try
            {
                CaptureCurrent();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

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
                try
                {
                    driver.Select(driver.FindFallback());
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            ThrowErrors("Focus Controller 전체 정리", errors);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 수집한 Focus 실패를 원래 예외 또는 AggregateException으로 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ThrowErrors
        (
            string message,
            List<Exception> errors
        )
        {
            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException(message, errors);
        }

    #endregion

    }
}
