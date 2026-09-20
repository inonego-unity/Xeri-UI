/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriUIViewSource.cs
수정일 : 2026-09-20

# 설명
UITK VisualElement 생성과 UI session 저장/로드를 제공하는 공통 view source 계약.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// UITK view를 생성하고 UI session을 저장/로드하는 공통 계약.
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
        /// View에 사용할 VisualElement를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        VisualElement CreateView(XeriUIViewScope scope);

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
