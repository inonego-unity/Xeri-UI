/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationBackend.cs
수정일 : 2026-10-07

# 설명
Presentation Core가 materialize할 UI backend와 Runtime bootstrap capability를 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    public enum PresentationBackend
    {
        UGUI = 0,
        UITK = 1,
    }

    [Flags]
    public enum PresentationBackendSupport
    {
        None = 0,
        UGUI = 1 << 0,
        UITK = 1 << 1,
        All = UGUI | UITK,
    }
}
