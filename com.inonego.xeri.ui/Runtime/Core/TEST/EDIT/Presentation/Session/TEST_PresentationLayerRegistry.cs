/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_PresentationLayerRegistry.cs
수정일 : 2026-09-23

# 설명
PresentationLayerRegistry의 stable LayerID 조회, usage lifetime과 종료 경계를 검증한다.
Ordering과 authoring asset identity가 Registry 책임에서 제거됐는지도 계약으로 고정한다.

# 테스트 구성
 R: stable ID 등록과 조회
 U: Layer usage lifetime
 L: Registry 종료와 backend 비활성화
 X: 등록 중 재진입 실패 경계
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ======================================================================
    /// <summary>
    /// PresentationLayerRegistry의 lookup과 usage lifetime 테스트.
    /// </summary>
    // ======================================================================
    public sealed class TEST_PresentationLayerRegistry
    {

    #region 헬퍼

        private sealed class TestLayerDriver : IPresentationLayerDriver<Transform>
        {
            public Transform Root { get; }
            public bool IsActive { get; private set; }
            public int DeactivateCount { get; private set; }
            public Action<bool> ActiveChanged { get; set; }

            public TestLayerDriver(Transform root)
            {
                Root = root;
            }

            public bool Validate(out string error)
            {
                error = Root == null ? "invalid" : "";
                return string.IsNullOrEmpty(error);
            }

            public void SetActive(bool active)
            {
                IsActive = active;

                if (!active)
                {
                    DeactivateCount++;
                }

                ActiveChanged?.Invoke(active);
            }
        }

        private readonly List<UnityEngine.Object> ownedObjects = new();

        private Transform CreateRoot(string name)
        {
            var root = new GameObject(name).transform;
            ownedObjects.Add(root.gameObject);
            return root;
        }

    #endregion

    #region 픽스처

        [TearDown]
        public void TearDown()
        {
            for (var index = ownedObjects.Count - 1; index >= 0; index--)
            {
                if (ownedObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[index]);
                }
            }

            ownedObjects.Clear();
        }

    #endregion

    #region R-1: stable ID 등록과 해제

        [Test]
        public void TEST_PresentationLayerRegistry_ID등록_조회와해제_대칭()
        {
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(CreateRoot("Screen"));
            var handle = registry.Register("Screen", driver);

            Assert.IsTrue(registry.Contains("Screen"));
            Assert.IsTrue(driver.IsActive);

            handle.Dispose();

            Assert.IsFalse(registry.Contains("Screen"));
            Assert.IsFalse(driver.IsActive);
            Assert.AreEqual(1, driver.DeactivateCount);
            registry.Dispose();
        }

    #endregion

    #region R-2: Registry 전체 ID uniqueness

        [Test]
        public void TEST_PresentationLayerRegistry_동일ID중복등록_거부()
        {
            var registry = new PresentationLayerRegistry();
            var firstHandle = registry.Register
            (
                "Shared",
                new TestLayerDriver(CreateRoot("First"))
            );

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => registry.Register
                (
                    "Shared",
                    new TestLayerDriver(CreateRoot("Second"))
                )
            );

            StringAssert.Contains("이미 등록", exception.Message);
            firstHandle.Dispose();
            registry.Dispose();
        }

    #endregion

    #region U-1: Registration Handle 해제 전 usage preflight

        [Test]
        public void TEST_PresentationLayerRegistry_활성Usage_RegistrationHandle해제거부후재시도()
        {
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(CreateRoot("Shared"));
            var handle = registry.Register("Shared", driver);
            Assert.IsTrue(registry.TryAcquireUsage("Shared", out _, out var usage));

            Assert.Throws<InvalidOperationException>(handle.Dispose);
            Assert.IsTrue(registry.Contains("Shared"));
            Assert.IsTrue(driver.IsActive);

            usage.Dispose();
            Assert.DoesNotThrow(handle.Dispose);
            Assert.IsFalse(registry.Contains("Shared"));
            registry.Dispose();
        }

    #endregion

    #region U-2: Registry 종료 전 usage preflight

        [Test]
        public void TEST_PresentationLayerRegistry_활성Usage_Registry종료상태변경전거부()
        {
            var registry = new PresentationLayerRegistry();
            var handle = registry.Register
            (
                "Shared",
                new TestLayerDriver(CreateRoot("Shared"))
            );
            Assert.IsTrue(registry.TryAcquireUsage("Shared", out _, out var usage));

            Assert.Throws<InvalidOperationException>(registry.Dispose);
            Assert.IsTrue(registry.Contains("Shared"));

            usage.Dispose();
            handle.Dispose();
            registry.Dispose();
        }

    #endregion

    #region L-1: Registry 선종료

        [Test]
        public void TEST_PresentationLayerRegistry_Registry선종료_backend한번만비활성화()
        {
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(CreateRoot("Layer"));
            var handle = registry.Register("Layer", driver);

            registry.Dispose();

            Assert.IsFalse(driver.IsActive);
            Assert.AreEqual(1, driver.DeactivateCount);
            Assert.IsTrue(handle.IsDisposed);

            handle.Dispose();
            Assert.AreEqual(1, driver.DeactivateCount);
        }

    #endregion

    #region X-1: 활성화 callback 재진입

        [Test]
        public void TEST_PresentationLayerRegistry_활성화중RegistryDispose_등록공개안함()
        {
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(CreateRoot("Interrupted"));
            driver.ActiveChanged = active =>
            {
                if (active)
                {
                    registry.Dispose();
                }
            };

            Assert.Throws<ObjectDisposedException>
            (
                () => registry.Register("Interrupted", driver)
            );

            Assert.IsFalse(registry.Contains("Interrupted"));
            Assert.IsTrue(registry.IsDisposed);
        }

    #endregion

    }
}
