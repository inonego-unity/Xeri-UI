/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_PresentationSurface.cs
수정일 : 2026-09-29

# 설명
PresentationSurface가 embedded/shared-output containment와 Layer lifetime만 소유하는지 검증한다.
Ordering policy는 Resolver/Plan에 남고 Surface는 resolved Plan 순서를 hierarchy에 반영한다.

# 테스트 구성
 O: resolved Plan order 반영
 N: nested Surface hierarchy 격리
 U: UGUI embedded hierarchy
 T: UITK Layer validation
 L: Surface 소유 Layer 종료 순서
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

    #region 헬퍼

        private sealed class TestLayerDriver : IPresentationLayerDriver<object>
        {
            public object Root { get; }
            public bool IsActive { get; private set; }

            public TestLayerDriver(object root)
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
            }
        }

        private sealed class TestPresentationSurface : PresentationSurface
        {
            public IReadOnlyList<IPresentationLayerDriver> OrderedDrivers => orderedDrivers;

            private readonly List<IPresentationLayerDriver> orderedDrivers = new();

            public TestPresentationSurface()
            {
                // NONE
            }

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

    #region O-1: resolved Plan order

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

    #region N-1: UITK nested hierarchy isolation

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

    #region U-1: UGUI embedded hierarchy

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

    #region T-1: UITK Layer validation

        [Test]
        public void TEST_UITKPresentationLayer_Panel미연결Root_Validation성공()
        {
            var root = new VisualElement();
            var layer = new UITKPresentationLayer(root);

            Assert.IsTrue(layer.Validate(out var error), error);
            Assert.IsNull(root.panel);
        }

    #endregion

    #region L-1: Surface lifetime

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
