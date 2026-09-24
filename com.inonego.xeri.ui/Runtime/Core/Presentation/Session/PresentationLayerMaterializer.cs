/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayerMaterializer.cs
수정일 : 2026-09-28

# 설명
Top-level Presentation Layer를 Unity Native Output, LayerRoot와 ManagedRoot로 materialize하는 구현 경계다.
PresentationSession은 runtime state와 lifetime만 소유하고 backend별 생성 세부사항은 이 타입에 위임한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    internal static class PresentationLayerMaterializer
    {
        private const string DEFAULT_UGUI_TEMPLATE_PATH =
            "Xeri/UI/Core/Presentation/UIUGUIPresentationOutput";

        internal static IPresentationLayerDriver CreateTopLevel
        (
            PresentationPlanLayer plan,
            Transform parent,
            PanelSettings panelSettingsTemplate,
            UGUIPresentationOutput uguiOutputTemplate,
            out Action release
        )
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            switch (plan.Backend)
            {
                case PresentationBackend.UGUI:
                    return CreateUGUI(plan, parent, uguiOutputTemplate, out release);

                case PresentationBackend.UITK:
                    return CreateUITK(plan, parent, panelSettingsTemplate, out release);

                default:
                    throw new ArgumentOutOfRangeException(nameof(plan.Backend));
            }
        }

        private static IPresentationLayerDriver CreateUGUI
        (
            PresentationPlanLayer plan,
            Transform parent,
            UGUIPresentationOutput template,
            out Action release
        )
        {
            template ??= Resources.Load<UGUIPresentationOutput>(DEFAULT_UGUI_TEMPLATE_PATH);

            if (template == null)
            {
                throw new MissingReferenceException
                (
                    $"UGUI Presentation Output template '{DEFAULT_UGUI_TEMPLATE_PATH}'을 찾을 수 없습니다."
                );
            }

            var output = UnityEngine.Object.Instantiate(template, parent, false);
            output.name = $"Xeri UI Layer - {plan.LayerID}";
            output.gameObject.SetActive(false);

            try
            {
                output.Initialize(plan.LayerOrder);
                var managedRoot = UGUIPresentationSurface.CreateRect
                (
                    $"Managed Root - {plan.LayerID}",
                    output.Root
                );
                output.gameObject.SetActive(true);
                var driver = new UGUIPresentationLayer(managedRoot);
                release = () => DestroyObject(output.gameObject);
                return driver;
            }
            catch
            {
                DestroyObject(output.gameObject);
                throw;
            }
        }

        private static IPresentationLayerDriver CreateUITK
        (
            PresentationPlanLayer plan,
            Transform parent,
            PanelSettings panelSettingsTemplate,
            out Action release
        )
        {
            if (panelSettingsTemplate == null)
            {
                throw new InvalidOperationException
                (
                    $"UITK Layer '{plan.LayerID}'를 materialize할 PanelSettings Template이 없습니다."
                );
            }

            var gameObject = new GameObject($"Xeri UI Layer - {plan.LayerID}");
            gameObject.SetActive(false);
            gameObject.transform.SetParent(parent, false);
            gameObject.AddComponent<PanelRenderer>();
            var output = gameObject.AddComponent<UITKPresentationOutput>();

            try
            {
                var layerRoot = UITKPresentationSurface.CreateRoot
                (
                    $"xeri-layer-{plan.LayerID}"
                );
                var managedRoot = UITKPresentationSurface.CreateRoot
                (
                    $"xeri-managed-{plan.LayerID}"
                );
                layerRoot.Add(managedRoot);
                output.Initialize(panelSettingsTemplate, plan.LayerOrder);
                output.AttachLayerRoot(layerRoot);
                gameObject.SetActive(true);
                var driver = new UITKPresentationLayer(managedRoot);
                release = () => ReleaseUITKOutput(output, gameObject);
                return driver;
            }
            catch (Exception exception)
            {
                try
                {
                    ReleaseUITKOutput(output, gameObject);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        $"UITK Layer '{plan.LayerID}' materialization과 롤백이 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// UITK generated output의 runtime state와 GameObject를 모두 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ReleaseUITKOutput
        (
            UITKPresentationOutput output,
            GameObject gameObject
        )
        {
            var errors = new List<Exception>();

            try
            {
                output?.ReleaseRuntimeSettings();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                DestroyObject(gameObject);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "UITK generated Presentation Output 해제가 실패했습니다.",
                errors
            );
        }

        internal static void DestroyObject(GameObject gameObject)
        {
            if (gameObject == null) return;

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
