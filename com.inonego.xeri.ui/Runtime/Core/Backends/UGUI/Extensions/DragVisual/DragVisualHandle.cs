/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DragVisualHandle.cs
수정일 : 2026-10-07
# 설명
드래그 시각물의 원래 계층·RectTransform pose와 Presentation lifetime을 함께 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 한 UGUI Drag Visual 재배치와 Layer 사용 수명 Handle.
    /// </summary>
    // ============================================================
    public sealed class DragVisualHandle : IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Drag Visual과 Layer Usage가 논리적으로 종료됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed => owner == null;

        // ------------------------------------------------------------
        /// <summary>
        /// Controller가 중복 점유를 판정할 현재 Drag Visual 대상.
        /// </summary>
        // ------------------------------------------------------------
        internal RectTransform Target => target;

        private RectTransform target = null;
        private DragVisualController owner = null;
        private IDisposable presentationLifetime = null;
        private readonly Transform originalParent = null;
        private readonly int originalSibling = 0;
        private readonly Vector2 originalAnchorMin = default;
        private readonly Vector2 originalAnchorMax = default;
        private readonly Vector2 originalPivot = default;
        private readonly Vector3 originalAnchoredPosition = default;
        private readonly Vector2 originalSizeDelta = default;
        private readonly Quaternion originalRotation = default;
        private readonly Vector3 originalScale = default;

    #endregion

    #region 생성자

        // ----------------------------------------------------------------------
        /// <summary>
        /// RectTransform의 원래 계층·pose와 선택적 Layer Usage를 보관한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal DragVisualHandle
        (
            DragVisualController owner,
            RectTransform target,
            IDisposable presentationLifetime
        ) : base()
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.presentationLifetime = presentationLifetime;
            originalParent = target.parent;
            originalSibling = target.GetSiblingIndex();
            originalAnchorMin = target.anchorMin;
            originalAnchorMax = target.anchorMax;
            originalPivot = target.pivot;
            originalAnchoredPosition = target.anchoredPosition3D;
            originalSizeDelta = target.sizeDelta;
            originalRotation = target.localRotation;
            originalScale = target.localScale;
        }

    #endregion

    #region 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Drag Visual과 Layer Usage를 한 번 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            Release();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Handle을 Terminal로 전환한 뒤 Drag Visual pose와
        /// <br/> Layer Usage를 한 번 정리한다.
        /// <br/> pose 복원 결과와 관계없이 Layer Usage를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Release(bool removeFromHandles = true)
        {
            if (owner == null) return;

            var current = target;
            var currentOwner = owner;
            var currentPresentationLifetime = presentationLifetime;
            var errors = new List<Exception>();

            // 외부 hierarchy callback이 재진입해도 같은 Handle 종료를 다시 시작하지 않게 먼저 terminalize한다.
            owner = null;
            presentationLifetime = null;

            if (current != null)
            {
                TryCleanup(() => current.SetParent(originalParent, false), errors);
                TryCleanup(() => current.SetSiblingIndex(originalSibling), errors);
                TryCleanup(() => current.anchorMin = originalAnchorMin, errors);
                TryCleanup(() => current.anchorMax = originalAnchorMax, errors);
                TryCleanup(() => current.pivot = originalPivot, errors);
                TryCleanup(() => current.anchoredPosition3D = originalAnchoredPosition, errors);
                TryCleanup(() => current.sizeDelta = originalSizeDelta, errors);
                TryCleanup(() => current.localRotation = originalRotation, errors);
                TryCleanup(() => current.localScale = originalScale, errors);
            }

            // 계층 복원 callback 동안에는 Target 점유를 유지하고 모든 authored state 복원 시도 뒤 해제한다.
            target = null;

            if (removeFromHandles)
            {
                TryCleanup(() => currentOwner.Release(this), errors);
            }

            if (currentPresentationLifetime != null)
            {
                TryCleanup(currentPresentationLifetime.Dispose, errors);
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "Drag Visual pose와 Presentation Layer Lease 정리 중 하나 이상의 작업이 실패했습니다.",
                errors
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 cleanup 하나를 끝까지 시도하고 실패를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void TryCleanup
        (
            Action cleanup,
            List<Exception> errors
        )
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

    #endregion

    }
}
