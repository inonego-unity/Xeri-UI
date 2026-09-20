/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriTrayBadge.cs
수정일 : 2026-09-20

# 설명
Tray entry에 표시할 badge 정보.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray entry badge 표시 정보.
    /// </summary>
    // ============================================================
    [Serializable]
    public struct XeriTrayBadge
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Badge에 표시할 텍스트.
        /// </summary>
        // ------------------------------------------------------------
        public string Text;

        // ------------------------------------------------------------
        /// <summary>
        /// Badge 색상.
        /// </summary>
        // ------------------------------------------------------------
        public Color Color;

    #endregion

    #region 프로퍼티

        // ------------------------------------------------------------
        /// <summary>
        /// Badge를 표시할 수 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsVisible
        {
            get => !string.IsNullOrEmpty(Text);
        }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Tray badge 정보를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayBadge(string text, Color color) : this()
        {
            Text  = text;
            Color = color;
        }

    #endregion

    }
}
