/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenInstance.cs
수정일 : 2026-09-17
# 설명
Screen Source가 조립한 표시 Driver와 선택적 단일 상태 Handler를 묶는다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Source가 획득·Bind한 Screen 실행 인스턴스.
    /// </summary>
    // ============================================================
    public sealed class ScreenInstance
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 표시 backend.
        /// </summary>
        // ------------------------------------------------------------
        public IScreenDriver Driver { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 선택적 단일 Screen 상태 Handler.
        /// </summary>
        // ------------------------------------------------------------
        public IScreenStateHandler StateHandler { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Driver와 선택적 State Handler를 묶는다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenInstance
        (
            IScreenDriver driver,
            IScreenStateHandler stateHandler = null
        ) : base()
        {
            Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            StateHandler = stateHandler;
        }

    #endregion

    }
}
