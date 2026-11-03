/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UITKUI.cs
수정일 : 2026-11-05

# 설명
UITK Interaction·Focus, Native Output, Safe Area, Screen, Modal과 Fade의 Runtime 경계를 검증한다.

# 테스트 구성
 I: UITK Interaction과 Focus Root 재진입 해제
 L: UITK Native Output, 소유 콘텐츠 및 Safe Area Root 재연결
 D: Screen·Modal·Fade Driver
 C: Screen Close와 Visual Tree 반환
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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

    // ============================================================
    /// <summary>
    /// UI Toolkit UI의 실제 VisualElement 경계 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_UITKUI
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// VisualElement Root를 제공하는 테스트 Layer Driver.
        /// </summary>
        // ============================================================
        private sealed class TestLayerDriver : IPresentationLayerDriver<VisualElement>
        {
            public VisualElement Root { get; }

            public TestLayerDriver(VisualElement root) : base()
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
                Root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ======================================================================
        /// <summary>
        /// Content Root 접근 중 동기 callback을 발생시키는 테스트 Layer Root.
        /// </summary>
        // ======================================================================
        private sealed class CallbackLayerRoot : VisualElement
        {
            public Action AccessingContent { get; set; }

            public override VisualElement contentContainer
            {
                get
                {
                    var callback = AccessingContent;
                    AccessingContent = null;
                    callback?.Invoke();
                    return base.contentContainer;
                }
            }
        }

        // ============================================================
        /// <summary>
        /// 자식 Container를 실제 콘텐츠 삽입 위치로 제공하는 테스트 Root.
        /// </summary>
        // ============================================================
        private sealed class NestedContentRoot : VisualElement
        {
            public VisualElement Container { get; } = new();
            public override VisualElement contentContainer => Container;

            public NestedContentRoot()
            {
                hierarchy.Add(Container);
            }
        }

        // ============================================================
        /// <summary>
        /// Visual Tree Screen을 생성하고 제거하는 테스트 Source.
        /// </summary>
        // ============================================================
        private sealed class TestScreenSource : IScreenSource
        {
            public VisualElement ScreenRoot { get; private set; }
            public int ReleaseCount { get; private set; }

            public ScreenInstance Acquire(ScreenViewScope scope)
            {
                if (!(scope.Layer is IPresentationLayerDriver<VisualElement> layer))
                {
                    throw new InvalidOperationException("VisualElement Layer가 필요합니다.");
                }

                ScreenRoot = new VisualElement
                {
                    name = "RuntimeScreen",
                };
                layer.Root.Add(ScreenRoot);
                return new ScreenInstance(new UITKScreenDriver(ScreenRoot));
            }

            public void Release(ScreenInstance instance)
            {
                ReleaseCount++;
                ScreenRoot.RemoveFromHierarchy();
            }
        }

        // ============================================================
        /// <summary>
        /// 즉시 완료되는 Presentation Transitioner.
        /// </summary>
        // ============================================================
        private sealed class ImmediateTransitioner : IPresentationTransitioner
        {
            public PresentationTransitionHandle Play
            (
                PresentationTransitionParams parameters,
                Action onCompleted,
                Action<Exception> onFailed
            )
            {
                parameters.Target.Set(parameters.EndValue);
                var handle = new PresentationTransitionHandle(null);
                handle.Complete();
                onCompleted?.Invoke();
                return handle;
            }

            public void Dispose()
            {
                // NONE
            }
        }

        // ============================================================
        /// <summary>
        /// Screen Focus 계약에 필요한 최소 메모리 Driver.
        /// </summary>
        // ============================================================
        private sealed class TestFocusDriver : IFocusDriver
        {
            public object Current { get; private set; }

            public bool IsValid(object target)
            {
                return target != null;
            }

            public void Select(object target)
            {
                Current = target;
            }

            public object FindFallback()
            {
                return null;
            }
        }

        // ============================================================
        /// <summary>
        /// 즉시 반환되는 Screen Input Session Driver.
        /// </summary>
        // ============================================================
        private sealed class TestInputDriver : IScreenInputDriver
        {
            private readonly List<ScreenInputSession> sessions =
                new List<ScreenInputSession>();

            public ScreenInputSession Acquire
            (
                ScreenOptions options,
                bool contributionEnabled = true
            )
            {
                var session = new ScreenInputSession(options, Release);
                sessions.Add(session);
                return session;
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
                for (var i = sessions.Count - 1; i >= 0; i--)
                {
                    sessions[i].MarkReleased();
                }

                sessions.Clear();
            }

            public void Dispose()
            {
                ForceReleaseAll();
            }

            private void Release
            (
                ScreenInputSession session,
                bool waitForInputRelease,
                bool retainCursorWhileAwaitingRelease
            )
            {
                sessions.Remove(session);
                session.MarkReleased();
            }
        }

        // ======================================================================
        /// <summary>
        /// Runtime Native Output GameObject의 획득·반환을 기록하는 Provider.
        /// </summary>
        // ======================================================================
        private sealed class TestProvider : IGameObjectProvider
        {
            public Transform Parent { get; set; }
            public int AcquireCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public bool LastReleasedActiveSelf { get; private set; }

            private readonly Func<Transform, GameObject> acquire = null;

            public TestProvider(Func<Transform, GameObject> acquire) : base()
            {
                this.acquire = acquire ?? throw new ArgumentNullException(nameof(acquire));
            }

            public GameObject Acquire(bool worldPositionStays = true)
            {
                AcquireCount++;
                return acquire(Parent);
            }

            public Awaitable<GameObject> AcquireAsync(bool worldPositionStays = true)
            {
                throw new NotSupportedException();
            }

            public void Release
            (
                GameObject gameObject,
                bool worldPositionStays = true
            )
            {
                ReleaseCount++;
                LastReleasedActiveSelf = gameObject.activeSelf;
            }
        }

        private readonly List<UnityEngine.Object> ownedObjects =
            new List<UnityEngine.Object>();

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 UXML Asset을 Resources에서 읽는다.
        /// </summary>
        // ------------------------------------------------------------
        private static VisualTreeAsset LoadViewAsset()
        {
            var asset = Resources.Load<VisualTreeAsset>
            (
                "Xeri/UI/TEST_UITKUI"
            );
            Assert.IsNotNull(asset);
            return asset;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 Provider가 반환할 실제 UGUI Native Output을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private GameObject CreateUGUISurfaceObject(Transform parent)
        {
            var gameObject = new GameObject
            (
                "UGUI Runtime Surface",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUIPresentationOutput)
            );
            gameObject.SetActive(false);
            gameObject.transform.SetParent(parent, false);
            ownedObjects.Add(gameObject);
            gameObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return gameObject;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// private 직렬화 필드를 설정한다.
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

            Assert.IsNotNull(field, $"{target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }

    #endregion

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트에서 생성한 Unity Object를 역순 제거한다.
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

    #region I-1: UITK 상호작용

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 중첩 UITK Blocker의 마지막 Lease 해제만 표시와 Picking을 종료하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKInteractionBlocker_중첩점유_마지막해제만비활성()
        {
            var element = new VisualElement();
            IInteractionBlocker blocker = new UITKInteractionBlocker(element);
            var first = blocker.Acquire();
            var second = blocker.Acquire();
            first.Dispose();

            Assert.AreEqual(DisplayStyle.Flex, element.style.display.value);
            Assert.AreEqual(PickingMode.Position, element.pickingMode);

            second.Dispose();

            Assert.AreEqual(DisplayStyle.None, element.style.display.value);
            Assert.AreEqual(PickingMode.Ignore, element.pickingMode);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Root 변경 중 Layer를 반환해도 구독이 남지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKFocusDriver_루트변경알림중바인딩해제_새루트구독없음()
        {
            var host = new GameObject("Focus Reentrant Release");
            ownedObjects.Add(host);
            var backend = host.AddComponent<UITKFocusDriver>();
            var previousRoot = new VisualElement();
            var focused = new Button();
            previousRoot.Add(focused);
            var nextRoot = new VisualElement();
            var layer = new UITKPresentationLayer(previousRoot);
            var binding = backend.RegisterLayer(layer);

            SetField(backend, "current", focused);
            SetField(backend, "reportedCurrent", focused);
            backend.OnFocusChanged += _ => binding.Dispose();

            layer.RebindRoot(nextRoot);

            var field = typeof(UITKFocusDriver).GetField
            (
                "layerRoots",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(field);
            var registered = (List<VisualElement>)field.GetValue(backend);

            Assert.IsEmpty(registered);
            Assert.IsNull(backend.Current);
            Assert.DoesNotThrow(binding.Dispose);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 공유 Root는 마지막 Layer Lease 해제까지 구독 대상으로 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKFocusDriver_공유Root등록_마지막Lease까지유지()
        {
            var host = new GameObject("Shared UITK Focus Host");
            ownedObjects.Add(host);
            var backend = host.AddComponent<UITKFocusDriver>();
            var root = new VisualElement();
            var first = backend.RegisterLayer(new UITKPresentationLayer(root));
            var second = backend.RegisterLayer(new UITKPresentationLayer(root));
            var field = typeof(UITKFocusDriver).GetField
            (
                "layerRoots",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(field);
            var roots = (List<VisualElement>)field.GetValue(backend);

            Assert.AreEqual(1, roots.Count);
            Assert.AreSame(root, roots[0]);
            first.Dispose();
            Assert.AreEqual(1, roots.Count);
            second.Dispose();
            Assert.IsEmpty(roots);
        }

    #endregion

    #region L-1: UITK 네이티브 출력 구체화

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> LayerOrder가 PanelRenderer가 소유한 runtime PanelSettings
        /// <br/> native order에 직접 materialize된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_Initialize_LayerOrderNativeMaterialize()
        {
            var gameObject = new GameObject("UITK Output");
            gameObject.SetActive(false);
            ownedObjects.Add(gameObject);
            var panelRenderer = gameObject.AddComponent<PanelRenderer>();
            var template = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(template);
            ownedObjects.Add(template);
            template.clearDepthStencil = false;
            var output = gameObject.AddComponent<UITKPresentationOutput>();

            output.Initialize(template, 23);

            Assert.AreNotSame(template, panelRenderer.panelSettings);
            Assert.AreSame(output.RuntimePanelSettings, panelRenderer.panelSettings);
            Assert.AreEqual(23.0f, output.RuntimePanelSettings.sortingOrder);
            Assert.IsNull(output.RuntimePanelSettings.targetTexture);
            Assert.IsFalse(output.RuntimePanelSettings.clearDepthStencil);

            output.ReleaseOutputState();
            Assert.IsNull(panelRenderer.panelSettings);
            Assert.IsNull(output.RuntimePanelSettings);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// ThemeStyleSheet이 없는 PanelSettings 구성을 Native Output 경계에서 거부한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_Validate_Theme없음거부()
        {
            var gameObject = new GameObject("UITK Missing Theme Surface");
            gameObject.SetActive(false);
            ownedObjects.Add(gameObject);
            var panelRenderer = gameObject.AddComponent<PanelRenderer>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            ownedObjects.Add(panelSettings);
            panelSettings.themeStyleSheet = null;
            panelRenderer.panelSettings = panelSettings;
            var output = gameObject.AddComponent<UITKPresentationOutput>();

            Assert.IsNull(panelSettings.themeStyleSheet);
            Assert.IsFalse(output.Validate(out var error));
            StringAssert.Contains("ThemeStyleSheet", error);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Top-level ScreenOverlay output은 template의
        /// <br/> TargetTexture를 runtime copy에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_Initialize_TargetTextureTemplate정규화()
        {
            var gameObject = new GameObject("UITK Render Texture Template");
            gameObject.SetActive(false);
            ownedObjects.Add(gameObject);
            gameObject.AddComponent<PanelRenderer>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            ownedObjects.Add(panelSettings);
            var targetTexture = new RenderTexture(16, 16, 0);
            ownedObjects.Add(targetTexture);
            panelSettings.targetTexture = targetTexture;
            var output = gameObject.AddComponent<UITKPresentationOutput>();

            output.Initialize(panelSettings, 23);

            Assert.AreSame(targetTexture, panelSettings.targetTexture);
            Assert.IsNull(output.RuntimePanelSettings.targetTexture);
            Assert.AreEqual(23.0f, output.RuntimePanelSettings.sortingOrder);

            output.ReleaseOutputState();
        }

    #endregion

    #region L-2: 런타임 콘텐츠의 루트 교체

        // ------------------------------------------------------------
        /// <summary>
        /// 루트 교체 시 소유 콘텐츠만 옮기고 원래 요소와 논리 상태를 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationLayer_루트교체와해제에서소유콘텐츠만이동()
        {
            var originalRoot = new VisualElement();
            var authored = new VisualElement();
            originalRoot.Add(authored);
            var originalDisplay = originalRoot.style.display;
            var layer = new UITKPresentationLayer(originalRoot);
            var content = new VisualElement();
            var button = new Button();
            content.Add(button);
            using var attachment = UITKPresentationLayer.Attach(layer, content);
            layer.SetActive(false);

            layer.RebindRoot(null);
            Assert.IsNull(layer.Root);
            Assert.IsNull(content.parent);
            Assert.AreSame(originalRoot, authored.parent);
            Assert.AreEqual(originalDisplay, originalRoot.style.display);

            var replacement = new VisualElement();
            var replacementAuthored = new VisualElement();
            replacement.Add(replacementAuthored);
            layer.RebindRoot(replacement);
            Assert.AreSame(replacement, content.parent);
            Assert.AreSame(content, button.parent);
            Assert.AreSame(replacement, replacementAuthored.parent);
            Assert.AreSame(originalRoot, authored.parent);
            Assert.AreEqual(DisplayStyle.None, replacement.style.display.value);

            attachment.Dispose();
            layer.RebindRoot(new VisualElement());
            Assert.IsNull(content.parent, "반환한 콘텐츠가 다음 루트에 다시 연결됐습니다.");
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 새 Root가 옛 Root의 조상이어도 소유 콘텐츠를 실제 ContentContainer로 옮긴다.
        /// </summary>
        // ----------------------------------------------------------------------
        [TestCase(false)]
        [TestCase(true)]
        public void TEST_UITKPresentationLayer_조상Root교체_실제ContentContainer로재부착(bool nestedContainer)
        {
            var previousRoot = new VisualElement();
            var layer = new UITKPresentationLayer(previousRoot);
            var content = new VisualElement();
            var attachment = UITKPresentationLayer.Attach(layer, content);
            VisualElement nextRoot = nestedContainer ? new NestedContentRoot() : new VisualElement();
            var container = nextRoot.contentContainer;
            container.Add(previousRoot);

            Assert.AreSame(previousRoot, content.parent);
            layer.RebindRoot(nextRoot);

            Assert.AreSame(container, content.parent);
            previousRoot.RemoveFromHierarchy();
            Assert.AreSame(container, content.parent);
            attachment.Dispose();
            Assert.IsNull(content.parent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Root가 해제돼도 유효한 Lease의 독점 소유권은 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [TestCase(false)]
        [TestCase(true)]
        public void TEST_UITKPresentationLayer_Root해제후중복획득_Lease소유권보호(bool foreignLayer)
        {
            var originalRoot = new VisualElement();
            var first = new UITKPresentationLayer(originalRoot);
            var content = new VisualElement();
            var lease = UITKPresentationLayer.Attach(first, content);
            first.RebindRoot(null);
            Assert.IsNull(content.parent);

            var destinationRoot = new VisualElement();
            IPresentationLayerDriver<VisualElement> second = foreignLayer
                ? new TestLayerDriver(destinationRoot)
                : new UITKPresentationLayer(destinationRoot);

            Assert.Throws<InvalidOperationException>
            (
                () => UITKPresentationLayer.Attach(second, content)
            );
            first.RebindRoot(originalRoot);
            Assert.AreSame(originalRoot, content.parent);

            lease.Dispose();
            using var acquired = UITKPresentationLayer.Attach(second, content);
            Assert.AreSame(destinationRoot, content.parent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부착 callback에서 Lease가 끝나면 콘텐츠를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationLayer_부착중Lease반환_콘텐츠미재부착()
        {
            var layer = new UITKPresentationLayer(new VisualElement());
            var content = new VisualElement();
            var attachment = UITKPresentationLayer.Attach(layer, content);
            var replacement = new CallbackLayerRoot();
            replacement.AccessingContent = attachment.Dispose;

            layer.RebindRoot(replacement);

            Assert.IsNull(content.parent);
            layer.RebindRoot(new VisualElement());
            Assert.IsNull(content.parent);
            Assert.DoesNotThrow(attachment.Dispose);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부착 중 Lease가 이전되면 새 소유자를 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [TestCase(false)]
        [TestCase(true)]
        public void TEST_UITKPresentationLayer_부착중Lease이전_새Layer소유권보호(bool foreignLayer)
        {
            var original = new UITKPresentationLayer(new VisualElement());
            var content = new VisualElement();
            var previousLease = UITKPresentationLayer.Attach(original, content);
            var destinationRoot = new VisualElement();
            IPresentationLayerDriver<VisualElement> destination = foreignLayer
                ? new TestLayerDriver(destinationRoot)
                : new UITKPresentationLayer(destinationRoot);
            var intermediate = new CallbackLayerRoot();
            Lease newLease = null;
            intermediate.AccessingContent = () =>
            {
                previousLease.Dispose();
                newLease = UITKPresentationLayer.Attach(destination, content);
            };

            original.RebindRoot(intermediate);

            Assert.AreSame(destinationRoot, content.parent);
            Assert.IsNotNull(newLease);
            newLease.Dispose();
            Assert.IsNull(content.parent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부착 중 더 최신 Root로 재진입하면 최종 Root에만 콘텐츠를 남긴다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationLayer_부착중루트재변경_최신Root만소유()
        {
            var layer = new UITKPresentationLayer(new VisualElement());
            var content = new VisualElement();
            using var attachment = UITKPresentationLayer.Attach(layer, content);
            var intermediate = new CallbackLayerRoot();
            var latest = new VisualElement();
            intermediate.AccessingContent = () => layer.RebindRoot(latest);

            layer.RebindRoot(intermediate);

            Assert.AreSame(latest, layer.Root);
            Assert.AreSame(latest, content.parent);
            Assert.AreEqual(0, intermediate.childCount);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Root 변경 observer가 재진입하면 오래된 변경 알림을 반복하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationLayer_알림중재변경_최신Root만전달()
        {
            var first = new VisualElement();
            var intermediate = new VisualElement();
            var latest = new VisualElement();
            var layer = new UITKPresentationLayer(first);
            var observed = new List<VisualElement>();
            layer.RootChanged += current =>
            {
                if (ReferenceEquals(current.Root, intermediate))
                {
                    current.RebindRoot(latest);
                }
            };
            layer.RootChanged += current => observed.Add(current.Root);

            layer.RebindRoot(intermediate);

            Assert.AreSame(latest, layer.Root);
            var expected = new[]
            {
                latest,
            };
            CollectionAssert.AreEqual(expected, observed);
        }

    #endregion

    #region L-3: Native Safe Area Root 재연결

        // ------------------------------------------------------------
        /// <summary>
        /// 출력 Root 교체와 반환을 Safe Area 관찰이 따라간다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKSafeAreaLayout_OutputRoot교체와반환_관찰대상갱신()
        {
            var host = new GameObject("Safe Area Root Rebind");
            host.SetActive(false);
            ownedObjects.Add(host);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            ownedObjects.Add(settings);
            var renderer = host.AddComponent<PanelRenderer>();
            renderer.panelSettings = settings;
            var output = host.AddComponent<UITKPresentationOutput>();
            output.InitializeBorrowed(31);
            var layout = host.AddComponent<UITKSafeAreaLayout>();
            SetField(layout, "rootName", "Safe Area");
            Assert.DoesNotThrow(() => host.SetActive(true));

            var reload = typeof(UITKPresentationOutput).GetMethod
            (
                "HandleUIReload",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            var observed = typeof(UITKSafeAreaLayout).GetField
            (
                "observedRoot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(reload);
            Assert.IsNotNull(observed);

            var firstRoot = new VisualElement();
            var first = new VisualElement();
            first.name = "Safe Area";
            firstRoot.Add(first);
            reload.Invoke
            (
                output,
                new object[]
                {
                    renderer, firstRoot, 1,
                }
            );
            Assert.AreSame(first, observed.GetValue(layout));

            var secondRoot = new VisualElement();
            var second = new VisualElement();
            second.name = "Safe Area";
            secondRoot.Add(second);
            reload.Invoke
            (
                output,
                new object[]
                {
                    renderer, secondRoot, 2,
                }
            );
            Assert.AreSame(second, layout.SafeAreaRoot);
            Assert.AreSame(second, observed.GetValue(layout));

            output.ReleaseOutputState();
            Assert.IsNull(observed.GetValue(layout));
            layout.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Root 알림 중 Layout 비활성화는 오래된 구독도 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKSafeAreaLayout_Root알림중비활성화_관찰재등록방지()
        {
            var host = new GameObject("Safe Area Disable Reentry");
            host.SetActive(false);
            ownedObjects.Add(host);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            ownedObjects.Add(settings);
            var renderer = host.AddComponent<PanelRenderer>();
            renderer.panelSettings = settings;
            var output = host.AddComponent<UITKPresentationOutput>();
            output.InitializeBorrowed(32);
            var layout = host.AddComponent<UITKSafeAreaLayout>();
            SetField(layout, "rootName", "Safe Area");
            host.SetActive(true);
            layout.enabled = false;

            // 먼저 등록된 observer가 Layout을 끈 뒤에도 snapshot의 뒤 콜백이 도착한다.
            output.RootUpdated += root =>
            {
                if (root != null)
                {
                    layout.enabled = false;
                }
            };
            layout.enabled = true;

            var reload = typeof(UITKPresentationOutput).GetMethod
            (
                "HandleUIReload",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            var observed = typeof(UITKSafeAreaLayout).GetField
            (
                "observedRoot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(reload);
            Assert.IsNotNull(observed);

            var rootElement = new VisualElement();
            var safeArea = new VisualElement();
            safeArea.name = "Safe Area";
            rootElement.Add(safeArea);
            reload.Invoke
            (
                output,
                new object[]
                {
                    renderer, rootElement, 1,
                }
            );

            Assert.IsFalse(layout.enabled);
            Assert.IsNull(observed.GetValue(layout));
            layout.enabled = true;
            Assert.AreSame(safeArea, observed.GetValue(layout));
            output.ReleaseOutputState();
            Assert.IsNull(observed.GetValue(layout));
            layout.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Safe Area가 Dispose되면 미전달 observer 호출을 중단한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKSafeAreaLayout_알림중Dispose_남은Observer중단()
        {
            var host = new GameObject("Safe Area Callback Lifetime");
            host.SetActive(false);
            ownedObjects.Add(host);
            var layout = host.AddComponent<UITKSafeAreaLayout>();
            var staleCalls = 0;
            layout.OnLayoutChanged += layout.Dispose;
            layout.OnLayoutChanged += () => staleCalls++;
            var notify = typeof(UITKSafeAreaLayout).GetMethod
            (
                "NotifyLayoutChanged",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(notify);

            Assert.DoesNotThrow(() => notify.Invoke(layout, null));
            Assert.AreEqual(0, staleCalls);
            Assert.Throws<ObjectDisposedException>(layout.Refresh);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Root 알림 중 Output이 반환되면 오래된 알림은 중단한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_Root알림중반환_오래된전파중단()
        {
            var host = new GameObject("Root Observer Reentry");
            host.SetActive(false);
            ownedObjects.Add(host);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            ownedObjects.Add(settings);
            var renderer = host.AddComponent<PanelRenderer>();
            renderer.panelSettings = settings;
            var output = host.AddComponent<UITKPresentationOutput>();
            output.InitializeBorrowed(14);
            var staleCallbacks = 0;

            output.RootUpdated += value =>
            {
                if (value != null)
                {
                    output.ReleaseOutputState();
                }
            };
            output.RootUpdated += value =>
            {
                if (value != null)
                {
                    staleCallbacks++;
                }
            };

            var reload = typeof(UITKPresentationOutput).GetMethod
            (
                "HandleUIReload",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(reload);
            reload.Invoke
            (
                output,
                new object[]
                {
                    renderer, new VisualElement(), 1,
                }
            );

            Assert.AreEqual(0, staleCallbacks);
            Assert.IsNull(output.Root);
            Assert.Throws<InvalidOperationException>(() => output.RequireRoot());

            // 해제 이후 도착한 Native callback은 반환된 Output을 다시 연결하지 못한다.
            reload.Invoke
            (
                output,
                new object[]
                {
                    renderer, new VisualElement(), 2,
                }
            );
            Assert.IsNull(output.Root);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Output 해제 알림 중 재획득을 거부하고 원래 설정을 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_해제알림중재초기화_소유권복원()
        {
            var host = new GameObject("Output Release Reentry");
            host.SetActive(false);
            ownedObjects.Add(host);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            ownedObjects.Add(settings);
            var renderer = host.AddComponent<PanelRenderer>();
            renderer.panelSettings = settings;
            var output = host.AddComponent<UITKPresentationOutput>();
            var originalOrder = settings.sortingOrder;
            output.InitializeBorrowed(14);
            var rejected = 0;

            output.RootUpdated += value =>
            {
                if (value != null) return;

                rejected++;
                Assert.Throws<InvalidOperationException>
                (
                    () => output.InitializeBorrowed(32)
                );
                Assert.DoesNotThrow(output.ReleaseOutputState);
            };

            output.ReleaseOutputState();

            Assert.AreEqual(1, rejected);
            Assert.AreEqual(originalOrder, settings.sortingOrder);
            Assert.Throws<InvalidOperationException>(() => output.RequireRoot());

            output.InitializeBorrowed(35);
            Assert.AreEqual(35.0f, settings.sortingOrder);
            output.ReleaseOutputState();
            Assert.AreEqual(originalOrder, settings.sortingOrder);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부착 중 Output이 종료되면 콘텐츠를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_부착중반환_해제된Layer미부착()
        {
            var host = new GameObject("Output Attach Reentry");
            host.SetActive(false);
            ownedObjects.Add(host);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            ownedObjects.Add(settings);
            var renderer = host.AddComponent<PanelRenderer>();
            renderer.panelSettings = settings;
            var output = host.AddComponent<UITKPresentationOutput>();
            output.InitializeBorrowed(14);
            var root = new CallbackLayerRoot();
            var reload = typeof(UITKPresentationOutput).GetMethod
            (
                "HandleUIReload",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(reload);
            reload.Invoke
            (
                output,
                new object[]
                {
                    renderer, root, 1,
                }
            );
            var content = new VisualElement();
            var releaseNotifications = 0;
            output.RootUpdated += current =>
            {
                if (current == null)
                {
                    releaseNotifications++;
                }
            };
            root.AccessingContent = output.ReleaseOutputState;

            Assert.Throws<InvalidOperationException>
            (
                () => output.AttachLayerRoot(content)
            );

            Assert.AreEqual(1, releaseNotifications);
            Assert.IsNull(output.Root);
            Assert.IsNull(content.parent);
            Assert.DoesNotThrow(output.ReleaseOutputState);
        }

    #endregion

    #region D-1: 스크린·모달·페이드 드라이버

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Screen Driver의 Presentation State가 초기값을 읽고 변경값을 UITK backend에 적용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKScreenDriver_PresentationState_초기값과변경적용()
        {
            var parent = new VisualElement();
            var screen = new VisualElement();
            screen.style.display = DisplayStyle.None;
            screen.style.opacity = 0.25f;
            parent.Add(screen);
            var driver = new UITKScreenDriver(screen);

            Assert.IsFalse(driver.Visibility.Base);
            Assert.AreEqual(0.25f, driver.Alpha.Base);

            driver.Visibility.Set(true);
            driver.Alpha.Set(0.75f);

            Assert.IsTrue(driver.Visibility.Base);
            Assert.AreEqual(0.75f, driver.Alpha.Base);
            Assert.AreEqual(DisplayStyle.Flex, screen.style.display.value);
            Assert.AreEqual(0.75f, screen.style.opacity.value);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Screen Root가 원래 Ignore picking이면 활성화 뒤에도 pass-through 정책을
        /// <br/> 유지한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKScreenDriver_Interactable_초기Picking정책복원()
        {
            var screen = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            var child = new Button();
            screen.Add(child);
            var driver = new UITKScreenDriver(screen, child);

            driver.SetInteractable(false);

            Assert.IsFalse(screen.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, screen.pickingMode);

            driver.SetInteractable(true);

            Assert.IsTrue(screen.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, screen.pickingMode);
            Assert.AreEqual(PickingMode.Position, child.pickingMode);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Screen Driver와 Modal interaction backend가 각자 표시·상호작용 책임만 적용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKDrivers_표시Opacity와ModalInteraction분리적용()
        {
            var parent = new VisualElement();
            var screen = new VisualElement();
            parent.Add(screen);
            var screenDriver = new UITKScreenDriver(screen);
            var modalDriver = new UITKModalInteractionDriver(screen);

            screenDriver.Visibility.Set(false);
            screenDriver.SetInteractable(false);
            screenDriver.Alpha.Set(0.25f);
            modalDriver.SetTop(true);

            Assert.AreEqual(DisplayStyle.None, screen.style.display.value);
            Assert.AreEqual(0.25f, screen.style.opacity.value);
            Assert.IsTrue(screen.enabledSelf);
            Assert.AreEqual(PickingMode.Position, screen.pickingMode);

            modalDriver.SetTop(false);

            Assert.IsFalse(screen.enabledSelf);
            Assert.AreEqual(PickingMode.Ignore, screen.pickingMode);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Fade Source 반환과 종료가 생성한 Visual Tree를 남기지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_UITKSceneFadeSource_AcquireRelease와Dispose_Tree제거()
        {
            var layerRoot = new VisualElement();
            var layer = new TestLayerDriver(layerRoot);
            var host = new GameObject("UITK Scene Fade Source");
            ownedObjects.Add(host);
            var source = host.AddComponent<UITKSceneFadeSource>();
            SetField(source, "viewAsset", LoadViewAsset());
            SetField(source, "rootName", "SceneFade");
            source.Initialize();
            var baselineCount = layerRoot.childCount;

            var first = source.Acquire(layer);
            var second = source.Acquire(layer);

            Assert.AreEqual(baselineCount + 2, layerRoot.childCount);
            Assert.IsTrue(first.Alpha.IsValid);
            Assert.IsTrue(second.Alpha.IsValid);

            source.Release(first);
            Assert.AreEqual(baselineCount + 1, layerRoot.childCount);
            Assert.IsFalse(first.Alpha.IsValid);

            source.Dispose();
            Assert.AreEqual(baselineCount, layerRoot.childCount);
            Assert.IsFalse(second.Alpha.IsValid);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Layer Add callback이 Source를 종료하면 뒤늦은 View를 공개하지 않고,
        /// <br/> 이번 획득이 추가한 Container를 즉시 제거한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKSceneFadeSource_Acquire중Dispose_뒤늦은View미공개()
        {
            var layerRoot = new CallbackLayerRoot();
            var layer = new TestLayerDriver(layerRoot);
            var sourceObject = new GameObject("UITK Scene Fade Source");
            ownedObjects.Add(sourceObject);
            var source = sourceObject.AddComponent<UITKSceneFadeSource>();
            SetField(source, "viewAsset", LoadViewAsset());
            SetField(source, "rootName", "SceneFade");
            source.Initialize();
            var baselineCount = layerRoot.childCount;
            layerRoot.AccessingContent = source.Dispose;

            Assert.Throws<ObjectDisposedException>
            (
                () => source.Acquire(layer)
            );

            Assert.AreEqual(baselineCount, layerRoot.childCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// UITK Fade Source가 UGUI Layer에서 View를 만들기 전에 명시적으로 실패한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKSceneFadeSource_UGUILayer_View생성전거부()
        {
            var sourceObject = new GameObject("UITK Scene Fade Source");
            ownedObjects.Add(sourceObject);
            var source = sourceObject.AddComponent<UITKSceneFadeSource>();
            SetField(source, "viewAsset", LoadViewAsset());
            SetField(source, "rootName", "SceneFade");
            source.Initialize();
            var layerObject = new GameObject("UGUI Layer", typeof(RectTransform));
            ownedObjects.Add(layerObject);
            var layer = new UGUIPresentationLayer(layerObject.GetComponent<RectTransform>());

            Assert.Throws<InvalidOperationException>
            (
                () => source.Acquire(layer)
            );

            Assert.AreEqual(0, layer.Root.childCount);
            source.Dispose();
        }

    #endregion

    #region C-1: 스크린 닫기

        // --------------------------------------------------------------------------------
        /// <summary>
        /// UITK Screen Close가 Session placement 경로를 거쳐 View와 Layer usage를 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKScreen_Close_SessionPlacementTree와Usage반환()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            ownedObjects.Add(panelSettings);
            var parentRootObject = new GameObject("Parent Presentation");
            ownedObjects.Add(parentRootObject);
            var parentLayer = PresentationTestScope.CreateLayer
            (
                "Parent",
                0,
                PresentationBackend.UITK
            );
            var parentPlan = new PresentationPlan
            (
                new List<PresentationPlanLayer>
                {
                    parentLayer,
                },
                new List<PresentationPlanPlacement>
                {
                    new PresentationPlanPlacement("Parent", parentLayer, 0),
                }
            );
            var parentPresentation = PresentationSession.CreateTopLevel
            (
                parentPlan,
                parentRootObject.transform,
                panelSettings,
                null
            );
            Assert.IsTrue
            (
                parentPresentation.TryAcquirePlacement
                (
                    "Parent",
                    out var parentPlacement,
                    out var parentUsage
                )
            );
            parentUsage.Dispose();
            var parentRoot =
                ((IPresentationLayerDriver<VisualElement>)parentPlacement).Root;
            var layout = ScriptableObject.CreateInstance<PresentationLayout>();
            ownedObjects.Add(layout);
            SetField
            (
                layout,
                "layers",
                new List<PresentationLayerDefinition>
                {
                    new PresentationLayerDefinition
                    (
                        "Screen",
                        "Screen",
                        0,
                        PresentationBackend.UITK
                    ),
                }
            );
            SetField
            (
                layout,
                "placements",
                new List<PresentationPlacementDefinition>
                {
                    new PresentationPlacementDefinition("Menu", "Screen", 0),
                }
            );
            var presentation = parentPresentation.CreateChild
            (
                layout,
                parentRoot
            );
            Assert.IsTrue
            (
                presentation.LayerRegistry.TryAcquireUsage
                (
                    "Screen",
                    out var layer,
                    out var layerUsage
                )
            );
            layerUsage.Dispose();
            var screenRoot = ((IPresentationLayerDriver<VisualElement>)layer).Root;
            var screenRegistry = new ScreenRegistry();
            var source = new TestScreenSource();
            var registration = screenRegistry.Register
            (
                new ScreenOptions("Menu"),
                PresentationTarget.Local("Screen"),
                source
            );
            var transitioner = new ImmediateTransitioner();
            var input = new TestInputDriver();
            var controller = new ScreenController
            (
                screenRegistry,
                target => presentation.AcquireLayer(target.ID),
                transitioner,
                new FocusController(new TestFocusDriver()),
                input
            );
            controller.Activate();

            var response = controller.Open("Menu");
            Assert.IsTrue(response.Accepted);
            Assert.IsTrue(presentation.LayerRegistry.HasConsumers);
            Assert.AreSame(screenRoot, source.ScreenRoot.parent);

            Assert.IsTrue(response.Session.Close());

            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsNull(source.ScreenRoot.parent);
            Assert.IsFalse(presentation.LayerRegistry.HasConsumers);

            controller.Clear();
            input.Dispose();
            transitioner.Dispose();
            registration.Dispose();
            screenRegistry.Dispose();
            presentation.Dispose();
            parentPresentation.Dispose();
        }

    #endregion

    }
}

namespace inonego.Xeri.UI.TEST
{
    // ======================================================================
    /// <summary>
    /// transient PanelSettings의 runtime theme을 보정하는 테스트 헬퍼.
    /// </summary>
    // ======================================================================
    internal static class UITKTestPanelSettings
    {

    #region 필드

        private const string DEFAULT_THEME_GETTER_FIELD_NAME = "GetOrCreateDefaultTheme";

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 임시 PanelSettings에 Unity 기본 runtime theme을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static void ApplyDefaultRuntimeTheme(PanelSettings target)
        {
            if (target == null)
            {
                return;
            }

            var field = typeof(PanelSettings).GetField
            (
                DEFAULT_THEME_GETTER_FIELD_NAME,
                BindingFlags.NonPublic | BindingFlags.Static
            );

            if (field == null)
            {
                return;
            }

            var getter = field.GetValue(null) as Func<ThemeStyleSheet>;
            var theme = getter?.Invoke();

            if (theme != null)
            {
                target.themeStyleSheet = theme;
            }
        }

    #endregion

    }
}
