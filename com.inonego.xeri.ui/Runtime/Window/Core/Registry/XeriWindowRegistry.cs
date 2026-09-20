/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowRegistry.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 controller와 저장 record를 관리하는 registry.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

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
        }

    #endregion

    #region 필드

        private readonly Dictionary<string, RegistryEntry> entries = new();
        private readonly List<string> order = new();

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 활성 윈도우 handle.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowHandle activeHandle = null;

        private int focusOrder = 0;

    #endregion

    #region 속성

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 활성 윈도우 handle.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowHandle ActiveHandle => activeHandle;

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

                    AddRecordByStackLayer(records, entry.Record);
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
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("윈도우 ID가 비어 있습니다.", nameof(id));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (entries.TryGetValue(id, out var exists))
            {
                return exists.Handle;
            }

            record ??= new XeriWindowRecord();
            record.ID = id;
            record.ApplyController(controller);

            var handle = new XeriWindowHandle(id, this);
            var entry = new RegistryEntry
            {
                Handle = handle,
                Controller = controller,
                Record = record,
            };

            entries.Add(id, entry);
            order.Add(id);

            BindController(entry);
            Focus(handle);

            OnRegister?.Invoke(this, CreateEventArgs(entry));
            OnCollectionChange?.Invoke(this, EventArgs.Empty);

            return handle;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 등록을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Unregister(XeriWindowHandle handle)
        {
            if (!TryGetEntry(handle, out var entry)) return false;

            entries.Remove(handle.ID);
            order.Remove(handle.ID);

            if (activeHandle == handle)
            {
                activeHandle = null;
            }

            OnUnregister?.Invoke(this, CreateEventArgs(entry));
            OnCollectionChange?.Invoke(this, EventArgs.Empty);
            OnOrderChange?.Invoke(this, EventArgs.Empty);

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
                record = entry.Record;
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
            if (!string.IsNullOrEmpty(id) && entries.TryGetValue(id, out var entry))
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

            activeHandle = handle;
            entry.Record.FocusOrder = ++focusOrder;
            MoveWindowOrderToFront(entry);
            entry.Controller.Focus();

            OnActiveChange?.Invoke(this, CreateEventArgs(entry));
            OnOrderChange?.Invoke(this, EventArgs.Empty);
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

            OnOrderChange?.Invoke(this, EventArgs.Empty);
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

            OnOrderChange?.Invoke(this, EventArgs.Empty);
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
            if (!TryGetEntry(handle, out var entry)) return;
            if (entry.Record.StackLayer == stackLayer) return;

            entry.Record.StackLayer = stackLayer;
            MoveWindowOrderToFront(entry);

            OnOrderChange?.Invoke(this, EventArgs.Empty);
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
                !string.IsNullOrEmpty(handle.ID) &&
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
            entry.Controller.OnPosChange += (_, _) => entry.Record.ApplyController(entry.Controller);
            entry.Controller.OnSizeChange += (_, _) => entry.Record.ApplyController(entry.Controller);
            entry.Controller.OnStateChange += (_, _) =>
            {
                entry.Record.ApplyController(entry.Controller);
                OnCollectionChange?.Invoke(this, EventArgs.Empty);
            };
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
            order.Remove(entry.Record.ID);
            order.Add(entry.Record.ID);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window를 같은 layer의 가장 뒤로 이동시킨다.
        /// </summary>
        // ------------------------------------------------------------
        private void MoveWindowOrderToBack(RegistryEntry entry)
        {
            order.Remove(entry.Record.ID);

            var insertIndex = order.FindIndex
            (
                id => entries[id].Record.StackLayer == entry.Record.StackLayer
            );

            if (insertIndex < 0)
            {
                order.Add(entry.Record.ID);
                return;
            }

            order.Insert(insertIndex, entry.Record.ID);
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
                ID = entry.Record.ID,
                Handle = entry.Handle,
                Pos = entry.Record.Pos,
                Size = entry.Record.Size,
                State = entry.Record.State,
            };
        }

    #endregion

    }
}
