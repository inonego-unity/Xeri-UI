/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationDesktopAuthoring.cs
수정일 : 2026-10-05

# 설명
Validation Scene의 scene-authored Desktop UITK Layer Host를 실제 PanelRenderer tree와 연결한다.
ManagedRoot와 authored Status/Taskbar Placement reference만 설정하며 topology/order는 PresentationPlan에 맡긴다.
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
    /// 작성된 Desktop Host와 실제 UITK 트리를 연결한다.
    /// </summary>
    // ============================================================
    [DisallowMultipleComponent]
    public sealed class XeriValidationDesktopAuthoring : MonoBehaviour
    {

    #region 필드와 상태

        private const int MANAGED_ROOT_AUTHORING_ID = 120;
        private const int STATUS_AUTHORING_ID = 130;
        private const int TASKBAR_AUTHORING_ID = 140;

        // ------------------------------------------------------------
        /// <summary>
        /// Desktop의 UITK 출력.
        /// </summary>
        // ------------------------------------------------------------
        public UITKPresentationOutput Output => output;
        private UITKPresentationOutput output = null;

        [SerializeField]
        private XeriValidationAssets assets = null;
        private PanelRenderer panelRenderer = null;
        private UITKPresentationLayerHost layerHost = null;
        private bool isPrepared = false;

    #endregion

    #region 작성된 Host 연결

        // ------------------------------------------------------------
        /// <summary>
        /// Desktop 렌더러와 Host 참조를 준비한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Prepare()
        {
            if (isPrepared)
            {
                return;
            }

            if (assets == null)
            {
                throw new InvalidOperationException
                (
                    "Validation Desktop Authoring Asset catalog가 없습니다."
                );
            }

            if (assets.PanelSettings == null || assets.DesktopTemplate == null)
            {
                throw new InvalidOperationException
                (
                    "Validation Desktop PanelSettings 또는 UXML이 없습니다."
                );
            }

            layerHost = GetComponent<UITKPresentationLayerHost>() ??
                throw new MissingComponentException
                (
                    "Validation Desktop에 UITKPresentationLayerHost가 없습니다."
                );
            panelRenderer = GetComponent<PanelRenderer>() ??
                gameObject.AddComponent<PanelRenderer>();
            output = GetComponent<UITKPresentationOutput>() ??
                gameObject.AddComponent<UITKPresentationOutput>();

            panelRenderer.panelSettings = assets.PanelSettings;
            panelRenderer.visualTreeAsset = assets.DesktopTemplate;

            layerHost.ManagedRootReference.SetReference
            (
                panelRenderer,
                new AuthoringIdPath(MANAGED_ROOT_AUTHORING_ID)
            );

            ConfigurePlacementHosts();
            isPrepared = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 작성된 Status와 Taskbar의 직접 자식 Host를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ConfigurePlacementHosts()
        {
            var hosts = GetComponentsInChildren<UITKPresentationPlacementHost>(true);
            var statusBound = false;
            var taskbarBound = false;

            foreach (var host in hosts)
            {
                if (host.PresentationID == XeriValidationIDs.StatusPresentation)
                {
                    host.RootReference.SetReference
                    (
                        panelRenderer,
                        new AuthoringIdPath(STATUS_AUTHORING_ID)
                    );
                    statusBound = true;
                    continue;
                }

                if (host.PresentationID == XeriValidationIDs.TaskbarPresentation)
                {
                    host.RootReference.SetReference
                    (
                        panelRenderer,
                        new AuthoringIdPath(TASKBAR_AUTHORING_ID)
                    );
                    taskbarBound = true;
                }
            }

            if (!statusBound || !taskbarBound)
            {
                throw new InvalidOperationException
                (
                    "Validation Desktop authored Status/Taskbar Placement Host 구성이 불완전합니다."
                );
            }
        }

    #endregion

    }
}
