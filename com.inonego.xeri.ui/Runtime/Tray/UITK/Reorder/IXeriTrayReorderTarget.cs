/* BLOCK_HEADER_BEGIN =======================================================================
파일명: IXeriTrayReorderTarget.cs
수정일 : 2026-09-20

# 설명
Tray reorder 입력 계층이 사용하는 최소 view 계약을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray reorder 대상 view 계약.
    /// </summary>
    // ============================================================
    public interface IXeriTrayReorderTarget
    {
        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 입력 허용 여부.
        /// </summary>
        // ------------------------------------------------------------
        bool Reorderable { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder가 잠기는 이동 축.
        /// </summary>
        // ------------------------------------------------------------
        XeriTrayReorderAxis ReorderAxis { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Entry button들을 직접 포함하는 container.
        /// </summary>
        // ------------------------------------------------------------
        VisualElement EntryContainer { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Preview offset을 적용하는 animator.
        /// </summary>
        // ------------------------------------------------------------
        IXeriTrayReorderAnimator ReorderAnimator { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder proxy 생성에 사용할 현재 Tray 표시 옵션.
        /// </summary>
        // ------------------------------------------------------------
        XeriTrayOptions ReorderOptions { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 표시 중인 Tray button 목록을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        IReadOnlyList<XeriTrayButton> GetEntryButtons();

        // ------------------------------------------------------------
        /// <summary>
        /// Entry container 좌표계의 Tray button bounds 목록을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        IReadOnlyList<Rect> GetEntryBounds();

        // ------------------------------------------------------------
        /// <summary>
        /// Reorder 확정 요청을 상위 계층으로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        void InvokeEntryReorder(XeriTrayReorderRequest request);
    }
}
