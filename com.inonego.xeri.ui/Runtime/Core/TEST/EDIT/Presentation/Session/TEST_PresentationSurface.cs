/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_PresentationSurface.cs
수정일 : 2026-11-10

# 설명
PresentationSurface가 embedded/shared-output containment와 Layer lifetime만 소유하는지 검증한다.
Ordering policy는 Resolver/Plan에 남고 Surface는 resolved Plan 순서를 hierarchy에 반영한다.

# 테스트 구성
 A: Surface 테스트 지원
 B: Plan 적용 순서
 C: UITK 중첩 계층 격리
 D: UGUI 내장 계층
 E: UITK Layer 검증
 F: Layer 실패 롤백과 역순 반환
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// PresentationSurface의 containment와 lifetime 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_PresentationSurface
    {

    #region A: Surface 테스트 지원

        private sealed class TestLayerDriver : IPresentationLayerDriver<object>
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer의 배치 Root를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public object Root { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer의 논리 활성 상태를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsActive { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 Root를 사용하는 테스트 Layer를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestLayerDriver(object root)
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
                error = Root == null ? "invalid" : "";
                return string.IsNullOrEmpty(error);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer의 논리 활성 상태를 적용한다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetActive(bool active)
            {
                IsActive = active;
            }
        }

        private sealed class TestPresentationSurface : PresentationSurface
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 현재 Surface에 적용된 Layer 순서를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public IReadOnlyList<IPresentationLayerDriver> OrderedDrivers => orderedDrivers;

            private readonly List<IPresentationLayerDriver> orderedDrivers = new();

            // ------------------------------------------------------------
            /// <summary>
            /// Layer 적용 중 주입할 실패 횟수를 지정한다.
            /// </summary>
            // ------------------------------------------------------------
            public int ApplyFailureCount { get; set; } = 0;

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트용 Presentation Surface를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestPresentationSurface()
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Resolved Plan Layer와 Driver를 테스트 Surface에 추가한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Add
            (
                PresentationPlanLayer plan,
                IPresentationLayerDriver driver,
                Action release = null
            )
            {
                AddLayer(plan, driver, release ?? (() => { }));
            }

            protected override void ApplyLayerOrder(IReadOnlyList<IPresentationLayerDriver> orderedDrivers)
            {
                this.orderedDrivers.Clear();
                this.orderedDrivers.AddRange(orderedDrivers);

                if (ApplyFailureCount <= 0) return;

                ApplyFailureCount--;
                throw new InvalidOperationException("injected surface order failure");
            }

            protected override void ReleaseSurfaceRoot()
            {
                // NONE
            }
        }

        private readonly List<UnityEngine.Object> ownedObjects = new();

        private static PresentationPlanLayer Layer
        (
            string id,
            int layerOrder,
            PresentationBackend backend = PresentationBackend.UITK
        )
        {
            return PresentationTestScope.CreateLayer(id, layerOrder, backend);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 Surface와 관련 객체를 정리한다.
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

    #region B: Plan 적용 순서

        // ------------------------------------------------------------
        /// <summary>
        /// Surface가 추가된 Layer Driver를 전달 순서대로 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_PresentationSurface_전달된Plan순서_그대로반영()
        {
            var surface = new TestPresentationSurface();
            var low = new TestLayerDriver(new object());
            var high = new TestLayerDriver(new object());

            surface.Add(Layer("Low", 0), low);
            surface.Add(Layer("High", 200), high);

            Assert.AreSame(low, surface.OrderedDrivers[0]);
            Assert.AreSame(high, surface.OrderedDrivers[1]);

            surface.Dispose();
        }

    #endregion

    #region C: UITK 중첩 계층 격리

        // ----------------------------------------------------------------------
        /// <summary>
        /// UITK Surface가 Child Layer를 만들 때 부모의 sibling 순서를 바꾸지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationSurface_ChildPlanOrder_ParentSibling순서침범안함()
        {
            var desktop = new VisualElement();
            var windowA = new VisualElement
            {
                name = "Window A",
            };
            var windowB = new VisualElement
            {
                name = "Window B",
            };
            desktop.Add(windowA);
            desktop.Add(windowB);

            var surface = new UITKPresentationSurface
            (
                windowA,
                new[]
                {
                    Layer("Screen", 0),
                    Layer("Modal", 200),
                }
            );

            Assert.AreSame(windowA, desktop[0]);
            Assert.AreSame(windowB, desktop[1]);
            Assert.AreSame(windowA, surface.Root.parent);
            Assert.AreEqual("xeri-layer-Screen", surface.Root[0].name);
            Assert.AreEqual("xeri-layer-Modal", surface.Root[1].name);

            surface.Dispose();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Window-local Layer를 부모 Workspace의 전역 Layer 트리와 분리해 배치한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationSurface_ParentGlobalLayer_WindowLocalLayer와격리()
        {
            var workspaceRoot = new VisualElement();
            var workspaceSurface = new UITKPresentationSurface
            (
                workspaceRoot,
                new[]
                {
                    Layer("WindowHost", 0),
                    Layer("GlobalModal", 100),
                }
            );
            Assert.IsTrue(workspaceSurface.TryGetLayer("WindowHost", out var windowHostLayer));
            Assert.IsTrue(workspaceSurface.TryGetLayer("GlobalModal", out var globalModalLayer));
            var windowHostRoot = ((IPresentationLayerDriver<VisualElement>)windowHostLayer).Root;
            var globalModalRoot = ((IPresentationLayerDriver<VisualElement>)globalModalLayer).Root;
            var windowContent = new VisualElement
            {
                name = "Window Content",
            };
            windowHostRoot.Add(windowContent);

            var windowSurface = new UITKPresentationSurface
            (
                windowContent,
                new[]
                {
                    Layer("WindowScreen", 0),
                    Layer("WindowModal", 10000),
                }
            );

            Assert.AreSame(windowHostRoot, workspaceSurface.Root[0]);
            Assert.AreSame(globalModalRoot, workspaceSurface.Root[1]);
            Assert.AreSame(windowContent, windowSurface.Root.parent);
            Assert.AreSame(windowHostRoot, windowContent.parent);

            windowSurface.Dispose();
            workspaceSurface.Dispose();
        }

    #endregion

    #region D: UGUI 내장 계층

        // ----------------------------------------------------------------------
        /// <summary>
        /// UGUI Surface가 하나의 Canvas 아래에 Plan 순서대로 Layer Root를 배치한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UGUIPresentationSurface_한Canvas아래Plan순서로Layer배치()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            ownedObjects.Add(canvasObject);
            var surface = new UGUIPresentationSurface
            (
                canvasObject.GetComponent<RectTransform>(),
                new[]
                {
                    Layer("Screen", 0, PresentationBackend.UGUI),
                    Layer("Overlay", 100, PresentationBackend.UGUI),
                    Layer("Modal", 200, PresentationBackend.UGUI),
                }
            );

            Assert.AreEqual("Layer - Screen", surface.Root.GetChild(0).name);
            Assert.AreEqual("Layer - Overlay", surface.Root.GetChild(1).name);
            Assert.AreEqual("Layer - Modal", surface.Root.GetChild(2).name);

            surface.Dispose();
        }

    #endregion

    #region E: UITK Layer 검증

        // ----------------------------------------------------------------------
        /// <summary>
        /// 아직 Panel에 연결되지 않은 UITK Root도 Layer validation을 통과한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationLayer_Panel미연결Root_Validation성공()
        {
            var root = new VisualElement();
            var layer = new UITKPresentationLayer(root);

            Assert.IsTrue(layer.Validate(out var error), error);
            Assert.IsNull(root.panel);
        }

    #endregion

    #region F: Layer 실패 롤백과 역순 반환

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Layer ordering 적용과 신규 Layer 반환이 함께 실패해도 기존 Layer ordering을
        /// <br/> 다시 적용하고 두 실패를 함께 보존한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_PresentationSurface_AddOrder실패_Release와기존OrderRollback끝까지()
        {
            var surface = new TestPresentationSurface();
            var first = new TestLayerDriver(new object());
            var second = new TestLayerDriver(new object());
            var releaseCount = 0;
            surface.Add(Layer("First", 0), first);
            surface.ApplyFailureCount = 1;

            var exception = Assert.Throws<AggregateException>
            (
                () => surface.Add
                (
                    Layer("Second", 100),
                    second,
                    () =>
                    {
                        releaseCount++;
                        throw new InvalidOperationException("injected surface release failure");
                    }
                )
            );

            Assert.AreEqual(2, exception.InnerExceptions.Count);
            StringAssert.Contains
            (
                "injected surface order failure",
                exception.InnerExceptions[0].Message
            );
            StringAssert.Contains
            (
                "injected surface release failure",
                exception.InnerExceptions[1].Message
            );
            Assert.AreEqual(1, releaseCount);
            Assert.AreEqual(1, surface.OrderedDrivers.Count);
            Assert.AreSame(first, surface.OrderedDrivers[0]);

            Assert.DoesNotThrow(surface.Dispose);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Surface 종료 시 Layer 자원을 생성 역순으로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_PresentationSurface_Dispose_Layer생성역순반환()
        {
            var surface = new TestPresentationSurface();
            var released = new List<string>();

            surface.Add
            (
                Layer("First", 0),
                new TestLayerDriver(new object()),
                () => released.Add("First")
            );
            surface.Add
            (
                Layer("Second", 100),
                new TestLayerDriver(new object()),
                () => released.Add("Second")
            );

            surface.Dispose();

            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Second",
                    "First",
                },
                released
            );
        }

    #endregion

    }
}
