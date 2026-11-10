/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UIContextFacade.cs
수정일 : 2026-11-10

# 설명
UIContext의 Screen registration, Layer 획득, dynamic Presentation 획득과 UITK Modal helper 조립을 검증한다.
Facade가 하위 composition primitive와 동일한 state/lifetime 계약을 사용하는지 고정한다.

# 테스트 구성
 A: UIContext 테스트 대역
 B: UIContext Fixture 구성과 정리
 C: 공통 Facade 등록과 Lease 수명
 D: UITK Modal 수명과 롤백
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
    /// UIContext facade의 state/lifetime 위임 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_UIContextFacade
    {

    #region A: UIContext 테스트 대역

        private sealed class TestFocusDriver : IFocusDriver
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 현재 포커스 선택 대역은 선택 상태를 보유하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public object Current => null;

            // ------------------------------------------------------------
            /// <summary>
            /// 포커스 선택에 주입할 실패 횟수를 지정한다.
            /// </summary>
            // ------------------------------------------------------------
            public int SelectFailureCount { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 대상이 유효한 포커스 대상인지 확인한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsValid(object target)
            {
                return target != null;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 대상으로 포커스를 선택하거나 실패를 주입한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Select(object target)
            {
                if (SelectFailureCount > 0)
                {
                    SelectFailureCount--;
                    throw new InvalidOperationException("Focus 선택 실패");
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 대체 포커스 대상이 없음을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public object FindFallback()
            {
                return null;
            }
        }
        private sealed class TestInputDriver : IScreenInputDriver
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 입력 드라이버는 실제 Session 획득을 지원하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public ScreenInputSession Acquire
            (
                ScreenOptions options,
                bool contributionEnabled = true
            )
            {
                throw new NotSupportedException();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 입력 상태 배치 시작 요청을 수신한다.
            /// </summary>
            // ------------------------------------------------------------
            public void BeginBatch()
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 입력 상태 배치 종료 요청을 수신한다.
            /// </summary>
            // ------------------------------------------------------------
            public void EndBatch()
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 보유한 입력 Session 전체 해제 요청을 수신한다.
            /// </summary>
            // ------------------------------------------------------------
            public void ForceReleaseAll()
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 입력 드라이버 대역의 수명을 종료한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Dispose()
            {
                // NONE
            }
        }
        private sealed class TestTransitioner : IPresentationTransitioner
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 전환 대역은 실제 전환 재생을 지원하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationTransitionHandle Play
            (
                PresentationTransitionParams parameters,
                Action onCompleted,
                Action<Exception> onFailed
            )
            {
                throw new NotSupportedException();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 전환 대역의 수명을 종료한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Dispose()
            {
                // NONE
            }
        }

        private sealed class TestScreenSource : IScreenSource
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Screen Source는 실제 View 획득을 지원하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public ScreenInstance Acquire(ScreenViewScope scope)
            {
                throw new NotSupportedException();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Screen Source는 실제 View 반환을 지원하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release(ScreenInstance instance)
            {
                throw new NotSupportedException();
            }
        }
        private sealed class TestPresentationSource : IPresentationSource<object>
        {

            // ------------------------------------------------------------
            /// <summary>
            /// Presentation Source 획득 횟수를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public int AcquireCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Presentation Source 반환 횟수를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public int ReleaseCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 마지막 획득 요청의 Layer를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public IPresentationLayerDriver AcquiredLayer { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 획득 중 발생시킬 예외를 지정한다.
            /// </summary>
            // ------------------------------------------------------------
            public Exception AcquireFailure { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 반환 중 발생시킬 예외를 지정한다.
            /// </summary>
            // ------------------------------------------------------------
            public Exception ReleaseFailure { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Layer에서 테스트 Presentation 객체를 획득한다.
            /// </summary>
            // ------------------------------------------------------------
            public object Acquire(IPresentationLayerDriver layer)
            {
                AcquireCount++;
                AcquiredLayer = layer;

                if (AcquireFailure != null)
                {
                    throw AcquireFailure;
                }

                return new object();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Presentation 객체를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release(object view)
            {
                ReleaseCount++;

                if (ReleaseFailure != null)
                {
                    throw ReleaseFailure;
                }
            }
        }

        private sealed class TestLifetime : IDisposable
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 수명 객체의 종료 여부를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsDisposed { get; private set; }

            private readonly Action disposing = null;

            // ------------------------------------------------------------
            /// <summary>
            /// 종료 콜백을 가진 테스트 수명 객체를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestLifetime(Action disposing = null)
            {
                this.disposing = disposing;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 수명 객체를 종료하고 콜백을 호출한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Dispose()
            {
                IsDisposed = true;
                disposing?.Invoke();
            }
        }

        private sealed class TestFocusScope : IFocusScope
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 포커스 범위에서 기본으로 선택할 대상을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public object DefaultFocus { get; }

            private readonly VisualElement root = null;

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 Root와 기본 포커스로 테스트 범위를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestFocusScope(VisualElement root, object defaultFocus)
            {
                this.root = root ?? throw new ArgumentNullException(nameof(root));
                DefaultFocus = defaultFocus;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 대상이 테스트 포커스 범위에 속하는지 확인한다.
            /// </summary>
            // ------------------------------------------------------------
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

    #region B: UIContext Fixture 구성과 정리

        // ------------------------------------------------------------
        /// <summary>
        /// UIContext 테스트를 위한 런타임과 Session을 구성한다.
        /// </summary>
        // ------------------------------------------------------------
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
            presentationRootObject = new GameObject("UIContext Facade Presentation Root");
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            presentation = PresentationSession.CreateTopLevel
            (
                plan,
                presentationRootObject.transform,
                panelSettings,
                null
            );
            runtimeObject = new GameObject("UIContext Facade Runtime Owner");
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

        // ------------------------------------------------------------
        /// <summary>
        /// UIContext 테스트에서 획득한 자원을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
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

    #region C: 공통 Facade 등록과 Lease 수명

        // ----------------------------------------------------------------------
        /// <summary>
        /// UIContext의 Screen 등록 Handle 해제가 Registry 등록을 제거한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIContext_RegisterScreen_Registry와같은등록수명()
        {
            var options = new ScreenOptions("Common.Screen");
            var source = new TestScreenSource();

            var registration = context.RegisterScreen
            (
                options,
                PresentationTarget.Local("Common"),
                source
            );

            Assert.IsTrue(context.ScreenRegistry.Contains(options.ID));

            registration.Dispose();

            Assert.IsFalse(context.ScreenRegistry.Contains(options.ID));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Local Target의 Layer Lease 획득·반환 대칭을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIContext_AcquireLayer_Local_LayerUsage대칭()
        {
            var layerLease = context.AcquireLayer(PresentationTarget.Local("Common"));

            Assert.IsTrue(presentation.LayerRegistry.HasConsumers);
            Assert.IsNotNull(layerLease.Layer);

            layerLease.Dispose();

            Assert.IsTrue(layerLease.IsDisposed);
            Assert.IsFalse(presentation.LayerRegistry.HasConsumers);
            Assert.Throws<ObjectDisposedException>(() => _ = layerLease.Layer);
            Assert.DoesNotThrow(layerLease.Dispose);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Presentation Lease가 획득한 Layer Usage와 Source를 같은 수명으로 해제한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIContext_AcquirePresentation_LocalLayerLease와Source를같은수명으로소유()
        {
            var source = new TestPresentationSource();
            var lease = context.AcquirePresentation
            (
                PresentationTarget.Local("Common"),
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Source 획득 실패가 Layer Lease consumer를 남기지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIContext_AcquirePresentation_Source획득실패_LayerLeaseRollback()
        {
            var source = new TestPresentationSource
            {
                AcquireFailure = new InvalidOperationException("injected acquire failure"),
            };

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => context.AcquirePresentation
                (
                    PresentationTarget.Local("Common"),
                    source
                )
            );

            StringAssert.Contains("injected acquire failure", exception.Message);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(0, source.ReleaseCount);
            Assert.IsFalse(presentation.LayerRegistry.HasConsumers);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Source 반환 실패 뒤에도 Layer Lease를 반환하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UIContext_AcquirePresentation_Source반환실패_LayerLease정리와Terminal유지()
        {
            var source = new TestPresentationSource
            {
                ReleaseFailure = new InvalidOperationException("injected release failure"),
            };
            var lease = context.AcquirePresentation
            (
                PresentationTarget.Local("Common"),
                source
            );

            var exception = Assert.Throws<InvalidOperationException>(lease.Dispose);

            StringAssert.Contains("injected release failure", exception.Message);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsFalse(presentation.LayerRegistry.HasConsumers);
            Assert.DoesNotThrow(lease.Dispose);
            Assert.AreEqual(1, source.ReleaseCount);
        }

    #endregion

    #region D: UITK Modal 수명과 롤백

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> UITK Modal을 열면 Placement Usage와 Root 상호작용 상태를 소유하고 닫을 때 원상
        /// <br/> 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
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
                PresentationTarget.Local("Common"),
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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Modal 종료 뒤 Root 상태를 되돌리고 동일 Focus Scope와 Root로 다시 열 수 있다.
        /// </summary>
        // --------------------------------------------------------------------------------
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
                PresentationTarget.Local("Common"),
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
                PresentationTarget.Local("Common"),
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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Focus 선택 실패 시 Modal 등록과 Root 상태를 롤백하고 같은 Root로 재시도할 수 있다.
        /// </summary>
        // --------------------------------------------------------------------------------
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
                    PresentationTarget.Local("Common"),
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
                PresentationTarget.Local("Common"),
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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> UITK Modal을 UGUI Placement에 열 수 없으며 실패 시 Root와 Presentation
        /// <br/> Usage를 변경하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
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
                    PresentationTarget.Local("Common.UGUI"),
                    root
                )
            );

            Assert.AreEqual(0, context.Modals.Count);
            Assert.IsNull(root.parent);
            Assert.IsFalse(root.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            Assert.DoesNotThrow(presentation.ValidateCanDispose);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Composition Modal이 전달받은 Lifetime을 소유하고 종료 시 Interaction 상태와
        /// <br/> 자원을 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
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
