/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : HDRPNativeOutputInstallationState.cs
수정일 : 2026-10-28

# 설명
설치기가 추가한 HDRP 등록·기본 볼륨·포맷 변경만 프로젝트에 기록한다.
기존 필드와 직렬화 identity를 유지하며 사용자 소유 항목은 기록하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace inonego.Xeri.UI.HDRP.Editor
{
    // ============================================================
    /// <summary>
    /// HDRP 자산 변경과 함께 저장·복원되는 설치 소유 기록.
    /// </summary>
    // ============================================================
    [FilePath("ProjectSettings/XeriUIHDRPNativeOutputInstallationState.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class HDRPNativeOutputInstallationState : ScriptableSingleton<HDRPNativeOutputInstallationState>
    {

    #region 소유 기록

        [SerializeField] internal string GlobalSettingsGuid;
        [SerializeField] internal string OwnedTypeName;
        [SerializeField] internal string OwnedBeforeTypeName;
        [SerializeField] internal VolumeProfile Profile;
        [SerializeField] internal List<VolumeComponent> Components = new();
        [SerializeField] internal List<PostProcessFormatChange> FormatChanges = new();

        // ============================================================
        /// <summary>
        /// HDRP 에셋의 설치 전 후처리 포맷을 복원하기 위한 기록.
        /// </summary>
        // ============================================================
        [Serializable]
        internal sealed class PostProcessFormatChange
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 포맷 변경 대상 HDRP Asset의 GUID.
            /// </summary>
            // ------------------------------------------------------------
            public string AssetGuid;

            // ------------------------------------------------------------
            /// <summary>
            /// 설치 전 HDRP 후처리 버퍼 포맷.
            /// </summary>
            // ------------------------------------------------------------
            public PostProcessBufferFormat OriginalFormat;
        }

    #endregion

    #region 변경 기록과 저장 수명

        // ------------------------------------------------------------
        /// <summary>
        /// 자산 Undo와 함께 설치 소유 기록도 되돌릴 수 있도록 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void RecordChange(string label)
        {
            Undo.RegisterCompleteObjectUndo(this, label);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 설치 소유 기록을 프로젝트에 영속 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Persist()
        {
            Save(true);
        }

        private void OnEnable()
        {
            Undo.undoRedoEvent -= HandleUndoRedo;
            Undo.undoRedoEvent += HandleUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoEvent -= HandleUndoRedo;
        }

        private void HandleUndoRedo(in UndoRedoInfo info)
        {
            // Undo가 복원한 메모리 상태를 재시작 뒤에도 같은 소유 기록으로 읽는다.
            if (File.Exists(GetFilePath()))
            {
                Persist();
            }
        }

    #endregion

    }
}
