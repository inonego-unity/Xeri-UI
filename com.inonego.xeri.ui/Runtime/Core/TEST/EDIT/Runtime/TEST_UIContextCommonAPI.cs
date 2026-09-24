/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UIContextCommonAPI.cs
수정일 : 2026-09-30

# 설명
UIContext Common API가 기존 ScreenRegistry와 PresentationLease primitive를 그대로 위임하는지 검증한다.
고급 Composition API를 제거하지 않고 Golden Path만 축약하는 progressive disclosure 계약을 고정한다.

# 테스트 구성
 C: UIContext Common facade와 advanced primitive 동등성
 M: UITK Modal facade의 기존 ModalController 위임
========================================================================= BLOCK_HEADER_END */

using System;

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
    /// UIContext Common API의 lossless facade 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_UIContextCommonAPI
    {

    #region 헬퍼

        private sealed class TestFocusDriver : IFocusDriver
        {
            public object Current => null;
            public int SelectFailureCount { get; set; }

            public bool IsValid(object target)
            {
                return target != null;
            }

            public void Select(object target)
            {
                if (SelectFailureCount > 0)
                {
                    SelectFailureCount--;
                    throw new InvalidOperationException("Focus 선택 실패");
                }
            }

            public object FindFallback()
            {
                return null;
            }
        }
        private sealed class TestInputDriver : IScreenInputDriver
        {
            public ScreenInputSession Acquire
            (
                ScreenOptions options,
                bool contributionEnabled = true
            )
            {
                throw new NotSupportedException();
            }

            public void BeginBatch()
            {
                // NONE
            }

            public void EndBatch()
            {
                // NONE
            }

            public void ForceReleaseAll()
            {
                // NONE
            }

            public void Dispose()
            {
                // NONE
            }
        }
        private sealed class TestTransitioner : IPresentationTransitioner
        {
            public PresentationTransitionHandle Play
            (
                PresentationTransitionParams parameters,
                Action onCompleted,
                Action<Exception> onFailed
            )
            {
                throw new NotSupportedException();
            }

            public void Dispose()
            {
                // NONE
            }
        }

        private sealed class TestScreenSource : IScreenSource
        {
            public ScreenInstance Acquire(ScreenViewScope scope)
            {
                throw new NotSupportedException();
            }

            public void Release(ScreenInstance instance)
            {
                throw new NotSupportedException();
            }
        }
        private sealed class TestPresentationSource : IPresentationSource<object>
        {
            public int AcquireCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public IPresentationLayerDriver AcquiredLayer { get; private set; }

            public object Acquire(IPresentationLayerDriver layer)
            {
                AcquireCount++;
                AcquiredLayer = layer;
                return new object();
            }

            public void Release(object view)
            {
                ReleaseCount++;
            }
        }

        private sealed class TestLifetime : IDisposable
        {
            public bool IsDisposed { get; private set; }

            private readonly Action disposing = null;

            public TestLifetime(Action disposing = null)
            {
                this.disposing = disposing;
            }

            public void Dispose()
            {
                IsDisposed = true;
                disposing?.Invoke();
            }
        }

        private sealed class TestFocusScope : IFocusScope
        {
            public object DefaultFocus { get; }

            private readonly VisualElement root = null;

            public TestFocusScope(VisualElement root, object defaultFocus)
            {
                this.root = root ?? throw new ArgumentNullException(nameof(root));
                DefaultFocus = defaultFocus;
            }

            public bool ContainsFocus(object target)
            {
                return ReferenceEquals(target, root) || ReferenceEquals(target, DefaultFocus);
            }
        }

        private GameObject runtimeObject = null;
        private GameObject presentationRootObject = null;
        private PanelSettings panelSettings = null;
        private PresentationSession presentation = null;
        private TestFocusDriver focusDriver = null;
        private UIContext context = null;

    #endregion

    #region 픽스처

        [SetUp]
        public void SetUp()
        {
            UIRuntime.Clear();

            var uitkLayer = new PresentationPlanLayer
            (
                "Common",
                "Common",
                0,
                PresentationBackend.UITK
            );
            var uguiLayer = new PresentationPlanLayer
            (
                "Common.UGUI",
                "Common UGUI",
                100,
                PresentationBackend.UGUI
            );
            var plan = new PresentationPlan
            (
                new[]
                {
                    uitkLayer,
                    uguiLayer,
                },
                new[]
                {
                    new PresentationPlanPlacement
                    (
                        "Common.Presentation",
                        uitkLayer,
                        0
                    ),
                    new PresentationPlanPlacement
                    (
                        "Common.Modal",
                        uitkLayer,
                        10
                    ),
                    new PresentationPlanPlacement
                    (
                        "Common.UGUI.Modal",
                        uguiLayer,
                        0
                    ),
                }
            );
            presentationRootObject = new GameObject("Common API Presentation Root");
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            presentation = PresentationSession.CreateTopLevel
            (
                plan,
                presentationRootObject.transform,
                panelSettings,
                null
            );
            runtimeObject = new GameObject("Common API Runtime Owner");
            var runtime = runtimeObject.AddComponent<UIRuntime>();

            focusDriver = new TestFocusDriver();
            context = new UIContext
            (
                runtime,
                null,
                presentation,
                new TestTransitioner(),
                focusDriver,
                new TestInputDriver()
            );
        }

        [TearDown]
        public void TearDown()
        {
            context?.DisposeFromRuntime();
            context = null;
            focusDriver = null;
            presentation?.Dispose();
            presentation = null;

            if (presentationRootObject != null)
            {
                UnityEngine.Object.DestroyImmediate(presentationRootObject);
                presentationRootObject = null;
            }

            if (panelSettings != null)
            {
                UnityEngine.Object.DestroyImmediate(panelSettings);
                panelSettings = null;
            }

            if (runtimeObject != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimeObject);
                runtimeObject = null;
            }

            UIRuntime.Clear();
        }

    #endregion

    #region C-1: Common facade

        [Test]
        public void TEST_UIContext_RegisterScreen_Registry와같은등록수명()
        {
            var options = new ScreenOptions("Common.Screen");
            var source = new TestScreenSource();

            var registration = context.RegisterScreen(options, source);

            Assert.IsTrue(context.ScreenRegistry.Contains(options.ID));

            registration.Dispose();

            Assert.IsFalse(context.ScreenRegistry.Contains(options.ID));
        }
        [Test]
        public void TEST_UIContext_AcquirePresentation_현재SessionLease와같은수명()
        {
            var source = new TestPresentationSource();
            var lease = context.AcquirePresentation
            (
                "Common.Presentation",
                source
            );

            try
            {
                Assert.AreEqual(1, source.AcquireCount);
                Assert.AreEqual(0, source.ReleaseCount);
                Assert.IsNotNull(source.AcquiredLayer);
                Assert.IsNotNull(lease.Value);
            }
            finally
            {
                lease.Dispose();
            }

            Assert.AreEqual(1, source.ReleaseCount);
        }

    #endregion

    #region M-1: UITK Modal facade

        [Test]
        public void TEST_UITKModal_Open_PlacementUsage와HierarchyLifetime소유()
        {
            var root = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            var modal = UITKModal.Open
            (
                context,
                "Common.Modal",
                root
            );

            Assert.AreEqual(1, context.Modals.Count);
            Assert.IsNotNull(root.parent);
            Assert.IsTrue(root.enabledSelf);
            Assert.AreEqual(PickingMode.Position, root.pickingMode);
            Assert.Throws<InvalidOperationException>(presentation.ValidateCanDispose);

            modal.Dispose();

            Assert.AreEqual(0, context.Modals.Count);
            Assert.IsNull(root.parent);
            Assert.IsTrue(root.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            Assert.DoesNotThrow(presentation.ValidateCanDispose);
        }

        [Test]
        public void TEST_UITKModal_Open_FocusScopeDispose후같은Scope재사용가능()
        {
            var root = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            root.SetEnabled(false);
            var defaultFocus = new Button();
            root.Add(defaultFocus);
            var focusScope = new TestFocusScope(root, defaultFocus);
            var first = UITKModal.OpenWithFocus
            (
                context,
                "Common.Modal",
                root,
                focusScope
            );

            Assert.AreEqual(1, context.Modals.Count);
            Assert.IsTrue(root.enabledSelf);
            Assert.AreEqual(PickingMode.Position, root.pickingMode);

            first.Dispose();

            Assert.AreEqual(0, context.Modals.Count);
            Assert.IsNull(root.parent);
            Assert.IsFalse(root.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);

            var second = UITKModal.OpenWithFocus
            (
                context,
                "Common.Modal",
                root,
                focusScope
            );

            Assert.AreEqual(1, context.Modals.Count);
            Assert.IsTrue(root.enabledSelf);
            Assert.AreEqual(PickingMode.Position, root.pickingMode);

            second.Dispose();

            Assert.AreEqual(0, context.Modals.Count);
            Assert.IsNull(root.parent);
            Assert.IsFalse(root.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
        }

        [Test]
        public void TEST_UITKModal_OpenWithFocus_Select실패_전체롤백후재시도가능()
        {
            var root = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            root.SetEnabled(false);
            var defaultFocus = new Button();
            root.Add(defaultFocus);
            var focusScope = new TestFocusScope(root, defaultFocus);
            focusDriver.SelectFailureCount = 1;

            Assert.Throws<InvalidOperationException>
            (
                () => UITKModal.OpenWithFocus
                (
                    context,
                    "Common.Modal",
                    root,
                    focusScope
                )
            );

            Assert.AreEqual(0, context.Modals.Count);
            Assert.IsNull(root.parent);
            Assert.IsFalse(root.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            Assert.DoesNotThrow(presentation.ValidateCanDispose);

            var retry = UITKModal.OpenWithFocus
            (
                context,
                "Common.Modal",
                root,
                focusScope
            );

            Assert.AreEqual(1, context.Modals.Count);
            Assert.IsNotNull(root.parent);

            retry.Dispose();

            Assert.AreEqual(0, context.Modals.Count);
            Assert.IsNull(root.parent);
            Assert.IsFalse(root.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            Assert.DoesNotThrow(presentation.ValidateCanDispose);
        }

        [Test]
        public void TEST_UITKModal_Open_UGUIPlacement이면Usage와Root상태롤백()
        {
            var root = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            root.SetEnabled(false);

            Assert.Throws<InvalidOperationException>
            (
                () => UITKModal.Open
                (
                    context,
                    "Common.UGUI.Modal",
                    root
                )
            );

            Assert.AreEqual(0, context.Modals.Count);
            Assert.IsNull(root.parent);
            Assert.IsFalse(root.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            Assert.DoesNotThrow(presentation.ValidateCanDispose);
        }

        [Test]
        public void TEST_UITKModal_CompositionOpen_전달Lifetime과Interaction상태복원()
        {
            var root = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            root.SetEnabled(false);
            var lifetime = new TestLifetime
            (
                () =>
                {
                    root.SetEnabled(true);
                    root.pickingMode = PickingMode.Position;
                }
            );
            var modal = UITKModal.Open(context, root, lifetime);

            Assert.AreEqual(1, context.Modals.Count);
            Assert.IsNull(root.parent);
            Assert.IsTrue(root.enabledSelf);
            Assert.AreEqual(PickingMode.Position, root.pickingMode);
            Assert.IsFalse(lifetime.IsDisposed);

            modal.Dispose();

            Assert.AreEqual(0, context.Modals.Count);
            Assert.IsFalse(root.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            Assert.IsTrue(lifetime.IsDisposed);
        }

    #endregion

    }
}
