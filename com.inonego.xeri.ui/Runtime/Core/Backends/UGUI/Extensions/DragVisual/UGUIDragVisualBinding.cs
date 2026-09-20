/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIDragVisualBinding.cs
수정일 : 2026-09-17
# 설명
기존 DraggableUI의 Begin·End·Cancel 수명에 UGUI Drag Visual Handle을 연결한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.DragDrop;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 기존 DraggableUI와 한 Drag Visual 설정의 연결 수명.
    /// </summary>
    // ============================================================
    internal sealed class UGUIDragVisualBinding : IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 이 연결이 구독한 DraggableUI.
        /// </summary>
        // ------------------------------------------------------------
        internal DraggableUI Draggable => draggable;

        private DraggableUI draggable = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 이 연결이 Drag Layer로 옮길 Visual 대상.
        /// </summary>
        // ------------------------------------------------------------
        internal UnityEngine.RectTransform Target => parameters.Target;

        private DragVisualController owner = null;
        private DragVisualHandle activeHandle = null;
        private readonly DragVisualParams parameters;

    #endregion

    #region 생성자

        // ----------------------------------------------------------------------
        /// <summary>
        /// DraggableUI의 시작과 보장된 종료 정리에 Drag Visual 처리를 연결한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal UGUIDragVisualBinding
        (
            DragVisualController owner,
            DraggableUI draggable,
            in DragVisualParams parameters
        ) : base()
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.draggable = draggable ?? throw new ArgumentNullException(nameof(draggable));
            this.parameters = parameters;

            draggable.OnDragBegin += HandleDragBegin;
            draggable.AddDragEndCleanup(ReleaseDragVisual);
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 시작에 맞춰 선택한 Presentation Layer로 시각물을 승격한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleDragBegin(Draggable sender, DragEventArgs eventData)
        {
            if (owner == null || activeHandle != null) return;

            var handle = owner.Begin(parameters);

            // 부모 변경 callback이 Drag이나 Binding을 먼저 종료했으면 뒤늦게 반환된 소유권을 즉시 반환한다.
            if (owner == null || !sender.IsDragging)
            {
                handle.Dispose();
                return;
            }

            activeHandle = handle;

            // 부모 좌표계가 달라졌으므로 다음 이동 전에 현재 Pointer offset을 새 기준으로 확정한다.
            sender.RebaseDrag();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 정상 종료와 강제 취소의 공통 Drag End에서 시각물을 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReleaseDragVisual()
        {
            var handle = activeHandle;
            activeHandle = null;
            handle?.Dispose();
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// Draggable 이벤트 연결과 남은 활성 Drag Visual을 한 번 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            Release();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Draggable 이벤트 연결을 끊고 남은 활성 Drag Visual을 한 번 종료한다.
        /// <br/> Controller 전체 종료에서는 목록을 순회한 뒤 한 번에 비우도록 등록 제거를 생략한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void Release(bool removeFromBindings = true)
        {
            if (owner == null) return;

            var currentOwner = owner;
            var currentDraggable = draggable;
            owner = null;
            draggable = null;

            if (removeFromBindings)
            {
                currentOwner.Release(this);
            }

            try
            {
                // Screen이나 Runtime이 연결을 먼저 닫아도 Drag 의미와 Raycast 상태를 함께 종료한다.
                currentDraggable.ForceDragEnd();
            }
            finally
            {
                currentDraggable.OnDragBegin -= HandleDragBegin;
                currentDraggable.RemoveDragEndCleanup(ReleaseDragVisual);
                ReleaseDragVisual();
            }
        }

    #endregion

    }
}
