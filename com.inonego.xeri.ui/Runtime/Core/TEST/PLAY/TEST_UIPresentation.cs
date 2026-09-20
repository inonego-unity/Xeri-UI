/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UIPresentation.cs
수정일 : 2026-09-20

# 설명
실제 Runtime Panel과 Canvas에서 UGUI·UITK 표시와 mixed Focus의 대표 경로를 검증한다.

# 테스트 구성
 O: UGUI·UITK 공통 Layer Order
 I: Input System Map·장치·해제 장벽
 P: DOTween Presentation Transition
 U: UGUI Layer·Screen·Modal·Fade·Layout
 T: UITK Panel·Focus·Screen·Modal·Fade·Spotlight
 M: UGUI·UITK mixed Focus
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityCursor = UnityEngine.Cursor;
using UGUIButton = UnityEngine.UI.Button;
using UGUIImage = UnityEngine.UI.Image;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.Primitive;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// UI native 표시와 mixed Focus의 대표 Runtime 통합 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_UIPresentation
    {

    #region 헬퍼

        // ======================================================================
        /// <summary>
        /// VisualElement Root를 공통 Layer Registry에 제공하는 테스트 Driver.
        /// </summary>
        // ======================================================================
        private sealed class TestUITKLayerDriver : IPresentationLayerDriver<VisualElement>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// View를 배치할 테스트 VisualElement Root.
            /// </summary>
            // ------------------------------------------------------------
            public VisualElement Root { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 VisualElement를 Root로 사용하는 Driver를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestUITKLayerDriver(VisualElement root) : base()
            {
                Root = root;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Layer Asset과 Root가 존재하는지 검증한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool Validate(PresentationLayerAsset asset, out string error)
            {
                error = asset == null || Root == null ? "invalid" : "";
                return string.IsNullOrEmpty(error);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 이 테스트 Driver에서는 native Order 적용을 생략한다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetOrder(int order)
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Root의 표시 상태를 적용한다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetActive(bool active)
            {
                Root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ============================================================
        /// <summary>
        /// UGUI Screen View를 생성하고 반환하는 테스트 Source.
        /// </summary>
        // ============================================================
        private sealed class TestUGUIScreenSource : IScreenSource
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 마지막으로 획득한 Screen의 기본 Focus.
            /// </summary>
            // ------------------------------------------------------------
            public GameObject DefaultFocus { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 누적 Screen 반환 호출 수.
            /// </summary>
            // ------------------------------------------------------------
            public int ReleaseCount { get; private set; }

            private GameObject root = null;

            // ------------------------------------------------------------
            /// <summary>
            /// RectTransform Layer에 UGUI Screen과 기본 Focus를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public ScreenInstance Acquire(ScreenViewScope scope)
            {
                if (!(scope.Layer is IPresentationLayerDriver<RectTransform> layer))
                {
                    throw new InvalidOperationException("RectTransform Layer가 필요합니다.");
                }

                root = new GameObject
                (
                    "UGUI Screen",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(UGUIScreenDriver)
                );
                root.transform.SetParent(layer.Root, false);
                DefaultFocus = new GameObject
                (
                    "UGUI Default Focus",
                    typeof(RectTransform),
                    typeof(UGUIButton)
                );
                DefaultFocus.transform.SetParent(root.transform, false);
                var driver = root.GetComponent<UGUIScreenDriver>();
                SetField(driver, "root", root);
                SetField(driver, "canvasGroup", root.GetComponent<CanvasGroup>());
                SetField(driver, "defaultFocus", DefaultFocus);
                return new ScreenInstance(driver);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Source가 생성한 UGUI Screen을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release(ScreenInstance instance)
            {
                ReleaseCount++;
                UnityEngine.Object.Destroy(root);
                root = null;
                DefaultFocus = null;
            }
        }

        // ======================================================================
        /// <summary>
        /// UITK Screen View를 생성하고 Visual Tree에서 제거하는 테스트 Source.
        /// </summary>
        // ======================================================================
        private sealed class TestUITKScreenSource : IScreenSource
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 마지막으로 획득한 Screen의 기본 Focus.
            /// </summary>
            // ------------------------------------------------------------
            public VisualElement DefaultFocus { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 누적 Screen 반환 호출 수.
            /// </summary>
            // ------------------------------------------------------------
            public int ReleaseCount { get; private set; }

            private VisualElement root = null;

            // ------------------------------------------------------------
            /// <summary>
            /// VisualElement Layer에 UITK Screen과 기본 Focus를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public ScreenInstance Acquire(ScreenViewScope scope)
            {
                if (!(scope.Layer is IPresentationLayerDriver<VisualElement> layer))
                {
                    throw new InvalidOperationException("VisualElement Layer가 필요합니다.");
                }

                root = new VisualElement
                {
                    name = "UITK Screen",
                };
                DefaultFocus = new Button
                {
                    name = "UITK Default Focus",
                };
                root.Add(DefaultFocus);
                layer.Root.Add(root);
                return new ScreenInstance(new UITKScreenDriver(root, DefaultFocus));
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Source가 생성한 UITK Screen을 Visual Tree에서 제거한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release(ScreenInstance instance)
            {
                ReleaseCount++;
                root.RemoveFromHierarchy();
                root = null;
                DefaultFocus = null;
            }
        }

        // ============================================================
        /// <summary>
        /// DOTween이 적용한 Presentation 값을 기록하는 테스트 Target.
        /// </summary>
        // ============================================================
        private sealed class TestTransitionTarget : IValueSetter<float>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 마지막으로 적용된 진행 값.
            /// </summary>
            // ------------------------------------------------------------
            public float Value { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Transition 진행 값을 기록한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Set(float value)
            {
                Value = value;
            }
        }

        // ============================================================
        /// <summary>
        /// 요청 값을 즉시 적용하고 Transition을 완료하는 테스트 구현.
        /// </summary>
        // ============================================================
        private sealed class ImmediateTransitioner : IPresentationTransitioner
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 요청한 마지막 값을 적용하고 즉시 완료한다.
            /// </summary>
            // ------------------------------------------------------------
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

            // ------------------------------------------------------------
            /// <summary>
            /// 이 테스트 구현에는 별도 소유 자원이 없다.
            /// </summary>
            // ------------------------------------------------------------
            public void Dispose()
            {
                // NONE
            }
        }

        // ============================================================
        /// <summary>
        /// Screen별 Input Session 획득과 반환만 수행하는 테스트 구현.
        /// </summary>
        // ============================================================
        private sealed class TestInputDriver : IScreenInputDriver
        {
            private readonly List<ScreenInputSession> sessions =
                new List<ScreenInputSession>();

            // ------------------------------------------------------------
            /// <summary>
            /// Screen 입력 정책 Session을 획득한다.
            /// </summary>
            // ------------------------------------------------------------
            public ScreenInputSession Acquire(ScreenOptions options)
            {
                var session = new ScreenInputSession(options, Release);
                sessions.Add(session);
                return session;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 이 테스트 구현에서는 Batch 중간 적용이 없다.
            /// </summary>
            // ------------------------------------------------------------
            public void BeginBatch()
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 이 테스트 구현에서는 Batch 종료 적용이 없다.
            /// </summary>
            // ------------------------------------------------------------
            public void EndBatch()
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 남은 Input Session을 모두 종결한다.
            /// </summary>
            // ------------------------------------------------------------
            public void ForceReleaseAll()
            {
                for (var i = sessions.Count - 1; i >= 0; i--)
                {
                    sessions[i].MarkReleased();
                }

                sessions.Clear();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 남은 Input Session을 모두 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Dispose()
            {
                ForceReleaseAll();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 Input Session을 소유 목록에서 제거하고 종결한다.
            /// </summary>
            // ------------------------------------------------------------
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// PanelRenderer와 UITK Layer backend를 테스트 Panel로 조립한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static UITKLayerPanel CreatePanelLayer
        (
            GameObject host,
            PanelSettings panelSettings,
            bool activate = true
        )
        {
            host.SetActive(false);
            var renderer = host.AddComponent<PanelRenderer>();
            renderer.panelSettings = panelSettings;
            var layer = host.AddComponent<UITKLayerPanel>();

            if (activate)
            {
                layer.SetActive(true);
            }

            return layer;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 Layer Asset을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static PresentationLayerAsset CreateLayerAsset
        (
            string id,
            int order
        )
        {
            var asset = ScriptableObject.CreateInstance<PresentationLayerAsset>();
            SetField(asset, "id", id);
            SetField(asset, "order", order);
            return asset;
        }

    #endregion

    #region O-1: mixed Layer Order

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 같은 Screen Overlay에서 더 높은 Layer Order의 UGUI 또는 UITK가
        /// <br/> 기술과 관계없이 실제 최종 픽셀의 전면에 렌더링되는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_PresentationLayerRegistry_UGUI와UITK교차Order_높은Layer가전면렌더링()
        {
            var uguiObject = new GameObject
            (
                "UGUI Order Layer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUILayerCanvas)
            );
            uguiObject.SetActive(false);
            var imageObject = new GameObject
            (
                "UGUI Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UGUIImage)
            );
            imageObject.transform.SetParent(uguiObject.transform, false);
            var imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageObject.GetComponent<UGUIImage>().color = Color.red;

            var ugui = uguiObject.GetComponent<UGUILayerCanvas>();
            uguiObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            SetField(ugui, "root", uguiObject.GetComponent<RectTransform>());
            SetField(ugui, "canvas", uguiObject.GetComponent<Canvas>());

            var uitkObject = new GameObject("UITK Order Layer");
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var uitk = CreatePanelLayer(uitkObject, panelSettings, false);

            var uguiAsset = CreateLayerAsset("UGUI Order", 10);
            var uitkAsset = CreateLayerAsset("UITK Order", 20);
            var registry = new PresentationLayerRegistry();
            PresentationLayerHandle uguiHandle = null;
            PresentationLayerHandle uitkHandle = null;

            try
            {
                uguiHandle = registry.Register(uguiAsset, ugui);
                uitkHandle = registry.Register(uitkAsset, uitk);

                var layerRoot = uitk.Root;
                layerRoot.style.position = Position.Absolute;
                layerRoot.style.left = 0.0f;
                layerRoot.style.top = 0.0f;
                layerRoot.style.right = 0.0f;
                layerRoot.style.bottom = 0.0f;
                layerRoot.style.backgroundColor = Color.blue;

                yield return null;
                yield return new WaitForEndOfFrame();

                var screenshot = ScreenCapture.CaptureScreenshotAsTexture();

                try
                {
                    var color = screenshot.GetPixel
                    (
                        screenshot.width / 2,
                        screenshot.height / 2
                    );
                    Assert.Greater(color.b, color.r);
                }
                finally
                {
                    UnityEngine.Object.Destroy(screenshot);
                }

                uitkHandle.Dispose();
                uitkHandle = null;
                uguiHandle.Dispose();
                uguiHandle = null;
                SetField(uguiAsset, "order", 30);
                SetField(uitkAsset, "order", 20);
                uguiHandle = registry.Register(uguiAsset, ugui);
                uitkHandle = registry.Register(uitkAsset, uitk);

                layerRoot = uitk.Root;
                layerRoot.style.position = Position.Absolute;
                layerRoot.style.left = 0.0f;
                layerRoot.style.top = 0.0f;
                layerRoot.style.right = 0.0f;
                layerRoot.style.bottom = 0.0f;
                layerRoot.style.backgroundColor = Color.blue;

                yield return null;
                yield return new WaitForEndOfFrame();

                screenshot = ScreenCapture.CaptureScreenshotAsTexture();

                try
                {
                    var color = screenshot.GetPixel
                    (
                        screenshot.width / 2,
                        screenshot.height / 2
                    );
                    Assert.Greater(color.r, color.b);
                }
                finally
                {
                    UnityEngine.Object.Destroy(screenshot);
                }
            }
            finally
            {
                uitkHandle?.Dispose();
                uguiHandle?.Dispose();
                registry.Dispose();
                UnityEngine.Object.Destroy(uitkObject);
                UnityEngine.Object.Destroy(uguiObject);
                UnityEngine.Object.Destroy(panelSettings);
                UnityEngine.Object.Destroy(uitkAsset);
                UnityEngine.Object.Destroy(uguiAsset);
            }
        }

    #endregion

    #region I-1: Input System 입력 해제 장벽

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 실제 해제 입력이 끝난 뒤 여러 Session을 같은 갱신에서 제거하고,
        /// <br/> 각 완료 callback이 최종 기준 Cursor 상태를 관찰하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_InputSystemScreenInputDriver_다중해제완료_Callback이최종Cursor만관찰()
        {
            var baselineCursorVisible = UnityCursor.visible;
            var baselineCursorLockMode = UnityCursor.lockState;
            var host = new GameObject("Input Host");
            var inputModule = host.AddComponent<InputSystemUIInputModule>();
            var driver = host.AddComponent<InputSystemScreenInputDriver>();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();
            Keyboard keyboard = null;

            try
            {
                keyboard = InputSystem.AddDevice<Keyboard>();
                var ui = new InputActionMap("UI");
                ui.AddAction("Cancel", InputActionType.Button)
                    .AddBinding("<Keyboard>/escape");
                var gameplay = new InputActionMap("Player");
                gameplay.AddAction("Cancel", InputActionType.Button)
                    .AddBinding("<Keyboard>/escape");
                actions.AddActionMap(ui);
                actions.AddActionMap(gameplay);
                gameplay.Enable();
                inputModule.actionsAsset = actions;
                SetField(settings, "uiActionMap", "UI");
                SetField(settings, "gameplayActionsAsset", actions);
                SetField(settings, "gameplayActionMap", "Player");
                SetField
                (
                    settings,
                    "releaseActionNames",
                    new[]
                    {
                        "Cancel",
                    }
                );

                UnityCursor.visible = false;
                driver.Initialize(inputModule, settings);
                var lower = driver.Acquire
                (
                    new ScreenOptions
                    (
                        "Lower",
                        "Screen",
                        showsCursor: true
                    )
                );
                var upper = driver.Acquire
                (
                    new ScreenOptions
                    (
                        "Upper",
                        "Screen",
                        showsCursor: false
                    )
                );
                bool? lowerObservedCursorVisible = null;
                bool? upperObservedCursorVisible = null;

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                lower.Release
                (
                    waitForInputRelease: true,
                    retainCursorWhileAwaitingRelease: true,
                    onReleaseCompleted: () => lowerObservedCursorVisible = UnityCursor.visible
                );
                upper.Release
                (
                    waitForInputRelease: true,
                    retainCursorWhileAwaitingRelease: true,
                    onReleaseCompleted: () => upperObservedCursorVisible = UnityCursor.visible
                );

                Assert.IsTrue(lower.IsAwaitingRelease);
                Assert.IsTrue(upper.IsAwaitingRelease);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                yield return null;
                yield return null;
                yield return null;

                Assert.IsTrue(lower.IsReleased);
                Assert.IsTrue(upper.IsReleased);
                Assert.AreEqual(false, lowerObservedCursorVisible);
                Assert.AreEqual(false, upperObservedCursorVisible);
                Assert.IsFalse(UnityCursor.visible);
            }
            finally
            {
                driver.Dispose();

                if (keyboard != null)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                UnityCursor.visible = baselineCursorVisible;
                UnityCursor.lockState = baselineCursorLockMode;
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(actions);
                UnityEngine.Object.Destroy(settings);
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 전체 해제 callback에서 획득한 새 Session을 계속 추적하고,
        /// <br/> 마지막 해제 뒤 Gameplay Action별 기준 활성 상태를 정확히 복원하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_InputSystemScreenInputDriver_전체해제재진입_Action별기준상태복원()
        {
            var baselineCursorVisible = UnityCursor.visible;
            var baselineCursorLockMode = UnityCursor.lockState;
            var host = new GameObject("Input Host");
            var inputModule = host.AddComponent<InputSystemUIInputModule>();
            var driver = host.AddComponent<InputSystemScreenInputDriver>();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();
            Keyboard keyboard = null;

            try
            {
                keyboard = InputSystem.AddDevice<Keyboard>();
                var ui = new InputActionMap("UI");
                ui.AddAction("Cancel", InputActionType.Button)
                    .AddBinding("<Keyboard>/escape");
                var gameplay = new InputActionMap("Player");
                var move = gameplay.AddAction("Move", InputActionType.Value);
                var attack = gameplay.AddAction("Attack", InputActionType.Button);
                actions.AddActionMap(ui);
                actions.AddActionMap(gameplay);
                move.Enable();
                inputModule.actionsAsset = actions;
                SetField(settings, "uiActionMap", "UI");
                SetField(settings, "gameplayActionsAsset", actions);
                SetField(settings, "gameplayActionMap", "Player");
                SetField
                (
                    settings,
                    "releaseActionNames",
                    new[]
                    {
                        "Cancel",
                    }
                );
                driver.Initialize(inputModule, settings);
                var baselineUIEnabled = ui.enabled;
                var closing = driver.Acquire(new ScreenOptions("Closing", "Screen"));
                ScreenInputSession nested = null;

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                closing.Release
                (
                    waitForInputRelease: true,
                    onReleaseCompleted: () =>
                    {
                        nested = driver.Acquire(new ScreenOptions("Nested", "Screen"));
                    }
                );

                driver.ForceReleaseAll();

                Assert.IsTrue(closing.IsReleased);
                Assert.IsNotNull(nested);
                Assert.IsFalse(nested.IsReleased);
                Assert.IsFalse(move.enabled);
                Assert.IsFalse(attack.enabled);

                nested.Release(false);

                Assert.IsTrue(nested.IsReleased);
                Assert.IsTrue(move.enabled);
                Assert.IsFalse(attack.enabled);
                Assert.AreEqual(baselineUIEnabled, ui.enabled);
            }
            finally
            {
                driver.Dispose();

                if (keyboard != null)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                UnityCursor.visible = baselineCursorVisible;
                UnityCursor.lockState = baselineCursorLockMode;
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(actions);
                UnityEngine.Object.Destroy(settings);
            }

            yield return null;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> UI Map의 일부 Action만 켜져 있어도 Screen 획득 중에는 전체를 활성화하고,
        /// <br/> 마지막 해제 뒤에는 Action별 기준 상태를 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_InputSystemScreenInputDriver_UIMap부분활성_획득전체활성후개별복원()
        {
            var host = new GameObject("Input Host");
            var inputModule = host.AddComponent<InputSystemUIInputModule>();
            var driver = host.AddComponent<InputSystemScreenInputDriver>();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();

            try
            {
                var ui = new InputActionMap("UI");
                var cancel = ui.AddAction("Cancel", InputActionType.Button);
                var submit = ui.AddAction("Submit", InputActionType.Button);
                var gameplay = new InputActionMap("Player");
                gameplay.AddAction("Move", InputActionType.Value);
                actions.AddActionMap(ui);
                actions.AddActionMap(gameplay);
                cancel.Enable();
                inputModule.actionsAsset = actions;
                SetField(settings, "uiActionMap", "UI");
                SetField(settings, "gameplayActionsAsset", actions);
                SetField(settings, "gameplayActionMap", "Player");
                SetField(settings, "releaseActionNames", Array.Empty<string>());
                driver.Initialize(inputModule, settings);
                ui.Disable();
                cancel.Enable();
                Assert.IsTrue(cancel.enabled);
                Assert.IsFalse(submit.enabled);

                var session = driver.Acquire(new ScreenOptions("Menu", "Screen"));

                Assert.IsTrue(cancel.enabled);
                Assert.IsTrue(submit.enabled);

                session.Release(false);

                Assert.IsTrue(cancel.enabled);
                Assert.IsFalse(submit.enabled);
            }
            finally
            {
                driver.Dispose();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(actions);
                UnityEngine.Object.Destroy(settings);
            }

            yield return null;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Runtime에 구성되지 않은 전역 Action은 마지막 UI 입력 장치를 변경하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_InputSystemScreenInputDriver_비구성전역Action_마지막장치변경안함()
        {
            var host = new GameObject("Input Host");
            var inputModule = host.AddComponent<InputSystemUIInputModule>();
            var driver = host.AddComponent<InputSystemScreenInputDriver>();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var externalActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();
            Keyboard keyboard = null;
            Mouse mouse = null;

            try
            {
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse = InputSystem.AddDevice<Mouse>();
                var ui = new InputActionMap("UI");
                ui.AddAction("Submit", InputActionType.Button)
                    .AddBinding("<Keyboard>/space");
                var gameplay = new InputActionMap("Player");
                gameplay.AddAction("Move", InputActionType.Button)
                    .AddBinding("<Keyboard>/q");
                actions.AddActionMap(ui);
                actions.AddActionMap(gameplay);
                var debug = new InputActionMap("Debug");
                debug.AddAction("Click", InputActionType.Button)
                    .AddBinding("<Mouse>/leftButton");
                externalActions.AddActionMap(debug);
                inputModule.actionsAsset = actions;
                SetField(settings, "uiActionMap", "UI");
                SetField(settings, "gameplayActionsAsset", actions);
                SetField(settings, "gameplayActionMap", "Player");
                SetField(settings, "releaseActionNames", Array.Empty<string>());
                driver.Initialize(inputModule, settings);
                var session = driver.Acquire(new ScreenOptions("Menu", "Screen"));
                debug.Enable();

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                InputSystem.Update();
                Assert.AreSame(keyboard, driver.LastInputDevice);

                InputSystem.QueueStateEvent
                (
                    mouse,
                    new MouseState().WithButton
                    (
                        UnityEngine.InputSystem.LowLevel.MouseButton.Left
                    )
                );
                InputSystem.Update();

                Assert.AreSame(keyboard, driver.LastInputDevice);
                session.Release(false);
            }
            finally
            {
                driver.Dispose();

                if (mouse != null)
                {
                    InputSystem.RemoveDevice(mouse);
                }

                if (keyboard != null)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(actions);
                UnityEngine.Object.Destroy(externalActions);
                UnityEngine.Object.Destroy(settings);
            }

            yield return null;
        }

    #endregion

    #region P-1: DOTween Presentation Transition

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 실제 DOTween backend가 0이 아닌 시간의 Transition을 완료 값까지 재생하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_DOTweenPresentationTransitioner_실제Tween_완료값적용()
        {
            var transitioner = new DOTweenPresentationTransitioner();
            var target = new TestTransitionTarget();
            PresentationTransitionHandle handle = null;
            var completed = false;
            Exception failure = null;

            try
            {
                handle = transitioner.Play
                (
                    new PresentationTransitionParams
                    (
                        target,
                        0.0f,
                        1.0f,
                        0.05f,
                        true
                    ),
                    () => completed = true,
                    exception => failure = exception
                );

                for (var i = 0; i < 120 && !completed && failure == null; i++)
                {
                    yield return null;
                }

                Assert.IsNull(failure);
                Assert.IsTrue(completed);
                Assert.IsTrue(handle.IsCompleted);
                Assert.AreEqual(1.0f, target.Value, 0.001f);
            }
            finally
            {
                handle?.Cancel();
                transitioner.Dispose();
            }
        }

    #endregion

    #region U-1: UGUI Runtime

        // ----------------------------------------------------------------------
        /// <summary>
        /// 비점유 Blocker가 활성화될 때 직렬화 표시 상태를 즉시 비활성으로 맞춘다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUIInteractionBlocker_최초활성_비점유상태동기화()
        {
            var host = new GameObject("Interaction Blocker");
            host.SetActive(false);
            var root = new GameObject("Blocker Root", typeof(CanvasGroup));
            root.transform.SetParent(host.transform, false);
            var canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            var blocker = host.AddComponent<UGUIInteractionBlocker>();
            SetField(blocker, "root", root);
            SetField(blocker, "canvasGroup", canvasGroup);

            try
            {
                host.SetActive(true);
                yield return null;

                Assert.IsFalse(root.activeSelf);
                Assert.IsFalse(canvasGroup.interactable);
                Assert.IsFalse(canvasGroup.blocksRaycasts);

                var lease = blocker.Acquire();
                blocker.enabled = false;

                Assert.IsFalse(root.activeSelf);
                Assert.IsFalse(canvasGroup.blocksRaycasts);

                blocker.enabled = true;

                Assert.IsTrue(root.activeSelf);
                Assert.IsTrue(canvasGroup.blocksRaycasts);
                lease.Dispose();
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Spotlight 대상이 비활성화되면 dim과 입력 차단을 비우고,
        /// <br/> Driver 비활성화 시 별도 표시 Root까지 닫는다.
        /// </summary>
        // ------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUISpotlightDriver_대상과Driver비활성_표시입력정리()
        {
            var host = new GameObject("UGUI Spotlight Driver");
            host.SetActive(false);
            var root = new GameObject
            (
                "Spotlight Root",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UGUISpotlightGraphic)
            );
            root.transform.SetParent(host.transform, false);
            var target = new GameObject("Spotlight Target", typeof(RectTransform));
            target.transform.SetParent(host.transform, false);
            var graphic = root.GetComponent<UGUISpotlightGraphic>();
            var driver = host.AddComponent<UGUISpotlightDriver>();
            SetField(driver, "root", root);
            SetField(driver, "graphic", graphic);

            try
            {
                host.SetActive(true);
                driver.Show
                (
                    new UGUISpotlightParams
                    (
                        new[]
                        {
                            new UGUISpotlightTarget
                            (
                                target.GetComponent<RectTransform>()
                            ),
                        }
                    )
                );
                yield return null;

                Assert.IsTrue(root.activeSelf);
                Assert.IsTrue(graphic.enabled);
                Assert.AreEqual(1, graphic.HoleCount);
                Assert.IsTrue(graphic.raycastTarget);

                target.SetActive(false);
                yield return null;

                Assert.IsFalse(graphic.enabled);
                Assert.AreEqual(0, graphic.HoleCount);
                Assert.IsFalse(graphic.raycastTarget);

                target.SetActive(true);
                yield return null;

                Assert.IsTrue(graphic.enabled);
                Assert.AreEqual(1, graphic.HoleCount);
                Assert.IsTrue(graphic.raycastTarget);

                driver.enabled = false;

                Assert.IsFalse(root.activeSelf);
                Assert.AreEqual(0, graphic.HoleCount);
                Assert.IsFalse(graphic.raycastTarget);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Layout Controller가 활성화 시점에 Safe Area를 즉시 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUISafeAreaLayout_최초활성_SafeArea즉시반영()
        {
            var host = new GameObject("Layout Controller");
            host.SetActive(false);
            var safeAreaObject = new GameObject("Safe Area", typeof(RectTransform));
            safeAreaObject.transform.SetParent(host.transform, false);
            var safeAreaRoot = safeAreaObject.GetComponent<RectTransform>();
            safeAreaRoot.anchorMin = Vector2.zero;
            safeAreaRoot.anchorMax = Vector2.zero;
            safeAreaRoot.offsetMin = Vector2.one;
            safeAreaRoot.offsetMax = Vector2.one;
            var layout = host.AddComponent<UGUISafeAreaLayout>();
            SetField(layout, "safeAreaRoot", safeAreaRoot);

            try
            {
                host.SetActive(true);
                yield return null;

                var width = Mathf.Max(1, Screen.width);
                var height = Mathf.Max(1, Screen.height);
                var area = Screen.safeArea;
                Assert.AreEqual
                (
                    new Vector2(area.xMin / width, area.yMin / height),
                    safeAreaRoot.anchorMin
                );
                Assert.AreEqual
                (
                    new Vector2(area.xMax / width, area.yMax / height),
                    safeAreaRoot.anchorMax
                );
                Assert.AreEqual(Vector2.zero, safeAreaRoot.offsetMin);
                Assert.AreEqual(Vector2.zero, safeAreaRoot.offsetMax);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 실제 Canvas에서 Layer Order와 Screen·Modal·Fade 표시 값이 적용된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUIPresentation_RuntimeCanvas_대표표시경로()
        {
            var canvasRoot = new GameObject
            (
                "UGUI Canvas Root",
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
            layerObject.transform.SetParent(canvasRoot.transform, false);
            var screenObject = new GameObject
            (
                "UGUI Screen",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(UGUIScreenDriver),
                typeof(UGUIModalInteractionDriver)
            );
            screenObject.transform.SetParent(layerObject.transform, false);
            var fadeObject = new GameObject
            (
                "Fade",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UGUIImage),
                typeof(CanvasGroup),
                typeof(UGUISceneFadeDriver)
            );
            fadeObject.transform.SetParent(layerObject.transform, false);
            var asset = CreateLayerAsset("UGUI", 17);
            var registry = new PresentationLayerRegistry();

            try
            {
                var layer = layerObject.GetComponent<UGUILayerCanvas>();
                var canvas = layerObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                SetField(layer, "root", layerObject.GetComponent<RectTransform>());
                SetField(layer, "canvas", canvas);
                var layerHandle = registry.Register(asset, layer);
                var screen = screenObject.GetComponent<UGUIScreenDriver>();
                SetField(screen, "root", screenObject);
                SetField(screen, "canvasGroup", screenObject.GetComponent<CanvasGroup>());
                var modal = screenObject.GetComponent<UGUIModalInteractionDriver>();
                SetField(modal, "canvasGroup", screenObject.GetComponent<CanvasGroup>());
                var fade = fadeObject.GetComponent<UGUISceneFadeDriver>();
                SetField(fade, "image", fadeObject.GetComponent<UGUIImage>());
                SetField(fade, "canvasGroup", fadeObject.GetComponent<CanvasGroup>());

                screen.Visibility.Set(true);
                screen.SetInteractable(true);
                screen.Alpha.Set(0.5f);
                modal.SetTop(true);
                fade.SetColor(Color.black);
                fade.Alpha.Set(1.0f);
                yield return null;

                Assert.IsTrue(canvas.overrideSorting);
                Assert.AreEqual(17, canvas.sortingOrder);
                Assert.AreEqual(0.5f, screenObject.GetComponent<CanvasGroup>().alpha);
                Assert.IsTrue(screenObject.GetComponent<CanvasGroup>().interactable);
                Assert.IsTrue(screenObject.GetComponent<CanvasGroup>().blocksRaycasts);
                Assert.AreEqual(1.0f, fadeObject.GetComponent<CanvasGroup>().alpha);

                layerHandle.Dispose();
                Assert.IsFalse(layerObject.activeSelf);
            }
            finally
            {
                registry.Dispose();
                UnityEngine.Object.Destroy(canvasRoot);
                UnityEngine.Object.Destroy(asset);
            }
        }

    #endregion

    #region T-1: UITK Runtime

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 실제 Panel의 Spotlight가 대상 구멍에서는 Pointer를 통과시키고,
        /// <br/> dim 영역만 차단하며 대상이 숨겨지면 전체 입력 잠금 없이
        /// <br/> 표시와 Picking을 함께 비우는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKSpotlight_구멍통과와대상숨김_입력차단해제()
        {
            var host = new GameObject("UITK Spotlight Panel");
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var layer = CreatePanelLayer(host, panelSettings);
            var panelRoot = layer.Root;
            var container = new VisualElement
            {
                name = "Spotlight Layer",
            };
            container.style.width = 400.0f;
            container.style.height = 300.0f;
            var target = new Button
            {
                name = "Spotlight Target",
            };
            target.style.position = Position.Absolute;
            target.style.left = 50.0f;
            target.style.top = 40.0f;
            target.style.width = 120.0f;
            target.style.height = 60.0f;
            var element = new UITKSpotlightElement();
            panelRoot.Add(container);
            container.Add(target);
            container.Add(element);
            var spotlight = new UITKSpotlight();
            Lease lease = null;

            try
            {
                yield return null;
                yield return null;

                lease = spotlight.Show
                (
                    element,
                    new UITKSpotlightParams
                    (
                        new[]
                        {
                            new UITKSpotlightTarget(target),
                        }
                    )
                );
                yield return null;

                var targetCenter = element.WorldToLocal(target.worldBound.center);
                var dimPoint = new Vector2
                (
                    element.localBound.xMax - 5.0f,
                    element.localBound.yMax - 5.0f
                );
                Assert.AreEqual(1, element.HoleCount);
                Assert.IsFalse(element.ContainsPoint(targetCenter));
                Assert.IsTrue(element.ContainsPoint(dimPoint));

                target.style.display = DisplayStyle.None;
                yield return null;

                Assert.AreEqual(0, element.HoleCount);
                Assert.AreEqual(PickingMode.Ignore, element.pickingMode);
            }
            finally
            {
                lease?.Dispose();
                spotlight.Dispose();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(panelSettings);
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 서로 다른 UITK Panel의 사용자 Focus 이동을 추적하고 이전 Panel Focus를 비운다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKFocusDriver_다중Panel사용자Focus_단일현재선택유지()
        {
            var driverHost = new GameObject("UITK Focus Driver");
            var firstHost = new GameObject("First Panel");
            var secondHost = new GameObject("Second Panel");
            var firstSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var secondSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var firstLayer = CreatePanelLayer(firstHost, firstSettings);
            var secondLayer = CreatePanelLayer(secondHost, secondSettings);
            var firstRoot = firstLayer.Root;
            var secondRoot = secondLayer.Root;
            var driver = driverHost.AddComponent<UITKFocusDriver>();
            var focus = driverHost.AddComponent<UIFocusDriver>();
            var first = new Button
            {
                name = "First",
            };
            var second = new Button
            {
                name = "Second",
            };
            firstRoot.Add(first);
            secondRoot.Add(second);

            try
            {
                yield return null;
                yield return null;

                focus.RegisterLayer
                (
                    new TestUITKLayerDriver(firstRoot)
                );
                focus.RegisterLayer
                (
                    new TestUITKLayerDriver(secondRoot)
                );
                first.Focus();
                yield return null;
                second.Focus();
                yield return null;

                Assert.AreSame(second, driver.Current);
                Assert.AreNotSame
                (
                    first,
                    first.panel.focusController.focusedElement
                );

                driver.Select(null);

                Assert.IsNull(driver.Current);
                Assert.AreNotSame
                (
                    second,
                    second.panel.focusController.focusedElement
                );
            }
            finally
            {
                UnityEngine.Object.Destroy(driverHost);
                UnityEngine.Object.Destroy(firstHost);
                UnityEngine.Object.Destroy(secondHost);
                UnityEngine.Object.Destroy(firstSettings);
                UnityEngine.Object.Destroy(secondSettings);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 실제 Panel에서 Focus와 Screen·Modal·Fade VisualElement 상태가 적용된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKPresentation_RuntimePanel_대표표시경로()
        {
            var host = new GameObject("UITK Panel");
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var layer = CreatePanelLayer(host, panelSettings);
            var panelRoot = layer.Root;
            var focus = host.AddComponent<UITKFocusDriver>();
            var screenRoot = new VisualElement
            {
                name = "Screen",
            };
            var button = new Button
            {
                name = "DefaultFocus",
            };
            var fadeRoot = new VisualElement
            {
                name = "Fade",
            };
            panelRoot.Add(screenRoot);
            screenRoot.Add(button);
            panelRoot.Add(fadeRoot);

            try
            {
                yield return null;
                yield return null;

                var screen = new UITKScreenDriver(screenRoot, button);
                var modal = new UITKModalInteractionDriver(screenRoot);
                var fade = new UITKSceneFadeDriver(fadeRoot);
                screen.Visibility.Set(true);
                screen.SetInteractable(true);
                screen.Alpha.Set(0.6f);
                modal.SetTop(true);
                fade.SetColor(Color.black);
                fade.Alpha.Set(1.0f);
                focus.Select(button);
                yield return null;

                Assert.AreSame(button, focus.Current);
                Assert.AreEqual(0.6f, screenRoot.style.opacity.value);
                Assert.IsTrue(screenRoot.enabledSelf);
                Assert.AreEqual(PickingMode.Position, screenRoot.pickingMode);
                Assert.AreEqual(1.0f, fadeRoot.style.opacity.value);
                Assert.AreEqual(PickingMode.Position, fadeRoot.pickingMode);

                focus.Select(null);
                Assert.IsNull(focus.Current);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(panelSettings);
            }
        }

    #endregion

    #region M-1: mixed Focus

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> UGUI와 UITK 대상을 교대로 선택할 때 이전 native Focus를 비우고,
        /// <br/> 사용자가 UITK Focus를 직접 이동해도 공통 Driver가 실제 Element를 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UIFocusDriver_UGUI와UITK교차선택_단일현재Focus유지()
        {
            var host = new GameObject("Mixed Focus Host");
            var eventSystem = host.AddComponent<EventSystem>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var panelLayer = CreatePanelLayer(host, panelSettings);
            var panelRoot = panelLayer.Root;
            var uguiFocus = host.AddComponent<UGUIFocusDriver>();
            var uitkFocus = host.AddComponent<UITKFocusDriver>();
            var focus = host.AddComponent<UIFocusDriver>();
            SetField(uguiFocus, "eventSystem", eventSystem);

            var uguiLayerObject = new GameObject
            (
                "UGUI Layer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUILayerCanvas)
            );
            uguiLayerObject.transform.SetParent(host.transform, false);
            var uguiLayer = uguiLayerObject.GetComponent<UGUILayerCanvas>();
            SetField(uguiLayer, "root", uguiLayerObject.GetComponent<RectTransform>());
            SetField(uguiLayer, "canvas", uguiLayerObject.GetComponent<Canvas>());
            var uguiTarget = new GameObject
            (
                "UGUI Focus",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Button)
            );
            uguiTarget.transform.SetParent(uguiLayerObject.transform, false);
            var uitkTarget = new Button
            {
                name = "UITK Focus",
            };
            panelRoot.Add(uitkTarget);

            try
            {
                yield return null;
                yield return null;
                focus.Initialize();
                focus.RegisterLayer(uguiLayer);

                focus.Select(uguiTarget);

                Assert.AreSame(uguiTarget, eventSystem.currentSelectedGameObject);
                Assert.AreSame(uguiTarget, focus.Current);

                focus.Select(uitkTarget);
                yield return null;

                Assert.AreNotSame(uguiTarget, eventSystem.currentSelectedGameObject);
                Assert.AreSame(uitkTarget, focus.Current);

                focus.Select(uguiTarget);

                Assert.AreSame(uguiTarget, eventSystem.currentSelectedGameObject);
                Assert.AreSame(uguiTarget, focus.Current);
                Assert.AreNotSame(uitkTarget, uitkFocus.Current);

                uitkTarget.Focus();
                yield return null;

                Assert.AreSame(uitkTarget, uitkFocus.Current);
                Assert.AreSame(uitkTarget, focus.Current);

                focus.Select(null);
                yield return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(panelSettings);
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 한 Screen Stack에서 UGUI Screen 위에 UITK Screen을 열고 닫을 때,
        /// <br/> Source와 Layer Usage를 반환한 뒤 이전 UGUI Focus를 복원하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_ScreenController_UGUI에서UITK교차OpenClose_이전Focus와소유권복원()
        {
            var host = new GameObject("Mixed Screen Host");
            var eventSystem = host.AddComponent<EventSystem>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var panelLayer = CreatePanelLayer(host, panelSettings);
            var panelRoot = panelLayer.Root;
            var uguiFocus = host.AddComponent<UGUIFocusDriver>();
            var uitkFocus = host.AddComponent<UITKFocusDriver>();
            var focus = host.AddComponent<UIFocusDriver>();
            SetField(uguiFocus, "eventSystem", eventSystem);

            var uguiLayerObject = new GameObject
            (
                "UGUI Layer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUILayerCanvas)
            );
            uguiLayerObject.transform.SetParent(host.transform, false);
            uguiLayerObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var uguiLayer = uguiLayerObject.GetComponent<UGUILayerCanvas>();
            SetField(uguiLayer, "root", uguiLayerObject.GetComponent<RectTransform>());
            SetField(uguiLayer, "canvas", uguiLayerObject.GetComponent<Canvas>());

            var uguiAsset = CreateLayerAsset("UGUI", 0);
            var uitkAsset = CreateLayerAsset("UITK", 1);
            var layerRegistry = new PresentationLayerRegistry();
            var screenRegistry = new ScreenRegistry(layerRegistry);
            var transitioner = new ImmediateTransitioner();
            var input = new TestInputDriver();
            ScreenRegistrationHandle uguiRegistration = null;
            ScreenRegistrationHandle uitkRegistration = null;
            PresentationLayerHandle uguiLayerHandle = null;
            PresentationLayerHandle uitkLayerHandle = null;
            ScreenController controller = null;

            try
            {
                yield return null;
                yield return null;
                focus.Initialize();

                var uitkLayer = new TestUITKLayerDriver(panelRoot);
                uguiLayerHandle = layerRegistry.Register(uguiAsset, uguiLayer);
                uitkLayerHandle = layerRegistry.Register(uitkAsset, uitkLayer);
                focus.RegisterLayer(uguiLayer);
                focus.RegisterLayer(uitkLayer);
                var uguiSource = new TestUGUIScreenSource();
                var uitkSource = new TestUITKScreenSource();
                uguiRegistration = screenRegistry.Register
                (
                    new ScreenOptions
                    (
                        "UGUI",
                        "UGUI",
                        openDuration: 0.0f,
                        closeDuration: 0.0f
                    ),
                    uguiSource
                );
                uitkRegistration = screenRegistry.Register
                (
                    new ScreenOptions
                    (
                        "UITK",
                        "UITK",
                        openDuration: 0.0f,
                        closeDuration: 0.0f
                    ),
                    uitkSource
                );
                controller = new ScreenController
                (
                    screenRegistry,
                    layerRegistry,
                    transitioner,
                    new FocusController(focus),
                    input
                );
                controller.Activate();

                var uguiResponse = controller.Open("UGUI");

                Assert.IsTrue(uguiResponse.Accepted);
                Assert.AreSame(uguiSource.DefaultFocus, focus.Current);
                Assert.IsTrue(uguiLayerHandle.HasConsumers);

                var uitkResponse = controller.Open("UITK");
                yield return null;

                Assert.IsTrue(uitkResponse.Accepted);
                Assert.AreSame(uitkSource.DefaultFocus, focus.Current);
                Assert.IsTrue(uitkLayerHandle.HasConsumers);
                Assert.AreEqual(0, uguiSource.ReleaseCount);

                Assert.IsTrue(uitkResponse.Session.Close());
                yield return null;

                Assert.AreEqual(1, uitkSource.ReleaseCount);
                Assert.IsFalse(uitkLayerHandle.HasConsumers);
                Assert.AreSame(uguiSource.DefaultFocus, focus.Current);

                controller.Clear();

                Assert.AreEqual(1, uguiSource.ReleaseCount);
                Assert.IsFalse(uguiLayerHandle.HasConsumers);
            }
            finally
            {
                controller?.Clear();
                uitkRegistration?.Dispose();
                uguiRegistration?.Dispose();
                input.Dispose();
                transitioner.Dispose();
                screenRegistry.Dispose();
                uitkLayerHandle?.Dispose();
                uguiLayerHandle?.Dispose();
                layerRegistry.Dispose();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(panelSettings);
                UnityEngine.Object.Destroy(uitkAsset);
                UnityEngine.Object.Destroy(uguiAsset);
            }
        }

    #endregion

    }
}
