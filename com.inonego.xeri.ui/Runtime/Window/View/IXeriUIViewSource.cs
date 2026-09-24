/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriUIViewSource.cs
수정일 : 2026-09-28

# 설명
UITK VisualElement 획득·반환과 UI session 저장/로드를 제공하는 공통 view source 계약.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// UITK view를 획득·반환하고 UI session을 저장/로드하는 공통 계약.
    /// </summary>
    // ============================================================
    public interface IXeriUIViewSource
    {

    #region 프로퍼티

        // ------------------------------------------------------------
        /// <summary>
        /// View source를 식별하는 stable ID.
        /// </summary>
        // ------------------------------------------------------------
        string ID { get; }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Window가 사용할 VisualElement를 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        VisualElement AcquireView(XeriUIViewScope scope);

        // ------------------------------------------------------------
        /// <summary>
        /// Source가 생성한 View를 원래 획득 경로로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        void ReleaseView(XeriUIViewScope scope, VisualElement view);

        // ------------------------------------------------------------
        /// <summary>
        /// View의 현재 UI session을 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        void SaveSession(XeriUIViewScope scope);

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 UI session을 view에 로드한다.
        /// </summary>
        // ------------------------------------------------------------
        void LoadSession(XeriUIViewScope scope);

    #endregion

    }
}
