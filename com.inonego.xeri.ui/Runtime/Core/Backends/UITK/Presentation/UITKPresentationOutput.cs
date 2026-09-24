/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationOutput.cs
수정일 : 2026-10-07

# 설명
Top-level UITK ScreenOverlay Layer 하나를 PanelRenderer Native Output으로 구성한다.
Generated output은 Runtime PanelSettings clone을 소유하고 Scene-authored output은 연결된 PanelSettings identity를 유지한 채 sortingOrder만 scoped 적용·복원한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class UITKPresentationOutput : MonoBehaviour
    {

    #region 필드

        private const string ROOT_USS_CLASS_NAME = "xeri-ui";
        private const string RUNTIME_BASELINE_RESOURCE_PATH = "Xeri/UI/Core/UIRuntimeBaseline";

        public PanelRenderer PanelRenderer => panelRenderer != null
            ? panelRenderer
            : GetComponent<PanelRenderer>();

        private PanelRenderer panelRenderer = null;

        public VisualElement Root => root;

        private VisualElement root = null;

        public PanelSettings RuntimePanelSettings { get; private set; }

        private PanelSettings originalPanelSettings = null;
        private float originalSortingOrder = 0.0f;
        private VisualElement layerRoot = null;
        private StyleSheet runtimeBaseline = null;
        private bool restoreOriginalPanelSettings = false;
        private bool isReloadCallbackRegistered = false;

    #endregion

    #region 출력 수명

        internal void Initialize
        (
            PanelSettings template,
            int layerOrder
        )
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            panelRenderer = GetComponent<PanelRenderer>() ??
                throw new MissingComponentException("UITK Presentation Output PanelRenderer가 없습니다.");

            if (RuntimePanelSettings != null || restoreOriginalPanelSettings)
            {
                throw new InvalidOperationException
                (
                    "UITK Presentation Output이 이미 초기화됐습니다."
                );
            }

            originalPanelSettings = null;
            restoreOriginalPanelSettings = false;

            try
            {
                CreateRuntimePanelSettings(template, layerOrder);
                panelRenderer.visualTreeAsset = null;
                panelRenderer.panelSettings = RuntimePanelSettings;

                // native callback 등록이 완료 뒤 throw해도 rollback이 unregister를 시도하도록 먼저 ownership을 표시한다.
                isReloadCallbackRegistered = true;
                panelRenderer.RegisterUIReloadCallback(HandleUIReload);
            }
            catch (Exception exception)
            {
                try
                {
                    ReleaseOutputState();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        "UITK Presentation Output 초기화와 rollback이 모두 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Scene-authored PanelRenderer와 PanelSettings를 borrow한다.
        /// <br/> 연결된 PanelSettings identity를 유지하고 sortingOrder만 scoped 적용·복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void InitializeBorrowed(int layerOrder)
        {
            panelRenderer = GetComponent<PanelRenderer>() ??
                throw new MissingComponentException("UITK Presentation Output PanelRenderer가 없습니다.");

            if (RuntimePanelSettings != null || restoreOriginalPanelSettings)
            {
                throw new InvalidOperationException
                (
                    "UITK Presentation Output이 이미 초기화됐습니다."
                );
            }

            originalPanelSettings = panelRenderer.panelSettings;

            if (originalPanelSettings == null)
            {
                throw new InvalidOperationException
                (
                    "Scene-authored UITK Output에는 PanelSettings가 미리 연결되어 있어야 합니다."
                );
            }

            restoreOriginalPanelSettings = true;
            originalSortingOrder = originalPanelSettings.sortingOrder;

            try
            {
                originalPanelSettings.sortingOrder = layerOrder;

                // callback registration은 외부 native 경계이므로 시도 전에 cleanup ownership을 확정한다.
                isReloadCallbackRegistered = true;
                panelRenderer.RegisterUIReloadCallback(HandleUIReload);
            }
            catch (Exception exception)
            {
                try
                {
                    ReleaseOutputState();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        "Borrowed UITK Presentation Output 초기화와 rollback이 모두 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        private void CreateRuntimePanelSettings
        (
            PanelSettings template,
            int layerOrder
        )
        {
            RuntimePanelSettings = Instantiate(template);
            RuntimePanelSettings.name = $"{template.name} - Layer {layerOrder}";
            RuntimePanelSettings.targetTexture = null;
            RuntimePanelSettings.sortingOrder = layerOrder;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> PanelRenderer callback과 Layer root를 해제하고
        /// <br/> generated/borrowed output state를 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ReleaseOutputState()
        {
            if
            (
                RuntimePanelSettings == null &&
                !restoreOriginalPanelSettings &&
                !isReloadCallbackRegistered &&
                layerRoot == null &&
                root == null
            )
            {
                return;
            }

            var errors = new List<Exception>();
            var currentLayerRoot = layerRoot;
            var settings = RuntimePanelSettings;
            var shouldRestoreOriginal = restoreOriginalPanelSettings;
            var originalSettings = originalPanelSettings;
            var sortingOrder = originalSortingOrder;

            layerRoot = null;
            root = null;
            RuntimePanelSettings = null;
            originalPanelSettings = null;
            originalSortingOrder = 0.0f;
            restoreOriginalPanelSettings = false;

            if (panelRenderer != null && isReloadCallbackRegistered)
            {
                isReloadCallbackRegistered = false;

                try
                {
                    panelRenderer.UnregisterUIReloadCallback(HandleUIReload);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            try
            {
                currentLayerRoot?.RemoveFromHierarchy();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (panelRenderer != null)
            {
                if (shouldRestoreOriginal)
                {
                    if (originalSettings != null)
                    {
                        try
                        {
                            originalSettings.sortingOrder = sortingOrder;
                        }
                        catch (Exception exception)
                        {
                            errors.Add(exception);
                        }
                    }
                }
                else
                {
                    try
                    {
                        panelRenderer.panelSettings = null;
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }

                    try
                    {
                        panelRenderer.visualTreeAsset = null;
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }

            if (settings != null)
            {
                try
                {
                    DestroyOwnedObject(settings);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    "UITK Presentation Output 해제가 실패했습니다.",
                    errors
                );
            }
        }

    #endregion

    #region 검증과 루트 연결

        public bool Validate(out string error)
        {
            if (PanelRenderer == null || PanelRenderer.panelSettings == null)
            {
                error = "UITK Presentation Output PanelRenderer/PanelSettings가 없습니다.";
                return false;
            }

            if (PanelRenderer.panelSettings.themeStyleSheet == null)
            {
                error = "UITK Presentation Output ThemeStyleSheet이 없습니다.";
                return false;
            }

            if (PanelRenderer.panelSettings.targetTexture != null)
            {
                error = "UITK ScreenOverlay Output은 Target Texture를 사용할 수 없습니다.";
                return false;
            }

            error = "";
            return true;
        }

        internal VisualElement RequireRoot()
        {
            if (!Validate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            RefreshCurrentRoot();

            return Root ??
                throw new MissingReferenceException("UITK Presentation Output Root를 찾을 수 없습니다.");
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 PanelRenderer Root를 public reload callback 경로로 동기 재확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void RefreshCurrentRoot()
        {
            if (panelRenderer == null) return;

            if (isReloadCallbackRegistered)
            {
                panelRenderer.UnregisterUIReloadCallback(HandleUIReload);
            }

            panelRenderer.RegisterUIReloadCallback(HandleUIReload);
            isReloadCallbackRegistered = true;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Xeri Layer Root를 현재 PanelRenderer visual tree에 연결하고 reload 뒤에도 보존한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void AttachLayerRoot(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (layerRoot != null && !ReferenceEquals(layerRoot, root))
            {
                throw new InvalidOperationException
                (
                    "UITK Presentation Output에는 Layer Root를 하나만 연결할 수 있습니다."
                );
            }

            layerRoot = root;
            AttachLayerRootToCurrentRoot();
        }

    #endregion

    #region 리로드와 Unity 수명

        // --------------------------------------------------------------------------------
        /// <summary>
        /// PanelRenderer가 새 visual tree를 제공하면 baseline과 Layer Root를 다시 연결한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void HandleUIReload
        (
            PanelRenderer renderer,
            VisualElement rootElement,
            int version
        )
        {
            if (!ReferenceEquals(renderer, panelRenderer)) return;

            root = rootElement;
            ApplyRuntimeBaseline(rootElement);
            AttachLayerRootToCurrentRoot();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 visual tree가 준비됐다면 Layer Root를 그 아래에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AttachLayerRootToCurrentRoot()
        {
            if (root == null || layerRoot == null) return;
            if (ReferenceEquals(layerRoot.parent, root)) return;

            layerRoot.RemoveFromHierarchy();
            root.Add(layerRoot);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Unity 파괴 경계에서도 runtime PanelSettings ownership을 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void OnDestroy()
        {
            ReleaseOutputState();
        }

        private void ApplyRuntimeBaseline(VisualElement root)
        {
            runtimeBaseline ??= Resources.Load<StyleSheet>(RUNTIME_BASELINE_RESOURCE_PATH);

            if (runtimeBaseline == null)
            {
                throw new MissingReferenceException
                (
                    $"UI Runtime Baseline '{RUNTIME_BASELINE_RESOURCE_PATH}'을 찾을 수 없습니다."
                );
            }

            root.AddToClassList(ROOT_USS_CLASS_NAME);

            if (!root.styleSheets.Contains(runtimeBaseline))
            {
                root.styleSheets.Add(runtimeBaseline);
            }
        }

        private static void DestroyOwnedObject(UnityEngine.Object target)
        {
            if (target == null) return;

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

    #endregion

    }
}
