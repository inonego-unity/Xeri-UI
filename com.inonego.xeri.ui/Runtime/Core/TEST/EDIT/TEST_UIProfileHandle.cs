/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UIProfileHandle.cs
수정일 : 2026-09-20

# 설명
UIProfileHandle의 활성 Layer 소비자 보호와 Provider 반환의 attempt-once 계약을 검증한다.

# 테스트 구성
 C: 활성 Layer 소비자 보호
 R: Provider 반환 실패의 Terminal 처리
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

    // ======================================================================
    /// <summary>
    /// UIProfileHandle의 대칭 소유권과 Provider 반환 실패 Terminal 처리 테스트.
    /// </summary>
    // ======================================================================
    public sealed class TEST_UIProfileHandle
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트 Transform을 제공하는 Layer backend.
        /// </summary>
        // ============================================================
        private sealed class TestLayerDriver : IPresentationLayerDriver<Transform>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Layer View 부모 Root.
            /// </summary>
            // ------------------------------------------------------------
            public Transform Root { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer backend를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestLayerDriver(Transform root) : base()
            {
                Root = root;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Layer Asset과 Root 존재 여부를 검증한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool Validate
            (
                PresentationLayerAsset asset,
                out string error
            )
            {
                error = "";
                return asset != null && Root != null;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer 순서는 별도로 기록하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetOrder(int order)
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Layer Root 활성 상태를 적용한다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetActive(bool active)
            {
                Root.gameObject.SetActive(active);
            }
        }

        // ============================================================
        /// <summary>
        /// GameObject별 Release 호출과 첫 실패를 기록하는 Provider.
        /// </summary>
        // ============================================================
        private sealed class TestProvider : IGameObjectProvider
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Provider 기본 부모.
            /// </summary>
            // ------------------------------------------------------------
            public Transform Parent { get; set; }

            private readonly Dictionary<GameObject, int> releaseCalls =
                new Dictionary<GameObject, int>();
            private readonly HashSet<GameObject> failNextRelease = new HashSet<GameObject>();

            // --------------------------------------------------------------------------------
            /// <summary>
            /// 이 테스트에서는 직접 생성된 GameObject를 Handle에 전달하므로 획득을 지원하지 않는다.
            /// </summary>
            // --------------------------------------------------------------------------------
            public GameObject Acquire(bool worldPositionStays = true)
            {
                throw new NotSupportedException();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 이 테스트에서는 비동기 획득을 지원하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public Awaitable<GameObject> AcquireAsync(bool worldPositionStays = true)
            {
                throw new NotSupportedException();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// GameObject 반환 호출을 기록하고 지정된 첫 호출만 실패시킨다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release
            (
                GameObject gameObject,
                bool worldPositionStays = true
            )
            {
                releaseCalls.TryGetValue(gameObject, out var count);
                releaseCalls[gameObject] = count + 1;

                if (failNextRelease.Remove(gameObject))
                {
                    throw new InvalidOperationException("injected provider release failure");
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 GameObject의 다음 Release를 한 번 실패시킨다.
            /// </summary>
            // ------------------------------------------------------------
            public void FailNext(GameObject gameObject)
            {
                failNextRelease.Add(gameObject);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 GameObject의 누적 Release 호출 횟수를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public int GetReleaseCount(GameObject gameObject)
            {
                return releaseCalls.TryGetValue(gameObject, out var count) ? count : 0;
            }
        }

        private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트용 Presentation Layer Asset을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private PresentationLayerAsset CreateLayerAsset
        (
            string id,
            int order = 0
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

            Assert.IsNotNull(field);
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

    #region C-1: 활성 Layer 소비자

        // ----------------------------------------------------------------------
        /// <summary>
        /// 활성 Layer 소비자가 남은 Profile Dispose가 변경 전에 실패하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIProfileHandle_활성Layer소비자_상태변경전Dispose거부()
        {
            var parent = new GameObject("Layer Parent");
            var root = new GameObject("Layer Root");
            var instance = new GameObject("Layer Instance");
            root.transform.SetParent(parent.transform, false);
            ownedObjects.Add(parent);
            ownedObjects.Add(root);
            ownedObjects.Add(instance);
            var profile = ScriptableObject.CreateInstance<UIProfileAsset>();
            ownedObjects.Add(profile);
            var provider = new TestProvider();
            var registry = new PresentationLayerRegistry();
            var layerHandle = registry.Register
            (
                CreateLayerAsset("Profile"),
                new TestLayerDriver(root.transform)
            );
            Assert.IsTrue(registry.TryAcquireUsage("Profile", out _, out var usage));
            var handle = new UIProfileHandle
            (
                profile,
                null
            );
            var ownedLayer = handle.AddLayer(provider, instance);
            handle.AttachLayerHandle(ownedLayer, layerHandle);

            Assert.Throws<InvalidOperationException>(handle.Dispose);
            Assert.IsFalse(handle.IsDisposed);
            Assert.IsTrue(registry.Contains("Profile"));
            Assert.AreEqual(0, provider.GetReleaseCount(instance));

            usage.Dispose();
            handle.Dispose();

            Assert.IsTrue(handle.IsDisposed);
            Assert.IsFalse(registry.Contains("Profile"));
            Assert.AreEqual(1, provider.GetReleaseCount(instance));
            registry.Dispose();
        }

    #endregion

    #region R-1: Provider 반환 실패

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Provider 반환 일부 실패 뒤 논리 Handle은 Terminal이고,
        /// <br/> 후속 Dispose가 성공하거나 실패한 Provider 반환을 반복하지 않는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIProfileHandle_Provider부분반환실패_모든Instance반환AttemptOnce()
        {
            var profile = ScriptableObject.CreateInstance<UIProfileAsset>();
            var first = new GameObject("First Layer");
            var second = new GameObject("Second Layer");
            ownedObjects.Add(profile);
            ownedObjects.Add(first);
            ownedObjects.Add(second);
            var provider = new TestProvider();
            provider.FailNext(second);
            var disposedCount = 0;
            var handle = new UIProfileHandle
            (
                profile,
                _ => disposedCount++
            );
            handle.AddLayer(provider, first);
            handle.AddLayer(provider, second);

            Assert.Throws<AggregateException>(handle.Dispose);

            Assert.IsTrue(handle.IsDisposed);
            Assert.AreEqual(1, provider.GetReleaseCount(first));
            Assert.AreEqual(1, provider.GetReleaseCount(second));
            Assert.AreEqual(1, disposedCount);

            Assert.DoesNotThrow(handle.Dispose);

            Assert.IsTrue(handle.IsDisposed);
            Assert.AreEqual(1, provider.GetReleaseCount(first));
            Assert.AreEqual(1, provider.GetReleaseCount(second));
            Assert.AreEqual(1, disposedCount);
        }

    #endregion

    }
}
