/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_HDRPNativeOutputInstaller.cs
수정일 : 2026-10-28

# 설명
복제한 HDRP Global Settings와 전용 자산에서 제품 설치·복원·Undo의 소유 계약을 검증한다.
프로젝트의 실제 Graphics/Quality 연결과 기본 Volume Profile은 변경하지 않는다.

# 테스트 구성
 E: 등록·영속 Volume·포맷 설치와 반복 복원
 S: 사용자 항목 보존 및 설치·복원 Undo/Redo
 X: 대상 삭제와 필수 자산 누락
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.HDRP.Editor.TEST
{
    // ============================================================
    /// <summary>
    /// HDRP 설치의 자산과 영속 소유 기록을 함께 검증한다.
    /// </summary>
    // ============================================================
    public sealed class TEST_HDRPNativeOutputInstaller
    {

    #region 격리 자산과 소유 기록 수명

        private const string STATE_PATH = "ProjectSettings/XeriUIHDRPNativeOutputInstallationState.asset";
        private string globalPath;
        private string profilePath;
        private string pipelinePath;
        private string originalState;
        private byte[] originalStateFile;
        private RenderPipelineGlobalSettings global;
        private VolumeProfile profile;
        private HDRenderPipelineAsset pipeline;

        [SetUp]
        public void Setup()
        {
            var source = GraphicsSettings.GetSettingsForRenderPipeline<HDRenderPipeline>();
            if (source == null)
            {
                Assert.Ignore("복제할 HDRP Global Settings가 구성된 프로젝트에서 실행합니다.");
            }

            originalStateFile = File.Exists(STATE_PATH) ? File.ReadAllBytes(STATE_PATH) : null;
            var state = HDRPNativeOutputInstallationState.instance;
            originalState = EditorJsonUtility.ToJson(state);
            state.GlobalSettingsGuid = null;
            state.OwnedTypeName = null;
            state.OwnedBeforeTypeName = null;
            state.Profile = null;
            state.Components.Clear();
            state.FormatChanges.Clear();
            state.Persist();

            var prefix = "Assets/XeriUITest_HDRP_" + Guid.NewGuid().ToString("N");
            globalPath = prefix + "_Global.asset";
            profilePath = prefix + "_Profile.asset";
            pipelinePath = prefix + "_Pipeline.asset";
            global = UnityEngine.Object.Instantiate(source);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            pipeline = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
            AssetDatabase.CreateAsset(global, globalPath);
            AssetDatabase.CreateAsset(profile, profilePath);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
            ClearFixtureRegistrations();
            SetFormat(PostProcessBufferFormat.R11G11B10);
        }

        [TearDown]
        public void Teardown()
        {
            if (originalState == null) return;
            try
            {
                HDRPNativeOutputInstaller.Uninstall();
            }
            finally
            {
                var state = HDRPNativeOutputInstallationState.instance;
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

                AssetDatabase.DeleteAsset(globalPath);
                AssetDatabase.DeleteAsset(profilePath);
                AssetDatabase.DeleteAsset(pipelinePath);
            }
        }

        private void SetFormat(PostProcessBufferFormat value)
        {
            var settings = pipeline.currentPlatformRenderPipelineSettings;
            var post = settings.postProcessSettings;
            post.bufferFormat = value;
            settings.postProcessSettings = post;
            pipeline.currentPlatformRenderPipelineSettings = settings;
            EditorUtility.SetDirty(pipeline);
        }

        private bool Install()
        {
            var pipelines = new[]
            {
                pipeline
            };
            return HDRPNativeOutputInstaller.Install(global, profile, pipelines);
        }

        private void ClearFixtureRegistrations()
        {
            var serialized = new SerializedObject(global);
            var iterator = serialized.GetIterator();
            var paths = new List<string>();
            while (iterator.NextVisible(true))
            {
                if (iterator.isArray && iterator.propertyPath.StartsWith("m_Settings.", StringComparison.Ordinal) &&
                    (iterator.propertyPath.EndsWith(".m_BeforePostProcessCustomPostProcesses.m_CustomPostProcessTypesAsString", StringComparison.Ordinal) ||
                    iterator.propertyPath.EndsWith(".m_AfterPostProcessCustomPostProcesses.m_CustomPostProcessTypesAsString", StringComparison.Ordinal)))
                {
                    paths.Add(iterator.propertyPath);
                }
            }

            // 테스트 복제본에서만 설치 대상 타입을 비워 실제 추가·삭제 경로를 검증한다.
            foreach (var path in paths)
            {
                var list = serialized.FindProperty(path);
                for (var index = list.arraySize - 1; index >= 0; index--)
                {
                    var type = list.GetArrayElementAtIndex(index).stringValue;
                    if (type == typeof(HDRPNativeOverlayPostProcess).AssemblyQualifiedName ||
                        type == typeof(HDRPNativeOverlayBeforePostProcess).AssemblyQualifiedName)
                    {
                        list.DeleteArrayElementAtIndex(index);
                    }
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private List<string> ReadOrder(bool before)
        {
            var serialized = new SerializedObject(global);
            var iterator = serialized.GetIterator();
            var stage = before ? ".m_BeforePostProcessCustomPostProcesses." : ".m_AfterPostProcessCustomPostProcesses.";
            while (iterator.NextVisible(true))
            {
                if
                (
                    !iterator.propertyPath.StartsWith("m_Settings.", StringComparison.Ordinal) ||
                    !iterator.propertyPath.Contains(stage) || !iterator.isArray
                )
                {
                    continue;
                }

                var result = new List<string>();
                for (var index = 0; index < iterator.arraySize; index++)
                {
                    result.Add(iterator.GetArrayElementAtIndex(index).stringValue);
                }

                return result;
            }

            throw new InvalidOperationException("현재 HDRP 등록 목록이 없습니다.");
        }

    #endregion

    #region E-1: 설치와 선택 복원

        // ------------------------------------------------------------
        /// <summary>
        /// 두 삽입 지점과 Player 볼륨을 한 번만 설치하고 원래 값을 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_HDRPNativeOutputInstaller_등록과볼륨포맷을반복없이설치복원()
        {
            var before = ReadOrder(true);
            var after = ReadOrder(false);
            Assert.IsTrue(Install());
            Assert.IsFalse(Install());
            CollectionAssert.Contains(ReadOrder(true), typeof(HDRPNativeOverlayBeforePostProcess).AssemblyQualifiedName);
            CollectionAssert.Contains(ReadOrder(false), typeof(HDRPNativeOverlayPostProcess).AssemblyQualifiedName);
            Assert.AreEqual(2, profile.components.Count);
            foreach (var component in profile.components)
            {
                Assert.IsTrue(EditorUtility.IsPersistent(component));
            }

            Assert.AreEqual(PostProcessBufferFormat.R16G16B16A16, pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat);
            Assert.IsTrue(HDRPNativeOutputInstaller.Uninstall());
            Assert.IsFalse(HDRPNativeOutputInstaller.Uninstall());
            CollectionAssert.AreEqual(before, ReadOrder(true));
            CollectionAssert.AreEqual(after, ReadOrder(false));
            Assert.AreEqual(0, profile.components.Count);
            Assert.AreEqual(PostProcessBufferFormat.R11G11B10, pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat);
        }

    #endregion

    #region S-1: 사용자 자산 보존

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 사용자 볼륨과 설치 뒤 직접 편집한 포맷은 제거하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_HDRPNativeOutputInstaller_사용자볼륨과수정한포맷을보존()
        {
            var authored = profile.Add<HDRPNativeOverlayPostProcess>(true);
            AssetDatabase.AddObjectToAsset(authored, profile);
            Install();
            SetFormat(PostProcessBufferFormat.R32G32B32A32);
            HDRPNativeOutputInstaller.Uninstall();
            Assert.AreEqual(1, profile.components.Count);
            Assert.AreSame(authored, profile.components[0]);
            Assert.AreEqual(PostProcessBufferFormat.R32G32B32A32, pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat);
            Assert.AreEqual(0, HDRPNativeOutputInstallationState.instance.FormatChanges.Count);
            Assert.IsTrue(Install());
            Assert.IsTrue(HDRPNativeOutputInstaller.Uninstall());
            Assert.AreEqual(1, profile.components.Count);
            Assert.AreSame(authored, profile.components[0]);
            Assert.AreEqual(PostProcessBufferFormat.R32G32B32A32, pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 편집한 설치 Volume과 등록은 보존하고 나머지 소유 변경을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        public void TEST_HDRPNativeOutputInstaller_편집한설치Volume과등록보존(bool before, int edit)
        {
            Install();
            var type = before ? typeof(HDRPNativeOverlayBeforePostProcess) : typeof(HDRPNativeOverlayPostProcess);
            var component = profile.components.Find(value => value != null && value.GetType() == type);
            Assert.IsNotNull(component);

            switch (edit)
            {
                case 0: component.active = false; break;
                case 1: component.name = "사용자 편집"; break;
                case 2: component.hideFlags = HideFlags.None; break;
            }

            EditorUtility.SetDirty(component);
            Assert.IsTrue(HDRPNativeOutputInstaller.Uninstall());
            Assert.AreEqual(1, profile.components.Count);
            Assert.AreSame(component, profile.components[0]);
            CollectionAssert.Contains(ReadOrder(before), type.AssemblyQualifiedName);
            CollectionAssert.DoesNotContain
            (
                ReadOrder(!before),
                (before ? typeof(HDRPNativeOverlayPostProcess) : typeof(HDRPNativeOverlayBeforePostProcess)).AssemblyQualifiedName
            );
            Assert.AreEqual(0, HDRPNativeOutputInstallationState.instance.Components.Count);
            Assert.AreEqual(PostProcessBufferFormat.R11G11B10, pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat);

            Assert.IsTrue(Install());
            Assert.IsTrue(HDRPNativeOutputInstaller.Uninstall());
            Assert.AreEqual(1, profile.components.Count);
            Assert.AreSame(component, profile.components[0]);
            CollectionAssert.Contains(ReadOrder(before), type.AssemblyQualifiedName);
        }

    #endregion

    #region S-2: 자산과 소유 기록의 Undo와 Redo

        // ------------------------------------------------------------
        /// <summary>
        /// 설치 취소와 재실행에서 등록·볼륨·포맷·소유 기록이 함께 이동한다.
        /// </summary>
        // ------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_HDRPNativeOutputInstaller_UndoRedo와소유기록이같이변경됨()
        {
            var before = ReadOrder(true);
            var after = ReadOrder(false);
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Install();
            Undo.CollapseUndoOperations(group);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();
            CollectionAssert.AreEqual(before, ReadOrder(true));
            CollectionAssert.AreEqual(after, ReadOrder(false));
            Assert.AreEqual(0, profile.components.Count);
            Assert.AreEqual(0, HDRPNativeOutputInstallationState.instance.Components.Count);
            Assert.AreEqual(PostProcessBufferFormat.R11G11B10, pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat);
            yield return null;
            StringAssert.Contains("Components: []", File.ReadAllText(STATE_PATH));

            Undo.PerformRedo();
            Assert.AreEqual(2, profile.components.Count);
            Assert.AreEqual(2, HDRPNativeOutputInstallationState.instance.Components.Count);
            Assert.AreEqual(PostProcessBufferFormat.R16G16B16A16, pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat);
            yield return null;
            Assert.IsTrue(HDRPNativeOutputInstaller.Uninstall());
            Assert.AreEqual(0, profile.components.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 복원 취소와 재실행에서도 설치 자산과 소유 기록을 함께 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_HDRPNativeOutputInstaller_복원UndoRedo가설치상태를보존()
        {
            Install();
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Assert.IsTrue(HDRPNativeOutputInstaller.Uninstall());
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            Assert.AreEqual(0, profile.components.Count);

            Undo.PerformUndo();
            Assert.AreEqual(2, profile.components.Count);
            Assert.AreEqual(2, HDRPNativeOutputInstallationState.instance.Components.Count);
            Assert.IsNotNull(HDRPNativeOutputInstallationState.instance.Components[0]);
            Assert.IsNotNull(HDRPNativeOutputInstallationState.instance.Components[1]);
            CollectionAssert.Contains(ReadOrder(true), typeof(HDRPNativeOverlayBeforePostProcess).AssemblyQualifiedName);
            CollectionAssert.Contains(ReadOrder(false), typeof(HDRPNativeOverlayPostProcess).AssemblyQualifiedName);

            Undo.PerformRedo();
            Assert.AreEqual(0, profile.components.Count);
            Assert.AreEqual(0, HDRPNativeOutputInstallationState.instance.Components.Count);
            Assert.AreEqual(PostProcessBufferFormat.R11G11B10, pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat);
        }

    #endregion

    #region X-1: 대상 삭제와 필수 자산 누락

        // ------------------------------------------------------------
        /// <summary>
        /// 삭제된 Global Settings의 소유권을 끝내고 새 자산에 재설치한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_HDRPNativeOutputInstaller_삭제된GlobalSettings를해제한뒤새대상설치()
        {
            var replacement = UnityEngine.Object.Instantiate(global);
            try
            {
                Install();
                var previousGuid = HDRPNativeOutputInstallationState.instance.GlobalSettingsGuid;
                Assert.IsFalse(string.IsNullOrEmpty(previousGuid));
                AssetDatabase.DeleteAsset(globalPath);
                Assert.IsTrue(HDRPNativeOutputInstaller.Uninstall());
                Assert.IsTrue(string.IsNullOrEmpty(HDRPNativeOutputInstallationState.instance.GlobalSettingsGuid));
                Assert.AreEqual(0, profile.components.Count);
                Assert.AreEqual(PostProcessBufferFormat.R11G11B10, pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat);
                global = replacement;
                globalPath = globalPath.Replace("_Global.asset", "_ReplacementGlobal.asset");
                AssetDatabase.CreateAsset(global, globalPath);
                Assert.IsTrue(Install());
                Assert.AreNotEqual(previousGuid, HDRPNativeOutputInstallationState.instance.GlobalSettingsGuid);
                CollectionAssert.Contains(ReadOrder(false), typeof(HDRPNativeOverlayPostProcess).AssemblyQualifiedName);
                Assert.AreEqual(2, profile.components.Count);
                Assert.IsTrue(HDRPNativeOutputInstaller.Uninstall());
                Assert.IsFalse(HDRPNativeOutputInstaller.Uninstall());
            }
            finally
            {
                if (!EditorUtility.IsPersistent(replacement))
                {
                    UnityEngine.Object.DestroyImmediate(replacement);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 필수 자산이 없으면 변경 없음과 구분해 거부하고 기존 자산을 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_HDRPNativeOutputInstaller_필수자산누락을변경전에거부()
        {
            var pipelines = new[]
            {
                pipeline
            };
            Assert.Throws<InvalidOperationException>(() => HDRPNativeOutputInstaller.Install(null, profile, pipelines));
            Assert.AreEqual(0, profile.components.Count);
            Assert.AreEqual(0, HDRPNativeOutputInstallationState.instance.FormatChanges.Count);
            Assert.AreEqual(PostProcessBufferFormat.R11G11B10, pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat);
        }

    #endregion

    }
}
