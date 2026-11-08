/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriUIViewSource.cs
수정일 : 2026-11-09

# 설명
UITK VisualElement 획득·반환을 담당하는 필수 view source 계약. UI session 영속화는 선택적 IXeriUIViewSessionPersistence가 담당한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// UITK view 획득·반환의 기본 계약. 세션 저장·로드는 선택적 계약이다.
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

    #endregion

    }
}
