/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUISceneFadeSource.cs
수정일 : 2026-09-17
# 설명
IGameObjectProvider 기반 UGUI Scene Fade View를 Runtime Fade Source 계약으로 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI Scene Fade View Source.
    /// </summary>
    // ============================================================
    public sealed class UGUISceneFadeSource : UISceneFadeSource
    {

    #region 필드

        [SerializeReference]
        private IGameObjectProvider viewProvider = new PrefabGameObjectProvider();

        private GameObjectProviderPresentationSource<ISceneFadeDriver> source = null;
        private bool isInitialized = false;
        private bool isDisposed = false;

    #endregion

    #region UISceneFadeSource

        // ------------------------------------------------------------
        /// <summary>
        /// UGUI Fade Provider를 검증하고 실제 Overlay Source를 준비한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Initialize()
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("UGUI Scene Fade Source가 이미 초기화됐습니다.");
            }

            if (isDisposed)
            {
                throw new InvalidOperationException("해제된 UGUI Scene Fade Source는 초기화할 수 없습니다.");
            }

            if (!enabled)
            {
                throw new InvalidOperationException("UGUI Scene Fade Source가 비활성 상태입니다.");
            }

            if (viewProvider == null)
            {
                throw new InvalidOperationException("UGUI Scene Fade View Provider가 설정되지 않았습니다.");
            }

            source = new GameObjectProviderPresentationSource<ISceneFadeDriver>(viewProvider);
            isInitialized = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// RectTransform Layer에 UGUI Scene Fade View를 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        public override ISceneFadeDriver Acquire(IPresentationLayerDriver layer)
        {
            ThrowIfUnavailable();
            return source.Acquire(layer);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이전에 획득한 UGUI Scene Fade View를 Provider에 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Release(ISceneFadeDriver view)
        {
            if (isDisposed) return;

            ThrowIfUnavailable();
            source.Release(view);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Source가 소유한 UGUI Fade View를 Provider에 한 번 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Dispose()
        {
            if (isDisposed) return;

            isInitialized = false;
            isDisposed = true;
            var current = source;
            source = null;

            current?.Dispose();
        }

    #endregion

    #region 내부 처리

        // ------------------------------------------------------------
        /// <summary>
        /// 초기화 완료 상태에서만 Fade View 수명 작업을 허용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfUnavailable()
        {
            if (!isInitialized || isDisposed || source == null)
            {
                throw new InvalidOperationException("UGUI Scene Fade Source가 사용 가능한 상태가 아닙니다.");
            }
        }

    #endregion

    }
}
