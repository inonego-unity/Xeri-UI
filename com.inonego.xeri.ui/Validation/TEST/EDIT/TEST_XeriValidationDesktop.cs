/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriValidationDesktop.cs
수정일 : 2026-10-05

# 설명
실제 Validation Desktop UXML의 authored Placement topology를 검증한다.

# 테스트 구성
 H: Layer ManagedRoot의 직계 자식이어야 하는 Placement 계약
========================================================================= BLOCK_HEADER_END */

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Validation
{
    // ============================================================
    /// <summary>
    /// 실제 Desktop 자산의 Presentation Host 계약을 검증한다.
    /// </summary>
    // ============================================================
    public sealed class TEST_XeriValidationDesktop
    {

    #region H-1: authored Placement topology

        // ----------------------------------------------------------------------
        /// <summary>
        /// 장식이나 스크롤을 추가해도 Host가 ManagedRoot 직계 자식으로 남는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [TestCase("DesktopStatus")]
        [TestCase("DesktopTaskbar")]
        public void TEST_XeriValidationDesktop_PlacementHost_ManagedRoot직계자식(string name)
        {
            // 실제 소비 자산을 사용해 UXML 구조 변경으로 인한 초기화 실패를 잡는다.
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>
            (
                "Packages/com.inonego.xeri.ui/Validation/UI/Shell/XeriValidationDesktop.uxml"
            );
            Assert.That(template, Is.Not.Null);
            var tree = template.CloneTree();
            var managed = tree.Q<VisualElement>("ManagedRoot");
            var placement = tree.Q<VisualElement>(name);

            // 요소 존재만으로는 부모 topology 계약이 충족되지 않는다.
            Assert.That(managed, Is.Not.Null);
            Assert.That(placement, Is.Not.Null);
            Assert.That(placement.parent, Is.SameAs(managed));
        }

    #endregion

    }
}
