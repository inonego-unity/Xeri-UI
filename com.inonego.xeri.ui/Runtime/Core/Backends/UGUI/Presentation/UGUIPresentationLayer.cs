/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIPresentationLayer.cs
수정일 : 2026-09-22

# 설명
UGUI Presentation Layer의 RectTransform View 배치 Root와 논리 활성 상태를 제공한다.
Canvas Native Output과 sibling ordering은 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// RectTransform을 View 배치 Root로 사용하는 UGUI Presentation Layer.
    /// </summary>
    // ======================================================================
    public sealed class UGUIPresentationLayer : IPresentationLayerDriver<RectTransform>
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// View를 배치할 RectTransform Root.
        /// </summary>
        // ------------------------------------------------------------
        public RectTransform Root { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 RectTransform을 Layer Root로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal UGUIPresentationLayer(RectTransform root)
        {
            Root = root;
        }

    #endregion

    #region IPresentationLayerDriver

        // ------------------------------------------------------------
        /// <summary>
        /// UGUI Layer Root가 존재하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Validate(out string error)
        {
            if (Root == null)
            {
                error = "UGUI Layer Root RectTransform이 없습니다.";
                return false;
            }

            error = "";
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Root GameObject의 논리 활성 상태를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetActive(bool active)
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(active);
            }
        }

    #endregion

    }
}
