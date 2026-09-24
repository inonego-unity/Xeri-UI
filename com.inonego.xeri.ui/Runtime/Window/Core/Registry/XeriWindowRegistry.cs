/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowRegistry.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 controller와 저장 record를 관리하는 registry.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 registry.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowRegistry : IXeriWindowRegistry
    {

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// Registry 내부 런타임 entry.
        /// </summary>
        // ============================================================
        private sealed class RegistryEntry
        {
            public XeriWindowHandle Handle = null;
            public XeriWindowController Controller = null;
            public XeriWindowRecord Record = null;

            public ValueChangeEventHandler<UnityEngine.Vector2> PosChange = null;
            public ValueChangeEventHandler<UnityEngine.Vector2> SizeChange = null;
            public ValueChangeEventHandler<XeriWindowState> StateChange = null;
        }

    #endregion

    #region 필드

        private readonly Dictionary<string, RegistryEntry> entries = new();
        private readonly List<string> order = new();

    #endregion

    #region 속성

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 활성 윈도우 handle.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowHandle ActiveHandle => activeHandle;

        private XeriWindowHandle activeHandle = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 윈도우 record 목록.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<XeriWindowRecord> Records
        {
            get
            {
                var records = new List<XeriWindowRecord>(order.Count);

                foreach (var id in order)
                {
                    if (!entries.TryGetValue(id, out var entry)) continue;

                    AddRecordByStackLayer(records, entry.Record.CreateSnapshot());
                }

                return records;
            }
        }

    #endregion

    #region 이벤트

        public event EventHandler OnCollectionChange = null;
        public event EventHandler<XeriWindowEventArgs> OnRegister = null;
        public event EventHandler<XeriWindowEventArgs> OnUnregister = null;
        public event EventHandler<XeriWindowEventArgs> OnActiveChange = null;
        public event EventHandler OnOrderChange = null;

    #endregion

    #region 등록 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 controller를 등록하고 handle을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowHandle Register(string id, XeriWindowController controller)
        {
            var record = new XeriWindowRecord
            {
                ID    = id ?? string.Empty,
                Title = id ?? string.Empty,
            };

            return Register(id, controller, record);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 controller와 record를 등록하고 handle을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowHandle Register(string id, XeriWindowController controller, XeriWindowRecord record)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("윈도우 ID가 비어 있습니다.", nameof(id));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (entries.ContainsKey(id))
            {
                throw new InvalidOperationException
                (
                    $"이미 등록된 Window ID입니다. ID: {id}"
                );
            }

            var storedRecord = record?.CreateSnapshot() ?? new XeriWindowRecord();
            storedRecord.ID = id;
            storedRecord.ApplyController(controller);

            var handle = new XeriWindowHandle(id, this);
            var entry = new RegistryEntry
            {
                Handle = handle,
                Controller = controller,
                Record = storedRecord,
            };

            entries.Add(id, entry);
            order.Add(id);

            BindController(entry);

            var errors = new List<Exception>();
            InvokeHandlers(OnRegister, CreateEventArgs(entry), errors);
            InvokeHandlers(OnCollectionChange, EventArgs.Empty, errors);

            if (errors.Count == 0)
            {
                return handle;
            }

            if
            (
                entries.TryGetValue(id, out var current) &&
                ReferenceEquals(current, entry)
            )
            {
                try
                {
                    UnbindController(entry);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                entries.Remove(id);
                order.Remove(id);
                InvokeHandlers(OnUnregister, CreateEventArgs(entry), errors);
                InvokeHandlers(OnCollectionChange, EventArgs.Empty, errors);
            }

            ThrowEventErrors("Window 등록과 롤백 이벤트 처리", errors);
            return null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 등록을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Unregister(XeriWindowHandle handle)
        {
            if (!TryGetEntry(handle, out var entry)) return false;

            UnbindController(entry);
            entries.Remove(handle.ID);
            order.Remove(handle.ID);

            var wasActive = activeHandle == handle;

            if (wasActive)
            {
                activeHandle = null;
            }

            var errors = new List<Exception>();
            InvokeHandlers(OnUnregister, CreateEventArgs(entry), errors);
            InvokeHandlers(OnCollectionChange, EventArgs.Empty, errors);
            InvokeHandlers(OnOrderChange, EventArgs.Empty, errors);

            if (wasActive)
            {
                InvokeHandlers
                (
                    OnActiveChange,
                    new XeriWindowEventArgs(),
                    errors
                );
            }

            ThrowEventErrors("Window 등록 해제 이벤트 처리", errors);
            return true;
        }

    #endregion

    #region 조회 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Handle이 현재 registry에서 유효한지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Contains(XeriWindowHandle handle)
        {
            return TryGetEntry(handle, out _);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Handle에 대응하는 record를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool TryGetRecord(XeriWindowHandle handle, out XeriWindowRecord record)
        {
            if (TryGetEntry(handle, out var entry))
            {
                record = entry.Record.CreateSnapshot();
                return true;
            }

            record = null;
            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Handle에 대응하는 controller를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool TryGetController(XeriWindowHandle handle, out XeriWindowController controller)
        {
            if (TryGetEntry(handle, out var entry))
            {
                controller = entry.Controller;
                return true;
            }

            controller = null;
            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// ID에 대응하는 handle을 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool TryGetHandle(string id, out XeriWindowHandle handle)
        {
            if (!string.IsNullOrWhiteSpace(id) && entries.TryGetValue(id, out var entry))
            {
                handle = entry.Handle;
                return true;
            }

            handle = null;
            return false;
        }

    #endregion

    #region 정렬 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 활성 순서의 앞으로 가져온다.
        /// </summary>
        // ------------------------------------------------------------
        public void Focus(XeriWindowHandle handle)
        {
            if (!TryGetEntry(handle, out var entry)) return;

            if (!entry.Controller.Options.CanFocus) return;

            var state = entry.Controller.EffectiveState;

            if (state == XeriWindowState.Minimized || state == XeriWindowState.Closed)
            {
                return;
            }

            activeHandle = handle;
            MoveWindowOrderToFront(entry);

            var errors = new List<Exception>();
            InvokeHandlers(OnActiveChange, CreateEventArgs(entry), errors);
            InvokeHandlers(OnOrderChange, EventArgs.Empty, errors);
            ThrowEventErrors("Window Focus 이벤트 처리", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Window가 active이면 active 상태를 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Deactivate(XeriWindowHandle handle)
        {
            if (!ReferenceEquals(activeHandle, handle)) return;

            activeHandle = null;
            var errors = new List<Exception>();
            InvokeHandlers(OnActiveChange, new XeriWindowEventArgs(), errors);
            ThrowEventErrors("Window active 해제 이벤트 처리", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window를 같은 layer의 가장 앞으로 이동시킨다.
        /// </summary>
        // ------------------------------------------------------------
        public void BringToFront(XeriWindowHandle handle)
        {
            if (!TryGetEntry(handle, out var entry)) return;

            MoveWindowOrderToFront(entry);

            var errors = new List<Exception>();
            InvokeHandlers(OnOrderChange, EventArgs.Empty, errors);
            ThrowEventErrors("Window 순서 변경 이벤트 처리", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window를 같은 layer의 가장 뒤로 이동시킨다.
        /// </summary>
        // ------------------------------------------------------------
        public void SendToBack(XeriWindowHandle handle)
        {
            if (!TryGetEntry(handle, out var entry)) return;

            MoveWindowOrderToBack(entry);

            var errors = new List<Exception>();
            InvokeHandlers(OnOrderChange, EventArgs.Empty, errors);
            ThrowEventErrors("Window 순서 변경 이벤트 처리", errors);
        }

    #endregion

    #region 상태 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Window의 화면 정렬 layer를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetStackLayer(XeriWindowHandle handle, XeriWindowStackLayer stackLayer)
        {
            if (!Enum.IsDefined(typeof(XeriWindowStackLayer), stackLayer))
            {
                throw new ArgumentOutOfRangeException(nameof(stackLayer));
            }

            if (!TryGetEntry(handle, out var entry)) return;
            if (entry.Record.StackLayer == stackLayer) return;

            entry.Record.StackLayer = stackLayer;
            MoveWindowOrderToFront(entry);

            var errors = new List<Exception>();
            InvokeHandlers(OnOrderChange, EventArgs.Empty, errors);
            ThrowEventErrors("Window StackLayer 이벤트 처리", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 normal 상태로 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
        public void ShowNormal(XeriWindowHandle handle)
        {
            if (!TryGetEntry(handle, out var entry)) return;

            entry.Controller.ShowNormal();
            Focus(handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 최소화 이전 표시 상태로 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Restore(XeriWindowHandle handle)
        {
            if (!TryGetEntry(handle, out var entry)) return;

            entry.Controller.Restore();
            Focus(handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 닫는다.
        /// </summary>
        // ------------------------------------------------------------
        public void Close(XeriWindowHandle handle)
        {
            if (!TryGetEntry(handle, out var entry)) return;

            entry.Controller.Close();
        }

    #endregion

    #region 내부 조회 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Handle에 대응하는 내부 entry를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool TryGetEntry(XeriWindowHandle handle, out RegistryEntry entry)
        {
            if
            (
                handle != null &&
                !string.IsNullOrWhiteSpace(handle.ID) &&
                entries.TryGetValue(handle.ID, out entry) &&
                entry.Handle == handle
            )
            {
                return true;
            }

            entry = null;
            return false;
        }

    #endregion

    #region 동기화 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Controller 이벤트를 record 동기화에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BindController(RegistryEntry entry)
        {
            entry.PosChange = (_, _) => entry.Record.ApplyController(entry.Controller);
            entry.SizeChange = (_, _) => entry.Record.ApplyController(entry.Controller);
            entry.StateChange = (_, _) =>
            {
                entry.Record.ApplyController(entry.Controller);

                var state = entry.Controller.EffectiveState;
                var shouldDeactivate =
                    state == XeriWindowState.Minimized ||
                    state == XeriWindowState.Closed;

                var errors = new List<Exception>();

                if (shouldDeactivate && ReferenceEquals(activeHandle, entry.Handle))
                {
                    activeHandle = null;
                    InvokeHandlers
                    (
                        OnActiveChange,
                        new XeriWindowEventArgs(),
                        errors
                    );
                }

                InvokeHandlers(OnCollectionChange, EventArgs.Empty, errors);
                ThrowEventErrors("Window 상태 동기화 이벤트 처리", errors);
            };

            entry.Controller.OnPosChange += entry.PosChange;
            entry.Controller.OnSizeChange += entry.SizeChange;
            entry.Controller.OnStateChange += entry.StateChange;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Controller와 Registry record 동기화 구독을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnbindController(RegistryEntry entry)
        {
            if (entry?.Controller == null) return;

            if (entry.PosChange != null)
            {
                entry.Controller.OnPosChange -= entry.PosChange;
                entry.PosChange = null;
            }

            if (entry.SizeChange != null)
            {
                entry.Controller.OnSizeChange -= entry.SizeChange;
                entry.SizeChange = null;
            }

            if (entry.StateChange != null)
            {
                entry.Controller.OnStateChange -= entry.StateChange;
                entry.StateChange = null;
            }
        }

    #endregion

    #region 이벤트 호출 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// EventHandler 구독자를 독립적으로 호출하고 오류를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private void InvokeHandlers
        (
            EventHandler handlers,
            EventArgs eventArgs,
            List<Exception> errors
        )
        {
            if (handlers == null) return;

            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, eventArgs);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Generic EventHandler 구독자를 독립적으로 호출하고 오류를 수집한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void InvokeHandlers<TEventArgs>
        (
            EventHandler<TEventArgs> handlers,
            TEventArgs eventArgs,
            List<Exception> errors
        )
        where TEventArgs : EventArgs
        {
            if (handlers == null) return;

            foreach (EventHandler<TEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, eventArgs);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 수집된 Registry event 오류를 한 번 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ThrowEventErrors
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

    #region Window 정렬 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// StackLayer 순서를 유지하며 record 목록에 record를 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AddRecordByStackLayer(List<XeriWindowRecord> records, XeriWindowRecord record)
        {
            var insertIndex = records.FindIndex
            (
                item => item.StackLayer > record.StackLayer
            );

            if (insertIndex < 0)
            {
                records.Add(record);
                return;
            }

            records.Insert(insertIndex, record);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window를 같은 layer의 가장 앞으로 이동시킨다.
        /// </summary>
        // ------------------------------------------------------------
        private void MoveWindowOrderToFront(RegistryEntry entry)
        {
            var id = entry.Handle.ID;
            order.Remove(id);
            order.Add(id);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window를 같은 layer의 가장 뒤로 이동시킨다.
        /// </summary>
        // ------------------------------------------------------------
        private void MoveWindowOrderToBack(RegistryEntry entry)
        {
            var id = entry.Handle.ID;
            order.Remove(id);

            var insertIndex = order.FindIndex
            (
                current => entries[current].Record.StackLayer == entry.Record.StackLayer
            );

            if (insertIndex < 0)
            {
                order.Add(id);
                return;
            }

            order.Insert(insertIndex, id);
        }

    #endregion

    #region 이벤트 인자 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 이벤트 인자를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowEventArgs CreateEventArgs(RegistryEntry entry)
        {
            return new XeriWindowEventArgs
            {
                ID = entry.Handle.ID,
                Handle = entry.Handle,
                Pos = entry.Record.Pos,
                Size = entry.Record.Size,
                State = entry.Record.State,
            };
        }

    #endregion

    }
}
