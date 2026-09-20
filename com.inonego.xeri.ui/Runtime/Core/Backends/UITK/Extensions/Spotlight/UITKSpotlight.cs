/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSpotlight.cs
수정일 : 2026-09-17
# 설명
UI Toolkit Spotlight 요청을 공통 최신 표시 우선 정책과 Lease 수명으로 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Spotlight 표시 요청과 Lease 수명을 소유한다.
    /// </summary>
    // ============================================================
    public sealed class UITKSpotlight : IDisposable
    {

    #region 필드

        private readonly SpotlightRequestStack<UITKSpotlightParams> requests =
            new SpotlightRequestStack<UITKSpotlightParams>();

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// UI Toolkit backend에 Spotlight 요청을 표시하고 소유 Lease를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public Lease Show
        (
            ISpotlightDriver<UITKSpotlightParams> driver,
            UITKSpotlightParams parameters
        )
        {
            return requests.Show(driver, parameters);
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 UI Toolkit Spotlight 표시 요청을 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            requests.Dispose();
        }

    #endregion

    }
}
