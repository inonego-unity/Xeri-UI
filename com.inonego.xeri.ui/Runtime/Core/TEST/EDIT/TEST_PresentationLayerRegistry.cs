/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_PresentationLayerRegistry.cs
수정일 : 2026-09-20

# 설명
동적 Profile 경계에서 Presentation Layer backend의 순서와 충돌 계약을 검증한다.

# 테스트 구성
 O: backend Layer Order 적용
 L: 등록 활성화와 Registry 종료 경계
 X: Order 범위·충돌 거부
 U: Layer 소비자 독립 수명
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// PresentationLayerRegistry의 동적 Layer 순서 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_PresentationLayerRegistry
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트 Transform을 사용하는 Presentation Layer backend.
        /// </summary>
        // ============================================================
        private sealed class TestLayerDriver : IPresentationLayerDriver<Transform>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer Root.
            /// </summary>
            // ------------------------------------------------------------
            public Transform Root => root;

            private readonly Transform root = null;

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 backend의 활성 상태.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsActive { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// backend에 마지막으로 적용된 Layer 순서.
            /// </summary>
            // ------------------------------------------------------------
            public int Order { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// backend 비활성화 요청을 받은 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int DeactivateCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// backend 활성 상태를 기록한 뒤 호출할 테스트 callback.
            /// </summary>
            // ------------------------------------------------------------
            public Action<bool> ActiveChanged { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 Root를 사용하는 테스트 backend를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestLayerDriver(Transform root) : base()
            {
                this.root = root;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Root와 Asset이 존재하는지 검증한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool Validate
            (
                PresentationLayerAsset asset,
                out string error
            )
            {
                error = asset == null || root == null ? "invalid" : "";
                return string.IsNullOrEmpty(error);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 backend의 Layer 순서를 기록한다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetOrder(int order)
            {
                Order = order;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 backend의 활성 상태를 기록한다.
            /// </summary>
            // ------------------------------------------------------------
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

        private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

        // ------------------------------------------------------------
        /// <summary>
        /// private 직렬화 값을 지정한 테스트 Layer Asset을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private PresentationLayerAsset CreateAsset
        (
            string id,
            int order
        )
        {
            var asset = ScriptableObject.CreateInstance<PresentationLayerAsset>();
            SetField(asset, "id", id);
            SetField(asset, "order", order);
            ownedObjects.Add(asset);
            return asset;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 Layer Root를 공통 부모 아래에 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private Transform CreateRoot
        (
            string name,
            Transform parent
        )
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            ownedObjects.Add(gameObject);
            return gameObject.transform;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 Asset의 private 직렬화 필드를 설정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SetField
        (
            object target,
            string name,
            object value
        )
        {
            var field = target.GetType().GetField
            (
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.IsNotNull(field, $"테스트 설정 필드 '{name}'을 찾지 못했습니다.");
            field.SetValue(target, value);
        }

    #endregion

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트에서 만든 Unity Object를 역순 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void TearDown()
        {
            for (var i = ownedObjects.Count - 1; i >= 0; i--)
            {
                if (ownedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }

            ownedObjects.Clear();
        }

    #endregion

    #region O-1: backend Layer 순서

        // ----------------------------------------------------------------------
        /// <summary>
        /// 각 Layer 등록 시 Asset Order가 해당 backend에 적용되는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationLayerRegistry_동적등록_backend별Order적용()
        {
            var parentObject = new GameObject("Layer Parent");
            ownedObjects.Add(parentObject);
            var highRoot = CreateRoot("High", parentObject.transform);
            var lowRoot = CreateRoot("Low", parentObject.transform);
            var registry = new PresentationLayerRegistry();
            var highDriver = new TestLayerDriver(highRoot);
            var lowDriver = new TestLayerDriver(lowRoot);
            var highHandle = registry.Register(CreateAsset("High", 20), highDriver);
            var lowHandle = registry.Register(CreateAsset("Low", 10), lowDriver);

            Assert.AreEqual(20, highDriver.Order);
            Assert.AreEqual(10, lowDriver.Order);
            Assert.IsTrue(highDriver.IsActive);
            Assert.IsTrue(lowDriver.IsActive);

            lowHandle.Dispose();

            Assert.IsFalse(lowDriver.IsActive);
            Assert.IsTrue(highDriver.IsActive);

            highHandle.Dispose();
            registry.Dispose();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> UGUI Layer 등록이 Prefab Root와 자식 표시 Root를 활성화하고,
        /// <br/> 독립 Canvas Order를 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UGUILayerCanvas_Register_독립CanvasOrder적용()
        {
            var parentObject = new GameObject
            (
                "Parent Canvas",
                typeof(RectTransform),
                typeof(Canvas)
            );
            var layerObject = new GameObject
            (
                "UGUI Layer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUILayerCanvas)
            );
            var contentObject = new GameObject("Content Root", typeof(RectTransform));
            layerObject.transform.SetParent(parentObject.transform, false);
            contentObject.transform.SetParent(layerObject.transform, false);
            layerObject.SetActive(false);
            ownedObjects.Add(parentObject);
            var canvas = layerObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var driver = layerObject.GetComponent<UGUILayerCanvas>();
            SetField(driver, "root", contentObject.GetComponent<RectTransform>());
            SetField(driver, "canvas", canvas);
            var registry = new PresentationLayerRegistry();

            var handle = registry.Register(CreateAsset("UGUI", 31), driver);

            Assert.IsTrue(canvas.overrideSorting);
            Assert.AreEqual(31, canvas.sortingOrder);
            Assert.IsTrue(layerObject.activeSelf, "Layer Prefab Root가 활성화돼야 합니다.");
            Assert.IsTrue(contentObject.activeInHierarchy, "자식 표시 Root가 Hierarchy에서 활성화돼야 합니다.");

            handle.Dispose();
            registry.Dispose();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 공통 Screen Overlay Order와 비교할 수 없는 World Space Canvas 등록을 거부한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UGUILayerCanvas_Register_WorldSpaceCanvas거부()
        {
            var layerObject = new GameObject
            (
                "UGUI World Space Layer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUILayerCanvas)
            );
            ownedObjects.Add(layerObject);
            var canvas = layerObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var driver = layerObject.GetComponent<UGUILayerCanvas>();
            SetField(driver, "root", layerObject.GetComponent<RectTransform>());
            SetField(driver, "canvas", canvas);
            var registry = new PresentationLayerRegistry();

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => registry.Register(CreateAsset("UGUI World Space", 31), driver)
            );

            StringAssert.Contains("Screen Space - Overlay", exception.Message);
            registry.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Canvas Order가 적용되지 않는 외부 Root 등록을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UGUILayerCanvas_Register_Canvas외부Root거부()
        {
            var parentObject = new GameObject("Layer Parent", typeof(RectTransform));
            var layerObject = new GameObject
            (
                "UGUI Layer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUILayerCanvas)
            );
            var externalRootObject = new GameObject
            (
                "External Root",
                typeof(RectTransform)
            );
            layerObject.transform.SetParent(parentObject.transform, false);
            externalRootObject.transform.SetParent(parentObject.transform, false);
            ownedObjects.Add(parentObject);
            var canvas = layerObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var driver = layerObject.GetComponent<UGUILayerCanvas>();
            SetField(driver, "root", externalRootObject.GetComponent<RectTransform>());
            SetField(driver, "canvas", canvas);
            var registry = new PresentationLayerRegistry();

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => registry.Register(CreateAsset("UGUI External Root", 31), driver)
            );

            StringAssert.Contains("Layer Canvas 자신이거나 하위", exception.Message);
            Assert.IsFalse(registry.Contains("UGUI External Root"));
            Assert.IsFalse(canvas.overrideSorting);
            registry.Dispose();
        }

    #endregion

    #region L-1: 활성화 중 Registry 종료

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> backend 활성화 callback에서 Registry가 종료되면 등록을 공개하지 않고,
        /// <br/> 이번 backend를 다시 비활성화하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationLayerRegistry_활성화중Dispose_등록거부하고Backend비활성화()
        {
            var parentObject = new GameObject("Layer Parent");
            ownedObjects.Add(parentObject);
            var root = CreateRoot("Interrupted", parentObject.transform);
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(root);
            driver.ActiveChanged = active =>
            {
                if (active)
                {
                    registry.Dispose();
                }
            };

            Assert.Throws<ObjectDisposedException>
            (
                () => registry.Register(CreateAsset("Interrupted", 0), driver)
            );

            Assert.IsTrue(registry.IsDisposed);
            Assert.IsFalse(driver.IsActive);
        }

    #endregion

    #region L-2: 비활성화 중 동일 Layer 등록

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> backend 비활성화 callback에서 같은 ID를 다시 등록하지 못하게 하고,
        /// <br/> 기존 등록만 Terminal 상태로 종료한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationLayerRegistry_비활성화중동일ID등록_기존등록만종료()
        {
            var parentObject = new GameObject("Layer Parent");
            ownedObjects.Add(parentObject);
            var root = CreateRoot("Shared", parentObject.transform);
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(root);
            var asset = CreateAsset("Shared", 0);
            Exception nestedException = null;
            var handle = registry.Register(asset, driver);
            driver.ActiveChanged = active =>
            {
                if (active) return;

                nestedException = Assert.Throws<InvalidOperationException>
                (
                    () => registry.Register(asset, driver)
                );
            };

            handle.Dispose();

            Assert.IsNotNull(nestedException);
            Assert.IsTrue(handle.IsDisposed);
            Assert.IsFalse(registry.Contains("Shared"));
            Assert.IsFalse(driver.IsActive);
            registry.Dispose();
        }

    #endregion

    #region L-3: 비활성화 중 Registry 종료

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Layer 비활성화 callback에서 Registry가 종료돼도,
        /// <br/> 같은 backend의 비활성화를 한 번만 요청하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_PresentationLayerRegistry_비활성화중Dispose_backend한번종료()
        {
            var parentObject = new GameObject("Layer Parent");
            ownedObjects.Add(parentObject);
            var root = CreateRoot("Shared", parentObject.transform);
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(root);
            var handle = registry.Register(CreateAsset("Shared", 0), driver);
            driver.ActiveChanged = active =>
            {
                if (!active)
                {
                    registry.Dispose();
                }
            };

            handle.Dispose();

            Assert.IsTrue(registry.IsDisposed);
            Assert.IsTrue(handle.IsDisposed);
            Assert.IsFalse(driver.IsActive);
            Assert.AreEqual(1, driver.DeactivateCount);
        }

    #endregion

    #region U-1: Layer 소비자 수명

        // ----------------------------------------------------------------------
        /// <summary>
        /// Layer 소비자 하나를 해제해도 다른 소비자와 등록이 유지되는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationLayerHandle_소비자하나해제_다른소비자와등록유지()
        {
            var parentObject = new GameObject("Layer Parent");
            ownedObjects.Add(parentObject);
            var root = CreateRoot("Shared", parentObject.transform);
            var registry = new PresentationLayerRegistry();
            var handle = registry.Register(CreateAsset("Shared", 0), new TestLayerDriver(root));
            Assert.IsTrue(registry.TryAcquireUsage("Shared", out _, out var first));
            Assert.IsTrue(registry.TryAcquireUsage("Shared", out _, out var second));

            first.Dispose();

            Assert.IsTrue(handle.HasConsumers);
            Assert.IsTrue(registry.Contains("Shared"));
            Assert.Throws<InvalidOperationException>(handle.Dispose);

            second.Dispose();
            handle.Dispose();

            Assert.IsFalse(handle.HasConsumers);
            Assert.IsTrue(handle.IsDisposed);
            Assert.IsFalse(registry.Contains("Shared"));
            registry.Dispose();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Registry 전체 종료가 남은 등록 Handle도 Terminal로 만들고,
        /// <br/> 소비자 재획득을 막는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationLayerRegistry_전체종료_남은HandleTerminal()
        {
            var parentObject = new GameObject("Layer Parent");
            ownedObjects.Add(parentObject);
            var root = CreateRoot("Shared", parentObject.transform);
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(root);
            var handle = registry.Register(CreateAsset("Shared", 0), driver);

            registry.Dispose();

            Assert.IsTrue(handle.IsDisposed);
            Assert.IsFalse(handle.HasConsumers);
            Assert.IsFalse(driver.IsActive);
            Assert.Throws<ObjectDisposedException>
            (
                () => registry.TryAcquireUsage("Shared", out _, out _)
            );
            Assert.DoesNotThrow(handle.Dispose);
        }

    #endregion

    #region X-1: Order 범위

        // ----------------------------------------------------------------------
        /// <summary>
        /// UGUI와 UITK가 공유할 수 없는 Order를 등록 전에 거부하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [TestCase(-32769)]
        [TestCase(32768)]
        public void TEST_PresentationLayerRegistry_공통범위밖Order_등록전거부(int order)
        {
            var parentObject = new GameObject("Layer Parent");
            ownedObjects.Add(parentObject);
            var root = CreateRoot("Invalid Order", parentObject.transform);
            var registry = new PresentationLayerRegistry();
            var driver = new TestLayerDriver(root);

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => registry.Register(CreateAsset("Invalid Order", order), driver)
            );

            StringAssert.Contains("공통 허용 범위", exception.Message);
            Assert.IsFalse(registry.Contains("Invalid Order"));
            Assert.IsFalse(driver.IsActive);
            registry.Dispose();
        }

    #endregion

    #region X-2: Order 충돌

        // ------------------------------------------------------------
        /// <summary>
        /// 다른 ID의 공유 Layer가 같은 Order를 등록하지 못하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_PresentationLayerRegistry_동일Order_등록전거부()
        {
            var parentObject = new GameObject("Layer Parent");
            ownedObjects.Add(parentObject);
            var firstRoot = CreateRoot("First", parentObject.transform);
            var secondRoot = CreateRoot("Second", parentObject.transform);
            var registry = new PresentationLayerRegistry();
            var firstHandle = registry.Register(CreateAsset("First", 10), new TestLayerDriver(firstRoot));

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => registry.Register(CreateAsset("Second", 10), new TestLayerDriver(secondRoot))
            );

            StringAssert.Contains("Order(10)", exception.Message);
            Assert.AreEqual(0, firstRoot.GetSiblingIndex());

            firstHandle.Dispose();
            registry.Dispose();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> backend 활성화 callback에서 같은 Order를 중첩 등록하지 못하게 하고,
        /// <br/> 활성화 중인 바깥 등록 하나만 공개하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationLayerRegistry_활성화재진입Order충돌_바깥등록만유지()
        {
            var parentObject = new GameObject("Layer Parent");
            ownedObjects.Add(parentObject);
            var outerRoot = CreateRoot("Outer", parentObject.transform);
            var nestedRoot = CreateRoot("Nested", parentObject.transform);
            var registry = new PresentationLayerRegistry();
            var outerDriver = new TestLayerDriver(outerRoot);
            var nestedDriver = new TestLayerDriver(nestedRoot);
            Exception nestedException = null;
            outerDriver.ActiveChanged = active =>
            {
                if (active)
                {
                    nestedException = Assert.Throws<InvalidOperationException>
                    (
                        () => registry.Register
                        (
                            CreateAsset("Nested", 10),
                            nestedDriver
                        )
                    );
                }
            };

            var outerHandle = registry.Register(CreateAsset("Outer", 10), outerDriver);

            Assert.IsNotNull(nestedException);
            StringAssert.Contains("Order(10)", nestedException.Message);
            Assert.IsTrue(registry.Contains("Outer"));
            Assert.IsFalse(registry.Contains("Nested"));
            Assert.IsTrue(outerDriver.IsActive);
            Assert.IsFalse(nestedDriver.IsActive);

            outerHandle.Dispose();
            registry.Dispose();
        }

    #endregion

    }
}
