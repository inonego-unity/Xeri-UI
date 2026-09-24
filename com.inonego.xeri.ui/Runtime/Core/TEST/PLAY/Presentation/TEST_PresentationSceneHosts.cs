/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_PresentationSceneHosts.cs
수정일 : 2026-09-30

# 설명
PanelRenderer와 VisualElementReference를 사용하는 scene-authored UITK Presentation Host의
borrow, LocalOrder 적용, authored state 복원 계약을 PlayMode에서 검증한다.

# 테스트 구성
 H: UITK Scene-authored LayerHost / PlacementHost
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;
    using inonego.Xeri.UI.TEST;

    // ======================================================================
    /// <summary>
    /// native authoring reference를 사용하는 UITK Scene Host 계약 테스트.
    /// </summary>
    // ======================================================================
    public sealed class TEST_PresentationSceneHosts
    {

    #region 헬퍼 타입

        private const int MANAGED_ROOT_AUTHORING_ID = 120;
        private const int LATE_PLACEMENT_AUTHORING_ID = 130;
        private const int EARLY_PLACEMENT_AUTHORING_ID = 140;
        private const string TEST_TREE_RESOURCE_PATH =
            "Xeri/UI/TEST_PresentationSceneHosts";

        // ============================================================
        /// <summary>
        /// 실제 PanelRenderer와 scene-authored Host 조립 상태.
        /// </summary>
        // ============================================================
        private sealed class HostFixture : IDisposable
        {
            public GameObject Root { get; }
            public GameObject LayerObject { get; }
            public PanelSettings OriginalPanelSettings { get; }
            public PanelRenderer Renderer { get; }
            public UITKPresentationOutput Output { get; }
            public UITKPresentationLayerHost LayerHost { get; }

            public HostFixture()
            {
                Root = new GameObject("Presentation Runtime Root");
                OriginalPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                UITKTestPanelSettings.ApplyDefaultRuntimeTheme(OriginalPanelSettings);
                OriginalPanelSettings.sortingOrder = 37.0f;
                var treeAsset = Resources.Load<VisualTreeAsset>
                (
                    TEST_TREE_RESOURCE_PATH
                );
                Assert.IsNotNull(treeAsset);

                LayerObject = new GameObject("Scene UITK Layer Host");
                LayerObject.SetActive(false);
                LayerObject.transform.SetParent(Root.transform, false);
                Renderer = LayerObject.AddComponent<PanelRenderer>();
                Output = LayerObject.AddComponent<UITKPresentationOutput>();
                LayerHost = LayerObject.AddComponent<UITKPresentationLayerHost>();
                Renderer.panelSettings = OriginalPanelSettings;
                Renderer.visualTreeAsset = treeAsset;
                SetField(LayerHost, "layerID", "Screen");
                LayerHost.ManagedRootReference.SetReference
                (
                    Renderer,
                    new AuthoringIdPath(MANAGED_ROOT_AUTHORING_ID)
                );
                LayerObject.SetActive(true);
            }

            public UITKPresentationPlacementHost CreatePlacementHost
            (
                string presentationID,
                int authoringID
            )
            {
                var hostObject = new GameObject($"{presentationID} Host");
                hostObject.transform.SetParent(Root.transform, false);
                var host = hostObject.AddComponent<UITKPresentationPlacementHost>();
                SetField(host, "presentationID", presentationID);
                host.RootReference.SetReference
                (
                    Renderer,
                    new AuthoringIdPath(authoringID)
                );
                return host;
            }

            public void Dispose()
            {
                UnityEngine.Object.Destroy(Root);
                UnityEngine.Object.Destroy(OriginalPanelSettings);
            }
        }

        // ======================================================================
        /// <summary>
        /// PanelRenderer reload를 따라 현재 authoring reference를 추적한다.
        /// </summary>
        // ======================================================================
        private sealed class ReferenceObserver : IDisposable
        {
            public VisualElement Value { get; private set; }

            private readonly VisualElementReference reference;

            public ReferenceObserver
            (
                PanelRenderer renderer,
                int authoringID
            )
            {
                reference = new VisualElementReference
                (
                    renderer,
                    new AuthoringIdPath(authoringID)
                );
                reference.RegisterReferenceResolvedCallback(HandleResolved);
                reference.RegisterReferenceUnloadedCallback(HandleUnloaded);
            }

            private void HandleResolved(VisualElement element)
            {
                Value = element;
            }

            private void HandleUnloaded(VisualElement element)
            {
                if (ReferenceEquals(Value, element))
                {
                    Value = null;
                }
            }

            public void Dispose()
            {
                reference.UnregisterReferenceResolvedCallback(HandleResolved);
                reference.UnregisterReferenceUnloadedCallback(HandleUnloaded);
                ((IDisposable)reference).Dispose();
                Value = null;
            }
        }

    #endregion

    #region 헬퍼

        // ------------------------------------------------------------
        /// <summary>
        /// private 직렬화 필드를 테스트 composition 값으로 설정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SetField
        (
            object target,
            string name,
            object value
        )
        {
            FieldInfo field = null;

            for
            (
                var type = target.GetType();
                type != null && field == null;
                type = type.BaseType
            )
            {
                field = type.GetField
                (
                    name,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly
                );
            }

            Assert.IsNotNull(field, $"{target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }

        private static PresentationPlanLayer Layer
        (
            string id,
            int order
        )
        {
            return new PresentationPlanLayer(id, id, order, PresentationBackend.UITK);
        }

        private static PresentationPlan CreatePlan
        (
            PresentationPlanLayer layer,
            params (string ID, int LocalOrder)[] placements
        )
        {
            var planPlacements =
                new List<PresentationPlanPlacement>(placements.Length);

            for (var index = 0; index < placements.Length; index++)
            {
                var placement = placements[index];
                planPlacements.Add
                (
                    new PresentationPlanPlacement
                    (
                        placement.ID,
                        layer,
                        placement.LocalOrder
                    )
                );
            }

            return new PresentationPlan
            (
                new List<PresentationPlanLayer>
                {
                    layer,
                },
                planPlacements
            );
        }

        private static IEnumerator WaitForReferences
        (
            params ReferenceObserver[] observers
        )
        {
            for (var frame = 0; frame < 10; frame++)
            {
                var allResolved = true;

                for (var index = 0; index < observers.Length; index++)
                {
                    if (observers[index].Value != null) continue;
                    allResolved = false;
                    break;
                }

                if (allResolved)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("VisualElementReference가 제한 프레임 안에 resolve되지 않았습니다.");
        }

    #endregion

    #region H-1: UITK Scene-authored Host

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> authoring-id reference가 nested ManagedRoot를 실제 PanelRenderer에서
        /// <br/> resolve한다.
        /// <br/> generated Placement도 같은 ManagedRoot 아래에 materialize된다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_PresentationSession_UITKLayerHost_AuthoringReference로ManagedRootBorrow()
        {
            using var fixture = new HostFixture();
            using var managedObserver = new ReferenceObserver
            (
                fixture.Renderer,
                MANAGED_ROOT_AUTHORING_ID
            );
            var layer = Layer("Screen", 100);
            var plan = CreatePlan
            (
                layer,
                ("Screen.Generated", 20)
            );
            var originalTreeAsset = fixture.Renderer.visualTreeAsset;
            PresentationSession session = null;
            Lease usage = null;

            yield return WaitForReferences(managedObserver);

            try
            {
                session = PresentationSession.CreateTopLevel
                (
                    plan,
                    fixture.Root.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        fixture.LayerHost,
                    }
                );
                Assert.IsTrue
                (
                    session.LayerRegistry.TryGet("Screen", out var layerDriver)
                );
                var managedRoot =
                    ((IPresentationLayerDriver<VisualElement>)layerDriver).Root;

                Assert.AreSame(managedObserver.Value, managedRoot);
                Assert.AreEqual("authored-container", managedRoot.parent.name);
                Assert.IsNotNull(fixture.Output.Root.Q("static-content"));
                Assert.AreSame
                (
                    fixture.OriginalPanelSettings,
                    fixture.Renderer.panelSettings
                );
                Assert.AreEqual(100.0f, fixture.Renderer.panelSettings.sortingOrder);

                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "Screen.Generated",
                        out var generatedDriver,
                        out usage
                    )
                );
                var generatedRoot =
                    ((IPresentationLayerDriver<VisualElement>)generatedDriver).Root;
                Assert.AreSame(managedRoot, generatedRoot.parent);
                Assert.AreEqual(20, generatedRoot.style.zIndex.value);

                usage.Dispose();
                usage = null;
                session.Dispose();

                yield return WaitForReferences(managedObserver);

                Assert.IsTrue(fixture.LayerObject.activeSelf);
                Assert.AreSame
                (
                    fixture.OriginalPanelSettings,
                    fixture.Renderer.panelSettings
                );
                Assert.AreEqual(37.0f, fixture.Renderer.panelSettings.sortingOrder);
                Assert.AreSame(originalTreeAsset, fixture.Renderer.visualTreeAsset);
                Assert.AreEqual
                (
                    "authored-container",
                    managedObserver.Value.parent.name
                );
            }

            finally
            {
                usage?.Dispose();

                if (session != null && !session.IsDisposed)
                {
                    session.Dispose();
                }
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> authored/generated UITK Placement가 같은 ManagedRoot에서
        /// <br/> Plan LocalOrder를 따른다.
        /// <br/> Session Dispose 뒤 authored z-index와 reference lifetime을
        /// <br/> 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_PresentationSession_UITKSceneHost_혼합PlacementLocalOrder와복원()
        {
            using var fixture = new HostFixture();
            var lateHost = fixture.CreatePlacementHost
            (
                "Screen.Late",
                LATE_PLACEMENT_AUTHORING_ID
            );
            var earlyHost = fixture.CreatePlacementHost
            (
                "Screen.Early",
                EARLY_PLACEMENT_AUTHORING_ID
            );
            SetField
            (
                fixture.LayerHost,
                "placementHosts",
                new PresentationPlacementHost[]
                {
                    lateHost,
                    earlyHost,
                }
            );
            using var managedObserver = new ReferenceObserver
            (
                fixture.Renderer,
                MANAGED_ROOT_AUTHORING_ID
            );
            using var lateObserver = new ReferenceObserver
            (
                fixture.Renderer,
                LATE_PLACEMENT_AUTHORING_ID
            );
            using var earlyObserver = new ReferenceObserver
            (
                fixture.Renderer,
                EARLY_PLACEMENT_AUTHORING_ID
            );

            var layer = Layer("Screen", 100);
            var plan = CreatePlan
            (
                layer,
                ("Screen.Early", 10),
                ("Screen.Generated", 20),
                ("Screen.Late", 30)
            );
            PresentationSession session = null;
            Lease earlyUsage = null;
            Lease generatedUsage = null;
            Lease lateUsage = null;

            yield return WaitForReferences
            (
                managedObserver,
                earlyObserver,
                lateObserver
            );
            yield return null;

            Assert.AreSame(managedObserver.Value, earlyObserver.Value.parent);
            Assert.AreSame(managedObserver.Value, lateObserver.Value.parent);
            earlyObserver.Value.style.zIndex = -3;
            lateObserver.Value.style.zIndex = 7;
            Assert.AreEqual
            (
                -3,
                earlyObserver.Value.style.zIndex.value,
                "Session activation 전 Early authored z-index"
            );
            Assert.AreEqual
            (
                7,
                lateObserver.Value.style.zIndex.value,
                "Session activation 전 Late authored z-index"
            );

            try
            {
                session = PresentationSession.CreateTopLevel
                (
                    plan,
                    fixture.Root.transform,
                    null,
                    null,
                    layerHosts: new PresentationLayerHost[]
                    {
                        fixture.LayerHost,
                    }
                );
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "Screen.Early",
                        out var earlyDriver,
                        out earlyUsage
                    )
                );
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "Screen.Generated",
                        out var generatedDriver,
                        out generatedUsage
                    )
                );
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "Screen.Late",
                        out var lateDriver,
                        out lateUsage
                    )
                );

                var earlyRoot =
                    ((IPresentationLayerDriver<VisualElement>)earlyDriver).Root;
                var generatedRoot =
                    ((IPresentationLayerDriver<VisualElement>)generatedDriver).Root;
                var lateRoot =
                    ((IPresentationLayerDriver<VisualElement>)lateDriver).Root;

                Assert.AreSame(earlyObserver.Value, earlyRoot);
                Assert.AreSame(lateObserver.Value, lateRoot);
                Assert.AreSame(managedObserver.Value, generatedRoot.parent);
                Assert.AreEqual(10, earlyRoot.style.zIndex.value);
                Assert.AreEqual(20, generatedRoot.style.zIndex.value);
                Assert.AreEqual(30, lateRoot.style.zIndex.value);

                earlyUsage.Dispose();
                earlyUsage = null;
                generatedUsage.Dispose();
                generatedUsage = null;
                lateUsage.Dispose();
                lateUsage = null;
                session.Dispose();

                yield return WaitForReferences
                (
                    managedObserver,
                    earlyObserver,
                    lateObserver
                );

                Assert.AreSame(managedObserver.Value, earlyObserver.Value.parent);
                Assert.AreSame(managedObserver.Value, lateObserver.Value.parent);
                Assert.AreEqual
                (
                    -3,
                    earlyObserver.Value.style.zIndex.value,
                    "Session 종료 후 Early authored z-index 복원"
                );
                Assert.AreEqual
                (
                    7,
                    lateObserver.Value.style.zIndex.value,
                    "Session 종료 후 Late authored z-index 복원"
                );
            }
            finally
            {
                earlyUsage?.Dispose();
                generatedUsage?.Dispose();
                lateUsage?.Dispose();

                if (session != null && !session.IsDisposed)
                {
                    session.Dispose();
                }
            }
        }

    #endregion

    }
}
