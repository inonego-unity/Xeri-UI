/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : URPDirectOutputInstallationState.cs
수정일 : 2026-10-28

# 설명
프로젝트 URP Direct Output 설치가 실제로 변경한 Asset 값과 Feature 소유권을 영속 기록한다.

# 특이사항, 제약사항
ProjectSettings에 저장하며 다른 사용자의 URP 설정과 기존 RendererFeature는 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace inonego.Xeri.UI.URP.Editor
{
    // ======================================================================
    /// <summary>
    /// URP Native UI 출력의 Feature·정밀도 변경에 대한 소유권을 기록한다.
    /// </summary>
    // ======================================================================
    [FilePath
    (
        "ProjectSettings/XeriUIURPDirectOutputInstallationState.asset",
        FilePathAttribute.Location.ProjectFolder
    )]
    internal sealed class URPDirectOutputInstallationState :
        ScriptableSingleton<URPDirectOutputInstallationState>
    {

    #region 기록

        // ============================================================
        /// <summary>
        /// 기존 URP HDR Color Buffer Precision의 복원 정보를 저장한다.
        /// </summary>
        // ============================================================
        [Serializable]
        internal sealed class PrecisionChange
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 정밀도 변경 대상 URP Asset의 GUID.
            /// </summary>
            // ------------------------------------------------------------
            public string AssetGuid;

            // ------------------------------------------------------------
            /// <summary>
            /// 설치 전 HDR 색상 버퍼 정밀도.
            /// </summary>
            // ------------------------------------------------------------
            public HDRColorBufferPrecision Original;
        }

        // ============================================================
        /// <summary>
        /// 추가하거나 활성화한 URP Renderer Feature의 복원 정보를 저장한다.
        /// </summary>
        // ============================================================
        [Serializable]
        internal sealed class FeatureChange
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Renderer Feature가 추가되거나 활성화된 Renderer Data GUID.
            /// </summary>
            // ------------------------------------------------------------
            public string RendererGuid;

            // ------------------------------------------------------------
            /// <summary>
            /// 변경한 Renderer Feature의 local file ID.
            /// </summary>
            // ------------------------------------------------------------
            public long FeatureLocalId;

            // ------------------------------------------------------------
            /// <summary>
            /// Feature를 새로 만들었는지 여부.
            /// </summary>
            // ------------------------------------------------------------
            public bool Created;
        }

        [SerializeField]
        internal List<PrecisionChange> PrecisionChanges = new();

        [SerializeField]
        internal List<FeatureChange> FeatureChanges = new();

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
        /// Xeri가 소유한 설치 기록을 ProjectSettings에 영속 저장한다.
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
