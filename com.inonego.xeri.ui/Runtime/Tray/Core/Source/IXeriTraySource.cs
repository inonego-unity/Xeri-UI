/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriTraySource.cs
수정일 : 2026-09-20

# 설명
공통 Tray entry를 공급하는 source 계약.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray entry 목록을 공급하는 source 계약.
    /// </summary>
    // ============================================================
    public interface IXeriTraySource
    {

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 목록 재조회가 필요한 시점에 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        event EventHandler OnReloadRequired;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Tray entry 목록을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        IReadOnlyList<XeriTrayEntry> GetEntries();

    #endregion

    }
}
