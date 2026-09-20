/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKLayerPanel.cs
수정일 : 2026-09-17
# 설명
PresentationLayerAsset의 공통 Screen Overlay 순서와 View 배치 Root를 PanelRenderer에 적용한다.
Layer는 placement·order·lifetime과 CSS/USS layout normalization만 담당한다.

# 특이사항, 제약사항
6000.7.0a6의 PanelRenderer Root getter가 internal이므로 해당 Unity 버전에서는 Reflection으로 조회한다.
별도 compositor나 Render Pipeline adapter를 만들지 않고 Unity native Screen Overlay 경로를 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Reflection;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ================================================================================
    /// <summary>
    /// PanelRenderer 기반 UI Toolkit Screen Overlay Presentation Layer backend.
    /// </summary>
    // ================================================================================
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class UITKLayerPanel :
        MonoBehaviour,
        IPresentationLayerDriver<VisualElement>
    {

    #region 내부 데이터

        private const string ROOT_USS_CLASS_NAME = "xeri-ui";
        private const string RUNTIME_BASELINE_RESOURCE_PATH =
            "Xeri/UI/Core/UIRuntimeBaseline";

        private static readonly PropertyInfo rootVisualElementProperty =
            typeof(PanelRenderer).GetProperty
            (
                "rootVisualElement",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

        private static StyleSheet runtimeBaseline = null;

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 View를 배치할 PanelRenderer Root.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Root => ResolveRoot();

        private PanelRenderer panelRenderer = null;

    #endregion

    #region IPresentationLayerDriver

        // ----------------------------------------------------------------------
        /// <summary>
        /// PanelRenderer가 Screen Overlay Layer 계약을 만족하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public bool Validate
        (
            PresentationLayerAsset asset,
            out string error
        )
        {
            if (asset == null)
            {
                error = "Layer Asset이 null입니다.";
                return false;
            }

            CachePanelRenderer();

            if (panelRenderer == null)
            {
                error = "Layer PanelRenderer가 없습니다.";
                return false;
            }

            if (!panelRenderer.enabled)
            {
                error = "Layer PanelRenderer가 비활성 상태입니다.";
                return false;
            }

            if (panelRenderer.panelSettings == null)
            {
                error = "Layer PanelRenderer PanelSettings가 연결되지 않았습니다.";
                return false;
            }

            if (panelRenderer.panelSettings.targetTexture != null)
            {
                error = "Layer PanelRenderer는 Target Texture를 사용할 수 없습니다.";
                return false;
            }

            if (panelRenderer.panelSettings.targetDisplay != 0)
            {
                error = "Layer PanelRenderer는 기본 Display를 사용해야 합니다.";
                return false;
            }

            if (rootVisualElementProperty == null)
            {
                error = "현재 Unity 버전에서 PanelRenderer Root를 조회할 수 없습니다.";
                return false;
            }

            error = "";
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// PanelRenderer의 Screen Overlay 정렬 순서를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetOrder(int order)
        {
            CachePanelRenderer();
            panelRenderer.sortingOrder = order;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Layer GameObject와 PanelRenderer Root 표시 상태를 함께 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void SetActive(bool active)
        {
            if (this == null) return;

            if (!active)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            var root = ResolveRoot();

            if (root == null)
            {
                throw new MissingReferenceException
                (
                    "UITK Layer PanelRenderer Root를 찾을 수 없습니다."
                );
            }

            ApplyRuntimeBaseline(root);
            root.style.display = DisplayStyle.Flex;
        }

    #endregion

    #region 내부 처리

        // ------------------------------------------------------------
        /// <summary>
        /// PanelRenderer가 생성한 내부 Root VisualElement를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        private VisualElement ResolveRoot()
        {
            CachePanelRenderer();

            if (panelRenderer == null || rootVisualElementProperty == null)
            {
                return null;
            }

            return rootVisualElementProperty.GetValue(panelRenderer) as VisualElement;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 같은 Layer GameObject의 PanelRenderer를 현재 Driver에 연결한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void CachePanelRenderer()
        {
            if (panelRenderer == null)
            {
                panelRenderer = GetComponent<PanelRenderer>();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Root 범위에 Xeri Control Baseline을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ApplyRuntimeBaseline(VisualElement root)
        {
            if (runtimeBaseline == null)
            {
                runtimeBaseline = Resources.Load<StyleSheet>
                (
                    RUNTIME_BASELINE_RESOURCE_PATH
                );
            }

            if (runtimeBaseline == null)
            {
                throw new MissingReferenceException
                (
                    $"UI Runtime Baseline '{RUNTIME_BASELINE_RESOURCE_PATH}'을 " +
                    "찾을 수 없습니다."
                );
            }

            root.AddToClassList(ROOT_USS_CLASS_NAME);

            if (!root.styleSheets.Contains(runtimeBaseline))
            {
                root.styleSheets.Add(runtimeBaseline);
            }
        }

    #endregion

    }
}