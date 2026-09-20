/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenOpenParams.cs
수정일 : 2026-09-17
# 설명
Screen Open 호출자가 소유하는 선택적 Payload를 불변 값으로 전달한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Screen Open 호출 인자.
    /// </summary>
    // ============================================================
    public readonly struct ScreenOpenParams
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Payload가 없는 기본 Open 인자.
        /// </summary>
        // ------------------------------------------------------------
        public static ScreenOpenParams Empty => default;

        // ------------------------------------------------------------
        /// <summary>
        /// 호출자가 소유하고 Source가 해석하는 선택적 Payload.
        /// </summary>
        // ------------------------------------------------------------
        public object Payload { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// caller-owned Payload를 담은 Open 인자를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenOpenParams(object payload) : this()
        {
            Payload = payload;
        }

    #endregion

    }
}
