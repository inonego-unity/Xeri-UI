/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenViewScope.cs
수정일 : 2026-09-17
# 설명
Screen Source에 ID, Open Params, 현재 Session과 선택 Layer Driver를 불변 범위로 전달한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Screen Source가 소비하는 불변 실행 범위.
    /// </summary>
    // ============================================================
    public sealed class ScreenViewScope
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Screen ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ScreenID { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Open 호출 인자.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenOpenParams OpenParams { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Screen Session의 비소유 참조.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenSession Session { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen을 표시할 Layer ID.
        /// </summary>
        // ------------------------------------------------------------
        public string LayerID { get; }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Screen View를 배치할 현재 Presentation Layer Driver의 비소유 참조.
        /// </summary>
        // ----------------------------------------------------------------------
        public IPresentationLayerDriver Layer { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Source 실행 범위를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal ScreenViewScope
        (
            string screenID,
            ScreenOpenParams openParams,
            ScreenSession session,
            string layerID,
            IPresentationLayerDriver layer
        ) : base()
        {
            ScreenID = screenID ?? throw new ArgumentNullException(nameof(screenID));
            OpenParams = openParams;
            Session = session ?? throw new ArgumentNullException(nameof(session));
            LayerID = layerID ?? throw new ArgumentNullException(nameof(layerID));
            Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        }

    #endregion

    }
}
