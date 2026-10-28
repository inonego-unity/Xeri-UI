/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : NativeUIOverlayOwnership.cs
수정일 : 2026-10-18

# 설명
중복 SRP Feature가 공유하는 Unity Native UI Overlay 소유권 경계.
하나의 Camera frame에서 단일 SRP owner만 전역 overlay 억제를 획득하고 원상 복원한다.

# 제약사항
Camera/RendererList 검사와 RenderGraph pass 성공 여부는 각 SRP가 판단한다.
UI tree, Window, Gradient 및 PresentationOutput 구성요소를 조회하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.Rendering;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// SRP 통합부가 공유하는 Camera 범위의 Native UI 전역 소유권.
    /// </summary>
    // ============================================================
    internal static class NativeUIOverlayOwnership
    {

    #region 소유 상태

        private static object currentOwner;
        private static Camera currentCamera;
        private static SupportedRenderingFeatures currentFeatures;

    #endregion

    #region 소유권 획득과 복원

        // ------------------------------------------------------------
        /// <summary>
        /// 단일 Camera와 owner의 Native UI 출력 억제 권한을 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static bool TryAcquire(object owner, Camera camera)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            var features = SupportedRenderingFeatures.active;
            if (currentOwner != null || !features.rendersUIOverlay)
            {
                return false;
            }

            currentOwner = owner;
            currentCamera = camera;
            currentFeatures = features;
            features.rendersUIOverlay = false;
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 획득한 owner와 선택적 Camera identity가 일치할 때만 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static bool Release(object owner, Camera camera = null)
        {
            if
            (
                !ReferenceEquals(owner, currentOwner) ||
                (camera != null && !ReferenceEquals(camera, currentCamera))
            )
            {
                return false;
            }

            Restore();
            return true;
        }

        private static void Restore()
        {
            if (currentOwner == null)
            {
                return;
            }

            // 파이프라인이 active 상태를 교체해도 직접 빌린 인스턴스만 반환한다.
            currentFeatures.rendersUIOverlay = true;
            currentOwner = null;
            currentCamera = null;
            currentFeatures = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Restore();
        }

    #endregion

    }
}
