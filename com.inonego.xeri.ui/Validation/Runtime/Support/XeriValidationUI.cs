/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationUI.cs
수정일 : 2026-10-05

# 설명
Validation UXML 인스턴스의 필수 element 조회, Presentation placement 획득과 Modal focus containment 공통 처리를 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// 템플릿의 필수 요소를 조회한다.
    /// </summary>
    // ============================================================
    internal static class XeriValidationUI
    {

    #region 템플릿 조회

        // ------------------------------------------------------------
        /// <summary>
        /// 템플릿에서 지정 Root를 분리해 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static VisualElement CloneRoot
        (
            VisualTreeAsset template,
            string rootName
        )
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var container = template.Instantiate();
            var root = container.Q<VisualElement>(rootName);

            if (root == null)
            {
                throw new InvalidOperationException
                (
                    $"Validation UXML에 '{rootName}' root가 없습니다."
                );
            }

            root.RemoveFromHierarchy();
            return root;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이름과 형식이 일치하는 필수 요소를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static T Require<T>
        (
            VisualElement root,
            string name
        )
        where T : VisualElement
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var element = root.Q<T>(name);

            if (element == null)
            {
                throw new InvalidOperationException
                (
                    $"Validation UI에 '{name}' {typeof(T).Name} element가 없습니다."
                );
            }

            return element;
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// 작성된 Placement에 콘텐츠를 연결하고 반환한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationPlacementSource : IPresentationSource<VisualElement>
    {

    #region 필드와 상태

        private readonly VisualElement element = null;
        private readonly bool attach = false;

    #endregion

    #region Placement 연결

        // ------------------------------------------------------------
        /// <summary>
        /// 작성된 Placement에 콘텐츠를 연결하고 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationPlacementSource
        (
            VisualElement element = null,
            bool attach = false
        )
        {
            if (attach && element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            this.element = element;
            this.attach = attach;
        }

    #endregion

    #region 콘텐츠 획득과 반환

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation에 표시할 콘텐츠를 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Acquire(IPresentationLayerDriver layer)
        {
            if
            (
                layer is not IPresentationLayerDriver<VisualElement> uitk ||
                uitk.Root == null
            )
            {
                throw new InvalidOperationException
                (
                    "Validation Placement가 UITK Root를 제공하지 않습니다."
                );
            }

            if (!attach)
            {
                return uitk.Root;
            }

            uitk.Root.Add(element);
            return element;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 소유한 콘텐츠를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Release(VisualElement view)
        {
            if (attach)
            {
                view?.RemoveFromHierarchy();
            }
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// 콘텐츠 하위 요소로 포커스 범위를 제한한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationVisualFocusScope : IFocusScope
    {

    #region 필드와 상태

        // ------------------------------------------------------------
        /// <summary>
        /// 화면 진입 시 포커스를 받을 요소.
        /// </summary>
        // ------------------------------------------------------------
        public object DefaultFocus { get; }

        private readonly VisualElement root = null;

    #endregion

    #region 포커스 범위 연결

        // ------------------------------------------------------------
        /// <summary>
        /// 콘텐츠 하위 요소로 포커스 범위를 제한한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationVisualFocusScope
        (
            VisualElement root,
            object defaultFocus
        )
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            DefaultFocus = defaultFocus;
        }

    #endregion

    #region 포커스 포함 여부

        // ------------------------------------------------------------
        /// <summary>
        /// 대상이 이 콘텐츠의 하위 요소인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool ContainsFocus(object target)
        {
            if (target is not VisualElement element)
            {
                return false;
            }

            for (var current = element; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, root))
                {
                    return true;
                }
            }

            return false;
        }

    #endregion

    }
}
