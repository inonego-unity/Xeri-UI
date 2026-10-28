/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UITKPresentationOutput.cs
수정일 : 2026-10-18

# 설명
실제 Unity 활성 전환·씬 종료에서 Root 재연결과 빌린 설정의 복원을 확인한다.

# 테스트 구성
 L: Native Output의 파괴·재활성·씬 종료와 대여 상태 반환
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    // ============================================================
    /// <summary>
    /// PlayMode 재연결과 파괴 경계에서 UITK 대여 수명을 검증한다.
    /// </summary>
    // ============================================================
    public sealed class TEST_UITKPresentationOutput
    {

    #region 픽스처

        private Scene createdScene;

        [UnityTearDown]
        public IEnumerator TeardownScene()
        {
            if (!createdScene.IsValid() || !createdScene.isLoaded)
            {
                yield break;
            }

            yield return SceneManager.UnloadSceneAsync(createdScene);
            createdScene = default;
        }

    #endregion

    #region L-1: 대여 출력의 파괴

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 활성화 후 Output 파괴 시 Borrowed PanelSettings 순서와 Root의 Layer·스타일
        /// <br/> 변경을 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKPresentationOutput_OnDestroy_대여설정과루트복원()
        {
            var host = new GameObject("Borrowed UITK lifetime");
            host.SetActive(false);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            settings.sortingOrder = 37f;
            var renderer = host.AddComponent<PanelRenderer>();
            renderer.panelSettings = settings;
            var output = host.AddComponent<UITKPresentationOutput>();

            try
            {
                output.InitializeBorrowed(111);
                host.SetActive(true);
                yield return null;

                var root = output.RequireRoot();
                var layer = new VisualElement();
                output.AttachLayerRoot(layer);
                Assert.AreEqual(111f, settings.sortingOrder);
                Assert.AreSame(root, layer.parent);
                Assert.IsTrue(root.ClassListContains("xeri-ui"));
                UnityEngine.Object.Destroy(output);
                yield return null;

                Assert.AreEqual(37f, settings.sortingOrder);
                Assert.AreSame(settings, renderer.panelSettings);
                Assert.IsNull(layer.parent);
                Assert.IsFalse(root.ClassListContains("xeri-ui"));
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(settings);
            }
        }

    #endregion

    #region L-2: 활성 전환의 루트 재연결

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Output 비활성화 후 재활성화하면 새 Native Root에 Layer와 기본 스타일을 다시
        /// <br/> 연결하고 해제 시 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKPresentationOutput_재활성시레이어와기본스타일재연결()
        {
            var host = new GameObject("Reenabled borrowed UITK");
            host.SetActive(false);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            settings.sortingOrder = 37f;
            var renderer = host.AddComponent<PanelRenderer>();
            renderer.panelSettings = settings;
            var output = host.AddComponent<UITKPresentationOutput>();

            try
            {
                output.InitializeBorrowed(111);
                host.SetActive(true);
                yield return null;
                var root = output.RequireRoot();
                var layer = new VisualElement();
                output.AttachLayerRoot(layer);

                host.SetActive(false);
                yield return null;
                host.SetActive(true);
                yield return null;
                yield return null;

                Assert.IsNotNull(output.Root);
                Assert.AreSame(output.Root, layer.parent);
                Assert.IsTrue(output.Root.ClassListContains("xeri-ui"));
                Assert.AreSame(settings, renderer.panelSettings);
                Assert.AreEqual(111f, settings.sortingOrder);
                output.ReleaseOutputState();
                Assert.AreEqual(37f, settings.sortingOrder);
                Assert.IsNull(layer.parent);
                Assert.IsFalse(root.ClassListContains("xeri-ui"));
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(settings);
            }
        }

    #endregion

    #region L-3: 씬 종료에서 대여 상태 반환

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 씬 언로드로 Output이 파괴될 때 Borrowed PanelSettings와 Native Root의
        /// <br/> Layer·스타일 상태를 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKPresentationOutput_씬종료시대여설정과레이어반환()
        {
            createdScene = SceneManager.CreateScene("Xeri UITK lifetime " + Guid.NewGuid().ToString("N"));
            var host = new GameObject("Scene borrowed UITK");
            host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, createdScene);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            settings.sortingOrder = 37f;
            var renderer = host.AddComponent<PanelRenderer>();
            renderer.panelSettings = settings;
            var output = host.AddComponent<UITKPresentationOutput>();

            try
            {
                output.InitializeBorrowed(111);
                host.SetActive(true);
                yield return null;
                var root = output.RequireRoot();
                var layer = new VisualElement();
                output.AttachLayerRoot(layer);
                yield return SceneManager.UnloadSceneAsync(createdScene);

                Assert.IsTrue(output == null);
                Assert.AreEqual(37f, settings.sortingOrder);
                Assert.IsNull(layer.parent);
                Assert.IsFalse(root.ClassListContains("xeri-ui"));
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(settings);
            }
        }

    #endregion

    }
}
