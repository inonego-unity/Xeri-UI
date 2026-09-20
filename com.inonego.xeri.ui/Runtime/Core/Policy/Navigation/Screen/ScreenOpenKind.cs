/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenOpenKind.cs
수정일 : 2026-07-29

# 설명
Screen Open 시작의 수락·거부·취소와 실패 단계를 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Screen Open 결과 종류.
    /// </summary>
    // ============================================================
    public enum ScreenOpenKind
    {
        None = 0,
        Accepted = 1,
        Rejected = 2,
        Cancelled = 3,
        SourceFailed = 4,
        TransitionFailed = 5,
    }
}
