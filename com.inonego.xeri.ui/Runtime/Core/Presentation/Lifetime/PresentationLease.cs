/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLease.cs
수정일 : 2026-09-17
# 설명
Presentation Layer 사용 수명과 Source View 소유권을 하나의 값 Lease로 획득한다.
특정 Overlay 개념 없이 Layer 기반 Presentation View의 기본 Acquire·Release를 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// Presentation View와 Layer Usage를 값 Lease로 결합하는 획득 진입점.
    /// </summary>
    // ======================================================================
    public static class PresentationLease
    {

    #region 획득

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Layer 사용 수명을 획득한 뒤 Source에서 Presentation View를 생성한다.
        /// <br/> View 획득 실패 시 Layer Usage를 즉시 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public static Lease<TView> Acquire<TView>
        (
            PresentationLayerRegistry layerRegistry,
            string layerID,
            IPresentationSource<TView> source
        )
        where TView : class
        {
            if (layerRegistry == null)
            {
                throw new ArgumentNullException(nameof(layerRegistry));
            }

            if (string.IsNullOrWhiteSpace(layerID))
            {
                throw new ArgumentException("Presentation Layer ID가 비어 있습니다.", nameof(layerID));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!layerRegistry.TryAcquireUsage(layerID, out var driver, out var usage))
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{layerID}'가 등록되어 있지 않습니다."
                );
            }

            try
            {
                var view = source.Acquire(driver);

                if (view == null)
                {
                    throw new InvalidOperationException("Presentation Source가 null View를 반환했습니다.");
                }

                return new Lease<TView>
                (
                    view,
                    () => Release(source, view, usage)
                );
            }
            catch
            {
                // View 획득이 확정되지 않았으므로 Layer Usage만 원래 상태로 복원한다.
                usage.Dispose();
                throw;
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// View와 Layer Usage를 한 번 반환하고 View 반환 실패 뒤에도 Layer Usage를 정리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private static void Release<TView>
        (
            IPresentationSource<TView> source,
            TView view,
            IDisposable usage
        )
        where TView : class
        {
            try
            {
                source.Release(view);
            }
            finally
            {
                usage.Dispose();
            }
        }

    #endregion

    }
}
