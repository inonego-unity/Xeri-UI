/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriUIViewScope.cs
수정일 : 2026-09-20

# 설명
UITK view 생성, session 저장, session 로드에 필요한 stable ID와 UI session 범위.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// UI view source 호출에 필요한 런타임 적용 범위.
    /// </summary>
    // ============================================================
    public sealed class XeriUIViewScope
    {

    #region 프로퍼티

        // ------------------------------------------------------------
        /// <summary>
        /// View source를 식별하는 stable ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ViewSourceID => viewSourceID;

        private readonly string viewSourceID = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// UITK viewDataKey로 사용할 수 있는 stable key.
        /// </summary>
        // ------------------------------------------------------------
        public string ViewDataKey => viewDataKey;

        private readonly string viewDataKey = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// View source가 이어받을 UI session.
        /// </summary>
        // ------------------------------------------------------------
        public IXeriUISession UISession => uiSession;

        private readonly IXeriUISession uiSession = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// UI view source 적용 범위를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriUIViewScope
        (
            string viewSourceID,
            string viewDataKey,
            IXeriUISession uiSession
        ) : base()
        {
            this.viewSourceID = viewSourceID ?? string.Empty;
            this.viewDataKey = viewDataKey ?? string.Empty;
            this.uiSession = uiSession;
        }

    #endregion

    }
}
