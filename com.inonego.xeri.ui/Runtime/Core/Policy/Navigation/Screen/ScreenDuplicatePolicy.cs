/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenDuplicatePolicy.cs
수정일 : 2026-07-29

# 설명
동일 Screen ID의 중복 Open 처리 정책을 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 동일 Screen ID의 중복 Open 정책.
    /// </summary>
    // ============================================================
    public enum ScreenDuplicatePolicy
    {
        Allow = 0,
        Reject = 1,
    }
}
