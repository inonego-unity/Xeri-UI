/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowRecord.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 저장 가능 상태 record.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Tray;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// 저장과 복원에 사용할 Xeri 커스텀 윈도우 상태 record.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class XeriWindowRecord
    {

    #region 필드

        public string ID = string.Empty;
        public string Title = string.Empty;
        public string Tooltip = string.Empty;
        public Texture2D Icon = null;
        public XeriTrayBadge Badge = default;
        public XeriWindowState State = XeriWindowState.Normal;
        public Vector2 Pos = Vector2.zero;
        public Vector2 Size = Vector2.zero;
        public Vector2 NormalPos = Vector2.zero;
        public Vector2 NormalSize = Vector2.zero;
        public int FocusOrder = 0;
        public XeriWindowStackLayer StackLayer = XeriWindowStackLayer.Normal;
        public string ThemeID = string.Empty;
        public string ViewSourceID = string.Empty;
        public string ViewDataKey = string.Empty;

        [SerializeReference]
        public IXeriUISession UISession = null;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Controller의 현재 상태를 record에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ApplyController(XeriWindowController controller)
        {
            if (controller == null || controller.Driver == null) return;

            Pos   = controller.Driver.Pos;
            Size  = controller.Driver.Size;
            State = controller.Driver.State;
        }

    #endregion

    }
}
