/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_ScreenController.cs
수정일 : 2026-10-05

# 설명
Screen 수명·상태 훅 정리, Focus 복원과 닫기 입력 장벽 계약을 검증한다.

# 테스트 구성
 N: 정상 Open·Close 수명과 Scope 전달
 H: 상태 훅 취소와 Open 재진입 경계
 D: 중복 정책과 동적 등록 수명
 O: 수락 전 자원 정리와 이전 Focus 복원
 C: Close 입력 장벽과 Focus 복원
 R: Replace 입력 정책 분리
 T: Transition 취소 Terminal 처리·실패·Clear
 F: persistent Focus Scope와 LastFocus
 X: 완료 훅 예외 정리
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// ScreenController의 상태·소유권·입력 장벽 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_ScreenController
    {

    #region 헬퍼

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

        // ======================================================================
        /// <summary>
        /// 완료와 실패 callback 시점을 테스트가 직접 제어하는 Transition backend.
        /// </summary>
        // ======================================================================
        private sealed class ManualTransitioner : IPresentationTransitioner
        {
            // ============================================================
            /// <summary>
            /// 한 Transition 요청의 Handle과 callback을 보관한다.
            /// </summary>
            // ============================================================
            public sealed class Request
            {
                public PresentationTransitionParams Params { get; }
                public PresentationTransitionHandle Handle { get; set; }
                public Action Completed { get; }
                public Action<Exception> Failed { get; }

                public Request
                (
                    PresentationTransitionParams parameters,
                    Action completed,
                    Action<Exception> failed
                ) : base()
                {
                    Params = parameters;
                    Completed = completed;
                    Failed = failed;
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 아직 테스트가 완료 callback을 발생시키지 않은 요청 수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count => requests.Count;

            // ------------------------------------------------------------
            /// <summary>
            /// 다음 Play 호출에서 동기 실패를 발생시킬지 여부.
            /// </summary>
            // ------------------------------------------------------------
            public bool FailNextPlay { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Transition Handle Cancel 누적 호출 수.
            /// </summary>
            // ------------------------------------------------------------
            public int CancelCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 다음 Cancel callback에서 테스트 예외를 발생시킬지 여부.
            /// </summary>
            // ------------------------------------------------------------
            public bool FailNextCancel { get; set; }

            private readonly List<Request> requests = new List<Request>();

            // ------------------------------------------------------------
            /// <summary>
            /// Transition 요청을 보관하고 취소 가능한 Handle을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationTransitionHandle Play
            (
                PresentationTransitionParams parameters,
                Action onCompleted,
                Action<Exception> onFailed
            )
            {
                if (FailNextPlay)
                {
                    FailNextPlay = false;
                    throw new InvalidOperationException("injected transition start failure");
                }

                var request = new Request(parameters, onCompleted, onFailed);
                request.Handle = new PresentationTransitionHandle
                (
                    () =>
                    {
                        CancelCount++;

                        if (FailNextCancel)
                        {
                            FailNextCancel = false;
                            throw new InvalidOperationException("injected transition cancel failure");
                        }
                    }
                );
                requests.Add(request);
                return request.Handle;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 가장 오래된 요청을 정상 완료하고 callback을 발생시킨다.
            /// </summary>
            // ------------------------------------------------------------
            public Request CompleteNext()
            {
                var request = TakeNext();
                var completed = request.Handle.Complete();

                if (completed)
                {
                    request.Params.Target.Set(request.Params.EndValue);
                }

                request.Completed?.Invoke();
                return request;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 가장 오래된 요청을 실패 완료하고 callback을 발생시킨다.
            /// </summary>
            // ------------------------------------------------------------
            public Request FailNext(Exception exception)
            {
                var request = TakeNext();
                request.Handle.Complete();
                request.Failed?.Invoke(exception);
                return request;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 취소·교체된 요청의 늦은 완료 callback을 강제로 다시 발생시킨다.
            /// </summary>
            // ------------------------------------------------------------
            public static void InvokeLateCompletion(Request request)
            {
                request?.Completed?.Invoke();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 보관된 Transition 요청을 제거한다.
            /// </summary>
            // ------------------------------------------------------------
            private Request TakeNext()
            {
                if (requests.Count == 0)
                {
                    throw new InvalidOperationException("완료할 Transition 요청이 없습니다.");
                }

                var request = requests[0];
                requests.RemoveAt(0);
                return request;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Transition backend를 종료한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Dispose()
            {
                requests.Clear();
            }
        }

        private sealed class TestFocusDriver : IFocusDriver
        {
            public object Current => CurrentValue;
            public object CurrentValue { get; set; }
            public object Fallback { get; } = new object();
            public Action<object> Selected { get; set; }

            public bool IsValid(object target)
            {
                return target != null;
            }

            public void Select(object target)
            {
                CurrentValue = target;
                Selected?.Invoke(target);
            }

            public object FindFallback()
            {
                return Fallback;
            }
        }

        // ============================================================
        /// <summary>
        /// 단일 기본 대상을 제공하는 테스트 Focus Scope.
        /// </summary>
        // ============================================================
        private sealed class TestFocusScope : IFocusScope
        {
            public object DefaultFocus { get; }

            private readonly HashSet<object> targets = new();

            public TestFocusScope(object defaultFocus, params object[] additionalTargets)
            {
                DefaultFocus = defaultFocus;
                targets.Add(defaultFocus);

                if (additionalTargets == null) return;

                for (var index = 0; index < additionalTargets.Length; index++)
                {
                    targets.Add(additionalTargets[index]);
                }
            }

            public bool ContainsFocus(object target)
            {
                return target != null && targets.Contains(target);
            }
        }

        // ============================================================
        /// <summary>
        /// UGUI 선택 callback을 테스트 흐름에 전달한다.
        /// </summary>
        // ============================================================
        private sealed class SelectCallback : MonoBehaviour, ISelectHandler
        {
            public Action Selected { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// EventSystem 선택 callback을 전달한다.
            /// </summary>
            // ------------------------------------------------------------
            public void OnSelect(BaseEventData eventData)
            {
                Selected?.Invoke();
            }
        }

        private sealed class TestInputDriver : IScreenInputDriver
        {
            private readonly List<ScreenInputSession> sessions = new List<ScreenInputSession>();

            public bool HoldRelease { get; set; }
            public bool FailRelease { get; set; }
            public Action<ScreenInputSession> ContributionChanged { get; set; }
            public int BeginBatchCount { get; private set; }
            public int EndBatchCount { get; private set; }

            // ----------------------------------------------------------------------
            /// <summary>
            /// 입력 Session을 소유 목록에 넣은 뒤 반환 전에 호출할 테스트 callback.
            /// </summary>
            // ----------------------------------------------------------------------
            public Action Acquiring { get; set; }

            public ScreenInputSession LastAcquired { get; private set; }
            public int Count => sessions.Count;

            public ScreenInputSession Acquire
            (
                ScreenOptions options,
                bool contributionEnabled = true
            )
            {
                LastAcquired = new ScreenInputSession
                (
                    options,
                    Release,
                    HandleContributionChanged,
                    contributionEnabled
                );
                sessions.Add(LastAcquired);
                Acquiring?.Invoke();
                return LastAcquired;
            }

            public void Complete(ScreenInputSession session)
            {
                sessions.Remove(session);
                session.MarkReleased();
            }

            public void BeginBatch()
            {
                BeginBatchCount++;
            }

            public void EndBatch()
            {
                EndBatchCount++;
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

            private void HandleContributionChanged(ScreenInputSession session)
            {
                ContributionChanged?.Invoke(session);
            }

            private void Release
            (
                ScreenInputSession session,
                bool waitForInputRelease,
                bool retainCursorWhileAwaitingRelease
            )
            {
                if (FailRelease)
                {
                    sessions.Remove(session);
                    session.MarkReleased(invokeCompletionCallback: false);
                    throw new InvalidOperationException("injected input release failure");
                }

                if (HoldRelease && waitForInputRelease)
                {
                    session.MarkAwaitingRelease(retainCursorWhileAwaitingRelease);
                    return;
                }

                Complete(session);
            }
        }

        private sealed class TestScreenDriver :
            IScreenDriver,
            IPresentationAlphaTarget,
            IPresentationVisibilityTarget
        {
            public bool IsValid => true;
            public PresentationAlpha Alpha { get; }
            public PresentationVisibility Visibility { get; }


            public object DefaultFocus { get; }
            public bool IsInteractable { get; private set; }
            public Action<bool> InteractableChanged { get; set; }
            public bool IsVisible { get; private set; } = true;
            public float AppliedAlpha { get; private set; } = 1.0f;

            private readonly HashSet<object> focusTargets = new();

            public TestScreenDriver(object defaultFocus) : base()
            {
                DefaultFocus = defaultFocus;
                focusTargets.Add(defaultFocus);
                Alpha = new PresentationAlpha(this);
                Visibility = new PresentationVisibility(this);
            }

            public void AddFocusTarget(object target)
            {
                focusTargets.Add(target);
            }

            bool IPresentationAlphaTarget.IsValid => IsValid;
            float IPresentationAlphaTarget.Alpha => AppliedAlpha;
            bool IPresentationVisibilityTarget.IsValid => IsValid;
            bool IPresentationVisibilityTarget.IsVisible => IsVisible;

            void IPresentationAlphaTarget.SetAlpha(float alpha)
            {
                AppliedAlpha = alpha;
            }

            void IPresentationVisibilityTarget.SetVisible(bool visible)
            {
                IsVisible = visible;
            }

            public bool ContainsFocus(object target)
            {
                return target != null && focusTargets.Contains(target);
            }

            public void SetInteractable(bool interactable)
            {
                IsInteractable = interactable;
                InteractableChanged?.Invoke(interactable);
            }
        }

        private sealed class TestStateHandler : IScreenStateHandler
        {
            public Action<ScreenStateContext> Opening { get; set; }
            public Action<ScreenStateContext> Opened { get; set; }
            public Action<ScreenStateContext> Closing { get; set; }
            public Action<ScreenStateContext> Closed { get; set; }
            public List<string> Calls { get; } = new List<string>();

            public void OnOpening(ScreenStateContext context)
            {
                Calls.Add("Opening");
                Opening?.Invoke(context);
            }

            public void OnOpened(ScreenStateContext context)
            {
                Calls.Add("Opened");
                Opened?.Invoke(context);
            }

            public void OnClosing(ScreenStateContext context)
            {
                Calls.Add("Closing");
                Closing?.Invoke(context);
            }

            public void OnClosed(ScreenStateContext context)
            {
                Calls.Add("Closed");
                Closed?.Invoke(context);
            }
        }

        private sealed class TestScreenSource : IScreenSource
        {
            private readonly IScreenStateHandler stateHandler = null;

            public TestScreenDriver Driver { get; }
            public Action<ScreenViewScope> Acquiring { get; set; }
            public Action Releasing { get; set; }
            public ScreenViewScope LastScope { get; private set; }
            public int AcquireCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public TestScreenSource
            (
                object defaultFocus,
                IScreenStateHandler stateHandler = null
            ) : base()
            {
                Driver = new TestScreenDriver(defaultFocus);
                this.stateHandler = stateHandler;
            }

            public ScreenInstance Acquire(ScreenViewScope scope)
            {
                AcquireCount++;
                LastScope = scope;
                Acquiring?.Invoke(scope);
                return new ScreenInstance(Driver, stateHandler);
            }

            public void Release(ScreenInstance instance)
            {
                Releasing?.Invoke();
                ReleaseCount++;
            }
        }

        private sealed class ThrowingHandle : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                throw new InvalidOperationException("injected child release failure");
            }
        }

        private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();
        private readonly List<ScreenRegistrationHandle> registrations = new List<ScreenRegistrationHandle>();

        private PresentationTestScope presentation = null;
        private IPresentationLayerDriver layerDriver = null;
        private ScreenRegistry screenRegistry = null;
        private ScreenController controller = null;
        private FocusController focusController = null;
        private TestFocusDriver focusDriver = null;
        private TestInputDriver inputDriver = null;

        private ScreenRegistrationHandle Register
        (
            string id,
            TestScreenSource source,
            ScreenDuplicatePolicy duplicatePolicy = ScreenDuplicatePolicy.Reject,
            float openDuration = 0.0f,
            float closeDuration = 0.0f
        )
        {
            var options = new ScreenOptions
            (
                id,
                duplicatePolicy,
                openDuration: openDuration,
                closeDuration: closeDuration
            );
            var handle = screenRegistry.Register
            (
                options,
                PresentationTarget.Local("Screen"),
                source
            );
            registrations.Add(handle);
            return handle;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 실제 UGUI Focus Driver가 선택할 대상들을 하나의 소유 Layer 아래에 연결한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void RegisterUGUIFocusLayer
        (
            UGUIFocusDriver driver,
            params GameObject[] targets
        )
        {
            var rootObject = new GameObject
            (
                "UGUI Focus Layer",
                typeof(RectTransform)
            );
            ownedObjects.Add(rootObject);
            var root = rootObject.GetComponent<RectTransform>();

            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].transform.SetParent(root, false);
                }
            }

            driver.RegisterLayer(new UGUIPresentationLayer(root));
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 현재 Registry·Focus·Input backend에 수동 Transition Controller를 연결한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private ManualTransitioner UseManualTransitioner()
        {
            controller.Clear();
            var manual = new ManualTransitioner();
            focusController = new FocusController(focusDriver);
            controller = new ScreenController
            (
                screenRegistry,
                target => presentation.Session.AcquireLayer(target.ID),
                manual,
                focusController,
                inputDriver
            );
            controller.Activate();
            return manual;
        }

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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Screen Controller와 최소 Layer/Input/Focus backend를 구성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            presentation = PresentationTestScope.CreateUGUI("Screen");
            Assert.IsTrue
            (
                presentation.Session.LayerRegistry.TryGet
                (
                    "Screen",
                    out layerDriver
                )
            );
            screenRegistry = new ScreenRegistry();
            focusDriver = new TestFocusDriver();
            inputDriver = new TestInputDriver();
            focusController = new FocusController(focusDriver);
            controller = new ScreenController
            (
                screenRegistry,
                target => presentation.Session.AcquireLayer(target.ID),
                new ImmediateTransitioner(),
                focusController,
                inputDriver
            );
            controller.Activate();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Controller와 등록 Handle, 생성한 Unity Object를 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void TearDown()
        {
            if (controller != null)
            {
                controller.Clear();
            }

            inputDriver?.ForceReleaseAll();

            for (var i = registrations.Count - 1; i >= 0; i--)
            {
                registrations[i].Dispose();
            }

            registrations.Clear();
            screenRegistry?.Dispose();
            presentation?.Dispose();
            presentation = null;

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

    #region N-1: 정상 열기·닫기 수명

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Open Response가 완료보다 먼저 반환되고 Scope·Payload·상태 훅·Source 반환이
        /// <br/> 하나의 Screen 수명에서 정해진 순서로 한 번씩 발생하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_정상OpenClose_Scope와상태훅수명일치()
        {
            var transitioner = UseManualTransitioner();
            var payload = new object();
            var order = new List<string>();
            var handler = new TestStateHandler
            {
                Opening = _ => order.Add("Opening"),
                Opened = _ => order.Add("Opened"),
                Closing = _ => order.Add("Closing"),
                Closed = _ => order.Add("Closed"),
            };
            var source = new TestScreenSource(new object(), handler)
            {
                Releasing = () => order.Add("Release"),
            };
            Register("Menu", source, openDuration: 1.0f, closeDuration: 1.0f);

            var response = controller.Open("Menu", new ScreenOpenParams(payload));

            Assert.IsTrue(response.Accepted);
            Assert.AreEqual(ScreenState.Opening, response.Session.State);
            Assert.AreEqual(1, transitioner.Count);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreSame(response.Session, source.LastScope.Session);
            Assert.AreSame(payload, source.LastScope.OpenParams.Payload);
            Assert.AreSame(layerDriver, source.LastScope.Layer);
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                },
                order
            );

            transitioner.CompleteNext();

            Assert.AreEqual(ScreenState.Active, response.Session.State);
            Assert.IsTrue(source.Driver.IsInteractable);
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Opened",
                },
                order
            );
            Assert.IsTrue(response.Session.Close());
            Assert.AreEqual(ScreenState.Closing, response.Session.State);
            Assert.AreEqual(1, transitioner.Count);

            transitioner.CompleteNext();

            Assert.AreEqual(ScreenState.Closed, response.Session.State);
            Assert.AreEqual(0, controller.Count);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsFalse(response.Session.Close());
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Opened",
                    "Closing",
                    "Closed",
                    "Release",
                },
                order
            );
        }

    #endregion

    #region N-2: 포커스 기본값

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 활성화는 실제 Driver가 제공하는 기본 Focus를 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_화면활성화_Driver기본Focus선택()
        {
            var driverFocus = new object();
            Register("Focus", new TestScreenSource(driverFocus));

            var response = controller.Open("Focus");

            Assert.IsTrue(response.Accepted);
            Assert.AreSame(driverFocus, focusDriver.Current);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 공개 생성자가 유한하지 않은 Transition 시간을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScreenOptions_유한하지않은Transition시간_생성거부()
        {
            Assert.Throws<ArgumentOutOfRangeException>
            (
                () => new ScreenOptions
                (
                    "NaN",
                    openDuration: float.NaN
                )
            );
            Assert.Throws<ArgumentOutOfRangeException>
            (
                () => new ScreenOptions
                (
                    "Infinity",
                    closeDuration: float.PositiveInfinity
                )
            );
        }

    #endregion

    #region N-3: 커서 정책 소유권

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 새 Screen이 Top이 되면 이전 Covered Screen의 Cursor ownership을 해제한다.
        /// <br/> Top 종료 뒤 이전 Screen에 ownership을 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Top변경_CursorOwner를현재ActiveScreen으로이동()
        {
            Register("Base", new TestScreenSource(new object()));
            Register("Child", new TestScreenSource(new object()));
            var baseSession = controller.Open("Base").Session;
            var baseInput = baseSession.Resources.InputSession;

            Assert.IsTrue(baseInput.IsCursorPolicyEnabled);

            var childSession = controller.Open("Child").Session;
            var childInput = childSession.Resources.InputSession;

            Assert.AreEqual(ScreenState.Covered, baseSession.State);
            Assert.IsFalse(baseInput.IsCursorPolicyEnabled);
            Assert.IsTrue(childInput.IsCursorPolicyEnabled);

            Assert.IsTrue(childSession.Close());

            Assert.AreEqual(ScreenState.Active, baseSession.State);
            Assert.IsTrue(baseInput.IsCursorPolicyEnabled);
            Assert.IsTrue(childInput.IsReleased);
        }

    #endregion

    #region H-1: 상태 훅 재진입

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 상태 훅 안의 Screen 명령은 거부되고 훅 반환 뒤 같은 명령이 정상 수락되는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Opening훅재진입_훅안거부후반환뒤수락()
        {
            var transitioner = UseManualTransitioner();
            ScreenOpenResponse nestedOpen = default;
            var nestedClose = true;
            var clearRejected = false;
            var handler = new TestStateHandler
            {
                Opening = _ =>
                {
                    nestedOpen = controller.Open("Other");
                    nestedClose = controller.Close();

                    try
                    {
                        controller.Clear();
                    }
                    catch (InvalidOperationException)
                    {
                        clearRejected = true;
                    }
                },
            };
            Register("First", new TestScreenSource(new object(), handler), openDuration: 1.0f);
            Register("Other", new TestScreenSource(new object()), openDuration: 1.0f);

            var first = controller.Open("First");

            Assert.IsTrue(first.Accepted);
            Assert.AreEqual(ScreenOpenKind.Rejected, nestedOpen.Kind);
            Assert.IsFalse(nestedClose);
            Assert.IsTrue(clearRejected);

            transitioner.CompleteNext();
            var other = controller.Open("Other");

            Assert.IsTrue(other.Accepted);
            transitioner.CompleteNext();
            Assert.AreSame(other.Session, controller.Top);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Source 획득 callback의 중첩 Open을 거부하고 바깥 Screen 하나만 공개하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Source획득재진입_중첩Open거부하고단일Top유지()
        {
            var nestedSource = new TestScreenSource(new object());
            var source = new TestScreenSource(new object());
            ScreenOpenResponse nestedOpen = default;
            source.Acquiring = _ => nestedOpen = controller.Open("Nested");
            Register("Outer", source);
            Register("Nested", nestedSource);

            var response = controller.Open("Outer");

            Assert.IsTrue(response.Accepted);
            Assert.AreEqual(ScreenOpenKind.Rejected, nestedOpen.Kind);
            Assert.AreEqual(1, controller.Count);
            Assert.AreSame(response.Session, controller.Top);
            Assert.AreEqual(0, nestedSource.AcquireCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Source 획득 callback에서 종료되면 늦게 반환된 Instance를 한 번 반환하고,
        /// <br/> 준비 Session과 Layer Usage를 남기지 않는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Source획득중Shutdown_늦은Instance한번반환()
        {
            var source = new TestScreenSource(new object());
            source.Acquiring = _ => controller.Shutdown();
            Register("Interrupted Source", source);

            var response = controller.Open("Interrupted Source");

            Assert.AreEqual(ScreenOpenKind.Rejected, response.Kind);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, controller.Count);
            Assert.IsFalse(controller.IsAvailable);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
            Assert.AreEqual(0, inputDriver.Count);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 입력 획득 callback에서 종료되면 늦게 반환된 입력 Session을 한 번 반환하고,
        /// <br/> 이미 획득한 Source와 Layer Usage도 남기지 않는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_입력획득중Shutdown_늦은InputSession한번반환()
        {
            var source = new TestScreenSource(new object());
            inputDriver.Acquiring = () => controller.Shutdown();
            Register("Interrupted Input", source);

            var response = controller.Open("Interrupted Input");

            Assert.AreEqual(ScreenOpenKind.Rejected, response.Kind);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsNotNull(inputDriver.LastAcquired);
            Assert.IsTrue(inputDriver.LastAcquired.IsReleased);
            Assert.AreEqual(0, inputDriver.Count);
            Assert.AreEqual(0, controller.Count);
            Assert.IsFalse(controller.IsAvailable);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
        }

    #endregion

    #region H-2: 닫기 취소

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 열기 완료 Focus callback에서 Screen이 닫히면 Active 완료 훅을 건너뛰고,
        /// <br/> Closing·Closed 수명만 순서대로 확정한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_열기Focus중Close_OnOpened호출하지않음()
        {
            var transitioner = UseManualTransitioner();
            var handler = new TestStateHandler();
            var source = new TestScreenSource(new object(), handler);
            Register("Focus Close", source, openDuration: 1.0f);
            var response = controller.Open("Focus Close");
            focusDriver.Selected = _ => response.Session.Close();

            transitioner.CompleteNext();

            Assert.AreEqual(ScreenState.Closing, response.Session.State);
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Closing",
                },
                handler.Calls
            );

            transitioner.CompleteNext();

            Assert.AreEqual(ScreenState.Closed, response.Session.State);
            Assert.AreEqual(1, source.ReleaseCount);
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Closing",
                    "Closed",
                },
                handler.Calls
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 0초 Open의 동기 Focus callback에서도 수락된 Session을 닫을 수 있고,
        /// <br/> OnOpened 없이 Closing·Closed 수명을 한 번씩 확정한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_0초열기Focus중Close_동기완료에서도허용()
        {
            var handler = new TestStateHandler();
            var source = new TestScreenSource(new object(), handler);
            Register("Immediate Focus Close", source);
            bool? closeResult = null;
            focusDriver.Selected = _ => closeResult = source.LastScope.Session.Close();

            var response = controller.Open("Immediate Focus Close");

            Assert.IsTrue(response.Accepted);
            Assert.IsTrue(closeResult.HasValue && closeResult.Value);
            Assert.AreEqual(ScreenState.Closed, response.Session.State);
            Assert.AreEqual(0, controller.Count);
            Assert.AreEqual(1, source.ReleaseCount);
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Closing",
                    "Closed",
                },
                handler.Calls
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Replace Focus callback에서 새 Screen을 열어도 이전 Session을 제거하고,
        /// <br/> 중첩 Open의 실제 UGUI Focus를 현재 top에 확정한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_ReplaceFocus재진입Open_이전Screen정리와새TopFocus유지()
        {
            controller.Clear();
            var focusHost = new GameObject
            (
                "UGUI Focus Host",
                typeof(EventSystem),
                typeof(UGUIFocusDriver)
            );
            var firstFocus = new GameObject("First Focus");
            var replaceFocus = new GameObject("Replace Focus", typeof(SelectCallback));
            var nestedFocus = new GameObject("Nested Focus");
            ownedObjects.Add(focusHost);
            ownedObjects.Add(firstFocus);
            ownedObjects.Add(replaceFocus);
            ownedObjects.Add(nestedFocus);
            var eventSystem = focusHost.GetComponent<EventSystem>();
            var uguiFocus = focusHost.GetComponent<UGUIFocusDriver>();
            SetField(uguiFocus, "eventSystem", eventSystem);
            RegisterUGUIFocusLayer
            (
                uguiFocus,
                firstFocus,
                replaceFocus,
                nestedFocus
            );
            controller = new ScreenController
            (
                screenRegistry,
                target => presentation.Session.AcquireLayer(target.ID),
                new ImmediateTransitioner(),
                new FocusController(uguiFocus),
                inputDriver
            );
            controller.Activate();
            var firstSource = new TestScreenSource(firstFocus);
            var replaceSource = new TestScreenSource(replaceFocus);
            var nestedSource = new TestScreenSource(nestedFocus);
            Register("First", firstSource);
            Register("Replace", replaceSource);
            Register("Nested", nestedSource);
            var first = controller.Open("First").Session;
            ScreenOpenResponse nested = default;
            var callback = replaceFocus.GetComponent<SelectCallback>();
            callback.Selected = () =>
            {
                callback.Selected = null;
                nested = controller.Open("Nested");
            };

            var replaced = controller.Replace("Replace");

            Assert.IsTrue(replaced.Accepted);
            Assert.IsTrue(nested.Accepted);
            Assert.AreEqual(ScreenState.Closed, first.State);
            Assert.AreEqual(ScreenState.Covered, replaced.Session.State);
            Assert.AreEqual(ScreenState.Active, nested.Session.State);
            Assert.AreEqual(1, firstSource.ReleaseCount);
            Assert.AreEqual(2, controller.Count);
            Assert.AreSame(nestedFocus, eventSystem.currentSelectedGameObject);

            Assert.IsTrue(nested.Session.Close());
            Assert.AreEqual(ScreenState.Active, replaced.Session.State);
            Assert.AreSame(replaceFocus, eventSystem.currentSelectedGameObject);

            Assert.IsTrue(replaced.Session.Close());
            Assert.AreEqual(0, controller.Count);
            Assert.AreEqual(1, firstSource.ReleaseCount);
            Assert.AreEqual(1, replaceSource.ReleaseCount);
            Assert.AreEqual(1, nestedSource.ReleaseCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Replace Focus callback에서 다시 Replace해도 교체 확정 대상을 잃지 않고,
        /// <br/> 최종 Screen만 Stack과 Focus에 남긴다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_ReplaceFocus재진입Replace_교체연쇄전체정리()
        {
            controller.Clear();
            var focusHost = new GameObject
            (
                "UGUI Focus Host",
                typeof(EventSystem),
                typeof(UGUIFocusDriver)
            );
            var firstFocus = new GameObject("First Focus");
            var replaceFocus = new GameObject("Replace Focus", typeof(SelectCallback));
            var finalFocus = new GameObject("Final Focus");
            ownedObjects.Add(focusHost);
            ownedObjects.Add(firstFocus);
            ownedObjects.Add(replaceFocus);
            ownedObjects.Add(finalFocus);
            var eventSystem = focusHost.GetComponent<EventSystem>();
            var uguiFocus = focusHost.GetComponent<UGUIFocusDriver>();
            SetField(uguiFocus, "eventSystem", eventSystem);
            RegisterUGUIFocusLayer
            (
                uguiFocus,
                firstFocus,
                replaceFocus,
                finalFocus
            );
            controller = new ScreenController
            (
                screenRegistry,
                target => presentation.Session.AcquireLayer(target.ID),
                new ImmediateTransitioner(),
                new FocusController(uguiFocus),
                inputDriver
            );
            controller.Activate();
            var firstSource = new TestScreenSource(firstFocus);
            var replaceSource = new TestScreenSource(replaceFocus);
            var finalSource = new TestScreenSource(finalFocus);
            Register("First", firstSource);
            Register("Replace", replaceSource);
            Register("Final", finalSource);
            var first = controller.Open("First").Session;
            ScreenOpenResponse final = default;
            var callback = replaceFocus.GetComponent<SelectCallback>();
            callback.Selected = () =>
            {
                callback.Selected = null;
                final = controller.Replace("Final");
            };

            var replaced = controller.Replace("Replace");

            Assert.IsTrue(replaced.Accepted);
            Assert.IsTrue(final.Accepted);
            Assert.AreEqual(ScreenState.Closed, first.State);
            Assert.AreEqual(ScreenState.Closed, replaced.Session.State);
            Assert.AreEqual(ScreenState.Active, final.Session.State);
            Assert.AreEqual(1, controller.Count);
            Assert.AreSame(finalFocus, eventSystem.currentSelectedGameObject);
            Assert.AreEqual(1, firstSource.ReleaseCount);
            Assert.AreEqual(1, replaceSource.ReleaseCount);

            Assert.IsTrue(final.Session.Close());
            Assert.AreEqual(0, controller.Count);
            Assert.AreEqual(1, finalSource.ReleaseCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> OnClosing에서 Controller가 종료돼 Session이 강제 정리되면,
        /// <br/> 바깥 Close가 해당 Session을 다시 Closing으로 되돌리지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_OnClosing중Shutdown_단일Closing후종료()
        {
            var handler = new TestStateHandler
            {
                Closing = _ => controller.Shutdown(),
            };
            var source = new TestScreenSource(new object(), handler);
            Register("Shutdown", source);
            var session = controller.Open("Shutdown").Session;

            Assert.IsTrue(session.Close());

            Assert.AreEqual(ScreenState.Closed, session.State);
            Assert.AreEqual(0, controller.Count);
            Assert.AreEqual(1, source.ReleaseCount);
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Opened",
                    "Closing",
                    "Closed",
                },
                handler.Calls
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 취소 가능한 OnClosing이 Screen과 View를 보존하고 강제 Clear만 최종 종료하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_OnClosing취소_Active보존후Clear강제종료()
        {
            var handler = new TestStateHandler
            {
                Closing = context =>
                {
                    if (context.CanCancel)
                    {
                        context.Cancel();
                    }
                },
            };
            var source = new TestScreenSource(new object(), handler);
            Register("Confirm", source);
            var session = controller.Open("Confirm").Session;

            Assert.IsFalse(controller.Close());
            Assert.AreEqual(ScreenState.Active, session.State);
            Assert.AreEqual(0, source.ReleaseCount, "child 정리 실패 전에 Source를 반환하면 안 됩니다.");
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Opened",
                    "Closing",
                },
                handler.Calls
            );

            controller.Clear();

            Assert.AreEqual(ScreenState.Closed, session.State);
            Assert.AreEqual(1, source.ReleaseCount);
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Opened",
                    "Closing",
                    "Closing",
                    "Closed",
                },
                handler.Calls
            );
        }

    #endregion

    #region H-3: 상태 명령 표와 열기 예외

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 초기화 전·빈 Stack·Opening·Closing의 공개 명령 결과와
        /// <br/> OnOpening 취소 Response 불변식이 상태표와 일치하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_공개명령_상태표와취소Response불변식일치()
        {
            var defaultResponse = default(ScreenOpenResponse);
            Assert.AreEqual(ScreenOpenKind.None, defaultResponse.Kind);
            Assert.IsFalse(defaultResponse.Accepted);
            Assert.IsNull(defaultResponse.Session);

            var transitioner = UseManualTransitioner();
            var firstSource = new TestScreenSource(new object());
            var secondSource = new TestScreenSource(new object());
            Register("First", firstSource, openDuration: 1.0f, closeDuration: 1.0f);
            Register("Second", secondSource, openDuration: 1.0f, closeDuration: 1.0f);
            var inactive = new ScreenController
            (
                screenRegistry,
                target => presentation.Session.AcquireLayer(target.ID),
                new ImmediateTransitioner(),
                new FocusController(focusDriver),
                inputDriver
            );

            var inactiveOpen = inactive.Open("First");
            Assert.AreEqual(ScreenOpenKind.Rejected, inactiveOpen.Kind);
            Assert.IsNull(inactiveOpen.Session);
            Assert.IsNull(inactiveOpen.Exception);
            Assert.IsFalse(inactive.Close());
            Assert.AreEqual(ScreenOpenKind.Rejected, inactive.Replace("First").Kind);
            Assert.DoesNotThrow(inactive.Clear);

            Assert.IsFalse(controller.Close());
            Assert.AreEqual(ScreenOpenKind.Rejected, controller.Replace("First").Kind);
            var first = controller.Open("First");

            Assert.IsTrue(first.Accepted);
            Assert.AreEqual(ScreenState.Opening, first.Session.State);
            Assert.AreEqual(ScreenOpenKind.Rejected, controller.Open("Second").Kind);
            Assert.AreEqual(ScreenOpenKind.Rejected, controller.Replace("Second").Kind);
            Assert.IsTrue(first.Session.Close());
            Assert.AreEqual(ScreenState.Closing, first.Session.State);
            Assert.IsFalse(controller.Close());
            Assert.AreEqual(ScreenOpenKind.Rejected, controller.Open("Second").Kind);
            Assert.AreEqual(ScreenOpenKind.Rejected, controller.Replace("Second").Kind);

            controller.Clear();

            var cancelling = new TestStateHandler
            {
                Opening = context => context.Cancel(),
            };
            Register("Cancelled", new TestScreenSource(new object(), cancelling));
            var cancelled = controller.Open("Cancelled");

            Assert.AreEqual(ScreenOpenKind.Cancelled, cancelled.Kind);
            Assert.IsFalse(cancelled.Accepted);
            Assert.IsNull(cancelled.Session);
            Assert.IsNull(cancelled.Exception);
            Assert.IsNotEmpty(cancelled.Error);
            Assert.AreEqual(0, controller.Count);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// OnOpening 예외가 Source와 준비 Session을 정리한 뒤 원본 예외로 다시 전달되는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_OnOpening예외_Source와준비Session정리후재전파()
        {
            var handler = new TestStateHandler
            {
                Opening = _ =>
                    throw new InvalidOperationException("injected opening hook failure"),
            };
            var source = new TestScreenSource(new object(), handler);
            Register("Failed Hook", source);

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => controller.Open("Failed Hook")
            );

            StringAssert.Contains("opening hook failure", exception.Message);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, controller.Count);
        }

    #endregion

    #region D-1: 중복과 등록 수명

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 중복 Open과 등록 해제 뒤 새 조회만 거부하고,
        /// <br/> 기존 Session 종료 후 같은 ID를 새 Source로 재등록할 수 있는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenRegistry_중복과등록해제_기존Session종료후재등록()
        {
            var firstSource = new TestScreenSource(new object());
            var firstRegistration = Register("Dynamic", firstSource);
            var first = controller.Open("Dynamic");

            var duplicate = controller.Open("Dynamic");

            Assert.AreEqual(ScreenOpenKind.Rejected, duplicate.Kind);
            Assert.AreEqual(1, firstSource.AcquireCount);

            firstRegistration.Dispose();
            var afterUnregister = controller.Open("Dynamic");

            Assert.AreEqual(ScreenOpenKind.Rejected, afterUnregister.Kind);
            Assert.IsTrue(first.Session.Close());
            Assert.AreEqual(1, firstSource.ReleaseCount);

            var secondSource = new TestScreenSource(new object());
            Register("Dynamic", secondSource);
            var second = controller.Open("Dynamic");

            Assert.IsTrue(second.Accepted);
            Assert.AreEqual(1, secondSource.AcquireCount);
            Assert.IsTrue(second.Session.Close());
            Assert.AreEqual(1, secondSource.ReleaseCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Registry 전체 종료가 남은 Screen 등록 Handle도 Terminal로 만드는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenRegistry_전체종료_남은등록HandleTerminal()
        {
            var handle = Register("Dynamic", new TestScreenSource(new object()));

            screenRegistry.Dispose();

            Assert.IsTrue(handle.IsDisposed);
            Assert.IsFalse(screenRegistry.Contains("Dynamic"));
            Assert.DoesNotThrow(handle.Dispose);
        }

    #endregion

    #region O-1: 수락 전 자식 정리

        // --------------------------------------------------------------------------------
        /// <summary>
        /// OnOpening 취소 중 자식 정리가 실패해도 준비 Session 전체가 Terminal인지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_OnOpening취소_자식정리실패에도SessionTerminal()
        {
            var child = new ThrowingHandle();
            var handler = new TestStateHandler
            {
                Opening = context =>
                {
                    context.Session.RegisterChild(child);
                    context.Cancel();
                },
            };
            var source = new TestScreenSource(new object(), handler);
            Register("Cancelled", source);

            Assert.Throws<AggregateException>(() => controller.Open("Cancelled"));
            Assert.AreEqual(1, child.DisposeCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, controller.Count);

            controller.Clear();

            Assert.AreEqual(1, child.DisposeCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, controller.Count);
        }

    #endregion

    #region O-2: 소스 실패 포커스 복원

        // ----------------------------------------------------------------------
        /// <summary>
        /// Source 획득 실패가 이전 Active top과 마지막 Focus를 함께 복원하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Source획득실패_이전Top과Focus복원()
        {
            var baseDefault = new object();
            var baseLast = new object();
            var foreignFocus = new object();
            var baseSource = new TestScreenSource(baseDefault);
            baseSource.Driver.AddFocusTarget(baseLast);
            var failedSource = new TestScreenSource(new object())
            {
                Acquiring = _ =>
                {
                    focusDriver.CurrentValue = foreignFocus;
                    throw new InvalidOperationException("injected source failure");
                },
            };
            Register("Base", baseSource);
            Register("Failed", failedSource);
            var baseSession = controller.Open("Base").Session;
            focusDriver.CurrentValue = baseLast;

            var response = controller.Open("Failed");

            Assert.AreEqual(ScreenOpenKind.SourceFailed, response.Kind);
            Assert.IsNull(response.Session);
            Assert.IsInstanceOf<InvalidOperationException>(response.Exception);
            Assert.AreEqual(1, controller.Count);
            Assert.AreSame(baseSession, controller.Top);
            Assert.AreEqual(ScreenState.Active, baseSession.State);
            Assert.AreSame(baseLast, focusDriver.Current);
            Assert.AreEqual(0, failedSource.ReleaseCount);
        }

    #endregion

    #region O-3: 첫 스크린 실패 포커스 복원

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 이전 Screen이 없는 첫 Open 취소도 반환된 View 선택을 fallback으로 복원하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_첫Opening취소_빈StackFallback복원()
        {
            var transientFocus = new object();
            var handler = new TestStateHandler
            {
                Opening = context => context.Cancel(),
            };
            var source = new TestScreenSource(new object(), handler)
            {
                Acquiring = _ => focusDriver.CurrentValue = transientFocus,
            };
            Register("First", source);

            var response = controller.Open("First");

            Assert.AreEqual(ScreenOpenKind.Cancelled, response.Kind);
            Assert.AreSame(focusDriver.Fallback, focusDriver.CurrentValue);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, controller.Count);
        }

    #endregion

    #region T-1: 열기 닫기와 늦은 콜백

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Opening 중 Close가 열기 Transition을 취소하고,
        /// <br/> 늦은 완료 callback이 Session·훅·Source 반환을 다시 변경하지 않는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Opening중Close_늦은열기완료무시()
        {
            var transitioner = UseManualTransitioner();
            var handler = new TestStateHandler();
            var source = new TestScreenSource(new object(), handler);
            Register("Opening", source, openDuration: 1.0f, closeDuration: 1.0f);
            var session = controller.Open("Opening").Session;

            Assert.IsTrue(session.Close());
            Assert.AreEqual(ScreenState.Closing, session.State);
            Assert.AreEqual(2, transitioner.Count);

            var cancelledOpen = transitioner.CompleteNext();

            Assert.AreEqual(ScreenState.Closing, session.State);
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Closing",
                },
                handler.Calls
            );

            ManualTransitioner.InvokeLateCompletion(cancelledOpen);
            Assert.AreEqual(ScreenState.Closing, session.State);

            transitioner.CompleteNext();

            Assert.AreEqual(ScreenState.Closed, session.State);
            Assert.AreEqual(1, source.ReleaseCount);
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Closing",
                    "Closed",
                },
                handler.Calls
            );
        }

    #endregion

    #region T-2: 교체 실패와 성공

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Replace 시작 실패는 이전 top을 복원하고 다음 독립 요청은 이전 Screen만 제거하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Replace시작실패후다음요청_이전Top복원후교체()
        {
            var transitioner = UseManualTransitioner();
            var firstSource = new TestScreenSource(new object());
            var secondSource = new TestScreenSource(new object());
            Register("First", firstSource, openDuration: 1.0f, closeDuration: 1.0f);
            Register("Second", secondSource, openDuration: 1.0f, closeDuration: 1.0f);
            var first = controller.Open("First").Session;
            transitioner.CompleteNext();
            transitioner.FailNextPlay = true;

            var failed = controller.Replace("Second");

            Assert.AreEqual(ScreenOpenKind.TransitionFailed, failed.Kind);
            Assert.AreSame(first, controller.Top);
            Assert.AreEqual(ScreenState.Active, first.State);
            Assert.AreEqual(1, secondSource.ReleaseCount);

            var accepted = controller.Replace("Second");
            transitioner.CompleteNext();

            Assert.IsTrue(accepted.Accepted);
            Assert.AreSame(accepted.Session, controller.Top);
            Assert.AreEqual(ScreenState.Active, accepted.Session.State);
            Assert.AreEqual(ScreenState.Closing, first.State);
            Assert.AreEqual(1, controller.Count);

            transitioner.CompleteNext();

            Assert.AreEqual(ScreenState.Closed, first.State);
            Assert.AreEqual(1, firstSource.ReleaseCount);
            Assert.AreEqual(1, secondSource.ReleaseCount);

            Assert.IsTrue(accepted.Session.Close());
            transitioner.CompleteNext();
            Assert.AreEqual(2, secondSource.ReleaseCount);
        }

    #endregion

    #region T-3: 교체 트랜잭션

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Replace Opening Screen 종료가 이전 top을 복원하고,
        /// <br/> 성공 뒤 분리된 이전 Closing Screen도 Clear가 한 번 반환하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_ReplaceOpening종료와성공후Clear_모든Session한번반환()
        {
            var transitioner = UseManualTransitioner();
            var firstSource = new TestScreenSource(new object());
            var secondSource = new TestScreenSource(new object());
            Register("First", firstSource, openDuration: 1.0f, closeDuration: 1.0f);
            Register("Second", secondSource, openDuration: 1.0f, closeDuration: 1.0f);
            var first = controller.Open("First").Session;
            transitioner.CompleteNext();

            var cancelledReplace = controller.Replace("Second");
            Assert.IsTrue(cancelledReplace.Accepted);
            Assert.IsTrue(cancelledReplace.Session.Close());

            var lateOpening = transitioner.CompleteNext();
            ManualTransitioner.InvokeLateCompletion(lateOpening);
            transitioner.CompleteNext();

            Assert.AreSame(first, controller.Top);
            Assert.AreEqual(ScreenState.Active, first.State);
            Assert.AreEqual(1, secondSource.ReleaseCount);

            var successfulReplace = controller.Replace("Second");
            transitioner.CompleteNext();

            Assert.AreSame(successfulReplace.Session, controller.Top);
            Assert.AreEqual(ScreenState.Closing, first.State);
            Assert.AreEqual(1, controller.Count);

            controller.Clear();

            Assert.AreEqual(ScreenState.Closed, first.State);
            Assert.AreEqual(ScreenState.Closed, successfulReplace.Session.State);
            Assert.AreEqual(1, firstSource.ReleaseCount);
            Assert.AreEqual(2, secondSource.ReleaseCount);
            Assert.AreEqual(0, controller.Count);

            var lateClosing = transitioner.CompleteNext();
            ManualTransitioner.InvokeLateCompletion(lateClosing);
            Assert.AreEqual(1, firstSource.ReleaseCount);
            Assert.AreEqual(2, secondSource.ReleaseCount);
        }

    #endregion

    #region T-4: 전체 정리 배치 종료

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Clear가 새 획득 없이 모든 Screen을 top부터 즉시 닫고,
        /// <br/> 상태 훅과 Source를 한 번 정리하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Clear_모든Screen즉시종료하고새획득없음()
        {
            var baseHandler = new TestStateHandler();
            var childHandler = new TestStateHandler();
            var baseSource = new TestScreenSource(new object(), baseHandler);
            var childSource = new TestScreenSource(new object(), childHandler);
            Register("Base", baseSource);
            Register("Child", childSource);
            var baseSession = controller.Open("Base").Session;
            var childSession = controller.Open("Child").Session;

            controller.Clear();

            Assert.AreEqual(0, controller.Count);
            Assert.AreEqual(ScreenState.Closed, baseSession.State);
            Assert.AreEqual(ScreenState.Closed, childSession.State);
            Assert.AreEqual(1, baseSource.AcquireCount);
            Assert.AreEqual(1, childSource.AcquireCount);
            Assert.AreEqual(1, baseSource.ReleaseCount);
            Assert.AreEqual(1, childSource.ReleaseCount);
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Opened",
                    "Closing",
                    "Closed",
                },
                baseHandler.Calls
            );
            CollectionAssert.AreEqual
            (
                new[]
                {
                    "Opening",
                    "Opened",
                    "Closing",
                    "Closed",
                },
                childHandler.Calls
            );
        }

    #endregion

    #region C-1: 닫기 입력 장벽과 포커스

        // ----------------------------------------------------------------------
        /// <summary>
        /// 닫기 입력 해제 전에는 하위 Screen과 Focus가 복원되지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Close입력대기_이전Screen과Focus를해제뒤복원()
        {
            var baseDefault = new object();
            var baseLast = new object();
            var childDefault = new object();
            var sourceFocus = new object();
            var baseSource = new TestScreenSource(baseDefault);
            baseSource.Driver.AddFocusTarget(baseLast);
            var childSource = new TestScreenSource(childDefault)
            {
                Acquiring = _ => focusDriver.CurrentValue = sourceFocus,
            };
            Register("Base", baseSource);
            Register("Child", childSource);

            var baseSession = controller.Open("Base").Session;
            focusDriver.CurrentValue = baseLast;
            var childSession = controller.Open("Child").Session;
            var childInput = childSession.Resources.InputSession;
            inputDriver.HoldRelease = true;

            Assert.IsTrue(controller.Close());
            Assert.AreEqual(ScreenState.Covered, baseSession.State);
            Assert.IsTrue(childInput.IsAwaitingRelease);
            Assert.IsTrue(childInput.RetainsCursorWhileAwaitingRelease);
            Assert.AreNotSame(baseLast, focusDriver.Current);

            inputDriver.Complete(childInput);

            Assert.AreEqual(ScreenState.Active, baseSession.State);
            Assert.AreSame(baseLast, focusDriver.Current);
        }

    #endregion

    #region C-2: 닫기 종료 상태 정리

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 하위 Screen의 child 정리가 실패해도 닫힌 top 아래의 이전 Screen을 복원하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Close자식정리실패_이전Screen복원()
        {
            var baseSource = new TestScreenSource(new object());
            var childSource = new TestScreenSource(new object());
            Register("Base", baseSource);
            Register("Child", childSource);
            var baseSession = controller.Open("Base").Session;
            var childSession = controller.Open("Child").Session;
            var child = new ThrowingHandle();
            childSession.RegisterChild(child);
            LogAssert.Expect
            (
                LogType.Exception,
                new Regex("injected child release failure")
            );

            Assert.IsTrue(childSession.Close());

            Assert.AreEqual(ScreenState.Closed, childSession.State);
            Assert.AreEqual(ScreenState.Active, baseSession.State);
            Assert.IsTrue(baseSource.Driver.IsInteractable);
            Assert.AreEqual(1, controller.Count);
            Assert.AreEqual(1, inputDriver.Count);
            Assert.AreEqual(1, childSource.ReleaseCount);
            Assert.AreEqual(1, child.DisposeCount);

            Assert.DoesNotThrow(controller.Clear);
            Assert.AreEqual(1, childSource.ReleaseCount);
            Assert.AreEqual(1, child.DisposeCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 하위 Screen의 입력 해제가 실패해도 Session을 종결하고 이전 Screen을 복원하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Close입력해제실패_이전Screen복원()
        {
            var baseSource = new TestScreenSource(new object());
            var childSource = new TestScreenSource(new object());
            Register("Base", baseSource);
            Register("Child", childSource);
            var baseSession = controller.Open("Base").Session;
            var childSession = controller.Open("Child").Session;
            var childInput = childSession.Resources.InputSession;
            inputDriver.FailRelease = true;
            LogAssert.Expect
            (
                LogType.Exception,
                new Regex("injected input release failure")
            );

            Assert.IsTrue(childSession.Close());

            Assert.IsTrue(childInput.IsReleased);
            Assert.AreEqual(ScreenState.Closed, childSession.State);
            Assert.AreEqual(ScreenState.Active, baseSession.State);
            Assert.IsTrue(baseSource.Driver.IsInteractable);
            Assert.AreEqual(1, controller.Count);
            Assert.AreEqual(1, inputDriver.Count);
            Assert.AreEqual(1, childSource.ReleaseCount);

            var lateReleaseCallbackCount = 0;
            Assert.DoesNotThrow
            (
                () => childInput.Release
                (
                    waitForInputRelease: false,
                    retainCursorWhileAwaitingRelease: false,
                    onReleaseCompleted: () => lateReleaseCallbackCount++
                )
            );
            Assert.AreEqual(0, lateReleaseCallbackCount);

            inputDriver.FailRelease = false;
            Assert.DoesNotThrow(controller.Clear);
            Assert.AreEqual(1, childSource.ReleaseCount);
        }

    #endregion

    #region F-1: 스크린 결합 포커스 범위

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Persistent Focus Scope registration이 Primary 전환 뒤에도
        /// <br/> 마지막 Focus memory를 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FocusController_PrimaryScope_A_B_A_LastFocus복원()
        {
            var driver = new TestFocusDriver();
            var focus = new FocusController(driver);
            var firstDefault = new object();
            var firstLast = new object();
            var secondDefault = new object();
            var firstRegistration = focus.RegisterScope
            (
                new TestFocusScope(firstDefault, firstLast)
            );
            var secondRegistration = focus.RegisterScope(new TestFocusScope(secondDefault));

            focus.SetPrimary(firstRegistration);
            Assert.AreSame(firstDefault, driver.Current);

            driver.CurrentValue = firstLast;
            focus.HandleNativeFocusChanged(firstLast);
            focus.SetPrimary(secondRegistration);
            Assert.AreSame(secondDefault, driver.Current);

            focus.SetPrimary(firstRegistration);
            Assert.AreSame(firstLast, driver.Current);

            focus.SetPrimary(null);
            secondRegistration.Dispose();
            firstRegistration.Dispose();
        }

    #endregion

    #region X-1: 완료 훅 예외

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> OnOpened·OnClosing·OnClosed 예외가 상태 전이와 Source 반환을 막지 않고,
        /// <br/> Screen을 Active 또는 Closed 최종 상태로 수렴시키는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [TestCase("Opened")]
        [TestCase("Closing")]
        [TestCase("Closed")]
        public void TEST_ScreenController_완료훅예외_상태전이와Source반환계속
        (
            string failingHook
        )
        {
            var handler = new TestStateHandler();
            var failure = new InvalidOperationException
            (
                $"injected {failingHook} hook failure"
            );

            switch (failingHook)
            {
                case "Opened":
                    handler.Opened = _ => throw failure;
                    break;
                case "Closing":
                    handler.Closing = _ => throw failure;
                    break;
                case "Closed":
                    handler.Closed = _ => throw failure;
                    break;
            }

            var source = new TestScreenSource(new object(), handler);
            Register("Faulted Hook", source);
            LogAssert.Expect
            (
                LogType.Exception,
                new Regex($"InvalidOperationException: {Regex.Escape(failure.Message)}")
            );

            var response = controller.Open("Faulted Hook");

            Assert.IsTrue(response.Accepted);
            Assert.AreEqual(ScreenState.Active, response.Session.State);
            Assert.IsTrue(controller.Close());
            Assert.AreEqual(ScreenState.Closed, response.Session.State);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, controller.Count);
        }

    #endregion

    #region R-1: 교체 입력 정책

        // ----------------------------------------------------------------------
        /// <summary>
        /// Replace 입력 장벽이 제거한 Screen의 Cursor 정책을 유지하지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Replace입력대기_이전Cursor정책을유지하지않음()
        {
            var firstSource = new TestScreenSource(new object());
            var secondSource = new TestScreenSource(new object());
            Register("First", firstSource);
            Register("Second", secondSource);

            var firstSession = controller.Open("First").Session;
            var firstInput = firstSession.Resources.InputSession;
            inputDriver.HoldRelease = true;

            var response = controller.Replace("Second");

            Assert.IsTrue(response.Accepted);
            Assert.AreSame(response.Session, controller.Top);
            Assert.AreEqual(ScreenState.Active, response.Session.State);
            Assert.IsTrue(firstInput.IsAwaitingRelease);
            Assert.IsFalse(firstInput.RetainsCursorWhileAwaitingRelease);

            inputDriver.Complete(firstInput);
        }

    #endregion

    #region Y-1: 실패 트랜잭션 회귀

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Context contribution 적용 실패가 이미 바뀐 Session과
        /// <br/> Controller authority를 함께 롤백한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_InputContribution실패_전체Session과AuthorityRollback()
        {
            Register("Base", new TestScreenSource(new object()));
            Register("Child", new TestScreenSource(new object()));
            var baseSession = controller.Open("Base").Session;
            var childSession = controller.Open("Child").Session;
            var failedApplyCount = 0;

            inputDriver.ContributionChanged = session =>
            {
                if (session.IsContributionEnabled) return;

                failedApplyCount++;

                if (failedApplyCount == 2)
                {
                    throw new InvalidOperationException("injected contribution apply failure");
                }
            };

            Assert.Throws<InvalidOperationException>
            (
                () => controller.SetInputContributionEnabled(false)
            );
            Assert.IsTrue(baseSession.Resources.InputSession.IsContributionEnabled);
            Assert.IsTrue(childSession.Resources.InputSession.IsContributionEnabled);

            inputDriver.ContributionChanged = null;
            Assert.DoesNotThrow(() => controller.SetInputContributionEnabled(false));
            Assert.IsFalse(baseSession.Resources.InputSession.IsContributionEnabled);
            Assert.IsFalse(childSession.Resources.InputSession.IsContributionEnabled);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Cursor owner 적용 실패가 Controller authority를 commit하지 않아
        /// <br/> 같은 요청을 재시도할 수 있다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_CursorPolicy실패_ControllerFlagRollback후재시도()
        {
            Register("Base", new TestScreenSource(new object()));
            Register("Child", new TestScreenSource(new object()));
            controller.Open("Base");
            var childSession = controller.Open("Child").Session;
            var childInput = childSession.Resources.InputSession;
            var failNextDisable = true;

            inputDriver.ContributionChanged = session =>
            {
                if
                (
                    failNextDisable &&
                    ReferenceEquals(session, childInput) &&
                    !session.IsCursorPolicyEnabled
                )
                {
                    failNextDisable = false;
                    throw new InvalidOperationException("injected cursor policy failure");
                }
            };

            Assert.Throws<InvalidOperationException>
            (
                () => controller.SetCursorPolicyEnabled(false)
            );
            Assert.IsTrue(childInput.IsCursorPolicyEnabled);

            inputDriver.ContributionChanged = null;
            Assert.DoesNotThrow(() => controller.SetCursorPolicyEnabled(false));
            Assert.IsFalse(childInput.IsCursorPolicyEnabled);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Open 완료 interaction callback에서 Close가 끝나도
        /// <br/> 바깥 완료 흐름이 Session을 다시 Active로 만들지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_Open완료Interactable재진입Close_ClosedSession부활방지()
        {
            var transitioner = UseManualTransitioner();
            var handler = new TestStateHandler();
            var source = new TestScreenSource(new object(), handler);
            Register("Opening", source, openDuration: 1.0f, closeDuration: 1.0f);
            var response = controller.Open("Opening");
            var closeRequested = false;

            source.Driver.InteractableChanged = interactable =>
            {
                if (!interactable || closeRequested) return;

                closeRequested = true;
                response.Session.Close();
            };

            transitioner.CompleteNext();

            Assert.IsTrue(closeRequested);
            Assert.AreEqual(ScreenState.Closing, response.Session.State);
            CollectionAssert.DoesNotContain(handler.Calls, "Opened");

            transitioner.CompleteNext();

            Assert.AreEqual(ScreenState.Closed, response.Session.State);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, controller.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Opening Transition Cancel backend가 실패해도
        /// <br/> Close는 terminal cleanup까지 계속 진행한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_OpeningCancel실패_CloseTerminal계속()
        {
            var transitioner = UseManualTransitioner();
            var source = new TestScreenSource(new object());
            Register("Opening", source, openDuration: 1.0f, closeDuration: 1.0f);
            var session = controller.Open("Opening").Session;
            transitioner.FailNextCancel = true;
            LogAssert.Expect
            (
                LogType.Exception,
                new Regex("injected transition cancel failure")
            );

            Assert.IsTrue(session.Close());
            Assert.AreEqual(ScreenState.Closing, session.State);
            Assert.AreEqual(1, transitioner.CancelCount);

            transitioner.CompleteNext();
            Assert.AreEqual(ScreenState.Closing, session.State);

            transitioner.CompleteNext();

            Assert.AreEqual(ScreenState.Closed, session.State);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, controller.Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Focus record 제거 중 native 선택이 실패해도 Source/Input을 반환하고
        /// <br/> 이전 Screen을 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_CloseFocusRemove실패_TerminalCleanup계속()
        {
            var baseSource = new TestScreenSource(new object());
            var childSource = new TestScreenSource(new object());
            Register("Base", baseSource);
            Register("Child", childSource);
            var baseSession = controller.Open("Base").Session;
            var childSession = controller.Open("Child").Session;
            var failNextSelection = true;

            focusDriver.Selected = _ =>
            {
                if (!failNextSelection) return;

                failNextSelection = false;
                throw new InvalidOperationException("injected focus remove failure");
            };
            LogAssert.Expect
            (
                LogType.Exception,
                new Regex("injected focus remove failure")
            );

            Assert.IsTrue(childSession.Close());

            Assert.AreEqual(ScreenState.Closed, childSession.State);
            Assert.AreEqual(ScreenState.Active, baseSession.State);
            Assert.AreEqual(1, childSource.ReleaseCount);
            Assert.AreEqual(1, inputDriver.Count);
            Assert.AreEqual(1, controller.Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Stack observer 실패가 Clear의 EndBatch·Focus cleanup과
        /// <br/> 모든 Session terminal 처리를 막지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_ClearStackObserver실패_후속Cleanup계속()
        {
            var source = new TestScreenSource(new object());
            Register("Base", source);
            var session = controller.Open("Base").Session;
            var observed = 0;
            controller.OnStackChanged += () =>
            {
                if (observed == 0)
                {
                    throw new InvalidOperationException("injected stack observer failure");
                }
            };
            controller.OnStackChanged += () => observed++;

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: injected stack observer failure");
            controller.Clear();
            Assert.Greater(observed, 0);
            Assert.AreEqual(ScreenState.Closed, session.State);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, inputDriver.Count);
            Assert.AreEqual(1, inputDriver.BeginBatchCount);
            Assert.AreEqual(1, inputDriver.EndBatchCount);
            Assert.AreEqual(0, controller.Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 이전 Screen interaction callback에서 Runtime이 종료되면
        /// <br/> 준비 Session을 다시 Stack에 공개하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenController_이전Screen비활성화중Shutdown_준비Session재공개방지()
        {
            var baseSource = new TestScreenSource(new object());
            var childSource = new TestScreenSource(new object());
            Register("Base", baseSource);
            Register("Child", childSource);
            controller.Open("Base");
            baseSource.Driver.InteractableChanged = interactable =>
            {
                if (!interactable)
                {
                    controller.Shutdown();
                }
            };

            var response = controller.Open("Child");

            Assert.AreEqual(ScreenOpenKind.Rejected, response.Kind);
            Assert.IsNull(response.Session);
            Assert.IsFalse(controller.IsAvailable);
            Assert.AreEqual(0, controller.Count);
            Assert.AreEqual(1, baseSource.ReleaseCount);
            Assert.AreEqual(1, childSource.ReleaseCount);
            Assert.AreEqual(0, inputDriver.Count);
        }

    #endregion

    }
}
