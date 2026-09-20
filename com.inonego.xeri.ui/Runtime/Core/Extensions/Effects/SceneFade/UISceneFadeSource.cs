/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UISceneFadeSource.cs
수정일 : 2026-07-31

# 설명
UIRuntime이 직렬화하고 초기화할 Scene Fade Source의 최소 Unity 조립 경계를 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Runtime에 하나 선택되는 Scene Fade View Source.
    /// </summary>
    // ============================================================
    public abstract class UISceneFadeSource :
        MonoBehaviour,
        IPresentationSource<ISceneFadeDriver>,
        IDisposable
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 직렬화된 native Fade 구성을 검증하고 Source를 준비한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract void Initialize();

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Layer에 Scene Fade View를 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract ISceneFadeDriver Acquire(IPresentationLayerDriver layer);

        // ------------------------------------------------------------
        /// <summary>
        /// 이전에 획득한 Scene Fade View를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract void Release(ISceneFadeDriver view);

        // ------------------------------------------------------------
        /// <summary>
        /// Source가 보유한 Scene Fade View와 native 자원을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract void Dispose();
    }
}
