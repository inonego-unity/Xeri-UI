/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriUIViewScope.cs
수정일 : 2026-09-20

# 설명
UITK view 생성, session 저장, session 로드에 필요한 런타임 적용 범위.

# 특이사항
VisualElement는 Unity 직렬화 대상이 아니므로 이 타입은 런타임 전달 객체로만 사용한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

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

        // ------------------------------------------------------------
        /// <summary>
        /// View가 붙을 host root.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement HostRoot => hostRoot;

        private readonly VisualElement hostRoot = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 생성된 view가 붙을 slot.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement ViewSlot => viewSlot;

        private readonly VisualElement viewSlot = null;

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
            IXeriUISession uiSession,
            VisualElement hostRoot,
            VisualElement viewSlot
        ) : base()
        {
            this.viewSourceID = viewSourceID ?? string.Empty;
            this.viewDataKey  = viewDataKey ?? string.Empty;
            this.uiSession    = uiSession;
            this.hostRoot     = hostRoot;
            this.viewSlot     = viewSlot;
        }

    #endregion

    }
}
