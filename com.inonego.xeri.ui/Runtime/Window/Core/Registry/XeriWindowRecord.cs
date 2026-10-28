/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowRecord.cs
수정일 : 2026-10-28

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

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 저장과 복원에 사용하는 안정적인 Window ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ID = string.Empty;

        public string Title = string.Empty;

        public string Tooltip = string.Empty;

        public Texture2D Icon = null;

        public XeriTrayBadge Badge = default;

        // ------------------------------------------------------------
        /// <summary>
        /// Window의 현재 완료 상태.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowState State = XeriWindowState.Normal;

        // ------------------------------------------------------------
        /// <summary>
        /// Window가 최소화됐을 때 복원할 상태.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowState MinimizedRestoreState = XeriWindowState.Normal;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Window bounds의 위치.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 Pos = Vector2.zero;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Window bounds의 크기.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 Size = Vector2.zero;

        // ------------------------------------------------------------
        /// <summary>
        /// Normal 상태로 돌아갈 Window bounds의 위치.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 NormalPos = Vector2.zero;

        // ------------------------------------------------------------
        /// <summary>
        /// Normal 상태로 돌아갈 Window bounds의 크기.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 NormalSize = Vector2.zero;

        public XeriWindowStackLayer StackLayer = XeriWindowStackLayer.Normal;

        // ------------------------------------------------------------
        /// <summary>
        /// Window UI에 적용할 Theme stable ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ThemeID = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// Window 콘텐츠를 제공하는 View Source ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ViewSourceID = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// View Source에서 Window 콘텐츠를 구분하는 stable key.
        /// </summary>
        // ------------------------------------------------------------
        public string ViewDataKey = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// View Source가 저장하는 Window별 UI session 상태.
        /// </summary>
        // ------------------------------------------------------------
        [SerializeReference]
        public IXeriUISession UISession = null;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 내부 state와 분리된 Record 복사본을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriWindowRecord CreateSnapshot()
        {
            return new XeriWindowRecord
            {
                ID = ID,
                Title = Title,
                Tooltip = Tooltip,
                Icon = Icon,
                Badge = Badge,
                State = State,
                MinimizedRestoreState = MinimizedRestoreState,
                Pos = Pos,
                Size = Size,
                NormalPos = NormalPos,
                NormalSize = NormalSize,
                StackLayer = StackLayer,
                ThemeID = ThemeID,
                ViewSourceID = ViewSourceID,
                ViewDataKey = ViewDataKey,
                UISession = UISession,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Controller의 현재 상태를 record에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ApplyController(XeriWindowController controller)
        {
            if (controller == null) return;

            Pos = controller.Pos;
            Size = controller.Size;
            State = controller.State;
            MinimizedRestoreState = controller.MinimizedRestoreState;

            var normalBounds = controller.NormalBounds;
            NormalPos = normalBounds.position;
            NormalSize = normalBounds.size;
        }

    #endregion

    }
}
