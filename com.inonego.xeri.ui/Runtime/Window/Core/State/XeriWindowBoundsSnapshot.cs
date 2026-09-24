/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowBoundsSnapshot.cs
수정일 : 2026-10-04

# 설명
Xeri 커스텀 윈도우 상태 전환 중 복구할 런타임 bounds snapshot.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 런타임 bounds snapshot.
    /// </summary>
    // ============================================================
    internal sealed class XeriWindowBoundsSnapshot
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Normal 상태의 마지막 bounds.
        /// </summary>
        // ------------------------------------------------------------
        internal Rect NormalBounds => normalBounds;

        private Rect normalBounds = default;

        // ------------------------------------------------------------
        /// <summary>
        /// Restore에 사용할 bounds.
        /// </summary>
        // ------------------------------------------------------------
        internal Rect RestoreBounds => restoreBounds;

        private Rect restoreBounds = default;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 빈 bounds snapshot을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriWindowBoundsSnapshot() : base()
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 초기 bounds를 가진 snapshot을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriWindowBoundsSnapshot(Rect bounds) : this()
        {
            normalBounds = bounds;
            restoreBounds = bounds;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Normal 상태에서만 normal bounds를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void UpdateNormalBounds
        (
            XeriWindowState currentState,
            Rect bounds
        )
        {
            if (currentState != XeriWindowState.Normal) return;

            normalBounds = bounds;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 normal bounds를 restore 기준으로 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void CaptureRestoreBounds()
        {
            restoreBounds = normalBounds;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패한 상태 전환 전에 보존한 bounds snapshot으로 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Restore
        (
            Rect normalBounds,
            Rect restoreBounds
        )
        {
            this.normalBounds = normalBounds;
            this.restoreBounds = restoreBounds;
        }

    #endregion

    }
}
