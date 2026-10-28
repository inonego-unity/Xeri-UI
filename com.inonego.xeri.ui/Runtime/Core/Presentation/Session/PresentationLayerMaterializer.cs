/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationLayerMaterializer.cs
수정일 : 2026-10-17

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
    // ======================================================================
    /// <summary>
    /// Backend별 Top-level Output 생성과 반환을 같은 Lifetime 계약으로 조립한다.
    /// </summary>
    // ======================================================================
    internal static class PresentationLayerMaterializer
    {

    #region 상위 레이어 생성

        private const string DEFAULT_UGUI_TEMPLATE_PATH =
            "Xeri/UI/Core/Presentation/UIUGUIPresentationOutput";

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Plan Backend에 따라 Native Output과 ManagedRoot를 생성한다.
        /// <br/> 생성된 자원의 명시적 해제 callback을 함께 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
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

    #endregion

    #region UGUI 생성

        // ------------------------------------------------------------
        /// <summary>
        /// UGUI Template Canvas를 생성해 Native Output 경계로 구성한다.
        /// </summary>
        // ------------------------------------------------------------
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
            var gameObject = output.gameObject;

            try
            {
                gameObject.name = $"Xeri UI Layer - {plan.LayerID}";
                gameObject.SetActive(false);
                output.Initialize(plan.LayerOrder);
                var managedRoot = UGUIPresentationSurface.CreateRect
                (
                    $"Managed Root - {plan.LayerID}",
                    output.Root
                );
                gameObject.SetActive(true);
                var driver = new UGUIPresentationLayer(managedRoot);
                release = () => ReleaseGeneratedOutput
                (
                    gameObject,
                    () =>
                    {
                        if (output != null)
                        {
                            output.ReleaseOutputState();
                        }
                    }
                );
                return driver;
            }
            catch (Exception exception)
            {
                try
                {
                    ReleaseGeneratedOutput
                    (
                        gameObject,
                        () =>
                        {
                            if (output != null)
                            {
                                output.ReleaseOutputState();
                            }
                        }
                    );
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        $"UGUI Layer '{plan.LayerID}' materialization과 롤백이 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

    #endregion

    #region UITK 생성과 해제

        // ----------------------------------------------------------------------
        /// <summary>
        /// UITK PanelRenderer와 Runtime PanelSettings clone을 구성한다.
        /// </summary>
        // ----------------------------------------------------------------------
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
                release = () => ReleaseGeneratedOutput
                (
                    gameObject,
                    () =>
                    {
                        if (output != null)
                        {
                            output.ReleaseOutputState();
                        }
                    }
                );
                return driver;
            }
            catch (Exception exception)
            {
                try
                {
                    ReleaseGeneratedOutput
                    (
                        gameObject,
                        () =>
                        {
                            if (output != null)
                            {
                                output.ReleaseOutputState();
                            }
                        }
                    );
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Generated native state를 복원하고 GameObject를 파괴한다.
        /// <br/> 일부 반환이 실패해도 나머지 정리를 시도한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ReleaseGeneratedOutput
        (
            GameObject gameObject,
            Action releaseNativeState
        )
        {
            var errors = new List<Exception>();

            try
            {
                releaseNativeState?.Invoke();
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
                "Generated Presentation Output 해제가 실패했습니다.",
                errors
            );
        }

    #endregion

    #region 유틸

        // ------------------------------------------------------------
        /// <summary>
        /// PlayMode와 EditMode의 GameObject 파괴 경계를 구분한다.
        /// </summary>
        // ------------------------------------------------------------
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

    #endregion

    }
}
