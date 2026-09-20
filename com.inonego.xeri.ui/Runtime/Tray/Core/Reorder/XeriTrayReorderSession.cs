/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayReorderSession.cs
수정일 : 2026-09-20

# 설명
Tray entry reorder drag 중의 임시 상태를 보관한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray entry reorder drag 세션.
    /// </summary>
    // ============================================================
    public sealed class XeriTrayReorderSession
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 중인 Tray button.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayButton Button { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 중인 Tray entry.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayEntry Entry { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 시작 index.
        /// </summary>
        // ------------------------------------------------------------
        public int SourceIndex { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 preview target index.
        /// </summary>
        // ------------------------------------------------------------
        public int TargetIndex
        {
            get => targetIndex;
            set => targetIndex = value;
        }

        private int targetIndex = 0;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry container 좌표계의 drag 시작 pointer 위치.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 StartPointerPos { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Tray reorder drag 세션을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayReorderSession
        (
            XeriTrayButton button,
            int sourceIndex,
            Vector2 startPointerPos
        ) : base()
        {
            Button = button;
            Entry = button != null ? button.Entry : null;
            SourceIndex = sourceIndex;
            targetIndex = sourceIndex;
            StartPointerPos = startPointerPos;
        }

    #endregion

    }
}
