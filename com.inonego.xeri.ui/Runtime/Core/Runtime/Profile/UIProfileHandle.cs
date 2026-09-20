/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIProfileHandle.cs
수정일 : 2026-09-17
# 설명
Profile이 획득한 Layer 등록과 Provider GameObject의 대칭 수명을 생성 역순으로 소유한다.

# 종료 계약
활성 Layer 소비자 검사는 상태 변경 전에 종료 요청을 거부한다.
검증을 통과한 종료는 각 Layer 등록과 Provider 반환을 한 번만 시도하고 Handle을 Terminal로 확정한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Profile 획득 수명 Handle.
    /// </summary>
    // ============================================================
    public sealed class UIProfileHandle : IDisposable
    {

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// Profile이 획득한 한 Layer의 Provider 인스턴스와 등록 Handle.
        /// </summary>
        // ============================================================
        internal sealed class OwnedLayer
        {
            public IGameObjectProvider Provider = null;
            public GameObject Instance = null;
            public PresentationLayerHandle LayerHandle = null;
        }

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Profile의 논리 소유권과 Layer 등록이 종료됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Handle이 획득한 Profile Asset.
        /// </summary>
        // ------------------------------------------------------------
        public UIProfileAsset Profile { get; }

        private readonly List<OwnedLayer> ownedLayers = new List<OwnedLayer>();
        private Action<UIProfileHandle> onReleaseCompleted = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 준비 중 Profile Handle을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal UIProfileHandle
        (
            UIProfileAsset profile,
            Action<UIProfileHandle> onReleaseCompleted
        ) : base()
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.onReleaseCompleted = onReleaseCompleted;
            IsDisposed = false;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Profile 획득 중 생성한 Provider 인스턴스 소유권을 즉시 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        internal OwnedLayer AddLayer
        (
            IGameObjectProvider provider,
            GameObject instance
        )
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(UIProfileHandle));
            }

            var layer = new OwnedLayer
            {
                Provider = provider ?? throw new ArgumentNullException(nameof(provider)),
                Instance = instance ?? throw new ArgumentNullException(nameof(instance)),
            };

            ownedLayers.Add(layer);
            return layer;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 소유 Layer에 완료된 등록 Handle을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void AttachLayerHandle
        (
            OwnedLayer layer,
            PresentationLayerHandle layerHandle
        )
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(UIProfileHandle));
            }

            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            layer.LayerHandle = layerHandle ?? throw new ArgumentNullException(nameof(layerHandle));
        }

    #endregion

    #region IDisposable

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 활성 Layer 소비자가 없는지 먼저 검증한 뒤 소유권을 Terminal 상태로 확정한다.
        /// <br/> 검증 뒤 시작한 Layer 등록과 Provider 반환은 성공 여부와 관계없이 한 번만 시도한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public void Dispose()
        {
            if (IsDisposed) return;

            // 일부 반환 전에 모든 활성 소비자를 검증해 잘못된 호출 순서가 상태를 훼손하지 않게 한다.
            for (var i = ownedLayers.Count - 1; i >= 0; i--)
            {
                var layerHandle = ownedLayers[i].LayerHandle;

                if (layerHandle != null && layerHandle.HasConsumers)
                {
                    throw new InvalidOperationException
                    (
                        $"UI Profile Layer '{layerHandle.ID}'에 활성 소비자가 남아 있습니다."
                    );
                }
            }

            IsDisposed = true;

            var errors = new List<Exception>();

            // 모든 Layer 등록을 먼저 종료해 Provider 인스턴스 반환 전에 UI 사용 경계를 닫는다.
            for (var i = ownedLayers.Count - 1; i >= 0; i--)
            {
                var layer = ownedLayers[i];

                if (layer.LayerHandle == null) continue;

                try
                {
                    layer.LayerHandle.Dispose();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                layer.LayerHandle = null;
            }

            // 불투명한 Provider 반환은 부분 실행 가능성이 있으므로 각 인스턴스를 한 번만 전달한다.
            for (var i = ownedLayers.Count - 1; i >= 0; i--)
            {
                var layer = ownedLayers[i];
                var provider = layer.Provider;
                var instance = layer.Instance;
                layer.Provider = null;
                layer.Instance = null;

                if (provider == null || instance == null) continue;

                try
                {
                    provider.Release(instance, false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            ownedLayers.Clear();
            NotifyReleaseCompleted();

            if (errors.Count > 0)
            {
                throw new AggregateException("UI Profile 해제 중 하나 이상의 반환이 실패했습니다.", errors);
            }
        }

    #endregion

    #region 내부 처리

        // ------------------------------------------------------------
        /// <summary>
        /// Terminal Handle을 Runtime 추적에서 한 번 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        private void NotifyReleaseCompleted()
        {
            if (onReleaseCompleted == null) return;

            var callback = onReleaseCompleted;
            onReleaseCompleted = null;
            callback(this);
        }

    #endregion

    }
}
