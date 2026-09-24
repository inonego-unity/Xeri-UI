/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriTrayEntry.cs
수정일 : 2026-10-05

# 설명
Tray에 표시할 단일 항목의 공통 데이터.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray에 표시할 단일 entry 데이터.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class XeriTrayEntry
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Entry를 식별하는 stable ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ID
        {
            get => id;
            set
            {
                id = value ?? string.Empty;
            }
        }

        [SerializeField]
        private string id = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 표시 제목.
        /// </summary>
        // ------------------------------------------------------------
        public string Title
        {
            get => title;
            set
            {
                title = value ?? string.Empty;
            }
        }

        [SerializeField]
        private string title = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry tooltip.
        /// </summary>
        // ------------------------------------------------------------
        public string Tooltip
        {
            get => tooltip;
            set
            {
                tooltip = value ?? string.Empty;
            }
        }

        [SerializeField]
        private string tooltip = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry icon.
        /// </summary>
        // ------------------------------------------------------------
        public Texture2D Icon
        {
            get => icon;
            set
            {
                icon = value;
            }
        }

        [SerializeField]
        private Texture2D icon = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry badge.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayBadge Badge
        {
            get => badge;
            set
            {
                badge = value;
            }
        }

        [SerializeField]
        private XeriTrayBadge badge = default;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry가 현재 활성 상태인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsActive
        {
            get => isActive;
            set
            {
                isActive = value;
            }
        }

        [SerializeField]
        private bool isActive = false;

        // ------------------------------------------------------------
        /// <summary>
        /// 연결 콘텐츠가 숨김 없이 표시된 상태이며 현재 포커스와 별개이다.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsVisible
        {
            get => isVisible;
            set
            {
                isVisible = value;
            }
        }

        [SerializeField]
        private bool isVisible = false;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 닫기 동작을 허용하는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool CanClose
        {
            get => canClose;
            set
            {
                canClose = value;
            }
        }

        [SerializeField]
        private bool canClose = true;

        // ------------------------------------------------------------
        /// <summary>
        /// 저장과 복원에 사용할 payload stable ID.
        /// </summary>
        // ------------------------------------------------------------
        public string PayloadID
        {
            get => payloadID;
            set
            {
                payloadID = value ?? string.Empty;
            }
        }

        [SerializeField]
        private string payloadID = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// 상위 시스템이 연결할 런타임 payload.
        /// </summary>
        // ------------------------------------------------------------
        public object Payload
        {
            get => payload;
            set
            {
                payload = value;
            }
        }

        [NonSerialized]
        private object payload = null;

    #endregion

    #region 생성자

        public XeriTrayEntry()
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayEntry(string id, string title) : this()
        {
            ID    = id;
            Title = title;
        }

    #endregion

    }
}
