/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenOptions.cs
수정일 : 2026-09-30
# 설명
Screen 등록 시 재사용할 중복, Gameplay Input, Cursor와 Transition 정책을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Primitive;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Screen 등록 정책.
    /// </summary>
    // ============================================================
    public sealed class ScreenOptions
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Screen stable string ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ID { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 동일 ID Screen의 중복 Open 정책.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenDuplicatePolicy DuplicatePolicy { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen이 Gameplay 입력을 차단하는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool BlocksGameplayInput { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 활성 중 Cursor 표시 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool ShowsCursor { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 활성 중 Cursor 잠금 정책.
        /// </summary>
        // ------------------------------------------------------------
        public CursorLockMode CursorLockMode { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 열기 Transition 시간.
        /// </summary>
        // ------------------------------------------------------------
        public float OpenDuration { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 닫기 Transition 시간.
        /// </summary>
        // ------------------------------------------------------------
        public float CloseDuration { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 등록 정책을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenOptions
        (
            string id,
            ScreenDuplicatePolicy duplicatePolicy = ScreenDuplicatePolicy.Reject,
            bool blocksGameplayInput = true,
            bool showsCursor = true,
            CursorLockMode cursorLockMode = CursorLockMode.None,
            float openDuration = 0.2f,
            float closeDuration = 0.2f
        ) : base()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Screen ID가 비어 있습니다.", nameof(id));
            }

            if
            (
                !openDuration.IsFinite() ||
                openDuration < 0.0f
            )
            {
                throw new ArgumentOutOfRangeException(nameof(openDuration));
            }

            if
            (
                !closeDuration.IsFinite() ||
                closeDuration < 0.0f
            )
            {
                throw new ArgumentOutOfRangeException(nameof(closeDuration));
            }

            ID = id;
            DuplicatePolicy = duplicatePolicy;
            BlocksGameplayInput = blocksGameplayInput;
            ShowsCursor = showsCursor;
            CursorLockMode = cursorLockMode;
            OpenDuration = openDuration;
            CloseDuration = closeDuration;
        }

    #endregion

    }
}
