/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UINativeOverlayOwnership.cs
수정일 : 2026-10-18

# 설명
SRP 통합부 공통 Native UI Overlay 소유권의 단일 lease 및 복원 계약을 검증한다.

# 테스트 구성
 O: 단일 소유권, 외부 억제, 예외 반환과 파이프라인 상태 교체
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.Rendering;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    // ============================================================
    /// <summary>
    /// Native Overlay 소유권의 단일 획득·복원 조건을 검증한다.
    /// </summary>
    // ============================================================
    public sealed class TEST_UINativeOverlayOwnership
    {

    #region O-1: Native 상태의 단일 소유와 복원

        // ----------------------------------------------------------------------
        /// <summary>
        /// 다른 SRP 소유자가 이미 획득했을 때 중복 획득과 잘못된 반환을 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_NativeOverlay_다른SRPOwner가이미선점하면중복획득불가()
        {
            var previous = SupportedRenderingFeatures.active.rendersUIOverlay;
            var first = new object();
            var second = new object();
            var host = new GameObject("Native UI Overlay Ownership Camera");
            var otherHost = new GameObject("Other Native UI Camera");

            try
            {
                var camera = host.AddComponent<Camera>();
                SupportedRenderingFeatures.active.rendersUIOverlay = true;

                Assert.IsTrue(NativeUIOverlayOwnership.TryAcquire(first, camera));
                Assert.IsFalse(SupportedRenderingFeatures.active.rendersUIOverlay);
                Assert.IsFalse(NativeUIOverlayOwnership.TryAcquire(second, camera));
                Assert.IsFalse(NativeUIOverlayOwnership.Release(second));
                Assert.IsFalse(NativeUIOverlayOwnership.Release(first, otherHost.AddComponent<Camera>()));
                Assert.IsFalse(SupportedRenderingFeatures.active.rendersUIOverlay);

                Assert.IsTrue(NativeUIOverlayOwnership.Release(first, camera));
                Assert.IsTrue(SupportedRenderingFeatures.active.rendersUIOverlay);
                Assert.IsFalse(NativeUIOverlayOwnership.Release(first));
            }
            finally
            {
                NativeUIOverlayOwnership.Release(first);
                NativeUIOverlayOwnership.Release(second);
                SupportedRenderingFeatures.active.rendersUIOverlay = previous;
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(otherHost);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 외부가 Native Overlay를 이미 비활성화한 경우 기존 상태를 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_NativeOverlay_외부에서Native억제중이면획득안함()
        {
            var previous = SupportedRenderingFeatures.active.rendersUIOverlay;
            var host = new GameObject("Native UI External Ownership Camera");
            var owner = new object();

            try
            {
                var camera = host.AddComponent<Camera>();
                SupportedRenderingFeatures.active.rendersUIOverlay = false;

                Assert.IsFalse(NativeUIOverlayOwnership.TryAcquire(owner, camera));
                Assert.IsFalse(NativeUIOverlayOwnership.Release(owner));
                Assert.IsFalse(SupportedRenderingFeatures.active.rendersUIOverlay);
            }
            finally
            {
                NativeUIOverlayOwnership.Release(owner);
                SupportedRenderingFeatures.active.rendersUIOverlay = previous;
                Object.DestroyImmediate(host);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 예외 후에도 다음 카메라가 Native 소유권을 다시 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_NativeOverlay_예외후복원하면재획득가능()
        {
            var previous = SupportedRenderingFeatures.active.rendersUIOverlay;
            var host = new GameObject("Native UI Lease Recovery Camera");
            var owner = new object();

            try
            {
                var camera = host.AddComponent<Camera>();
                SupportedRenderingFeatures.active.rendersUIOverlay = true;

                try
                {
                    Assert.IsTrue(NativeUIOverlayOwnership.TryAcquire(owner, camera));
                    throw new System.InvalidOperationException("의도된 패스 등록 실패");
                }
                catch (System.InvalidOperationException)
                {
                    NativeUIOverlayOwnership.Release(owner, camera);
                }

                Assert.IsTrue(SupportedRenderingFeatures.active.rendersUIOverlay);
                Assert.IsTrue(NativeUIOverlayOwnership.TryAcquire(owner, camera));
                Assert.IsTrue(NativeUIOverlayOwnership.Release(owner, camera));
            }
            finally
            {
                NativeUIOverlayOwnership.Release(owner);
                SupportedRenderingFeatures.active.rendersUIOverlay = previous;
                Object.DestroyImmediate(host);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 파이프라인 상태가 교체돼도 실제로 빌린 상태만 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_NativeOverlay_파이프라인교체시빌린상태만복원()
        {
            var previous = SupportedRenderingFeatures.active;
            var previousOverlay = previous.rendersUIOverlay;
            var replacement = new SupportedRenderingFeatures();
            replacement.rendersUIOverlay = false;
            var owner = new object();
            var host = new GameObject("Native UI Pipeline Replacement Camera");

            try
            {
                var camera = host.AddComponent<Camera>();
                previous.rendersUIOverlay = true;
                Assert.IsTrue(NativeUIOverlayOwnership.TryAcquire(owner, camera));
                Assert.IsFalse(previous.rendersUIOverlay);

                SupportedRenderingFeatures.active = replacement;
                Assert.IsTrue(NativeUIOverlayOwnership.Release(owner, camera));
                Assert.IsTrue(previous.rendersUIOverlay);
                Assert.IsFalse(replacement.rendersUIOverlay);
                Assert.AreSame(replacement, SupportedRenderingFeatures.active);
                Assert.IsFalse(NativeUIOverlayOwnership.TryAcquire(owner, camera));
            }
            finally
            {
                NativeUIOverlayOwnership.Release(owner);
                SupportedRenderingFeatures.active = previous;
                previous.rendersUIOverlay = previousOverlay;
                Object.DestroyImmediate(host);
            }
        }

    #endregion

    }
}
