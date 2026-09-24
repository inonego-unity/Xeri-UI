/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_InputAndFocus.cs
수정일 : 2026-10-03

# 설명
UGUI Focus backend 유효성, persistent Focus record의 Primary/Override/Authority 복원과 Spotlight 요청 수명을 검증한다.

# 테스트 구성
 F: UGUI backend Focus 유효성과 Layer 소유권
 P: persistent Scope와 Primary LastFocus
 O: Override Stack, LastFocus와 activation 실패 rollback
 A: Context authority suspend/resume와 logical containment
 I: Screen Input Session policy rollback
 S: Spotlight 활성화 재진입
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// UGUI Focus와 Spotlight의 유효 대상·요청 수명 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_InputAndFocus
    {

    #region 필드

        private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

    #endregion

    #region 헬퍼

        // ======================================================================
        /// <summary>
        /// 표시 적용 중 외부 callback을 실행하는 UGUI Spotlight 테스트 backend.
        /// </summary>
        // ======================================================================
        private sealed class TestSpotlightDriver : ISpotlightDriver<UGUISpotlightParams>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 backend는 항상 유효하다.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsValid => true;

            // ------------------------------------------------------------
            /// <summary>
            /// 현재 Spotlight 표시 여부.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsVisible { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 표시 적용 중 실행할 테스트 callback.
            /// </summary>
            // ------------------------------------------------------------
            public System.Action Showing { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 표시 상태를 적용한 뒤 외부 callback을 실행한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Show(UGUISpotlightParams parameters)
            {
                IsVisible = true;
                Showing?.Invoke();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Spotlight 표시를 종료한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Hide()
            {
                IsVisible = false;
            }
        }

        // ================================================================================
        /// <summary>
        /// Layer registration과 rollback 실패를 주입하는 Focus Component backend.
        /// </summary>
        // ================================================================================
        private sealed class LayerRegistrationFocusDriver : FocusDriverBehaviour
        {
            public int RegistrationCount { get; private set; } = 0;
            public bool ThrowOnRegister { get; set; } = false;
            public bool ThrowOnUnregister { get; set; } = false;

            public override object Current => null;

            public override bool CanSelect(object target)
            {
                return false;
            }

            public override bool IsValid(object target)
            {
                return false;
            }

            public override void Select(object target)
            {
                // NONE
            }

            public override object FindFallback()
            {
                return null;
            }

            protected override void HandleLayerRegistered(IPresentationLayerDriver driver)
            {
                RegistrationCount++;

                if (ThrowOnRegister)
                {
                    ThrowOnRegister = false;
                    throw new InvalidOperationException("injected layer registration failure");
                }
            }

            protected override void HandleLayerUnregistered(IPresentationLayerDriver driver)
            {
                if (RegistrationCount > 0)
                {
                    RegistrationCount--;
                }

                if (ThrowOnUnregister)
                {
                    ThrowOnUnregister = false;
                    throw new InvalidOperationException("injected layer unregister failure");
                }
            }
        }

        // ============================================================
        /// <summary>
        /// 공통 Focus backend 전환의 partial native 적용 실패를 재현한다.
        /// </summary>
        // ============================================================
        private sealed class SelectionFocusDriver : FocusDriverBehaviour
        {
            public object Target { get; } = new object();
            public object CurrentValue { get; private set; } = null;
            public int SelectFailureCount { get; set; } = 0;

            public override object Current => CurrentValue;

            public override bool CanSelect(object target)
            {
                return ReferenceEquals(target, Target);
            }

            public override bool IsValid(object target)
            {
                return ReferenceEquals(target, Target);
            }

            public override void Select(object target)
            {
                CurrentValue = IsValid(target) ? target : null;

                if (SelectFailureCount <= 0) return;

                SelectFailureCount--;
                throw new InvalidOperationException("injected native focus failure");
            }

            public override object FindFallback()
            {
                return null;
            }

            public void Publish(object target)
            {
                CurrentValue = IsValid(target) ? target : null;
                NotifyFocusChanged();
            }
        }

        // ============================================================
        /// <summary>
        /// Focus 선택과 유효 대상을 기록하는 테스트 backend.
        /// </summary>
        // ============================================================
        private sealed class TestFocusDriver : IFocusDriver
        {
            private readonly List<object> validTargets = new List<object>();

            public object Current
            {
                get
                {
                    if (CurrentFailureCount > 0)
                    {
                        CurrentFailureCount--;
                        throw new InvalidOperationException("Focus 조회 실패");
                    }

                    return current;
                }
                private set => current = value;
            }

            public object Fallback { get; set; }
            public int SelectFailureCount { get; set; }
            public int CurrentFailureCount { get; set; }

            private object current = null;

            public void AddValid(object target)
            {
                validTargets.Add(target);
            }

            public bool IsValid(object target)
            {
                return target != null && validTargets.Contains(target);
            }

            public void Select(object target)
            {
                if (SelectFailureCount > 0)
                {
                    SelectFailureCount--;
                    throw new InvalidOperationException("Focus 선택 실패");
                }

                Current = IsValid(target) ? target : null;
            }

            public object FindFallback()
            {
                return IsValid(Fallback) ? Fallback : null;
            }
        }

        // ============================================================
        /// <summary>
        /// 지정 대상 집합을 persistent Focus Scope로 제공한다.
        /// </summary>
        // ============================================================
        private sealed class TestFocusScope : IFocusScope
        {
            private readonly List<object> targets = new List<object>();

            public object DefaultFocus { get; }

            public TestFocusScope(object defaultFocus, params object[] targets)
            {
                DefaultFocus = defaultFocus;
                this.targets.AddRange(targets);
            }

            public bool ContainsFocus(object target)
            {
                return target != null && targets.Contains(target);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// private 직렬화 필드를 테스트 값으로 설정한다.
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

        // ------------------------------------------------------------
        /// <summary>
        /// UGUI Selectable GameObject를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private GameObject CreateSelectable(string name)
        {
            var gameObject = new GameObject
            (
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            ownedObjects.Add(gameObject);
            return gameObject;
        }

    #endregion

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 생성한 Unity Object를 역순 제거한다.
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

    #region F-1: UGUI 포커스 유효성

        // --------------------------------------------------------------------------------
        /// <summary>
        /// disabled, inactive와 파괴된 Selectable을 Focus 대상으로 인정하지 않는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UGUIFocusDriver_비활성Selectable_유효Focus거부()
        {
            var eventSystemObject = new GameObject("EventSystem");
            ownedObjects.Add(eventSystemObject);
            var eventSystem = eventSystemObject.AddComponent<EventSystem>();
            var driverObject = new GameObject("Focus Driver");
            ownedObjects.Add(driverObject);
            var driver = driverObject.AddComponent<UGUIFocusDriver>();
            var layerObject = new GameObject("UGUI Layer", typeof(RectTransform));
            ownedObjects.Add(layerObject);
            var layer = new UGUIPresentationLayer(layerObject.GetComponent<RectTransform>());
            var target = CreateSelectable("Target");
            var fallback = CreateSelectable("Fallback");
            target.transform.SetParent(layerObject.transform, false);
            fallback.transform.SetParent(layerObject.transform, false);
            SetField(driver, "eventSystem", eventSystem);
            SetField(driver, "fallback", fallback);
            driver.RegisterLayer(layer);

            Assert.IsTrue(driver.IsValid(target));
            target.GetComponent<Button>().enabled = false;
            Assert.IsFalse(driver.IsValid(target));

            fallback.GetComponent<Button>().enabled = false;
            Assert.IsNull(driver.FindFallback());

            target.GetComponent<Button>().enabled = true;
            target.SetActive(false);
            Assert.IsFalse(driver.IsValid(target));

            target.SetActive(true);
            UnityEngine.Object.DestroyImmediate(target);
            Assert.IsFalse(driver.IsValid(target));
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 등록 UGUI Layer 밖의 EventSystem 선택은 UGUI Focus 소유권으로 취급하거나 지우지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UGUIFocusDriver_비소유EventSystem선택_SelectNull로보존()
        {
            var eventSystemObject = new GameObject("EventSystem");
            ownedObjects.Add(eventSystemObject);
            var eventSystem = eventSystemObject.AddComponent<EventSystem>();
            var driverObject = new GameObject("Focus Driver");
            ownedObjects.Add(driverObject);
            var driver = driverObject.AddComponent<UGUIFocusDriver>();
            var layerObject = new GameObject("UGUI Layer", typeof(RectTransform));
            ownedObjects.Add(layerObject);
            var layer = new UGUIPresentationLayer(layerObject.GetComponent<RectTransform>());
            var owned = CreateSelectable("Owned");
            var external = new GameObject("External EventSystem Selection");
            ownedObjects.Add(external);
            owned.transform.SetParent(layerObject.transform, false);
            SetField(driver, "eventSystem", eventSystem);
            driver.RegisterLayer(layer);

            eventSystem.SetSelectedGameObject(external);

            Assert.IsFalse(driver.IsValid(external));
            Assert.IsNull(driver.Current);
            driver.Select(null);
            Assert.AreSame(external, eventSystem.currentSelectedGameObject);

            driver.Select(owned);
            Assert.AreSame(owned, eventSystem.currentSelectedGameObject);
            driver.Select(null);
            Assert.IsNull(eventSystem.currentSelectedGameObject);
        }

    #endregion

    #region F-2: 레이어 바인딩 롤백

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 뒤 Focus backend 등록과 중간 rollback이 실패해도 앞서 등록된 Layer를 모두 해제한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIFocusDriver_Layer등록실패_Rollback끝까지수행()
        {
            var host = new GameObject("Focus Host");
            ownedObjects.Add(host);
            var first = host.AddComponent<LayerRegistrationFocusDriver>();
            var second = host.AddComponent<LayerRegistrationFocusDriver>();
            var third = host.AddComponent<LayerRegistrationFocusDriver>();
            var focus = host.AddComponent<UIFocusDriver>();
            second.ThrowOnUnregister = true;
            third.ThrowOnRegister = true;

            var layerObject = new GameObject("Layer", typeof(RectTransform));
            ownedObjects.Add(layerObject);
            var layer = new UGUIPresentationLayer(layerObject.GetComponent<RectTransform>());

            focus.Initialize();

            var exception = Assert.Throws<AggregateException>
            (
                () => focus.BindLayer(layer)
            );

            Assert.GreaterOrEqual(exception.InnerExceptions.Count, 2);
            Assert.AreEqual(0, first.RegistrationCount);
            Assert.AreEqual(0, second.RegistrationCount);
            Assert.AreEqual(0, third.RegistrationCount);
        }

    #endregion

    #region F-3: 공통 포커스 백엔드 전환 롤백

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 새 native Focus backend 적용이 partial 실패하면 이전 backend 선택을 복원하고
        /// <br/> 같은 대상 전환의 재시도를 허용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIFocusDriver_Select실패_이전Backend복원후재시도()
        {
            var host = new GameObject("Common Focus Host");
            ownedObjects.Add(host);
            var first = host.AddComponent<SelectionFocusDriver>();
            var second = host.AddComponent<SelectionFocusDriver>();
            var focus = host.AddComponent<UIFocusDriver>();
            focus.Initialize();

            focus.Select(first.Target);
            Assert.AreSame(first.Target, focus.Current);

            second.SelectFailureCount = 1;

            Assert.Throws<InvalidOperationException>
            (
                () => focus.Select(second.Target)
            );

            Assert.AreSame(first.Target, focus.Current);
            Assert.AreSame(first.Target, first.Current);
            Assert.IsNull(second.Current);

            Assert.DoesNotThrow(() => focus.Select(second.Target));
            Assert.AreSame(second.Target, focus.Current);
            Assert.IsNull(first.Current);
            Assert.AreSame(second.Target, second.Current);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> native Focus 변경 중 다른 backend clear 하나가 실패해도 나머지 backend와
        /// <br/> Runtime observer까지 처리한 뒤 실패를 전달한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UIFocusDriver_Native변경_Clear실패_나머지정리와Observer계속()
        {
            var host = new GameObject("Native Focus Host");
            ownedObjects.Add(host);
            var first = host.AddComponent<SelectionFocusDriver>();
            var second = host.AddComponent<SelectionFocusDriver>();
            var third = host.AddComponent<SelectionFocusDriver>();
            var focus = host.AddComponent<UIFocusDriver>();
            var observerCount = 0;
            object observed = null;
            focus.Initialize();
            focus.OnFocusChanged += target =>
            {
                observerCount++;
                observed = target;
            };
            first.Select(first.Target);
            third.Select(third.Target);
            first.SelectFailureCount = 1;

            Assert.Throws<InvalidOperationException>
            (
                () => second.Publish(second.Target)
            );

            Assert.IsNull(first.Current);
            Assert.IsNull(third.Current);
            Assert.AreSame(second.Target, focus.Current);
            Assert.AreEqual(1, observerCount);
            Assert.AreSame(second.Target, observed);
        }

    #endregion

    #region P-1: 영속 주 마지막 포커스

        [Test]
        public void TEST_FocusController_Primary_A_B_A_각ScopeLastFocus복원()
        {
            var fallback = new object();
            var firstDefault = new object();
            var firstLast = new object();
            var secondDefault = new object();
            var secondLast = new object();
            var driver = new TestFocusDriver
            {
                Fallback = fallback,
            };
            driver.AddValid(fallback);
            driver.AddValid(firstDefault);
            driver.AddValid(firstLast);
            driver.AddValid(secondDefault);
            driver.AddValid(secondLast);
            var controller = new FocusController(driver);
            var firstRegistration = controller.RegisterScope
            (
                new TestFocusScope(firstDefault, firstDefault, firstLast)
            );
            var secondRegistration = controller.RegisterScope
            (
                new TestFocusScope(secondDefault, secondDefault, secondLast)
            );

            controller.SetPrimary(firstRegistration);
            driver.Select(firstLast);
            controller.HandleNativeFocusChanged(firstLast);
            controller.SetPrimary(secondRegistration);
            driver.Select(secondLast);
            controller.HandleNativeFocusChanged(secondLast);

            controller.SetPrimary(firstRegistration);
            Assert.AreSame(firstLast, driver.Current);

            controller.SetPrimary(secondRegistration);
            Assert.AreSame(secondLast, driver.Current);

            secondRegistration.Dispose();
            firstRegistration.Dispose();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Primary Focus native 적용 실패는 이전 Primary와 native Focus를 복원하고
        /// <br/> 같은 요청의 재시도를 허용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_FocusController_SetPrimary_Select실패_PrimaryRollback후재시도()
        {
            var firstDefault = new object();
            var secondDefault = new object();
            var driver = new TestFocusDriver();
            driver.AddValid(firstDefault);
            driver.AddValid(secondDefault);
            var controller = new FocusController(driver);
            var first = controller.RegisterScope
            (
                new TestFocusScope(firstDefault, firstDefault)
            );
            var second = controller.RegisterScope
            (
                new TestFocusScope(secondDefault, secondDefault)
            );

            controller.SetPrimary(first);
            Assert.AreSame(firstDefault, driver.Current);

            driver.SelectFailureCount = 1;

            Assert.Throws<InvalidOperationException>
            (
                () => controller.SetPrimary(second)
            );

            Assert.AreSame(firstDefault, driver.Current);

            Assert.DoesNotThrow(() => controller.SetPrimary(second));
            Assert.AreSame(secondDefault, driver.Current);

            second.Dispose();
            first.Dispose();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Scope 해제 중 Focus 기억 조회가 실패해도 등록을 terminal로 제거하고
        /// <br/> 같은 Scope 재등록을 허용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FocusController_Scope해제_Capture실패_Terminal제거후재등록()
        {
            var defaultFocus = new object();
            var scope = new TestFocusScope(defaultFocus, defaultFocus);
            var driver = new TestFocusDriver();
            driver.AddValid(defaultFocus);
            var controller = new FocusController(driver);
            var registration = controller.RegisterScope(scope);
            controller.SetPrimary(registration);
            driver.CurrentFailureCount = 1;

            Assert.Throws<InvalidOperationException>(registration.Dispose);
            Assert.IsTrue(registration.IsDisposed);

            var retry = controller.RegisterScope(scope);
            Assert.DoesNotThrow(() => controller.SetPrimary(retry));

            retry.Dispose();
        }

    #endregion

    #region O-1: 오버라이드 스택 마지막 포커스

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 중첩 Focus Scope는 각 범위의 마지막 선택을 기억하고,
        /// <br/> 해제 역순으로 이전 Scope와 Runtime fallback을 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FocusController_FocusScope_중첩Last와Fallback_복원()
        {
            var fallback = new object();
            var firstDefault = new object();
            var firstLast = new object();
            var secondDefault = new object();
            var driver = new TestFocusDriver
            {
                Fallback = fallback,
            };
            driver.AddValid(fallback);
            driver.AddValid(firstDefault);
            driver.AddValid(firstLast);
            driver.AddValid(secondDefault);

            var controller = new FocusController(driver);
            var firstScope = new TestFocusScope(firstDefault, firstDefault, firstLast);
            var secondScope = new TestFocusScope(secondDefault, secondDefault);
            var firstRegistration = controller.RegisterScope(firstScope);
            var secondRegistration = controller.RegisterScope(secondScope);
            var first = controller.PushOverride(firstRegistration);

            Assert.AreSame(firstDefault, driver.Current);

            driver.Select(firstLast);
            controller.HandleNativeFocusChanged(firstLast);
            var second = controller.PushOverride(secondRegistration);

            Assert.AreSame(secondDefault, driver.Current);

            second.Dispose();

            Assert.AreSame(firstLast, driver.Current);

            first.Dispose();

            Assert.AreSame(fallback, driver.Current);
            secondRegistration.Dispose();
            firstRegistration.Dispose();
        }

        [Test]
        public void TEST_FocusController_PushOverride_Select실패_Override롤백후재시도가능()
        {
            var fallback = new object();
            var overrideDefault = new object();
            var driver = new TestFocusDriver
            {
                Fallback = fallback,
            };
            driver.AddValid(fallback);
            driver.AddValid(overrideDefault);
            driver.Select(fallback);
            var controller = new FocusController(driver);
            var registration = controller.RegisterScope
            (
                new TestFocusScope(overrideDefault, overrideDefault)
            );
            driver.SelectFailureCount = 1;

            Assert.Throws<InvalidOperationException>
            (
                () => controller.PushOverride(registration)
            );

            Assert.AreSame(fallback, driver.Current);

            var retry = controller.PushOverride(registration);

            Assert.AreSame(overrideDefault, driver.Current);

            retry.Dispose();

            Assert.AreSame(fallback, driver.Current);
            registration.Dispose();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> top Override 해제 중 Focus 기억 조회가 실패해도 Lease 소유권을 제거하고
        /// <br/> 같은 Override 재획득을 허용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_FocusController_Override해제_Capture실패_Terminal제거후재획득()
        {
            var fallback = new object();
            var overrideDefault = new object();
            var driver = new TestFocusDriver
            {
                Fallback = fallback,
            };
            driver.AddValid(fallback);
            driver.AddValid(overrideDefault);
            driver.Select(fallback);
            var controller = new FocusController(driver);
            var registration = controller.RegisterScope
            (
                new TestFocusScope(overrideDefault, overrideDefault)
            );
            var authority = controller.PushOverride(registration);
            driver.CurrentFailureCount = 1;

            Assert.Throws<InvalidOperationException>(authority.Dispose);
            Assert.IsTrue(authority.IsDisposed);
            Assert.AreSame(fallback, driver.Current);

            var retry = controller.PushOverride(registration);
            Assert.AreSame(overrideDefault, driver.Current);

            retry.Dispose();
            registration.Dispose();
        }

    #endregion

    #region A-1: 권한 일시중단과 재개

        [Test]
        public void TEST_FocusController_SuspendResume_비활성중변경무시하고LastFocus복원()
        {
            var defaultFocus = new object();
            var lastFocus = new object();
            var external = new object();
            var driver = new TestFocusDriver();
            driver.AddValid(defaultFocus);
            driver.AddValid(lastFocus);
            driver.AddValid(external);
            var controller = new FocusController(driver);
            var registration = controller.RegisterScope
            (
                new TestFocusScope(defaultFocus, defaultFocus, lastFocus)
            );

            controller.SetPrimary(registration);
            driver.Select(lastFocus);
            controller.HandleNativeFocusChanged(lastFocus);
            controller.Suspend();

            driver.Select(external);
            controller.HandleNativeFocusChanged(external);
            controller.Resume();

            Assert.AreSame(lastFocus, driver.Current);
            registration.Dispose();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Resume의 native Focus 적용 실패는 authority 획득을 확정하지 않고
        /// <br/> 다음 Resume에서 같은 Focus 적용을 재시도한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FocusController_Resume_Select실패_AuthorityRollback후재시도()
        {
            var defaultFocus = new object();
            var driver = new TestFocusDriver();
            driver.AddValid(defaultFocus);
            var controller = new FocusController(driver);
            var registration = controller.RegisterScope
            (
                new TestFocusScope(defaultFocus, defaultFocus)
            );

            controller.SetPrimary(registration);
            controller.Suspend();
            driver.SelectFailureCount = 1;

            Assert.Throws<InvalidOperationException>(controller.Resume);
            Assert.IsFalse(controller.HasAuthority);

            Assert.DoesNotThrow(controller.Resume);
            Assert.IsTrue(controller.HasAuthority);
            Assert.AreSame(defaultFocus, driver.Current);

            registration.Dispose();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Suspend의 Focus 기억 조회가 실패해도 authority 반환은 terminal로 확정한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FocusController_Suspend_Capture실패_Authority반환()
        {
            var defaultFocus = new object();
            var driver = new TestFocusDriver();
            driver.AddValid(defaultFocus);
            var controller = new FocusController(driver);
            var registration = controller.RegisterScope
            (
                new TestFocusScope(defaultFocus, defaultFocus)
            );
            controller.SetPrimary(registration);
            driver.CurrentFailureCount = 1;

            Assert.Throws<InvalidOperationException>(controller.Suspend);
            Assert.IsFalse(controller.HasAuthority);

            Assert.DoesNotThrow(controller.Resume);
            Assert.IsTrue(controller.HasAuthority);
            registration.Dispose();
        }

    #endregion

    #region A-2: 논리 포커스 포함 관계

        [Test]
        public void TEST_FocusController_EffectiveScope밖NativeFocus_LastFocus로복구()
        {
            var defaultFocus = new object();
            var lastFocus = new object();
            var external = new object();
            var driver = new TestFocusDriver();
            driver.AddValid(defaultFocus);
            driver.AddValid(lastFocus);
            driver.AddValid(external);
            var controller = new FocusController(driver);
            var registration = controller.RegisterScope
            (
                new TestFocusScope(defaultFocus, defaultFocus, lastFocus)
            );

            controller.SetPrimary(registration);
            driver.Select(lastFocus);
            controller.HandleNativeFocusChanged(lastFocus);

            driver.Select(external);
            controller.HandleNativeFocusChanged(external);

            Assert.AreSame(lastFocus, driver.Current);
            registration.Dispose();
        }

    #endregion

    #region A-3: 네이티브 포커스 해제

        [Test]
        public void TEST_FocusController_NativeFocusNull_해제허용하고LastFocus보존()
        {
            var defaultFocus = new object();
            var lastFocus = new object();
            var driver = new TestFocusDriver();
            driver.AddValid(defaultFocus);
            driver.AddValid(lastFocus);
            var controller = new FocusController(driver);
            var registration = controller.RegisterScope
            (
                new TestFocusScope(defaultFocus, defaultFocus, lastFocus)
            );

            controller.SetPrimary(registration);
            driver.Select(lastFocus);
            controller.HandleNativeFocusChanged(lastFocus);

            driver.Select(null);
            controller.HandleNativeFocusChanged(null);

            Assert.IsNull(driver.Current);

            controller.Suspend();
            controller.Resume();

            Assert.AreSame(lastFocus, driver.Current);
            registration.Dispose();
        }

    #endregion

    #region I-1: 스크린 입력 세션 정책 롤백

        // --------------------------------------------------------------------------------
        /// <summary>
        /// contribution 적용 callback이 실패하면 local enabled flag를 이전 값으로 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenInputSession_ContributionApply실패_LocalFlagRollback()
        {
            var appliedContribution = true;
            var failNextApply = true;
            var session = new ScreenInputSession
            (
                new ScreenOptions("Input.Test"),
                (_, _, _) => { },
                current =>
                {
                    appliedContribution = current.IsContributionEnabled;

                    if (failNextApply)
                    {
                        failNextApply = false;
                        throw new InvalidOperationException("injected contribution failure");
                    }
                }
            );

            Assert.IsTrue(session.IsContributionEnabled);

            Assert.Throws<InvalidOperationException>
            (
                () => session.SetContributionEnabled(false)
            );

            Assert.IsTrue(session.IsContributionEnabled);
            Assert.IsTrue(appliedContribution);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Cursor policy 적용 callback이 실패하면 local owner flag를 이전 값으로 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ScreenInputSession_CursorApply실패_LocalFlagRollback()
        {
            var appliedCursorPolicy = false;
            var failNextApply = true;
            var session = new ScreenInputSession
            (
                new ScreenOptions("Cursor.Test"),
                (_, _, _) => { },
                current =>
                {
                    appliedCursorPolicy = current.IsCursorPolicyEnabled;

                    if (failNextApply)
                    {
                        failNextApply = false;
                        throw new InvalidOperationException("injected cursor failure");
                    }
                }
            );

            Assert.IsFalse(session.IsCursorPolicyEnabled);

            Assert.Throws<InvalidOperationException>
            (
                () => session.SetCursorPolicyEnabled(true)
            );

            Assert.IsFalse(session.IsCursorPolicyEnabled);
            Assert.IsFalse(appliedCursorPolicy);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> release 완료 observer 하나가 실패해도 뒤 observer를 호출하고
        /// <br/> Session은 Terminal로 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenInputSession_ReleaseObserver실패_뒤Observer호출()
        {
            var session = new ScreenInputSession
            (
                new ScreenOptions("Release.Callback"),
                (current, _, _) => current.MarkAwaitingRelease(false)
            );
            var secondCallbackCount = 0;

            session.Release
            (
                true,
                onReleaseCompleted: () =>
                    throw new InvalidOperationException("injected release observer failure")
            );
            session.Release
            (
                true,
                onReleaseCompleted: () => secondCallbackCount++
            );

            Assert.Throws<InvalidOperationException>(() => session.MarkReleased());

            Assert.IsTrue(session.IsReleased);
            Assert.AreEqual(1, secondCallbackCount);
        }

    #endregion

    #region S-1: 스포트라이트 활성화 재진입

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Spotlight Root 활성화 callback에서 Controller가 종료되면 요청을 공개하지 않고,
        /// <br/> Root와 Graphic 표시 상태를 함께 정리하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UGUISpotlight_Show중Dispose_표시정리와Lease미반환()
        {
            var target = new GameObject("Spotlight Target", typeof(RectTransform));
            ownedObjects.Add(target);
            var driver = new TestSpotlightDriver();
            var spotlight = new UGUISpotlight();
            driver.Showing = spotlight.Dispose;
            var parameters = new UGUISpotlightParams
            (
                new[]
                {
                    new UGUISpotlightTarget(target.GetComponent<RectTransform>()),
                }
            );

            Assert.Throws<System.ObjectDisposedException>
            (
                () => spotlight.Show(driver, parameters)
            );

            Assert.IsFalse(driver.IsVisible);
            Assert.DoesNotThrow(spotlight.Dispose);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// top 요청 해제 중 이전 Spotlight 복원이 실패하면 이미 해제된 표시를 ghost로 남기지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UGUISpotlight_Top해제복원실패_Ghost표시정리()
        {
            var target = new GameObject("Spotlight Target", typeof(RectTransform));
            ownedObjects.Add(target);
            var driver = new TestSpotlightDriver();
            var spotlight = new UGUISpotlight();
            var parameters = new UGUISpotlightParams
            (
                new[]
                {
                    new UGUISpotlightTarget(target.GetComponent<RectTransform>()),
                }
            );
            var first = spotlight.Show(driver, parameters);
            var second = spotlight.Show(driver, parameters);
            driver.Showing = () =>
                throw new InvalidOperationException("injected spotlight restore failure");

            Assert.Throws<InvalidOperationException>(second.Dispose);
            Assert.IsFalse(driver.IsVisible);

            driver.Showing = null;
            Assert.DoesNotThrow(first.Dispose);
            Assert.DoesNotThrow(spotlight.Dispose);
        }

    #endregion

    }
}
