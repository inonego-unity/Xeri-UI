/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UISettingsAsset.cs
수정일 : 2026-10-07
# 설명
UI Runtime의 기본 PresentationLayout, app-wide Presentation destination, backend capability와 Input System 공통 설정을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

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
        /// App 수명 기본 Presentation Layout.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationLayout DefaultLayout => defaultLayout;

        [SerializeField]
        private PresentationLayout defaultLayout = null;

        // ----------------------------------------------------------------------
        /// <summary>
        /// Top-level UITK Layer Output마다 복제할 PanelSettings template.
        /// </summary>
        // ----------------------------------------------------------------------
        public PanelSettings UITKPanelSettingsTemplate => uitkPanelSettingsTemplate;

        [SerializeField]
        private PanelSettings uitkPanelSettingsTemplate = null;

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Top-level UGUI Layer Output의 authoring template.
        /// <br/> 비어 있으면 package 기본 template을 사용한다.
        /// </summary>
        // ------------------------------------------------------------
        public UGUIPresentationOutput UGUIOutputTemplate => uguiOutputTemplate;

        [SerializeField]
        private UGUIPresentationOutput uguiOutputTemplate = null;

        // ----------------------------------------------------------------------
        /// <summary>
        /// Root Presentation Layer에 연결할 app-wide semantic destination 정의.
        /// </summary>
        // ----------------------------------------------------------------------
        public IReadOnlyList<PresentationDestinationDefinition> DestinationDefinitions =>
            presentationDestinations;

        [SerializeField]
        private PresentationDestinationDefinition[] presentationDestinations =
            Array.Empty<PresentationDestinationDefinition>();

        // ----------------------------------------------------------------------
        /// <summary>
        /// Runtime bootstrap에서 지원할 UI backend infrastructure capability.
        /// </summary>
        // ----------------------------------------------------------------------
        public PresentationBackendSupport BackendSupport => backendSupport;

        [SerializeField]
        private PresentationBackendSupport backendSupport =
            PresentationBackendSupport.UITK;

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
        /// UI Toolkit/Input policy가 사용할 UI Action Asset.
        /// </summary>
        // ------------------------------------------------------------
        public InputActionAsset UIActionsAsset => uiActionsAsset;

        [SerializeField]
        private InputActionAsset uiActionsAsset = null;

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
            if (defaultLayout == null)
            {
                throw new InvalidOperationException("UI 기본 Presentation Layout이 설정되지 않았습니다.");
            }

            if (uitkPanelSettingsTemplate == null)
            {
                throw new InvalidOperationException("UITK PanelSettings Template이 설정되지 않았습니다.");
            }

            ValidatePresentationComposition();

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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// destination mapping과 backend capability가 Root Layout 계약과 일치하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ValidatePresentationComposition()
        {
            if (presentationDestinations == null || presentationDestinations.Length == 0)
            {
                throw new InvalidOperationException("Presentation Destination이 하나 이상 필요합니다.");
            }

            if (backendSupport == PresentationBackendSupport.None)
            {
                throw new InvalidOperationException("Presentation Backend capability가 비어 있습니다.");
            }

            var unsupportedCapabilities =
                backendSupport & ~PresentationBackendSupport.All;

            if (unsupportedCapabilities != PresentationBackendSupport.None)
            {
                throw new InvalidOperationException
                (
                    $"정의되지 않은 Presentation Backend capability '{unsupportedCapabilities}'가 있습니다."
                );
            }

            var destinationIDs = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < presentationDestinations.Length; index++)
            {
                var definition = presentationDestinations[index];

                if (definition == null)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Destination {index} 정의가 비어 있습니다."
                    );
                }

                if
                (
                    string.IsNullOrWhiteSpace(definition.DestinationID) ||
                    !destinationIDs.Add(definition.DestinationID)
                )
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Destination ID '{definition.DestinationID}'가 비어 있거나 중복됐습니다."
                    );
                }

                if (string.IsNullOrWhiteSpace(definition.LayerID))
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Destination '{definition.DestinationID}' Layer ID가 비어 있습니다."
                    );
                }
            }

            if (!destinationIDs.Contains(PresentationDestinationID.System))
            {
                throw new InvalidOperationException
                (
                    $"Scene Fade에 필요한 Presentation Destination '{PresentationDestinationID.System}'이 없습니다."
                );
            }

            var layers = defaultLayout.Layers;

            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer == null) continue;

                var required = layer.Backend switch
                {
                    PresentationBackend.UGUI => PresentationBackendSupport.UGUI,
                    PresentationBackend.UITK => PresentationBackendSupport.UITK,
                    _ => throw new InvalidOperationException
                    (
                        $"Presentation Layer '{layer.LayerID}' backend 값 '{layer.Backend}'이 정의되지 않았습니다."
                    ),
                };

                if ((backendSupport & required) != required)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer '{layer.LayerID}' backend '{layer.Backend}'이 " +
                        "Runtime Backend capability에 포함되지 않았습니다."
                    );
                }
            }
        }

        internal bool SupportsBackend(PresentationBackend backend)
        {
            var required = backend switch
            {
                PresentationBackend.UGUI => PresentationBackendSupport.UGUI,
                PresentationBackend.UITK => PresentationBackendSupport.UITK,
                _ => throw new ArgumentOutOfRangeException
                (
                    nameof(backend),
                    backend,
                    "정의되지 않은 Presentation Backend입니다."
                ),
            };

            return (backendSupport & required) == required;
        }

    #endregion

    }
}
