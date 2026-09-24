/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLease.cs
수정일 : 2026-09-23
# 설명
활성 PresentationSession의 Presentation identity를 placement root와 Layer usage로 resolve해 View 수명을 획득한다.
기능 코드가 raw LayerID를 선택하지 않도록 Session placement 계약을 단일 진입점으로 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// Presentation placement와 Source View 소유권을 값 Lease로 결합한다.
    /// </summary>
    // ======================================================================
    public static class PresentationLease
    {

    #region 획득

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Session의 Presentation placement와 Layer usage를 획득한 뒤 View를 생성한다.
        /// <br/> View 획득 실패 시 Layer usage를 즉시 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public static Lease<TView> Acquire<TView>
        (
            PresentationSession session,
            string presentationID,
            IPresentationSource<TView> source
        )
        where TView : class
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(presentationID))
            {
                throw new ArgumentException("Presentation ID가 비어 있습니다.", nameof(presentationID));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!session.TryAcquirePlacement(presentationID, out var driver, out var usage))
            {
                throw new InvalidOperationException
                (
                    $"Presentation '{presentationID}'가 활성 Session Plan에 없습니다."
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
