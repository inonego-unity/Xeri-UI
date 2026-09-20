/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UISettingsAsset.cs
수정일 : 2026-09-17
# 설명
UI Runtime의 기본 Profile, Scene Fade와 Input System 공통 설정을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Primitive;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Runtime 조립 설정 Asset.
    /// </summary>
    // ============================================================
    [CreateAssetMenu
    (
        fileName = "UI Settings",
        menuName = "Xeri/UI/Settings"
    )]
    public sealed class UISettingsAsset : ScriptableObject
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// App 수명 기본 Layer Profile.
        /// </summary>
        // ------------------------------------------------------------
        public UIProfileAsset DefaultProfile => defaultProfile;

        [SerializeField]
        private UIProfileAsset defaultProfile = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Scene Fade Overlay를 표시할 기본 Profile Layer ID.
        /// </summary>
        // ------------------------------------------------------------
        public string SceneFadeLayerID => sceneFadeLayerID;

        [SerializeField]
        private string sceneFadeLayerID = "";

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 Scene Fade 색상.
        /// </summary>
        // ------------------------------------------------------------
        public Color DefaultFadeColor => defaultFadeColor;

        [SerializeField]
        private Color defaultFadeColor = Color.black;

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 Scene Fade 시간.
        /// </summary>
        // ------------------------------------------------------------
        public float DefaultFadeDuration => defaultFadeDuration;

        [SerializeField]
        [Min(0.0f)]
        private float defaultFadeDuration = 0.25f;

        // ------------------------------------------------------------
        /// <summary>
        /// Input System UI Action Map 이름.
        /// </summary>
        // ------------------------------------------------------------
        public string UIActionMap => uiActionMap;

        [SerializeField]
        private string uiActionMap = "UI";

        // ------------------------------------------------------------
        /// <summary>
        /// 프로젝트 Gameplay Action을 소유하는 Input Action Asset.
        /// </summary>
        // ------------------------------------------------------------
        public InputActionAsset GameplayActionsAsset => gameplayActionsAsset;

        [SerializeField]
        private InputActionAsset gameplayActionsAsset = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Input System Gameplay Action Map 이름.
        /// </summary>
        // ------------------------------------------------------------
        public string GameplayActionMap => gameplayActionMap;

        [SerializeField]
        private string gameplayActionMap = "Player";

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 종료 뒤 해제를 기다릴 UI Action 이름.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<string> ReleaseActionNames => releaseActionNames;

        [SerializeField]
        private string[] releaseActionNames =
        {
            "Cancel",
            "Submit",
        };

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 조립에 필요한 순수 설정을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Validate()
        {
            if (defaultProfile == null)
            {
                throw new InvalidOperationException("UI 기본 Profile이 설정되지 않았습니다.");
            }

            if (string.IsNullOrWhiteSpace(sceneFadeLayerID))
            {
                throw new InvalidOperationException("Scene Fade Layer ID가 비어 있습니다.");
            }

            if
            (
                !defaultFadeDuration.IsFinite() ||
                defaultFadeDuration < 0.0f
            )
            {
                throw new InvalidOperationException("Scene Fade 시간은 유한한 0 이상의 값이어야 합니다.");
            }

            if (string.IsNullOrWhiteSpace(uiActionMap))
            {
                throw new InvalidOperationException("UI Action Map 이름이 비어 있습니다.");
            }

            if (gameplayActionsAsset == null)
            {
                throw new InvalidOperationException("Gameplay Input Action Asset이 설정되지 않았습니다.");
            }

            if (string.IsNullOrWhiteSpace(gameplayActionMap))
            {
                throw new InvalidOperationException("Gameplay Action Map 이름이 비어 있습니다.");
            }

            if (releaseActionNames == null || releaseActionNames.Length == 0)
            {
                throw new InvalidOperationException("입력 해제 Action 이름이 하나 이상 필요합니다.");
            }

            for (var i = 0; i < releaseActionNames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(releaseActionNames[i]))
                {
                    throw new InvalidOperationException
                    (
                        $"입력 해제 Action 이름 {i}가 비어 있습니다."
                    );
                }
            }
        }

    #endregion

    }
}
