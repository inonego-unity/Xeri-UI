/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ProjectionResult.cs
수정일 : 2026-09-17
# 설명
World 위치를 backend Root의 로컬 위치에 투영한 결과를 정의한다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// backend Root 로컬 좌표의 UI Projection 결과.
    /// </summary>
    // ============================================================
    public readonly struct ProjectionResult
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Projection이 성공했는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool Succeeded { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// backend Root 기준 로컬 위치.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 LocalPosition { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// World 위치가 Camera 뒤에 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsBehindCamera { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// UI Projection 결과를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public ProjectionResult
        (
            bool succeeded,
            Vector2 localPosition,
            bool isBehindCamera
        ) : this()
        {
            Succeeded = succeeded;
            LocalPosition = localPosition;
            IsBehindCamera = isBehindCamera;
        }

    #endregion

    }
}
