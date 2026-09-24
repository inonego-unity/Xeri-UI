/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayOptions.cs
수정일 : 2026-09-20

# 설명
공통 Tray 표시 옵션.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray 표시 옵션.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class XeriTrayOptions
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Entry에서 표시할 구성 요소.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayContent VisibleContent
        {
            get => visibleContent;
            set => visibleContent = value;
        }

        [SerializeField]
        private XeriTrayContent visibleContent = XeriTrayContent.All;

        // ------------------------------------------------------------
        /// <summary>
        /// Tray root에 추가할 USS class.
        /// </summary>
        // ------------------------------------------------------------
        public string UssClass
        {
            get => ussClass;
            set => ussClass = value ?? string.Empty;
        }

        [SerializeField]
        private string ussClass = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry reorder drag 허용 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool Reorderable
        {
            get => reorderable;
            set => reorderable = value;
        }

        [SerializeField]
        private bool reorderable = false;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry reorder drag 축.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayReorderAxis ReorderAxis
        {
            get => reorderAxis;
            set => reorderAxis = value;
        }

        [SerializeField]
        private XeriTrayReorderAxis reorderAxis = XeriTrayReorderAxis.Horizontal;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry reorder preview 애니메이션 사용 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool AnimateReorder
        {
            get => animateReorder;
            set => animateReorder = value;
        }

        [SerializeField]
        private bool animateReorder = true;

    #endregion

    #region 생성자

        public XeriTrayOptions() : base()
        {
            // NONE
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 Tray 옵션을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public static XeriTrayOptions Default()
        {
            return new XeriTrayOptions
            {
                VisibleContent = XeriTrayContent.All,
                UssClass = string.Empty,
                Reorderable = false,
                ReorderAxis = XeriTrayReorderAxis.Horizontal,
                AnimateReorder = true,
            };
        }

    #endregion

    }
}
