/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriUIViewResolver.cs
수정일 : 2026-09-20

# 설명
stable ID로 UITK view source를 등록하고 조회하는 기본 resolver.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Stable ID 기반 UI view source resolver.
    /// </summary>
    // ============================================================
    public sealed class XeriUIViewResolver : IXeriUIViewResolver
    {

    #region 필드

        private readonly Dictionary<string, IXeriUIViewSource> viewSources = new();

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// View source를 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Register(IXeriUIViewSource viewSource)
        {
            if (viewSource == null)
            {
                throw new ArgumentNullException(nameof(viewSource));
            }

            if (string.IsNullOrEmpty(viewSource.ID))
            {
                throw new ArgumentException("View source ID가 비어 있습니다.", nameof(viewSource));
            }

            if (viewSources.ContainsKey(viewSource.ID))
            {
                throw new InvalidOperationException($"이미 등록된 view source ID입니다. ID: {viewSource.ID}");
            }

            viewSources.Add(viewSource.ID, viewSource);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// View source 등록을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Unregister(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            return viewSources.Remove(id);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 view source 등록을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Clear()
        {
            viewSources.Clear();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Stable ID에 대응하는 view source를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool TryGetViewSource(string id, out IXeriUIViewSource viewSource)
        {
            if (string.IsNullOrEmpty(id))
            {
                viewSource = null;
                return false;
            }

            return viewSources.TryGetValue(id, out viewSource);
        }

    #endregion

    }
}
