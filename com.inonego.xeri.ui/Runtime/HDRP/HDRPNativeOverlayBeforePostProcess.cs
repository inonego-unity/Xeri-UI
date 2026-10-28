/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : HDRPNativeOverlayBeforePostProcess.cs
수정일 : 2026-10-28

# 설명
HDRP가 BeforePostProcess 목록에 요구하는 별도 VolumeComponent 진입점.
정책과 자원 수명은 공통 Native Overlay 진입점이 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.HDRP
{
    // ======================================================================
    /// <summary>
    /// HDRP의 BeforePostProcess 삽입 지점에서 Native UI 합성을 수행한다.
    /// </summary>
    // ======================================================================
    [Serializable]
    public sealed class HDRPNativeOverlayBeforePostProcess : HDRPNativeOverlayPostProcess
    {
        protected override bool Affected => true;
    }
}
