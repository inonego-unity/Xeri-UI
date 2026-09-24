/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayerLease.cs
수정일 : 2026-10-07

# 설명
resolved Presentation Layer와 해당 Layer consumer lifetime을 하나의 Lease로 묶는다.
Local Session Layer와 app-wide Host destination이 같은 소비 계약을 사용하게 한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// resolved Presentation Layer의 사용 수명을 소유하는 Lease.
    /// </summary>
    // ============================================================
    public sealed class PresentationLayerLease : IDisposable
    {

    #region 필드

        public IPresentationLayerDriver Layer =>
            layer ?? throw new ObjectDisposedException(nameof(PresentationLayerLease));
        public bool IsDisposed => usage == null;

        private IPresentationLayerDriver layer = null;
        private Lease usage = null;

    #endregion

    #region 생성자

        internal PresentationLayerLease
        (
            IPresentationLayerDriver layer,
            Lease usage
        )
        {
            this.layer = layer ?? throw new ArgumentNullException(nameof(layer));
            this.usage = usage ?? throw new ArgumentNullException(nameof(usage));
        }

    #endregion

    #region 수명 해제

        public void Dispose()
        {
            var current = usage;
            usage = null;
            layer = null;
            current?.Dispose();
        }

    #endregion

    }
}
