/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UIPresentation.cs
수정일 : 2026-10-03

# 설명
실제 Runtime Panel과 Canvas에서 UGUI·UITK 표시와 mixed Focus의 대표 경로를 검증한다.

# 테스트 구성
 I: Input System Map·해제 장벽·Context contribution
 P: DOTween Presentation Transition
 U: UGUI Layer·Screen·Modal·Fade·Layout
 T: UITK Panel·Focus·Screen·Modal·Fade·Spotlight
 M: UGUI·UITK mixed Focus
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

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
    using inonego.Xeri.UI.TEST;

    // ============================================================
    /// <summary>
    /// UI native 표시와 mixed Focus의 대표 Runtime 통합 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_UIPresentation
    {

    #region 헬퍼

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
        /// PanelRenderer와 UITK Native Output을 테스트 Panel로 조립한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static UITKPresentationOutput CreatePanelOutput
        (
            GameObject host,
            PanelSettings panelSettings,
            bool activate = true
        )
        {
            host.SetActive(false);
            host.AddComponent<PanelRenderer>();
            var output = host.AddComponent<UITKPresentationOutput>();
            output.Initialize(panelSettings, 0);
            host.SetActive(activate);
            return output;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// PlayMode 통합 테스트가 사용할 resolved Layer를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static PresentationPlanLayer CreateLayer
        (
            string id,
            int layerOrder,
            PresentationBackend backend
        )
        {
            return new PresentationPlanLayer(id, id, layerOrder, backend);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// resolved Layer와 semantic placement를 immutable 테스트 Plan으로 묶는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private static PresentationPlan CreatePlan
        (
            IList<PresentationPlanLayer> layers,
            params (string ID, PresentationPlanLayer Layer, int LocalOrder)[] placements
        )
        {
            var resolvedPlacements = new List<PresentationPlanPlacement>(placements.Length);

            for (var index = 0; index < placements.Length; index++)
            {
                var placement = placements[index];
                resolvedPlacements.Add
                (
                    new PresentationPlanPlacement
                    (
                        placement.ID,
                        placement.Layer,
                        placement.LocalOrder
                    )
                );
            }

            return new PresentationPlan(layers, resolvedPlacements);
        }

        private static VisualElement CreateSolidUITK(Color color)
        {
            var element = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            element.style.position = Position.Absolute;
            element.style.left = 0f;
            element.style.top = 0f;
            element.style.right = 0f;
            element.style.bottom = 0f;
            element.style.backgroundColor = color;
            return element;
        }

        private static UGUIImage CreateSolidUGUI
        (
            RectTransform parent,
            Color color
        )
        {
            var gameObject = new GameObject
            (
                "Cross Backend Color",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UGUIImage)
            );
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            var image = gameObject.GetComponent<UGUIImage>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Color CaptureCenterPixel()
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.IsNotNull(texture);
            Assert.Greater(texture.width, 0);
            Assert.Greater(texture.height, 0);

            try
            {
                return texture.GetPixel(texture.width / 2, texture.height / 2);
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static void AssertDominantChannel
        (
            Color color,
            int channel
        )
        {
            var values = new[]
            {
                color.r,
                color.g,
                color.b,
            };
            Assert.Greater(values[channel], 0.65f);

            for (var index = 0; index < values.Length; index++)
            {
                if (index == channel) continue;
                Assert.Less(values[index], 0.35f);
            }
        }

    #endregion

    #region I-1: 입력 시스템 입력 해제 장벽

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 여러 Session이 동시에 해제 대기해도 Screen topology가 선택한 Cursor owner만
        /// <br/> 자기 Cursor 정책을 유지하고 non-owner는 retain 요청을 무시하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_InputSystemScreenInputDriver_다중해제대기_CursorOwner만정책Retain()
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
                        showsCursor: true
                    )
                );
                var upper = driver.Acquire
                (
                    new ScreenOptions
                    (
                        "Upper",
                        showsCursor: false
                    )
                );
                upper.SetCursorPolicyEnabled(true);
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
                Assert.IsFalse(lower.RetainsCursorWhileAwaitingRelease);
                Assert.IsTrue(upper.RetainsCursorWhileAwaitingRelease);

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
                var closing = driver.Acquire(new ScreenOptions("Closing"));
                ScreenInputSession nested = null;

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                closing.Release
                (
                    waitForInputRelease: true,
                    onReleaseCompleted: () =>
                    {
                        nested = driver.Acquire(new ScreenOptions("Nested"));
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

                var session = driver.Acquire(new ScreenOptions("Menu"));

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
            var backgroundBehavior = InputSystem.settings.backgroundBehavior;
            Gamepad gamepad = null;
            Joystick joystick = null;

            try
            {
                InputSystem.settings.backgroundBehavior =
                    InputSettings.BackgroundBehavior.IgnoreFocus;
                gamepad = InputSystem.AddDevice<Gamepad>();
                joystick = InputSystem.AddDevice<Joystick>();
                InputSystem.EnableDevice(gamepad);
                InputSystem.EnableDevice(joystick);
                var ui = new InputActionMap("UI");
                var point = ui.AddAction("Point", InputActionType.PassThrough);
                point.AddBinding("<Gamepad>/leftStick");
                var gameplay = new InputActionMap("Player");
                gameplay.AddAction("Move", InputActionType.Value)
                    .AddBinding("<Gamepad>/rightStick");
                actions.AddActionMap(ui);
                actions.AddActionMap(gameplay);
                var debug = new InputActionMap("Debug");
                var debugMove = debug.AddAction("Move", InputActionType.PassThrough);
                debugMove.AddBinding("<Joystick>/stick");
                externalActions.AddActionMap(debug);
                inputModule.actionsAsset = actions;
                SetField(settings, "uiActionMap", "UI");
                SetField(settings, "gameplayActionsAsset", actions);
                SetField(settings, "gameplayActionMap", "Player");
                SetField(settings, "releaseActionNames", Array.Empty<string>());
                driver.Initialize(inputModule, settings);
                var session = driver.Acquire(new ScreenOptions("Menu"));
                debug.Enable();

                Assert.IsTrue(point.enabled);
                Assert.IsTrue(debugMove.enabled);

                var configuredValue = new Vector2(0.75f, 0.25f);
                InputSystem.QueueDeltaStateEvent(gamepad.leftStick, configuredValue);
                yield return null;

                Assert.Greater(point.ReadValue<Vector2>().sqrMagnitude, 0.0f);
                Assert.AreSame(gamepad, point.activeControl?.device);
                Assert.AreSame(gamepad, driver.LastInputDevice);

                var externalValue = new Vector2(0.5f, -0.5f);
                InputSystem.QueueDeltaStateEvent(joystick.stick, externalValue);
                yield return null;

                Assert.Greater(debugMove.ReadValue<Vector2>().sqrMagnitude, 0.0f);
                Assert.AreSame(joystick, debugMove.activeControl?.device);
                Assert.AreSame(gamepad, driver.LastInputDevice);
                session.Release(false);
            }
            finally
            {
                driver.Dispose();
                InputSystem.settings.backgroundBehavior = backgroundBehavior;

                if (joystick != null)
                {
                    InputSystem.RemoveDevice(joystick);
                }

                if (gamepad != null)
                {
                    InputSystem.RemoveDevice(gamepad);
                }

                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(actions);
                UnityEngine.Object.Destroy(externalActions);
                UnityEngine.Object.Destroy(settings);
            }

            yield return null;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 마지막 입력 장치 observer 하나가 실패해도 뒤 observer와 Input System 처리를 계속한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_InputSystemScreenInputDriver_장치Observer실패_뒤Observer호출()
        {
            var host = new GameObject("Input Observer Host");
            var inputModule = host.AddComponent<InputSystemUIInputModule>();
            var driver = host.AddComponent<InputSystemScreenInputDriver>();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();
            var backgroundBehavior = InputSystem.settings.backgroundBehavior;
            Gamepad gamepad = null;
            var secondObserverCount = 0;

            try
            {
                InputSystem.settings.backgroundBehavior =
                    InputSettings.BackgroundBehavior.IgnoreFocus;
                gamepad = InputSystem.AddDevice<Gamepad>();
                InputSystem.EnableDevice(gamepad);
                var ui = new InputActionMap("UI");
                ui.AddAction("Point", InputActionType.PassThrough)
                    .AddBinding("<Gamepad>/leftStick");
                var gameplay = new InputActionMap("Player");
                gameplay.AddAction("Move", InputActionType.Value);
                actions.AddActionMap(ui);
                actions.AddActionMap(gameplay);
                inputModule.actionsAsset = actions;
                SetField(settings, "uiActionMap", "UI");
                SetField(settings, "gameplayActionsAsset", actions);
                SetField(settings, "gameplayActionMap", "Player");
                SetField(settings, "releaseActionNames", Array.Empty<string>());
                driver.Initialize(inputModule, settings);
                var session = driver.Acquire(new ScreenOptions("Observer"));

                driver.OnLastInputDeviceChanged += _ =>
                    throw new InvalidOperationException("injected input observer failure");
                driver.OnLastInputDeviceChanged += _ => secondObserverCount++;
                LogAssert.Expect
                (
                    LogType.Exception,
                    new Regex("injected input observer failure")
                );

                InputSystem.QueueDeltaStateEvent
                (
                    gamepad.leftStick,
                    new Vector2(0.5f, 0.25f)
                );
                yield return null;

                Assert.AreSame(gamepad, driver.LastInputDevice);
                Assert.AreEqual(1, secondObserverCount);
                session.Release(false);
            }
            finally
            {
                driver.Dispose();
                InputSystem.settings.backgroundBehavior = backgroundBehavior;

                if (gamepad != null)
                {
                    InputSystem.RemoveDevice(gamepad);
                }

                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(actions);
                UnityEngine.Object.Destroy(settings);
            }

            yield return null;
        }

    #endregion

    #region I-2: 컨텍스트 기여 일시중단와 재개

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 살아 있는 ScreenInputSession의 contribution을 suspend하면 global gameplay
        /// <br/> policy에서 제외되고 resume하면 같은 Session 정책이 다시 반영된다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_InputSystemScreenInputDriver_ContributionSuspendResume_GlobalPolicy제외복원()
        {
            var baselineCursorVisible = UnityCursor.visible;
            var baselineCursorLockMode = UnityCursor.lockState;
            var host = new GameObject("Input Contribution Host");
            var inputModule = host.AddComponent<InputSystemUIInputModule>();
            var driver = host.AddComponent<InputSystemScreenInputDriver>();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var settings = ScriptableObject.CreateInstance<UISettingsAsset>();
            ScreenInputSession session = null;

            try
            {
                var ui = new InputActionMap("UI");
                ui.AddAction("Cancel", InputActionType.Button);
                var gameplay = new InputActionMap("Player");
                gameplay.AddAction("Move", InputActionType.Value);
                actions.AddActionMap(ui);
                actions.AddActionMap(gameplay);
                inputModule.actionsAsset = actions;
                SetField(settings, "uiActionMap", "UI");
                SetField(settings, "gameplayActionsAsset", actions);
                SetField(settings, "gameplayActionMap", "Player");
                SetField(settings, "releaseActionNames", Array.Empty<string>());
                driver.Initialize(inputModule, settings);
                gameplay.Enable();

                session = driver.Acquire
                (
                    new ScreenOptions
                    (
                        "Blocking",
                        blocksGameplayInput: true,
                        showsCursor: baselineCursorVisible,
                        cursorLockMode: baselineCursorLockMode
                    )
                );

                Assert.IsFalse(gameplay.enabled);
                Assert.IsFalse(session.IsReleased);

                session.SetContributionEnabled(false);

                Assert.IsTrue(gameplay.enabled);
                Assert.IsFalse(session.IsReleased);

                session.SetContributionEnabled(true);

                Assert.IsFalse(gameplay.enabled);
                Assert.IsFalse(session.IsReleased);

                session.Release(false);

                Assert.IsTrue(session.IsReleased);
                Assert.IsTrue(gameplay.enabled);
            }
            finally
            {
                if (session != null && !session.IsReleased)
                {
                    session.Release(false);
                }

                driver.Dispose();
                UnityCursor.visible = baselineCursorVisible;
                UnityCursor.lockState = baselineCursorLockMode;
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(actions);
                UnityEngine.Object.Destroy(settings);
            }

            yield return null;
        }

    #endregion

    #region P-1: DOTween 프레젠테이션 전환

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

                yield return new WaitForSecondsRealtime(0.1f);

                for (var i = 0; i < 10 && !completed && failure == null; i++)
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

    #region U-1: UGUI 런타임

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
        /// Layout observer 하나가 실패해도 뒤 observer까지 같은 갱신을 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UGUISafeAreaLayout_Observer실패_다음Observer호출()
        {
            var host = new GameObject("Layout Controller");
            host.SetActive(false);
            var safeAreaObject = new GameObject("Safe Area", typeof(RectTransform));
            safeAreaObject.transform.SetParent(host.transform, false);
            var layout = host.AddComponent<UGUISafeAreaLayout>();
            SetField(layout, "safeAreaRoot", safeAreaObject.GetComponent<RectTransform>());
            var observerCount = 0;
            layout.OnLayoutChanged += () =>
            {
                throw new InvalidOperationException("injected layout observer failure");
            };
            layout.OnLayoutChanged += () => observerCount++;

            try
            {
                Assert.Throws<InvalidOperationException>(layout.Refresh);
                Assert.AreEqual(1, observerCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 실제 Canvas에서 Surface Layer와 Screen·Modal·Fade 표시 값이 독립적으로 적용된다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUIPresentation_RuntimeCanvas_대표표시경로()
        {
            var host = new GameObject("UGUI Presentation Host");
            var layer = CreateLayer("UGUI", 17, PresentationBackend.UGUI);
            var plan = CreatePlan
            (
                new List<PresentationPlanLayer>
                {
                    layer,
                },
                ("UGUI.Runtime", layer, 0)
            );
            var session = PresentationSession.CreateTopLevel
            (
                plan,
                host.transform,
                null,
                null
            );

            try
            {
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "UGUI.Runtime",
                        out var placement,
                        out var usage
                    )
                );
                usage.Dispose();
                Assert.IsInstanceOf<IPresentationLayerDriver<RectTransform>>(placement);
                var placementRoot = ((IPresentationLayerDriver<RectTransform>)placement).Root;
                var canvas = placementRoot.GetComponentInParent<Canvas>(true);
                Assert.IsNotNull(canvas);

                var screenObject = new GameObject
                (
                    "UGUI Screen",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(UGUIScreenDriver),
                    typeof(UGUIModalInteractionDriver)
                );
                screenObject.transform.SetParent(placementRoot, false);
                var fadeObject = new GameObject
                (
                    "Fade",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UGUIImage),
                    typeof(CanvasGroup),
                    typeof(UGUISceneFadeDriver)
                );
                fadeObject.transform.SetParent(placementRoot, false);

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

                Assert.IsFalse(canvas.overrideSorting);
                Assert.AreEqual(17, canvas.sortingOrder);
                Assert.AreEqual(0.5f, screenObject.GetComponent<CanvasGroup>().alpha);
                Assert.IsTrue(screenObject.GetComponent<CanvasGroup>().interactable);
                Assert.IsTrue(screenObject.GetComponent<CanvasGroup>().blocksRaycasts);
                Assert.AreEqual(1.0f, fadeObject.GetComponent<CanvasGroup>().alpha);
            }
            finally
            {
                session.Dispose();
                UnityEngine.Object.Destroy(host);
            }
        }

    #endregion

    #region T-1: UITK 런타임

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
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            var output = CreatePanelOutput(host, panelSettings);
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
            container.Add(target);
            container.Add(element);
            var spotlight = new UITKSpotlight();
            Lease lease = null;

            try
            {
                yield return null;
                yield return null;

                var panelRoot = output.RequireRoot();
                panelRoot.Add(container);
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// 스크롤과 조상 이동 뒤 보이는 대상 영역만 Spotlight 구멍으로 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKSpotlight_스크롤과조상이동_좌표추적과ViewportClip()
        {
            var host = new GameObject("Scrolling Spotlight Panel");
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            var output = CreatePanelOutput(host, settings);
            var container = new VisualElement();
            container.style.width = 400f;
            container.style.height = 300f;
            var scroll = new ScrollView();
            scroll.style.width = 300f;
            scroll.style.height = 120f;
            scroll.contentContainer.style.height = 600f;
            var target = new VisualElement();
            target.style.position = Position.Absolute;
            target.style.left = 40f;
            target.style.top = 80f;
            target.style.width = 100f;
            target.style.height = 60f;
            scroll.Add(target);
            var spotlight = new UITKSpotlightElement();
            container.Add(scroll);
            container.Add(spotlight);

            try
            {
                yield return null;
                yield return null;
                output.RequireRoot().Add(container);
                yield return null;
                yield return null;
                spotlight.Show
                (
                    new UITKSpotlightParams
                    (
                        new[]
                        {
                            new UITKSpotlightTarget(target),
                        }
                    )
                );

                var oldPoint = target.ChangeCoordinatesTo(spotlight, new Vector2(50f, 10f));
                var clippedPoint = target.ChangeCoordinatesTo(spotlight, new Vector2(50f, 55f));
                Assert.IsFalse(spotlight.ContainsPoint(oldPoint));
                Assert.IsTrue(spotlight.ContainsPoint(clippedPoint));

                scroll.scrollOffset = new Vector2(0f, 60f);
                yield return null;
                yield return null;
                var newPoint = target.ChangeCoordinatesTo(spotlight, new Vector2(50f, 10f));
                Assert.IsFalse(spotlight.ContainsPoint(newPoint));
                Assert.IsTrue(spotlight.ContainsPoint(oldPoint));

                scroll.style.translate = new Translate(160f, 0f);
                yield return null;
                yield return null;
                Assert.IsTrue(spotlight.ContainsPoint(newPoint));
                Assert.IsFalse
                (
                    spotlight.ContainsPoint(target.ChangeCoordinatesTo(spotlight, new Vector2(50f, 10f)))
                );

                scroll.scrollOffset = new Vector2(0f, 250f);
                yield return null;
                yield return null;
                Assert.AreEqual(0, spotlight.HoleCount);
                Assert.AreEqual(PickingMode.Ignore, spotlight.pickingMode);

                scroll.scrollOffset = Vector2.zero;
                yield return null;
                yield return null;
                Assert.AreEqual(1, spotlight.HoleCount);
                spotlight.Hide();
                Assert.AreEqual(0, spotlight.HoleCount);
            }
            finally
            {
                spotlight.Hide();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(settings);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// detached Layer 등록은 Panel attach 뒤 Focus 추적으로 지연 연결된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKFocusDriver_DetachedLayer등록_Attach후Focus추적()
        {
            var driverHost = new GameObject("UITK Focus Driver");
            var panelHost = new GameObject("Deferred Panel");
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            var output = CreatePanelOutput(panelHost, panelSettings, activate: false);
            var driver = driverHost.AddComponent<UITKFocusDriver>();
            var focus = driverHost.AddComponent<UIFocusDriver>();
            var layerRoot = new VisualElement
            {
                name = "Deferred Layer",
            };
            var layer = new UITKPresentationLayer(layerRoot);
            var button = new Button
            {
                name = "Deferred Focus",
            };
            layerRoot.Add(button);
            IDisposable binding = null;

            try
            {
                Assert.IsNull(layerRoot.panel);
                binding = focus.BindLayer(layer);

                panelHost.SetActive(true);
                yield return null;
                yield return null;

                var panelRoot = output.Root;
                Assert.IsNotNull(panelRoot);
                panelRoot.Add(layerRoot);
                yield return null;

                Assert.IsNotNull(layerRoot.panel);
                button.Focus();
                yield return null;

                Assert.AreSame(button, driver.Current);
            }
            finally
            {
                binding?.Dispose();
                layerRoot.RemoveFromHierarchy();
                UnityEngine.Object.Destroy(driverHost);
                UnityEngine.Object.Destroy(panelHost);
                UnityEngine.Object.Destroy(panelSettings);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 서로 다른 UITK Panel의 사용자 Focus 이동을 추적한다.
        /// <br/> 최신 native Focus를 logical current로 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKFocusDriver_다중Panel사용자Focus_최신LogicalCurrent추적()
        {
            var driverHost = new GameObject("UITK Focus Driver");
            var firstHost = new GameObject("First Panel");
            var secondHost = new GameObject("Second Panel");
            var firstSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(firstSettings);
            var secondSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(secondSettings);
            var firstOutput = CreatePanelOutput(firstHost, firstSettings);
            var secondOutput = CreatePanelOutput(secondHost, secondSettings);
            var driver = driverHost.AddComponent<UITKFocusDriver>();
            var focus = driverHost.AddComponent<UIFocusDriver>();
            var firstLayerRoot = new VisualElement
            {
                name = "First Layer",
            };
            var secondLayerRoot = new VisualElement
            {
                name = "Second Layer",
            };
            var first = new Button
            {
                name = "First",
            };
            var second = new Button
            {
                name = "Second",
            };
            firstLayerRoot.Add(first);
            secondLayerRoot.Add(second);
            IDisposable firstBinding = null;
            IDisposable secondBinding = null;

            try
            {
                yield return null;
                yield return null;

                Assert.IsNotNull(firstOutput.Root);
                Assert.IsNotNull(secondOutput.Root);
                firstOutput.Root.Add(firstLayerRoot);
                secondOutput.Root.Add(secondLayerRoot);
                firstBinding = focus.BindLayer(new UITKPresentationLayer(firstLayerRoot));
                secondBinding = focus.BindLayer(new UITKPresentationLayer(secondLayerRoot));
                first.Focus();
                yield return null;
                second.Focus();
                yield return null;

                Assert.AreSame(second, driver.Current);

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
                secondBinding?.Dispose();
                firstBinding?.Dispose();
                firstLayerRoot.RemoveFromHierarchy();
                secondLayerRoot.RemoveFromHierarchy();
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
            var host = new GameObject("UITK Presentation Host");
            var focus = host.AddComponent<UITKFocusDriver>();
            var commonFocus = host.AddComponent<UIFocusDriver>();
            commonFocus.Initialize();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            var layer = CreateLayer("UITK.Runtime", 23, PresentationBackend.UITK);
            var plan = CreatePlan
            (
                new List<PresentationPlanLayer>
                {
                    layer,
                },
                ("UITK.Runtime", layer, 0)
            );
            var session = PresentationSession.CreateTopLevel
            (
                plan,
                host.transform,
                panelSettings,
                null,
                commonFocus.BindLayer
            );

            try
            {
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "UITK.Runtime",
                        out var placement,
                        out var usage
                    )
                );
                usage.Dispose();
                Assert.IsInstanceOf<IPresentationLayerDriver<VisualElement>>(placement);
                var placementRoot = ((IPresentationLayerDriver<VisualElement>)placement).Root;
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
                placementRoot.Add(screenRoot);
                screenRoot.Add(button);
                placementRoot.Add(fadeRoot);

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
                session.Dispose();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(panelSettings);
            }
        }

    #endregion

    #region T-2: 임베디드 UITK 포인터 피킹

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Screen 위에 비어 있는 Overlay/Modal Layer root가 있어도
        /// <br/> 실제 Panel hit-test는 Screen의 focusable control까지
        /// <br/> 도달해야 한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_EmbeddedUITK_빈상위Layer_PointerPick이ScreenControl도달()
        {
            var host = new GameObject("Embedded Picking Host");
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            var hostLayer = CreateLayer("Host", 0, PresentationBackend.UITK);
            var hostPlan = CreatePlan
            (
                new List<PresentationPlanLayer>
                {
                    hostLayer,
                },
                ("Host", hostLayer, 0)
            );
            var session = PresentationSession.CreateTopLevel
            (
                hostPlan,
                host.transform,
                panelSettings,
                null
            );
            UITKPresentationSurface surface = null;

            try
            {
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "Host",
                        out var placement,
                        out var usage
                    )
                );
                usage.Dispose();
                var placementRoot = ((IPresentationLayerDriver<VisualElement>)placement).Root;
                surface = new UITKPresentationSurface
                (
                    placementRoot,
                    new[]
                    {
                        CreateLayer("Screen", 0, PresentationBackend.UITK),
                        CreateLayer("Overlay", 100, PresentationBackend.UITK),
                        CreateLayer("Modal", 200, PresentationBackend.UITK),
                    }
                );
                Assert.IsTrue(surface.TryGetLayer("Screen", out var screenLayer));
                var screenRoot = ((IPresentationLayerDriver<VisualElement>)screenLayer).Root;
                var button = new Button
                {
                    name = "Screen Button",
                };
                button.style.position = Position.Absolute;
                button.style.left = 40f;
                button.style.top = 40f;
                button.style.width = 120f;
                button.style.height = 60f;
                screenRoot.Add(button);

                yield return null;
                yield return null;

                Assert.IsNotNull(button.panel);
                Assert.Greater(button.worldBound.width, 0f);
                Assert.Greater(button.worldBound.height, 0f);

                var picked = button.panel.Pick(button.worldBound.center);

                Assert.AreSame(button, picked);
            }
            finally
            {
                surface?.Dispose();
                session.Dispose();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(panelSettings);
            }
        }

    #endregion

    #region O-1: 백엔드 간 네이티브 정렬

        [Explicit]
        [Category("Rendering")]
        [UnityTest]
        public IEnumerator TEST_PresentationSession_UITK_UGUI_UITK_LayerOrder_실제PixelInterleave()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore
                (
                    "실제 픽셀 interleave 검증은 GameView/WaitForEndOfFrame이 있는 interactive PlayMode가 필요합니다."
                );
            }

            var host = new GameObject("Cross Backend Ordering Host");
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            var bottomLayer = CreateLayer("UITK.Bottom", 100, PresentationBackend.UITK);
            var middleLayer = CreateLayer("UGUI.Middle", 200, PresentationBackend.UGUI);
            var topLayer = CreateLayer("UITK.Top", 300, PresentationBackend.UITK);
            var plan = CreatePlan
            (
                new List<PresentationPlanLayer>
                {
                    bottomLayer,
                    middleLayer,
                    topLayer,
                },
                ("UITK.Bottom", bottomLayer, 0),
                ("UGUI.Middle", middleLayer, 0),
                ("UITK.Top", topLayer, 0)
            );
            var session = PresentationSession.CreateTopLevel
            (
                plan,
                host.transform,
                panelSettings,
                null
            );
            Lease bottomUsage = null;
            Lease middleUsage = null;
            Lease topUsage = null;

            try
            {
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "UITK.Bottom",
                        out var bottomPlacement,
                        out bottomUsage
                    )
                );
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "UGUI.Middle",
                        out var middlePlacement,
                        out middleUsage
                    )
                );
                Assert.IsTrue
                (
                    session.TryAcquirePlacement
                    (
                        "UITK.Top",
                        out var topPlacement,
                        out topUsage
                    )
                );

                ((IPresentationLayerDriver<VisualElement>)bottomPlacement).Root.Add
                (
                    CreateSolidUITK(Color.red)
                );
                CreateSolidUGUI
                (
                    ((IPresentationLayerDriver<RectTransform>)middlePlacement).Root,
                    Color.green
                );
                ((IPresentationLayerDriver<VisualElement>)topPlacement).Root.Add
                (
                    CreateSolidUITK(Color.blue)
                );

                yield return null;
                yield return null;
                yield return new WaitForEndOfFrame();

                AssertDominantChannel(CaptureCenterPixel(), 2);

                Assert.IsTrue(session.LayerRegistry.TryGet("UITK.Top", out var topDriver));
                topDriver.SetActive(false);
                yield return null;
                yield return new WaitForEndOfFrame();

                AssertDominantChannel(CaptureCenterPixel(), 1);

                Assert.IsTrue(session.LayerRegistry.TryGet("UGUI.Middle", out var middleDriver));
                middleDriver.SetActive(false);
                yield return null;
                yield return new WaitForEndOfFrame();

                AssertDominantChannel(CaptureCenterPixel(), 0);
            }
            finally
            {
                topUsage?.Dispose();
                middleUsage?.Dispose();
                bottomUsage?.Dispose();
                session.Dispose();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(panelSettings);
            }
        }

    #endregion

    #region M-1: 혼합 포커스

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
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            var panelOutput = CreatePanelOutput(host, panelSettings);
            var uguiFocus = host.AddComponent<UGUIFocusDriver>();
            var uitkFocus = host.AddComponent<UITKFocusDriver>();
            var focus = host.AddComponent<UIFocusDriver>();
            SetField(uguiFocus, "eventSystem", eventSystem);

            var uguiCanvasObject = new GameObject
            (
                "UGUI Canvas",
                typeof(RectTransform),
                typeof(Canvas)
            );
            uguiCanvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var uguiLayer = new UGUIPresentationLayer
            (
                uguiCanvasObject.GetComponent<RectTransform>()
            );
            var uitkLayerRoot = new VisualElement
            {
                name = "Mixed UITK Layer",
            };
            var uitkLayer = new UITKPresentationLayer(uitkLayerRoot);
            IDisposable uguiBinding = null;
            IDisposable uitkBinding = null;

            try
            {
                yield return null;
                yield return null;

                Assert.IsNotNull(panelOutput.Root);
                panelOutput.Root.Add(uitkLayerRoot);

                var uguiTarget = new GameObject
                (
                    "UGUI Focus",
                    typeof(RectTransform),
                    typeof(UnityEngine.UI.Button)
                );
                uguiTarget.transform.SetParent(uguiLayer.Root, false);
                var uitkTarget = new Button
                {
                    name = "UITK Focus",
                };
                uitkLayer.Root.Add(uitkTarget);

                focus.Initialize();
                uguiBinding = focus.BindLayer(uguiLayer);
                uitkBinding = focus.BindLayer(uitkLayer);

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
                uitkBinding?.Dispose();
                uguiBinding?.Dispose();
                uitkLayerRoot.RemoveFromHierarchy();
                UnityEngine.Object.Destroy(uguiCanvasObject);
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
            var uguiFocus = host.AddComponent<UGUIFocusDriver>();
            host.AddComponent<UITKFocusDriver>();
            var focus = host.AddComponent<UIFocusDriver>();
            SetField(uguiFocus, "eventSystem", eventSystem);
            focus.Initialize();

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);
            var uguiLayer = CreateLayer("UGUI", 100, PresentationBackend.UGUI);
            var uitkLayer = CreateLayer("UITK", 200, PresentationBackend.UITK);
            var plan = CreatePlan
            (
                new List<PresentationPlanLayer>
                {
                    uguiLayer,
                    uitkLayer,
                }
            );
            var presentation = PresentationSession.CreateTopLevel
            (
                plan,
                host.transform,
                panelSettings,
                null,
                focus.BindLayer
            );
            var screenRegistry = new ScreenRegistry();
            var transitioner = new ImmediateTransitioner();
            var input = new TestInputDriver();
            ScreenRegistrationHandle uguiRegistration = null;
            ScreenRegistrationHandle uitkRegistration = null;
            ScreenController controller = null;

            try
            {
                yield return null;
                yield return null;

                Assert.IsTrue
                (
                    presentation.LayerRegistry.TryAcquireUsage
                    (
                        "UGUI",
                        out var uguiDriver,
                        out var uguiProbe
                    )
                );
                uguiProbe.Dispose();
                Assert.IsTrue
                (
                    presentation.LayerRegistry.TryAcquireUsage
                    (
                        "UITK",
                        out var uitkDriver,
                        out var uitkProbe
                    )
                );
                uitkProbe.Dispose();
                var uguiLayerRoot =
                    ((IPresentationLayerDriver<RectTransform>)uguiDriver).Root;
                var uitkLayerRoot =
                    ((IPresentationLayerDriver<VisualElement>)uitkDriver).Root;

                var uguiSource = new TestUGUIScreenSource();
                var uitkSource = new TestUITKScreenSource();
                uguiRegistration = screenRegistry.Register
                (
                    new ScreenOptions
                    (
                        "UGUI",
                        openDuration: 0.0f,
                        closeDuration: 0.0f
                    ),
                    PresentationTarget.Local("UGUI"),
                    uguiSource
                );
                uitkRegistration = screenRegistry.Register
                (
                    new ScreenOptions
                    (
                        "UITK",
                        openDuration: 0.0f,
                        closeDuration: 0.0f
                    ),
                    PresentationTarget.Local("UITK"),
                    uitkSource
                );
                controller = new ScreenController
                (
                    screenRegistry,
                    target => presentation.AcquireLayer(target.ID),
                    transitioner,
                    new FocusController(focus),
                    input
                );
                controller.Activate();

                var uguiResponse = controller.Open("UGUI");

                Assert.IsTrue(uguiResponse.Accepted);
                Assert.AreSame(uguiSource.DefaultFocus, focus.Current);
                Assert.AreEqual(1, uguiLayerRoot.childCount);

                var uitkResponse = controller.Open("UITK");
                yield return null;

                Assert.IsTrue(uitkResponse.Accepted);
                Assert.AreSame(uitkSource.DefaultFocus, focus.Current);
                Assert.AreEqual(1, uitkLayerRoot.childCount);
                Assert.AreEqual(0, uguiSource.ReleaseCount);

                Assert.IsTrue(uitkResponse.Session.Close());
                yield return null;

                Assert.AreEqual(1, uitkSource.ReleaseCount);
                Assert.AreEqual(0, uitkLayerRoot.childCount);
                Assert.AreSame(uguiSource.DefaultFocus, focus.Current);

                Assert.Throws<InvalidOperationException>(presentation.Dispose);
                controller.Clear();
                yield return null;

                Assert.AreEqual(1, uguiSource.ReleaseCount);
                Assert.AreEqual(0, uguiLayerRoot.childCount);
            }
            finally
            {
                controller?.Clear();
                uitkRegistration?.Dispose();
                uguiRegistration?.Dispose();
                input.Dispose();
                transitioner.Dispose();
                screenRegistry.Dispose();

                if (!presentation.IsDisposed)
                {
                    presentation.Dispose();
                }

                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(panelSettings);
            }
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
