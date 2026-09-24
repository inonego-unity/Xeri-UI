/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DragVisualController.cs
수정일 : 2026-10-07
# 설명
UGUI Drag Visual의 PresentationTarget Layer Lease, 일시적 계층 재배치와 기존 Draggable 연결을 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.DragDrop;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// UGUI Drag Visual의 일시적 계층 재배치와 Draggable 연결을 관리한다.
    /// </summary>
    // ======================================================================
    public sealed class DragVisualController : IDisposable
    {

    #region 필드

        private readonly List<DragVisualHandle> handles = new List<DragVisualHandle>();
        private readonly List<UGUIDragVisualBinding> bindings = new List<UGUIDragVisualBinding>();
        private readonly Func<PresentationTarget, PresentationLayerLease> acquireLayer = null;
        private bool isDisposed = false;

    #endregion

    #region 생성자

        // ----------------------------------------------------------------------
        /// <summary>
        /// 명시적 RectTransform Root만 사용하는 독립 Controller를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public DragVisualController() : base()
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// UIContext의 Layer 획득 경로를 사용하는 Controller를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public DragVisualController(UIContext context) : this
        (
            context != null
                ? context.AcquireLayer
                : throw new ArgumentNullException(nameof(context))
        )
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Target을 Layer Lease로 resolve하는
        /// <br/> composition Controller를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public DragVisualController
        (
            Func<PresentationTarget, PresentationLayerLease> acquireLayer
        ) : this()
        {
            this.acquireLayer = acquireLayer ??
                throw new ArgumentNullException(nameof(acquireLayer));
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Drag Visual을 명시적 Root의 마지막 sibling으로 옮긴다.
        /// </summary>
        // ------------------------------------------------------------
        public DragVisualHandle Begin
        (
            RectTransform target,
            RectTransform dragRoot
        )
        {
            ThrowIfDisposed();
            return BeginInternal(target, dragRoot, null);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> PresentationTarget Layer Lease를 획득하고,
        /// <br/> Drag Visual을 해당 UGUI Root의 마지막 sibling으로 옮긴다.
        /// </summary>
        // ----------------------------------------------------------------------
        public DragVisualHandle Begin(in DragVisualParams parameters)
        {
            ThrowIfDisposed();
            ValidateParameters(parameters);
            ThrowIfLayerAcquirerMissing();
            var layerLease = acquireLayer(parameters.TargetPresentation);

            if (layerLease.Layer is not IPresentationLayerDriver<RectTransform> layerCanvas)
            {
                throw CombineLayerLeaseCleanupFailure
                (
                    new InvalidOperationException
                    (
                        $"Drag Visual Target '{parameters.TargetPresentation}'이 UGUI Layer가 아닙니다."
                    ),
                    layerLease
                );
            }

            return BeginInternal(parameters.Target, layerCanvas.Root, layerLease);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 기존 DraggableUI의 Begin·End·Cancel 수명에 Drag Visual을 연결한다.
        /// <br/> 반환된 연결은 소유 Screen 또는 기능 수명에서 해제한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public IDisposable Bind
        (
            DraggableUI draggable,
            in DragVisualParams parameters
        )
        {
            ThrowIfDisposed();
            ValidateParameters(parameters);
            ThrowIfLayerAcquirerMissing();

            if (draggable == null)
            {
                throw new ArgumentNullException(nameof(draggable));
            }

            for (var i = 0; i < bindings.Count; i++)
            {
                if
                (
                    ReferenceEquals(bindings[i].Draggable, draggable) ||
                    ReferenceEquals(bindings[i].Target, parameters.Target)
                )
                {
                    throw new InvalidOperationException
                    (
                        "같은 DraggableUI 또는 Drag Visual 대상을 중복으로 연결할 수 없습니다."
                    );
                }
            }

            var binding = new UGUIDragVisualBinding(this, draggable, parameters);
            bindings.Add(binding);
            return binding;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Drag Visual 상태를 기록해 지정 Root로 옮긴다.
        /// <br/> 실패하면 획득한 Layer Lease도 함께 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private DragVisualHandle BeginInternal
        (
            RectTransform target,
            RectTransform dragRoot,
            PresentationLayerLease layerLease
        )
        {
            if (target == null)
            {
                throw CombineLayerLeaseCleanupFailure
                (
                    new ArgumentNullException(nameof(target)),
                    layerLease
                );
            }

            if (dragRoot == null)
            {
                throw CombineLayerLeaseCleanupFailure
                (
                    new ArgumentNullException(nameof(dragRoot)),
                    layerLease
                );
            }

            if (ReferenceEquals(target, dragRoot) || dragRoot.IsChildOf(target))
            {
                throw CombineLayerLeaseCleanupFailure
                (
                    new InvalidOperationException
                    (
                        "Drag Visual Layer Root는 대상 자신이나 대상의 하위 Transform일 수 없습니다."
                    ),
                    layerLease
                );
            }

            for (var i = 0; i < handles.Count; i++)
            {
                if (ReferenceEquals(handles[i].Target, target))
                {
                    throw CombineLayerLeaseCleanupFailure
                    (
                        new InvalidOperationException
                        (
                            "같은 Drag Visual 대상을 중복으로 시작할 수 없습니다."
                        ),
                        layerLease
                    );
                }
            }

            var handle = new DragVisualHandle(this, target, layerLease);

            // 계층 callback의 중첩 Begin과 Controller 종료가 같은 대상을 다시 소유하지 않게 먼저 예약한다.
            handles.Add(handle);

            try
            {
                target.SetParent(dragRoot, true);

                // 부모 변경 callback에서 종료됐으면 복원된 Target을 다시 변경하지 않는다.
                ThrowIfDisposed();

                target.SetAsLastSibling();

                // 계층 변경 callback에서 종료됐으면 이미 Terminal인 Handle을 호출자에게 공개하지 않는다.
                ThrowIfDisposed();
                return handle;
            }
            catch (Exception exception)
            {
                try
                {
                    handle.Dispose();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        "Drag Visual 시작과 획득 상태 롤백이 모두 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패 원인과 이미 획득한 Layer Lease 반환 실패를 모두 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Exception CombineLayerLeaseCleanupFailure
        (
            Exception failure,
            PresentationLayerLease layerLease
        )
        {
            if (layerLease == null) return failure;

            try
            {
                layerLease.Dispose();
                return failure;
            }
            catch (Exception cleanupException)
            {
                return new AggregateException
                (
                    "Drag Visual 요청 실패와 Presentation Layer Lease 반환이 모두 실패했습니다.",
                    failure,
                    cleanupException
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 복원된 Drag Visual Handle을 활성 목록에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Release(DragVisualHandle handle)
        {
            if (isDisposed) return;

            handles.Remove(handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 해제된 Draggable 연결을 활성 목록에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Release(UGUIDragVisualBinding binding)
        {
            if (isDisposed) return;

            bindings.Remove(binding);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag Visual 호출 인자가 시작 가능한지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ValidateParameters(in DragVisualParams parameters)
        {
            if (parameters.Target == null)
            {
                throw new ArgumentException
                (
                    "Drag Visual 대상이 없습니다.",
                    nameof(parameters)
                );
            }

            if (!parameters.TargetPresentation.IsValid)
            {
                throw new ArgumentException
                (
                    "Drag Visual Presentation Target이 유효하지 않습니다.",
                    nameof(parameters)
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation 기반 요청에 필요한 Session 구성을 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfLayerAcquirerMissing()
        {
            if (acquireLayer == null)
            {
                throw new InvalidOperationException
                (
                    "PresentationTarget 기반 Drag Visual을 사용하려면 " +
                    "Layer Lease resolver가 있는 Controller를 생성해야 합니다."
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 해제된 Controller의 새 요청을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(DragVisualController));
            }
        }

    #endregion

    #region 해제

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Draggable 연결을 먼저 끊고 남은 Drag Visual을 최신 시작부터 복원한다.
        /// <br/> 각 소유권은 한 번만 종료하고 실패를 수집한 뒤 함께 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            var errors = new List<Exception>();

            try
            {
                // 새 Drag 진입을 먼저 차단한 뒤 활성 연결을 생성 역순으로 종료한다.
                for (var i = bindings.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        bindings[i].Release(removeFromBindings: false);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }
            finally
            {
                bindings.Clear();
            }

            try
            {
                // 연결에 속하지 않은 수동 Handle도 생성 역순으로 종료한다.
                for (var i = handles.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        handles[i].Release(removeFromHandles: false);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }
            finally
            {
                handles.Clear();
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Drag Visual Controller 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
