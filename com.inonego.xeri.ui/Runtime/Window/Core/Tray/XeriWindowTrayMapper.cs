/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowTrayMapper.cs
수정일 : 2026-09-20

# 설명
Xeri 윈도우 record와 handle을 공통 Tray entry로 변환한다.
========================================================================= BLOCK_HEADER_END */

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Tray;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 윈도우 상태를 공통 Tray entry로 변환하는 mapper.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowTrayMapper
    {

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 record와 handle을 Tray entry로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayEntry Map(XeriWindowRecord record, XeriWindowHandle handle)
        {
            if (record == null) return null;

            var id = handle?.ID ?? record.ID;

            return new XeriTrayEntry(id, record.Title)
            {
                Tooltip = record.Tooltip,
                Icon = record.Icon,
                Badge = record.Badge,
                PayloadID = id,
                Payload = handle,
            };
        }

    #endregion

    }
}
