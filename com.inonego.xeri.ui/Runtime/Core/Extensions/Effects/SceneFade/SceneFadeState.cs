/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SceneFadeState.cs
수정일 : 2026-07-29

# 설명
Scene Fade Overlay의 표시 상태를 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Scene Fade 상태.
    /// </summary>
    // ============================================================
    public enum SceneFadeState
    {
        Clear = 0,
        Covering = 1,
        Covered = 2,
        Revealing = 3,
    }
}
