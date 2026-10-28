/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_URPDirectOutputInstaller.cs
수정일 : 2026-10-28

# 설명
URP 설치·해제의 소유 기록, 사용자 설정 보존과 Undo/Redo를 검증한다.

# 테스트 구성
 E: 생성·대여 Feature 설치와 선택 복원
 S: 설치 Undo/Redo와 영속 소유 기록의 일치
 X: 사용자 편집과 삭제된 자산의 소유권 종료
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.IO;

using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.URP.Editor.TEST
{
    // ============================================================
    /// <summary>
    /// URP 출력 설정의 설치와 소유한 변경의 복원을 검증한다.
    /// </summary>
    // ============================================================
    public sealed class TEST_URPDirectOutputInstaller
    {

    #region 격리 자산 수명

        private string assetPath;
        private UniversalRendererData renderer;
        private string pipelinePath;
        private string originalState;
        private byte[] originalStateFile;
        private const string STATE_PATH = "ProjectSettings/XeriUIURPDirectOutputInstallationState.asset";

        // ------------------------------------------------------------
        /// <summary>
        /// 프로젝트의 URP 에셋 상태를 테스트용으로 준비한다.
        /// </summary>
        // ------------------------------------------------------------
        [SetUp]
        public void Setup()
        {
            originalStateFile = File.Exists(STATE_PATH) ? File.ReadAllBytes(STATE_PATH) : null;
            var state = URPDirectOutputInstallationState.instance;
            originalState = EditorJsonUtility.ToJson(state);
            state.FeatureChanges.Clear();
            state.PrecisionChanges.Clear();
            assetPath = "Assets/XeriUITest_ProjectInstaller_" +
                Guid.NewGuid().ToString("N") + ".asset";
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, assetPath);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트가 변경한 URP Pipeline·Renderer 설정을 원래대로 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void Teardown()
        {
            URPDirectOutputInstaller.UninstallProject();
            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            renderer = null;
            assetPath = null;
            if (pipelinePath != null)
            {
                AssetDatabase.DeleteAsset(pipelinePath);
                pipelinePath = null;
            }

            var state = URPDirectOutputInstallationState.instance;
            Undo.ClearUndo(state);
            EditorJsonUtility.FromJsonOverwrite(originalState, state);
            EditorUtility.ClearDirty(state);
            if (originalStateFile != null)
            {
                File.WriteAllBytes(STATE_PATH, originalStateFile);
            }
            else if (File.Exists(STATE_PATH))
            {
                File.Delete(STATE_PATH);
            }
        }

    #endregion

    #region E-1: 공개 설치와 소유 기록 통합

        // ------------------------------------------------------------
        /// <summary>
        /// 공개 설치 API로 변경한 정밀도와 Feature도 같은 기록으로 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_URPDirectOutputInstaller_공개설치도소유기록을남기고함께복원()
        {
            var pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();
            pipelinePath = assetPath.Replace(".asset", "_Pipeline.asset");
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
            try
            {
                var serialized = new SerializedObject(pipeline);
                var renderers = serialized.FindProperty("m_RendererDataList");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                pipeline.hdrColorBufferPrecision = HDRColorBufferPrecision._32Bits;
                JsonUtility.FromJsonOverwrite("{\"highPrecisionOutput\":true}", settings);

                Assert.AreEqual(1, URPDirectOutputInstaller.Install(pipeline, settings));
                Assert.AreEqual(HDRColorBufferPrecision._64Bits, pipeline.hdrColorBufferPrecision);
                Assert.AreEqual(1, renderer.rendererFeatures.Count);
                Assert.AreEqual(2, URPDirectOutputInstaller.UninstallProject());
                Assert.AreEqual(HDRColorBufferPrecision._32Bits, pipeline.hdrColorBufferPrecision);
                Assert.AreEqual(0, renderer.rendererFeatures.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

    #endregion

    #region E-2: 설치와 복원 계약

        // ------------------------------------------------------------
        /// <summary>
        /// Xeri에서 설치한 Feature만 반환하고 사용자 Feature는 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_URPDirectOutputInstaller_Xeri생성Feature만원복하고기존Feature보존()
        {
            var authored = ScriptableObject.CreateInstance<RenderObjects>();
            AssetDatabase.AddObjectToAsset(authored, renderer);
            renderer.rendererFeatures.Add(authored);

            var first = URPDirectOutputInstaller.InstallRenderer(renderer);
            var second = URPDirectOutputInstaller.InstallRenderer(renderer);
            Assert.AreSame(first, second);
            Assert.IsTrue(first.isActive);
            Assert.AreEqual(2, renderer.rendererFeatures.Count);
            Assert.AreSame(authored, renderer.rendererFeatures[0]);

            var count = URPDirectOutputInstaller.UninstallProject();
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, renderer.rendererFeatures.Count);
            Assert.AreSame(authored, renderer.rendererFeatures[0]);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 Feature 활성화와 정밀도 변경을 올바르게 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_URPDirectOutputInstaller_기존비활성Feature상태만복원()
        {
            var authored = ScriptableObject.CreateInstance<URPNativeOverlayFeature>();
            authored.SetActive(false);
            AssetDatabase.AddObjectToAsset(authored, renderer);
            renderer.rendererFeatures.Add(authored);

            var feature = URPDirectOutputInstaller.InstallRenderer(renderer);
            Assert.AreSame(authored, feature);
            Assert.IsTrue(feature.isActive);

            var count = URPDirectOutputInstaller.UninstallProject();
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, renderer.rendererFeatures.Count);
            Assert.AreSame(authored, renderer.rendererFeatures[0]);
            Assert.IsFalse(feature.isActive);
        }

    #endregion

    #region S-1: 설치 기록의 Undo와 Redo

        // ------------------------------------------------------------
        /// <summary>
        /// 설치 취소·재실행에서도 소유 기록과 실제 Feature가 함께 이동한다.
        /// </summary>
        // ------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_URPDirectOutputInstaller_UndoRedo와소유기록이같이변경됨()
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            URPDirectOutputInstaller.InstallRenderer(renderer);
            Undo.CollapseUndoOperations(group);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            var state = URPDirectOutputInstallationState.instance;
            Assert.AreEqual(1, state.FeatureChanges.Count);
            Assert.AreEqual(1, renderer.rendererFeatures.Count);

            Undo.PerformUndo();
            Assert.AreEqual(0, renderer.rendererFeatures.Count);
            Assert.AreEqual(0, state.FeatureChanges.Count, "취소된 설치의 소유 기록이 남았습니다.");
            yield return null;
            StringAssert.Contains("FeatureChanges: []", File.ReadAllText(STATE_PATH));

            Undo.PerformRedo();
            Assert.AreEqual(1, renderer.rendererFeatures.Count);
            Assert.AreEqual(1, state.FeatureChanges.Count);
            yield return null;
            StringAssert.Contains(state.FeatureChanges[0].RendererGuid, File.ReadAllText(STATE_PATH));
            Assert.AreEqual(1, URPDirectOutputInstaller.UninstallProject());
            Assert.AreEqual(0, renderer.rendererFeatures.Count);
        }

    #endregion

    #region X-1: 사용자 편집과 삭제된 대상

        // ------------------------------------------------------------
        /// <summary>
        /// 사용자에게 넘긴 기능을 보존하고 재설치에도 옛 소유권을 남기지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_URPDirectOutputInstaller_사용자편집을보존하고소유기록을종료()
        {
            var feature = URPDirectOutputInstaller.InstallRenderer(renderer);
            feature.name = "사용자가 관리하는 출력 기능";
            LogAssert.Expect(LogType.Warning, "사용자가 편집한 출력 기능을 보존합니다: " + assetPath);
            Assert.AreEqual(0, URPDirectOutputInstaller.UninstallProject());
            Assert.AreSame(feature, renderer.rendererFeatures[0]);
            Assert.AreEqual(0, URPDirectOutputInstallationState.instance.FeatureChanges.Count);

            Assert.AreSame(feature, URPDirectOutputInstaller.InstallRenderer(renderer));
            Assert.AreEqual(0, URPDirectOutputInstaller.UninstallProject());
            Assert.AreSame(feature, renderer.rendererFeatures[0]);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 삭제된 렌더러의 소유 기록을 종료하고 반복 해제를 변경 없음으로 처리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_URPDirectOutputInstaller_삭제된대상기록을종료()
        {
            URPDirectOutputInstaller.InstallRenderer(renderer);
            AssetDatabase.DeleteAsset(assetPath);
            Assert.AreEqual(0, URPDirectOutputInstaller.UninstallProject());
            Assert.AreEqual(0, URPDirectOutputInstallationState.instance.FeatureChanges.Count);
            Assert.AreEqual(0, URPDirectOutputInstaller.UninstallProject());
        }

    #endregion

    }
}
