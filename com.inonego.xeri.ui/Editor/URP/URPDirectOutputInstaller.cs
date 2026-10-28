/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : URPDirectOutputInstaller.cs
수정일 : 2026-10-28

# 설명
프로젝트의 URP Native UI 출력 설정을 적용·해제하고 소유 변경과 Undo를 함께 관리한다.
High Precision ON인 경우에만 대상 URP Asset의 HDR 정밀도를 64-bit로 구성한다.

# 제약사항
Graphics/Quality가 참조하는 자산 또는 명시한 자산을 구성하며 파이프라인 참조는 유지한다.
Renderer Data 내 기존 사용자 Feature와 정렬을 보존하고 Editor Undo를 지원한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.URP.Editor
{
    // ============================================================
    /// <summary>
    /// URP 출력 설정의 적용·해제와 프로젝트 소유 기록을 관리한다.
    /// </summary>
    // ============================================================
    public static class URPDirectOutputInstaller
    {

    #region 설치 대상 확인

        private const string MENU_PREFIX = "Tools/Xeri UI/URP/";

        // ------------------------------------------------------------
        /// <summary>
        /// Graphics와 Quality가 참조하는 중복 없는 URP 자산을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static IReadOnlyList<UniversalRenderPipelineAsset> FindProjectPipelineAssets()
        {
            var result = new List<UniversalRenderPipelineAsset>();
            var visited = new HashSet<UniversalRenderPipelineAsset>();

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset graphics &&
                visited.Add(graphics))
            {
                result.Add(graphics);
            }

            for (var index = 0; index < QualitySettings.names.Length; index++)
            {
                var pipeline = QualitySettings.GetRenderPipelineAssetAt(index)
                    as UniversalRenderPipelineAsset;
                if (pipeline != null && visited.Add(pipeline))
                {
                    result.Add(pipeline);
                }
            }

            return result;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선택한 설정 또는 프로젝트의 유일한 설정을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        internal static UISettingsAsset ResolveSettings()
        {
            if (Selection.activeObject is UISettingsAsset selected)
            {
                return selected;
            }

            var paths = AssetDatabase.FindAssets("t:UISettingsAsset");

            if (paths.Length != 1)
            {
                // 설정이 여러 개면 적용 대상을 추측하지 않는다.
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<UISettingsAsset>
            (
                AssetDatabase.GUIDToAssetPath(paths[0])
            );
        }

    #endregion

    #region 변경 미리보기

        // ------------------------------------------------------------
        /// <summary>
        /// 프로젝트 설치 대상과 누적된 적용 해제 범위를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public static string Preview(UISettingsAsset settings)
        {
            if (settings == null)
            {
                return "프로젝트 UISettingsAsset이 유일하지 않습니다. 대상 SettingsAsset을 선택하세요.";
            }

            var text = new StringBuilder();
            text.AppendLine("Xeri 설정: " + AssetDatabase.GetAssetPath(settings));
            text.AppendLine("고정밀 출력: " + settings.HighPrecisionOutput);
            text.AppendLine("UI 후처리: " + settings.UIPostProcessing);
            text.AppendLine("Graphics: " + DescribePipeline(GraphicsSettings.defaultRenderPipeline));

            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                var pipeline = QualitySettings.GetRenderPipelineAssetAt(i);
                var qualityDescription = DescribePipeline
                (
                    pipeline ?? GraphicsSettings.defaultRenderPipeline
                );
                text.AppendLine("Quality " + QualitySettings.names[i] + ": " + qualityDescription);
            }

            var assets = FindProjectPipelineAssets();
            if (assets.Count == 0)
            {
                text.AppendLine("호환 URP Asset이 없습니다. 기존 설정을 변경하지 않습니다.");
            }

            foreach (var asset in assets)
            {
                text.AppendLine(Preview(asset, settings, singleAsset: false));
            }

            var state = URPDirectOutputInstallationState.instance;
            text.AppendLine("기록된 정밀도 변경: " + state.PrecisionChanges.Count);
            text.AppendLine("기록된 출력 기능 변경: " + state.FeatureChanges.Count);
            text.AppendLine("프로젝트 적용 해제는 누적된 Xeri 소유 변경을 처리하고 사용자 편집은 보존합니다.");
            text.AppendLine("Graphics/Quality 참조를 변경하지 않습니다.");
            return text.ToString();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 단일 URP 자산의 출력 설정 변경을 미리 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public static string Preview
        (
            UniversalRenderPipelineAsset asset,
            UISettingsAsset settings,
            bool singleAsset = true
        )
        {
            if (asset == null)
            {
                return "대상 URP Asset이 없습니다.";
            }

            if (settings == null)
            {
                return "UISettingsAsset을 선택하거나 프로젝트에 단 하나만 둬야 합니다.";
            }

            var text = new StringBuilder();
            text.AppendLine("Xeri 설정: " + AssetDatabase.GetAssetPath(settings));
            text.AppendLine("대상 URP: " + AssetDatabase.GetAssetPath(asset));
            text.AppendLine("고정밀 출력: " + settings.HighPrecisionOutput);
            text.AppendLine("UI 후처리: " + settings.UIPostProcessing);
            text.AppendLine
            (
                settings.HighPrecisionOutput
                    ? "출력 정밀도: " + asset.hdrColorBufferPrecision + " → 64비트"
                    : "출력 정밀도: 유지 (고정밀 출력 꺼짐)"
            );

            var unique = new HashSet<UniversalRendererData>();

            foreach (var data in asset.rendererDataList)
            {
                if (data is not UniversalRendererData renderer)
                {
                    text.AppendLine
                    (
                        data == null
                            ? "렌더러 데이터 없음: 제외"
                            : data.name + ": 지원하지 않는 렌더러: 제외"
                    );
                    continue;
                }

                if (!unique.Add(renderer))
                {
                    continue;
                }

                text.AppendLine
                (
                    renderer.name + ": " +
                    (FindRendererFeature(renderer) == null
                        ? "Native UI 출력 기능 추가"
                        : "기존 Native UI 출력 기능 사용")
                );
            }

            if (unique.Count == 0)
            {
                text.AppendLine("지원하는 Universal Renderer Data가 없어 적용하지 않습니다.");
            }

            text.AppendLine("다른 기능과 Graphics/Quality 참조는 유지합니다.");
            if (singleAsset)
            {
                text.AppendLine("다른 Quality 자산에는 프로젝트 전체 적용을 사용할 수 있습니다.");
            }

            return text.ToString();
        }

        private static string DescribePipeline(RenderPipelineAsset asset)
        {
            return asset == null ? "Built-in / None" :
                AssetDatabase.GetAssetPath(asset) + " (" + asset.GetType().Name + ")";
        }

    #endregion

    #region 출력 설정 적용

        // ------------------------------------------------------------
        /// <summary>
        /// 단일 자산을 설치하고 구성 가능한 고유 렌더러 수를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static int Install(UniversalRenderPipelineAsset asset, UISettingsAsset settings)
        {
            ValidateAsset(asset, settings);
            return ExecuteInstallation
            (
                () =>
                {
                    InstallAsset(asset, settings);
                    return FindRenderers(asset).Count;
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 프로젝트에 설치하고 실제 변경된 URP 자산 수를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static int InstallProject(UISettingsAsset settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var assets = FindProjectPipelineAssets();
            foreach (var asset in assets)
            {
                ValidateAsset(asset, settings);
            }

            return ExecuteInstallation
            (
                () =>
                {
                    var changed = 0;
                    foreach (var asset in assets)
                    {
                        if (InstallAsset(asset, settings))
                        {
                            changed++;
                        }
                    }

                    return changed;
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 렌더러의 출력 기능을 구성하고 해당 기능을 반환한다.
        /// <br/> 실제 변경만 소유 기록으로 남겨 프로젝트 해제에 포함한다.
        /// </summary>
        // ------------------------------------------------------------
        public static URPNativeOverlayFeature InstallRenderer(UniversalRendererData renderer)
        {
            RequirePersistentAsset(renderer);
            return ExecuteInstallation(() => InstallRendererCore(renderer, out _));
        }

        private static void ValidateAsset(UniversalRenderPipelineAsset asset, UISettingsAsset settings)
        {
            RequirePersistentAsset(asset);
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            foreach (var renderer in FindRenderers(asset))
            {
                RequirePersistentAsset(renderer);
            }
        }

        private static void RequirePersistentAsset(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))))
            {
                throw new InvalidOperationException("출력 설정 대상은 프로젝트에 저장된 자산이어야 합니다.");
            }
        }

        private static HashSet<UniversalRendererData> FindRenderers(UniversalRenderPipelineAsset asset)
        {
            var renderers = new HashSet<UniversalRendererData>();
            foreach (var data in asset.rendererDataList)
            {
                if (data is UniversalRendererData renderer)
                {
                    renderers.Add(renderer);
                }
            }

            return renderers;
        }

        private static bool InstallAsset(UniversalRenderPipelineAsset asset, UISettingsAsset settings)
        {
            var renderers = FindRenderers(asset);
            if (renderers.Count == 0) return false;
            var changed = false;
            foreach (var renderer in renderers)
            {
                InstallRendererCore(renderer, out var rendererChanged);
                changed |= rendererChanged;
            }

            if (settings.HighPrecisionOutput && asset.hdrColorBufferPrecision != HDRColorBufferPrecision._64Bits)
            {
                var state = URPDirectOutputInstallationState.instance;
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
                state.RecordChange("Xeri 출력 정밀도 소유 기록");
                // 현재 값이 설치 값과 다르면 이전 소유권은 끝내고 사용자 값을 새 원본으로 삼는다.
                state.PrecisionChanges.RemoveAll(record => record.AssetGuid == guid);
                state.PrecisionChanges.Add
                (
                    new URPDirectOutputInstallationState.PrecisionChange
                    {
                        AssetGuid = guid,
                        Original = asset.hdrColorBufferPrecision
                    }
                );
                Undo.RecordObject(asset, "Xeri 출력 정밀도 적용");
                asset.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;
                EditorUtility.SetDirty(asset);
                changed = true;
            }

            return changed;
        }

        private static URPNativeOverlayFeature InstallRendererCore(UniversalRendererData renderer, out bool changed)
        {
            var previous = FindRendererFeature(renderer);
            var previouslyActive = previous != null && previous.isActive;
            changed = previous == null || !previouslyActive;
            if (!changed) return previous;
            var feature = CreateOrActivateFeature(renderer);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
            {
                throw new InvalidOperationException("설치한 출력 기능의 영속 자산 ID를 찾을 수 없습니다.");
            }

            var state = URPDirectOutputInstallationState.instance;
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer));
            // 하위 자산 생성 뒤에 기록해야 Redo가 기능과 소유 기록을 함께 복원한다.
            state.RecordChange("Xeri 출력 기능 소유 기록");
            state.FeatureChanges.RemoveAll(record => record.RendererGuid == guid && record.FeatureLocalId == localId);
            state.FeatureChanges.Add
            (
                new URPDirectOutputInstallationState.FeatureChange
                {
                    RendererGuid = guid,
                    FeatureLocalId = localId,
                    Created = previous == null
                }
            );
            return feature;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 설치 호출이 만든 변경과 소유 기록만 함께 저장하거나 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
        private static T ExecuteInstallation<T>(Func<T> install)
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Xeri URP 출력 설정 적용");
            var state = URPDirectOutputInstallationState.instance;
            try
            {
                var result = install();
                Undo.FlushUndoRecordObjects();
                state.Persist();
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                return result;
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
                    throw new AggregateException("URP 설치와 변경 복원이 모두 실패했습니다.", exception, cleanupException);
                }

                throw;
            }
        }

        private static URPNativeOverlayFeature CreateOrActivateFeature(UniversalRendererData renderer)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            var feature = FindRendererFeature(renderer);

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<URPNativeOverlayFeature>();
                feature.name = "Xeri Native UI Output";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                Undo.RegisterCreatedObjectUndo(feature, "Xeri Native UI Output Feature");
                // Redo는 하위 자산을 먼저 복구한 뒤 이 참조 목록을 재생해야 한다.
                Undo.RegisterCompleteObjectUndo(renderer, "Xeri Native UI Output Feature");
                renderer.rendererFeatures.Add(feature);
                EditorUtility.SetDirty(renderer);
            }

            if (!feature.isActive)
            {
                Undo.RecordObject(feature, "Xeri Native UI Output Enable");
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
            }

            return feature;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 렌더러에 이미 있는 Xeri 출력 기능을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        internal static URPNativeOverlayFeature FindRendererFeature
        (
            UniversalRendererData renderer
        )
        {
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is URPNativeOverlayFeature output)
                {
                    return output;
                }
            }

            return null;
        }

    #endregion

    #region 출력 설정 적용 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 프로젝트 소유 변경을 해제하고 실제 복원한 항목 수를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static int UninstallProject()
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Xeri URP 출력 설정 적용 해제");
            var state = URPDirectOutputInstallationState.instance;
            state.RecordChange("Xeri URP 소유 기록 해제");
            var restored = 0;
            var errors = new List<Exception>();
            for (var index = state.FeatureChanges.Count - 1; index >= 0; index--)
            {
                try
                {
                    if (UninstallFeature(state.FeatureChanges[index]))
                    {
                        restored++;
                    }

                    // 사용자 편집·대상 삭제도 정상적인 소유권 종료다.
                    state.FeatureChanges.RemoveAt(index);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            for (var index = state.PrecisionChanges.Count - 1; index >= 0; index--)
            {
                try
                {
                    if (UninstallPrecision(state.PrecisionChanges[index]))
                    {
                        restored++;
                    }

                    state.PrecisionChanges.RemoveAt(index);
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
                throw new AggregateException("URP 적용 해제 중 완료하지 못한 변경이 있습니다.", errors);
            }

            return restored;
        }

        private static bool UninstallFeature
        (
            URPDirectOutputInstallationState.FeatureChange record
        )
        {
            var path = AssetDatabase.GUIDToAssetPath(record.RendererGuid);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (renderer == null)
            {
                return false;
            }

            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is not URPNativeOverlayFeature direct)
                {
                    continue;
                }

                if
                (
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier
                    (
                        direct, out string _, out long id
                    ) ||
                    id != record.FeatureLocalId
                )
                {
                    continue;
                }

                if (!record.Created)
                {
                    if (direct.isActive)
                    {
                        Undo.RecordObject(direct, "Restore Xeri Native UI Feature State");
                        direct.SetActive(false);
                        EditorUtility.SetDirty(direct);
                        AssetDatabase.SaveAssetIfDirty(direct);
                        return true;
                    }

                    AssetDatabase.SaveAssetIfDirty(direct);
                    return false;
                }

                // 설치 후 사용자가 비활성화하거나 이름을 바꾼 항목은 보존한다.
                if (!direct.isActive || direct.name != "Xeri Native UI Output")
                {
                    var rendererPath = AssetDatabase.GetAssetPath(renderer);
                    Debug.LogWarning("사용자가 편집한 출력 기능을 보존합니다: " + rendererPath);
                    AssetDatabase.SaveAssetIfDirty(direct);
                    return false;
                }

                Undo.RegisterCompleteObjectUndo(renderer, "Restore Xeri Native UI Feature");
                renderer.rendererFeatures.Remove(direct);
                Undo.DestroyObjectImmediate(direct);
                EditorUtility.SetDirty(renderer);
                AssetDatabase.SaveAssetIfDirty(renderer);
                return true;
            }

            AssetDatabase.SaveAssetIfDirty(renderer);
            return false;
        }

        private static bool UninstallPrecision
        (
            URPDirectOutputInstallationState.PrecisionChange record
        )
        {
            var path = AssetDatabase.GUIDToAssetPath(record.AssetGuid);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                return false;
            }

            if (asset.hdrColorBufferPrecision != HDRColorBufferPrecision._64Bits)
            {
                Debug.LogWarning("사용자가 편집한 출력 정밀도를 보존합니다: " + path);
                AssetDatabase.SaveAssetIfDirty(asset);
                return false;
            }

            Undo.RecordObject(asset, "Restore Xeri Native UI HDR Precision");
            asset.hdrColorBufferPrecision = record.Original;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return true;
        }

    #endregion

    #region 메뉴

        [MenuItem(MENU_PREFIX + "출력 설정 미리보기", false, 2100)]
        private static void ShowPreview()
        {
            EditorUtility.DisplayDialog("Xeri URP 출력 설정", Preview(ResolveSettings()), "확인");
        }

        [MenuItem(MENU_PREFIX + "출력 설정 적용", false, 2101)]
        private static void ShowInstall()
        {
            var settings = ResolveSettings();
            var preview = Preview(settings);
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Xeri URP 출력 설정", preview, "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog("Xeri URP 출력 설정", preview, "적용", "취소")) return;
            try
            {
                var changed = InstallProject(settings);
                EditorUtility.DisplayDialog("Xeri URP 출력 설정", changed == 0 ? "변경할 출력 설정이 없습니다." : changed + "개 URP 자산의 출력 설정을 변경했습니다.", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Xeri URP 출력 설정", "변경에 실패했습니다.\n" + exception.Message, "확인");
            }
        }

        [MenuItem(MENU_PREFIX + "출력 설정 적용 해제", false, 2102)]
        private static void ShowUninstall()
        {
            var state = URPDirectOutputInstallationState.instance;
            var count = state.FeatureChanges.Count + state.PrecisionChanges.Count;
            var preview = "누적된 프로젝트 소유 기록 " + count + "건을 해제합니다.\n" +
                "사용자 편집과 기존 출력 기능은 보존하며 Xeri가 변경한 설정만 복원합니다.";
            if (!EditorUtility.DisplayDialog("Xeri URP 출력 설정", preview, "적용 해제", "취소")) return;
            try
            {
                var restored = UninstallProject();
                EditorUtility.DisplayDialog("Xeri URP 출력 설정", restored + "개 항목을 복원했습니다. 사용자 편집·삭제된 대상의 소유 기록도 정리했습니다.", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Xeri URP 출력 설정", "변경에 실패했습니다.\n" + exception.Message, "확인");
            }
        }

    #endregion

    }
}
