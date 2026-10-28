/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : HDRPNativeOutputInstaller.cs
수정일 : 2026-10-28

# 설명
HDRP 전후처리 순서와 Player 기본 Volume 타입을 함께 등록한다.
기존 사용자 항목·순서·Graphics/Quality 연결은 보존하고 소유 변경만 복원한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace inonego.Xeri.UI.HDRP.Editor
{
    // ============================================================
    /// <summary>
    /// HDRP 출력 설정의 적용·해제와 소유 기록을 관리한다.
    /// </summary>
    // ============================================================
    public static class HDRPNativeOutputInstaller
    {

    #region 대상과 등록 목록

        private const string MENU_PREFIX = "Tools/Xeri UI/HDRP/";
        private const string BEFORE_SUFFIX = ".m_BeforePostProcessCustomPostProcesses.m_CustomPostProcessTypesAsString";
        private const string AFTER_SUFFIX = ".m_AfterPostProcessCustomPostProcesses.m_CustomPostProcessTypesAsString";

        private static SerializedProperty FindList(SerializedObject serialized, string suffix)
        {
            var iterator = serialized.GetIterator();
            while (iterator.NextVisible(true))
            {
                if
                (
                    iterator.propertyPath.StartsWith("m_Settings.", StringComparison.Ordinal) &&
                    iterator.propertyPath.EndsWith(suffix, StringComparison.Ordinal) && iterator.isArray
                )
                {
                    return iterator.Copy();
                }
            }

            throw new InvalidOperationException("HDRP의 현재 Custom Post Process 등록 목록을 찾지 못했습니다.");
        }

        private static int FindEntry(SerializedProperty list, string typeName)
        {
            for (var index = 0; index < list.arraySize; index++)
            {
                if (list.GetArrayElementAtIndex(index).stringValue == typeName) return index;
            }

            return -1;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 프로젝트 Graphics/Quality가 실제 참조하는 HDRP 에셋을 중복 없이 찾는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static IReadOnlyList<HDRenderPipelineAsset> FindProjectPipelineAssets()
        {
            var assets = new List<HDRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is HDRenderPipelineAsset graphics)
            {
                assets.Add(graphics);
            }

            for (var index = 0; index < QualitySettings.names.Length; index++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(index) as HDRenderPipelineAsset;
                if (asset != null && !assets.Contains(asset))
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        private static bool AddEntry(SerializedProperty list, Type type)
        {
            if (FindEntry(list, type.AssemblyQualifiedName) >= 0) return false;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = type.AssemblyQualifiedName;
            return true;
        }

    #endregion

    #region 출력 설정 적용

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 HDRP에 설치하고 변경 여부를 반환하며 필수 자산 누락은 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool Install()
        {
            var global = GraphicsSettings.GetSettingsForRenderPipeline<HDRenderPipeline>();
            var profile = GraphicsSettings.GetRenderPipelineSettings<HDRPDefaultVolumeProfileSettings>()?.volumeProfile;
            return Install(global, profile, FindProjectPipelineAssets());
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 자산에 설치하며 실제 자산 변경이 있으면 true를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static bool Install
        (
            RenderPipelineGlobalSettings global,
            VolumeProfile profile,
            IReadOnlyList<HDRenderPipelineAsset> pipelines
        )
        {
            RequirePersistentAsset(global);
            RequirePersistentAsset(profile);
            if (pipelines == null)
            {
                throw new ArgumentNullException(nameof(pipelines));
            }

            foreach (var pipeline in pipelines)
            {
                RequirePersistentAsset(pipeline);
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Xeri HDRP 출력 설정 적용");
            var state = HDRPNativeOutputInstallationState.instance;
            try
            {
                var changed = InstallCore(global, profile, pipelines);
                Undo.FlushUndoRecordObjects();
                state.Persist();
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                return changed;
            }
            catch (Exception exception)
            {
                try
                {
                    Undo.FlushUndoRecordObjects();
                    Undo.RevertAllDownToGroup(group);
                    state.Persist();
                    AssetDatabase.SaveAssets();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException("HDRP 설치와 변경 복원이 모두 실패했습니다.", exception, cleanupException);
                }

                throw;
            }
        }

        private static void RequirePersistentAsset(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                throw new InvalidOperationException("HDRP Global Settings·기본 Volume Profile·Pipeline 자산이 필요합니다.");
            }

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))))
            {
                throw new InvalidOperationException("출력 설정 대상은 프로젝트에 저장된 자산이어야 합니다.");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정된 HDRP 자산에 등록·볼륨·포맷과 복원 기록을 설치한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool InstallCore
        (
            RenderPipelineGlobalSettings global,
            VolumeProfile profile,
            IReadOnlyList<HDRenderPipelineAsset> pipelines
        )
        {
            if (global == null || profile == null)
            {
                throw new InvalidOperationException("HDRP Global Settings와 프로젝트 기본 Volume Profile이 필요합니다.");
            }

            if (!AssetDatabase.GetAssetPath(profile).StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("HDRP 기본 Volume Profile을 프로젝트 Assets에 저장한 뒤 설치해야 합니다.");
            }

            var state = HDRPNativeOutputInstallationState.instance;
            var globalGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(global));
            if (!string.IsNullOrEmpty(state.GlobalSettingsGuid) && state.GlobalSettingsGuid != globalGuid)
            {
                throw new InvalidOperationException("기존 Xeri HDRP 설치를 복원한 뒤 다른 Global Settings에 설치해야 합니다.");
            }

            if (state.Profile != null && state.Profile != profile)
            {
                throw new InvalidOperationException("기존 Xeri HDRP 기본 Volume 설치를 먼저 복원해야 합니다.");
            }

            var serialized = new SerializedObject(global);
            var before = FindList(serialized, BEFORE_SUFFIX);
            var after = FindList(serialized, AFTER_SUFFIX);
            state.RecordChange("Xeri HDRP 설치 소유 기록");
            Undo.RecordObject(global, "Xeri HDRP Native UI 등록");
            var changed = false;
            if (AddEntry(before, typeof(HDRPNativeOverlayBeforePostProcess)))
            {
                state.OwnedBeforeTypeName = typeof(HDRPNativeOverlayBeforePostProcess).AssemblyQualifiedName;
                changed = true;
            }

            if (AddEntry(after, typeof(HDRPNativeOverlayPostProcess)))
            {
                state.OwnedTypeName = typeof(HDRPNativeOverlayPostProcess).AssemblyQualifiedName;
                changed = true;
            }

            serialized.ApplyModifiedProperties();
            if (!string.IsNullOrEmpty(state.OwnedBeforeTypeName) || !string.IsNullOrEmpty(state.OwnedTypeName))
            {
                state.GlobalSettingsGuid = globalGuid;
            }

            if (changed)
            {
                EditorUtility.SetDirty(global);
                AssetDatabase.SaveAssetIfDirty(global);
                // HDRP 목록의 비직렬화 Type 캐시도 같은 설치 상태로 다시 읽는다.
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(global), ImportAssetOptions.ForceUpdate);
            }

            changed |= AddComponent(profile, typeof(HDRPNativeOverlayPostProcess), state);
            changed |= AddComponent(profile, typeof(HDRPNativeOverlayBeforePostProcess), state);
            foreach (var asset in pipelines)
            {
                var settings = asset.currentPlatformRenderPipelineSettings;
                var post = settings.postProcessSettings;
                if (post.bufferFormat == PostProcessBufferFormat.R16G16B16A16) continue;
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
                if (string.IsNullOrEmpty(guid)) continue;
                state.RecordChange("Xeri HDRP 포맷 소유 기록");
                // 현재 포맷이 설치 값과 다르면 사용자 값을 새 설치의 원본으로 삼는다.
                state.FormatChanges.RemoveAll(record => record.AssetGuid == guid);
                state.FormatChanges.Add
                (
                    new HDRPNativeOutputInstallationState.PostProcessFormatChange
                    {
                        AssetGuid = guid,
                        OriginalFormat = post.bufferFormat
                    }
                );

                Undo.RecordObject(asset, "Xeri HDRP FP16 포맷");
                post.bufferFormat = PostProcessBufferFormat.R16G16B16A16;
                settings.postProcessSettings = post;
                asset.currentPlatformRenderPipelineSettings = settings;
                EditorUtility.SetDirty(asset);
                changed = true;
            }

            return changed;
        }

        private static bool AddComponent(VolumeProfile profile, Type type, HDRPNativeOutputInstallationState state)
        {
            if (profile.components.Exists(component => component != null && component.GetType() == type)) return false;
            var component = (VolumeComponent)ScriptableObject.CreateInstance(type);
            component.name = type.Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            component.SetAllOverridesTo(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            Undo.RegisterCreatedObjectUndo(component, "Xeri HDRP Player 기본 Volume 타입");
            state.RecordChange("Xeri HDRP 볼륨 소유 기록");
            // Redo에서 볼륨을 먼저 복구해야 Profile의 Native 검증이 빈 참조를 제거하지 않는다.
            Undo.RegisterCompleteObjectUndo(profile, "Xeri HDRP Player 기본 Volume 타입");
            profile.components.Add(component);
            profile.Reset();
            state.Profile = profile;
            state.Components.Add(component);
            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            return true;
        }

    #endregion

    #region 출력 설정 적용 해제

        // ----------------------------------------------------------------------
        /// <summary>
        /// 자산 또는 소유 기록을 해제한 경우 true를 반환하며 사용자 편집을 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static bool Uninstall()
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Xeri HDRP 출력 설정 적용 해제");
            var state = HDRPNativeOutputInstallationState.instance;
            state.RecordChange("Xeri HDRP 소유 기록 해제");
            var changed = !string.IsNullOrEmpty(state.GlobalSettingsGuid) ||
                state.Components.Count > 0 || state.FormatChanges.Count > 0 || state.Profile != null;
            var errors = new List<Exception>();
            var preservedTypes = new HashSet<string>();
            foreach (var component in state.Components)
            {
                if (state.Profile != null && component != null && state.Profile.components.Contains(component) &&
                    IsEditedComponent(component))
                {
                    preservedTypes.Add(component.GetType().AssemblyQualifiedName);
                }
            }

            try
            {
                UninstallRegistration(state, preservedTypes);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            for (var index = state.Components.Count - 1; index >= 0; index--)
            {
                try
                {
                    var component = state.Components[index];
                    if (component != null && state.Profile != null && state.Profile.components.Contains(component))
                    {
                        if (preservedTypes.Contains(component.GetType().AssemblyQualifiedName))
                        {
                            Debug.LogWarning("사용자가 편집한 HDRP 출력 Volume을 보존합니다: " + AssetDatabase.GetAssetPath(state.Profile));
                        }
                        else
                        {
                            Undo.RecordObject(state.Profile, "Xeri HDRP 기본 Volume 적용 해제");
                            state.Profile.components.Remove(component);
                            Undo.DestroyObjectImmediate(component);
                            EditorUtility.SetDirty(state.Profile);
                        }
                    }

                    if (state.Profile != null)
                    {
                        AssetDatabase.SaveAssetIfDirty(state.Profile);
                    }

                    state.Components.RemoveAt(index);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (state.Components.Count == 0)
            {
                state.Profile = null;
            }

            for (var index = state.FormatChanges.Count - 1; index >= 0; index--)
            {
                try
                {
                    UninstallFormat(state.FormatChanges[index]);
                    // 사용자 편집과 대상 삭제도 완료된 소유권 종료로 처리한다.
                    state.FormatChanges.RemoveAt(index);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            try
            {
                Undo.FlushUndoRecordObjects();
                state.Persist();
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("HDRP 적용 해제 중 완료하지 못한 변경이 있습니다.", errors);
            }

            return changed;
        }

        private static bool IsEditedComponent(VolumeComponent component)
        {
            return !component.active || component.name != component.GetType().Name ||
                component.hideFlags != (HideFlags.HideInInspector | HideFlags.HideInHierarchy);
        }

        private static void UninstallRegistration(HDRPNativeOutputInstallationState state, HashSet<string> preservedTypes)
        {
            var global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>
            (
                AssetDatabase.GUIDToAssetPath(state.GlobalSettingsGuid ?? string.Empty)
            );
            if (global != null)
            {
                var serialized = new SerializedObject(global);
                var before = FindList(serialized, BEFORE_SUFFIX);
                var after = FindList(serialized, AFTER_SUFFIX);
                Undo.RecordObject(global, "Xeri HDRP 출력 등록 적용 해제");
                if (!preservedTypes.Contains(state.OwnedBeforeTypeName))
                {
                    RemoveEntry(before, state.OwnedBeforeTypeName);
                }

                if (!preservedTypes.Contains(state.OwnedTypeName))
                {
                    RemoveEntry(after, state.OwnedTypeName);
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(global);
                AssetDatabase.SaveAssetIfDirty(global);
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(global), ImportAssetOptions.ForceUpdate);
            }

            // 삭제된 Global Settings의 기록도 끝내야 다른 프로젝트 대상에 설치할 수 있다.
            state.OwnedBeforeTypeName = null;
            state.OwnedTypeName = null;
            state.GlobalSettingsGuid = null;
        }

        private static void UninstallFormat(HDRPNativeOutputInstallationState.PostProcessFormatChange record)
        {
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>
            (
                AssetDatabase.GUIDToAssetPath(record.AssetGuid)
            );
            if (asset == null) return;
            var settings = asset.currentPlatformRenderPipelineSettings;
            var post = settings.postProcessSettings;
            if (post.bufferFormat != PostProcessBufferFormat.R16G16B16A16)
            {
                AssetDatabase.SaveAssetIfDirty(asset);
                return;
            }

            Undo.RecordObject(asset, "Xeri HDRP 출력 포맷 적용 해제");
            post.bufferFormat = record.OriginalFormat;
            settings.postProcessSettings = post;
            asset.currentPlatformRenderPipelineSettings = settings;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static bool RemoveEntry(SerializedProperty list, string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var index = FindEntry(list, name);
            if (index < 0) return false;
            list.DeleteArrayElementAtIndex(index);
            return true;
        }

    #endregion

    #region 메뉴

        // ------------------------------------------------------------
        /// <summary>
        /// 적용할 대상과 실제 동작 조건을 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public static string Preview()
        {
            var text = new StringBuilder();
            text.AppendLine("HDRP Native UI: 주요 후처리 전/후 등록 + Player 기본 Volume 타입");
            foreach (var asset in FindProjectPipelineAssets())
            {
                text.AppendLine(AssetDatabase.GetAssetPath(asset) + " : Post Process Buffer -> R16G16B16A16");
            }

            text.AppendLine("UISettings의 고정밀 출력 / UI 후처리 정책을 실행 중 적용합니다.");
            text.AppendLine("카메라 후처리 또는 Custom Post Process가 꺼지면 Native 출력을 유지합니다.");
            text.AppendLine("단일 전체 화면 카메라·Linear SDR·원래 해상도만 지원합니다.");
            return text.ToString();
        }

        [MenuItem(MENU_PREFIX + "출력 설정 미리보기", false, 2300)]
        private static void ShowPreview()
        {
            EditorUtility.DisplayDialog("Xeri HDRP 출력 설정", Preview(), "확인");
        }

        [MenuItem(MENU_PREFIX + "출력 설정 적용", false, 2301)]
        private static void ShowInstall()
        {
            if (!EditorUtility.DisplayDialog("Xeri HDRP 출력 설정", Preview(), "적용", "취소")) return;
            try
            {
                var changed = Install();
                EditorUtility.DisplayDialog("Xeri HDRP 출력 설정", changed ? "출력 설정을 변경했습니다. 실행 취소로 되돌릴 수 있습니다." : "변경할 출력 설정이 없습니다.", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Xeri HDRP 출력 설정", "변경에 실패했습니다.\n" + exception.Message, "확인");
            }
        }

        [MenuItem(MENU_PREFIX + "출력 설정 적용 해제", false, 2302)]
        private static void ShowUninstall()
        {
            var preview = "프로젝트에 기록된 Xeri 출력 설정을 해제합니다.\n" +
                "추가한 등록과 Volume 타입을 제거하고 포맷을 복원하며 사용자 편집은 보존합니다.";
            if (!EditorUtility.DisplayDialog("Xeri HDRP 출력 설정", preview, "적용 해제", "취소")) return;
            try
            {
                var changed = Uninstall();
                EditorUtility.DisplayDialog("Xeri HDRP 출력 설정", changed ? "출력 설정과 소유 기록의 적용을 해제했습니다." : "적용 해제할 소유 기록이 없습니다.", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Xeri HDRP 출력 설정", "변경에 실패했습니다.\n" + exception.Message, "확인");
            }
        }

    #endregion

    }
}
