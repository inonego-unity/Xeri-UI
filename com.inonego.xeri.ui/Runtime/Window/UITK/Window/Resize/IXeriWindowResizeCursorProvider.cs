/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriWindowResizeCursorProvider.cs
작성일 : 2026-05-24

# 설명
Xeri 커스텀 윈도우 resize 방향에 맞는 cursor 적용 기능을 추상화한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Window resize cursor 적용 인터페이스.
    /// </summary>
    // ============================================================
    public interface IXeriWindowResizeCursorProvider
    {

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Resize 방향에 맞는 cursor를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        void Apply(XeriWindowResizeMode mode);

        // ------------------------------------------------------------
        /// <summary>
        /// Cursor를 기본 상태로 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        void Reset();

    #endregion

    }
}
