/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationLayer.cs
수정일 : 2026-09-22

# 설명
UITK Presentation Layer의 VisualElement View 배치 Root와 논리 활성 상태를 제공한다.
PanelRenderer Native Output과 sibling ordering은 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// VisualElement를 View 배치 Root로 사용하는 UITK Presentation Layer.
    /// </summary>
    // ======================================================================
    public sealed class UITKPresentationLayer : IPresentationLayerDriver<VisualElement>
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// View를 배치할 VisualElement Root.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Root { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 VisualElement를 Layer Root로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal UITKPresentationLayer(VisualElement root)
        {
            Root = root;
        }

    #endregion

    #region 프레젠테이션 레이어 드라이버 구현

        // ------------------------------------------------------------
        /// <summary>
        /// UITK Layer Root가 존재하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Validate(out string error)
        {
            if (Root == null)
            {
                error = "UITK Layer Root VisualElement가 없습니다.";
                return false;
            }

            error = "";
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Root의 display 상태로 논리 활성 상태를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetActive(bool active)
        {
            if (Root != null)
            {
                Root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

    #endregion

    }
}
