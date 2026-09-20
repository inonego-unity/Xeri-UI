/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowStateCommandRequest.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 상태 전환 요청 데이터.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 상태 전환 요청.
    /// </summary>
    // ============================================================
    [Serializable]
    public readonly struct XeriWindowStateCommandRequest
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 명령 종류.
        /// </summary>
        // ------------------------------------------------------------
        public readonly XeriWindowStateCommandKind Kind;

        // ------------------------------------------------------------
        /// <summary>
        /// 명령 발생 원인.
        /// </summary>
        // ------------------------------------------------------------
        public readonly XeriWindowCommandSource Source;

        // ------------------------------------------------------------
        /// <summary>
        /// 명령이 요구하는 명시적 target bounds.
        /// </summary>
        // ------------------------------------------------------------
        public readonly Rect? TargetBounds;

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 animation 사용 여부.
        /// </summary>
        // ------------------------------------------------------------
        public readonly bool Animate;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 전환 요청을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowStateCommandRequest
        (
            XeriWindowStateCommandKind kind,
            XeriWindowCommandSource source,
            Rect? targetBounds = null,
            bool animate = true
        )
        {
            Kind = kind;
            Source = source;
            TargetBounds = targetBounds;
            Animate = animate;
        }

    #endregion

    }
}
