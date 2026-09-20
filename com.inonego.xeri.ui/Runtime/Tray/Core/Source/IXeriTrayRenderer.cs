/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriTrayRenderer.cs
수정일 : 2026-09-20

# 설명
공통 Tray entry 목록을 표시하는 renderer 계약.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray entry 목록 표시 계약.
    /// </summary>
    // ============================================================
    public interface IXeriTrayRenderer
    {

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 선택 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        event EventHandler<XeriTrayEventArgs> OnEntrySelect;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 닫기 입력 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        event EventHandler<XeriTrayEventArgs> OnEntryClose;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 목록을 다시 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        void Reload(IReadOnlyList<XeriTrayEntry> entries, XeriTrayOptions options);

    #endregion

    }
}
