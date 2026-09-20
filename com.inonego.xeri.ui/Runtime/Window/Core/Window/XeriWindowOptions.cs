/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowOptions.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 기능 활성화와 크기 제한 옵션.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 옵션.
    /// </summary>
    // ============================================================
    [Serializable]
    public struct XeriWindowOptions
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 최소 윈도우 크기.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 MinSize;

        // ------------------------------------------------------------
        /// <summary>
        /// 최대 윈도우 크기.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 MaxSize;

        // ------------------------------------------------------------
        /// <summary>
        /// 이동 가능 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool CanMove;

        // ------------------------------------------------------------
        /// <summary>
        /// 크기 변경 가능 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool CanResize;

        // ------------------------------------------------------------
        /// <summary>
        /// 최소화 가능 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool CanMinimize;

        // ------------------------------------------------------------
        /// <summary>
        /// 최대화 가능 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool CanMaximize;

        // ------------------------------------------------------------
        /// <summary>
        /// 닫기 가능 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool CanClose;

        // ------------------------------------------------------------
        /// <summary>
        /// 포커스 가능 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool CanFocus;

        // ------------------------------------------------------------
        /// <summary>
        /// Titlebar double click으로 maximize/show normal을 토글할지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool CanTitleBarDoubleClickMaximize;

        // ------------------------------------------------------------
        /// <summary>
        /// 비활성화된 control button을 숨길지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowStackLayer StackLayer;

        // ------------------------------------------------------------
        /// <summary>
        /// 화면 정렬 layer.
        /// </summary>
        // ------------------------------------------------------------
        public bool HideDisabledButtons;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 윈도우 옵션을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static XeriWindowOptions Default()
        {
            return new XeriWindowOptions
            {
                MinSize = new Vector2(152f, 80f),
                MaxSize = new Vector2(float.MaxValue, float.MaxValue),
                CanMove = true,
                CanResize = true,
                CanMinimize = true,
                CanMaximize = true,
                CanClose = true,
                CanFocus = true,
                CanTitleBarDoubleClickMaximize = true,
                StackLayer = XeriWindowStackLayer.Normal,
                HideDisabledButtons = false,
            };
        }

    #endregion

    }
}
