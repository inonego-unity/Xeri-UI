/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIProfileAsset.cs
수정일 : 2026-09-17
# 설명
App·Scene·게임 모드 수명에서 획득할 Presentation Layer Asset과 Provider 구성을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Presentation Layer 묶음의 직렬화 Profile.
    /// </summary>
    // ============================================================
    [CreateAssetMenu
    (
        fileName = "UI Profile",
        menuName = "Xeri/UI/Profile"
    )]
    public sealed class UIProfileAsset : ScriptableObject
    {

    #region 내부 데이터

        [Serializable]
        private sealed class LayerEntry
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Entry가 등록할 Presentation Layer Asset.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationLayerAsset Asset => asset;

            [SerializeField]
            private PresentationLayerAsset asset = null;

            // ------------------------------------------------------------
            /// <summary>
            /// Layer Root를 획득·반환할 Provider.
            /// </summary>
            // ------------------------------------------------------------
            public IGameObjectProvider Provider => provider;

            [SerializeReference]
            private IGameObjectProvider provider = new PrefabGameObjectProvider();
        }

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Profile에 정의된 Layer Entry 수.
        /// </summary>
        // ------------------------------------------------------------
        internal int Count => layers.Count;

        [SerializeField]
        private List<LayerEntry> layers = new List<LayerEntry>();

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 순서의 Presentation Layer Asset을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal PresentationLayerAsset GetLayerAsset(int index)
        {
            return layers[index].Asset;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 순서의 Layer Root Provider를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal IGameObjectProvider GetProvider(int index)
        {
            return layers[index].Provider;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Asset, Provider와 Profile 내부 ID 중복을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Validate()
        {
            if (layers == null)
            {
                throw new InvalidOperationException("UI Profile Layer 목록이 null입니다.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < layers.Count; i++)
            {
                var entry = layers[i];

                if (entry == null)
                {
                    throw new InvalidOperationException($"UI Profile Layer Entry {i}가 null입니다.");
                }

                if (entry.Asset == null)
                {
                    throw new InvalidOperationException($"UI Profile Layer Entry {i}의 Asset이 null입니다.");
                }

                if (entry.Provider == null)
                {
                    throw new InvalidOperationException
                    (
                        $"UI Profile Layer '{entry.Asset.ID}'의 Provider가 null입니다."
                    );
                }

                entry.Asset.Validate();

                if (!ids.Add(entry.Asset.ID))
                {
                    throw new InvalidOperationException
                    (
                        $"UI Profile에 Layer '{entry.Asset.ID}'가 중복 정의되어 있습니다."
                    );
                }
            }
        }

    #endregion

    }
}
