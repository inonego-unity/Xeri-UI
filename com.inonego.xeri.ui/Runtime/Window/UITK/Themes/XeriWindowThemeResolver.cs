/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowThemeResolver.cs
수정일 : 2026-11-10

# 설명
stable theme ID로 Xeri window theme asset을 등록하고 조회하는 기본 resolver.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Stable theme ID 기반 theme resolver.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowThemeResolver : IXeriWindowThemeResolver
    {

    #region 필드

        private readonly Dictionary<string, XeriWindowThemeAsset> themes = new();

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Theme ID가 비어 있거나 중복되면 예외를 던진다.
        /// <br/> 유효한 ID를 하나의 Theme asset에 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Register(XeriWindowThemeAsset theme)
        {
            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            if (string.IsNullOrEmpty(theme.ID))
            {
                throw new ArgumentException("Theme ID가 비어 있습니다.", nameof(theme));
            }

            var id = theme.ID;

            if (themes.ContainsKey(id))
            {
                throw new InvalidOperationException($"이미 등록된 theme ID입니다. ID: {id}");
            }

            themes.Add(id, theme);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 등록된 Theme ID에 대응하는 Theme asset을 조회한다.
        /// <br/> ID가 없거나 등록 후 ID가 바뀌었으면 조회 실패한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool TryGetTheme(string id, out XeriWindowThemeAsset theme)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                theme = null;
                return false;
            }

            var key = id.Trim();

            if (themes.TryGetValue(key, out theme) && theme != null && theme.ID == key)
            {
                return true;
            }

            theme = null;
            return false;
        }

    #endregion

    }
}
