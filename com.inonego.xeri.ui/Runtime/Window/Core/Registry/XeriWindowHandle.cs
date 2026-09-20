/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowHandle.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 registry에 등록된 윈도우 참조.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Registry에 등록된 Xeri 커스텀 윈도우 참조.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class XeriWindowHandle
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 저장과 복원에 사용하는 안정적인 윈도우 ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ID => id;

        private readonly string id = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// 이 handle이 아직 registry에서 유효한지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsValid
        {
            get => registry != null && registry.Contains(this);
        }

        private readonly IXeriWindowRegistry registry = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Registry handle을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriWindowHandle(string id, IXeriWindowRegistry registry) : base()
        {
            this.id       = id ?? string.Empty;
            this.registry = registry;
        }

    #endregion

    }
}
