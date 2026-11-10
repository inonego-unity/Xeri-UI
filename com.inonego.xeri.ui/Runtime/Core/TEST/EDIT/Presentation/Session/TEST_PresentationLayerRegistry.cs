/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_PresentationLayerRegistry.cs
수정일 : 2026-11-10

# 설명
PresentationLayerRegistry의 stable LayerID 조회, usage lifetime과 종료 경계를 검증한다.
Ordering과 authoring asset identity가 Registry 책임에서 제거됐는지도 계약으로 고정한다.

# 테스트 구성
 A: Layer Driver 테스트 지원
 B: 테스트 Root 수명
 C: Layer ID 등록과 반환
 D: Layer ID 중복 거부
 E: Registration Handle의 Usage 보호
 F: Registry 종료의 Usage 보호
 G: Registry 선종료
 H: 등록 중 재진입
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

    #region A: Layer Driver 테스트 지원

        private sealed class TestLayerDriver : IPresentationLayerDriver<Transform>
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer가 관리하는 배치 Root를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public Transform Root { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer의 현재 활성 상태를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsActive { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer의 비활성화 횟수를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public int DeactivateCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 활성 상태 변경 시 호출할 테스트 콜백을 지정한다.
            /// </summary>
            // ------------------------------------------------------------
            public Action<bool> ActiveChanged { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 활성 변경 콜백 후 실제 상태를 적용할지 설정한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool ApplyAfterCallback { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Layer 유효성 확인 시 실행할 테스트 콜백을 지정한다.
            /// </summary>
            // ------------------------------------------------------------
            public Action Validating { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 Transform을 배치 Root로 사용하는 테스트 Layer를 만든다.
            /// </summary>
            // ------------------------------------------------------------
            public TestLayerDriver(Transform root)
            {
                Root = root;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer의 Root 유효성을 검사한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool Validate(out string error)
            {
                Validating?.Invoke();
                error = Root == null ? "invalid" : "";
                return string.IsNullOrEmpty(error);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer의 논리 활성 상태와 콜백 순서를 적용한다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetActive(bool active)
            {
                if (!ApplyAfterCallback)
                {
                    IsActive = active;
                }

                if (!active)
                {
                    DeactivateCount++;
                }

                ActiveChanged?.Invoke(active);

                if (ApplyAfterCallback)
                {
                    IsActive = active;
                }
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

    #region B: 테스트 Root 수명

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 테스트 객체의 수명을 종료한다.
        /// </summary>
        // ------------------------------------------------------------
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

    #region C: Layer ID 등록과 반환

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Layer ID 등록이 조회 가능 상태와 Driver 활성화를 만들고 Handle 해제가 둘 다
        /// <br/> 정리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
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

    #region D: Layer ID 중복 거부

        // ------------------------------------------------------------
        /// <summary>
        /// 이미 등록된 Layer ID의 중복 등록을 예외로 거부한다.
        /// </summary>
        // ------------------------------------------------------------
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

    #region E: Registration Handle의 Usage 보호

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 활성 Usage가 있는 동안 등록 Handle 해제를 거부하고 Usage 반환 뒤 해제를 허용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
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

    #region F: Registry 종료의 Usage 보호

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 Usage가 있으면 Registry 종료를 거부하고 등록 상태를 유지한다.
        /// </summary>
        // ------------------------------------------------------------
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

    #region G: Registry 선종료

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Registry 선종료가 Backend를 한 번 비활성화하고 이후 Handle 해제가 중복 정리하지
        /// <br/> 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
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

    #region H: 등록 중 재진입

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Driver 활성화 콜백에서 Registry가 종료되면 등록을 공개하지 않고 활성화 상태를
        /// <br/> 되돌린다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [TestCase(false)]
        [TestCase(true)]
        public void TEST_PresentationLayerRegistry_활성화중RegistryDispose_등록공개안함(bool applyAfterCallback)
        {
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(CreateRoot("Interrupted"))
            {
                ApplyAfterCallback = applyAfterCallback,
            };
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
            Assert.IsFalse(driver.IsActive);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 검증 중 Registry가 종료되면 활성화를 시작하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_PresentationLayerRegistry_검증중종료_활성화시작안함()
        {
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(CreateRoot("Validation Interrupted"));
            var activationCount = 0;
            driver.Validating = registry.Dispose;
            driver.ActiveChanged = _ => activationCount++;

            Assert.Throws<ObjectDisposedException>(() => registry.Register("Layer", driver));

            Assert.IsTrue(registry.IsDisposed);
            Assert.IsFalse(driver.IsActive);
            Assert.AreEqual(0, activationCount);
        }

    #endregion

    }
}
