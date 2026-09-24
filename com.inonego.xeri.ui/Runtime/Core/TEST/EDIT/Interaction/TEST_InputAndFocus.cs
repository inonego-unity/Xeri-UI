/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_InputAndFocus.cs
수정일 : 2026-09-30

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

        // ============================================================
        /// <summary>
        /// Focus 선택과 유효 대상을 기록하는 테스트 backend.
        /// </summary>
        // ============================================================
        private sealed class TestFocusDriver : IFocusDriver
        {
            private readonly List<object> validTargets = new List<object>();

            public object Current { get; private set; }
            public object Fallback { get; set; }
            public int SelectFailureCount { get; set; }

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

    #region F-1: UGUI Focus 유효성

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

    #region P-1: persistent Primary LastFocus

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

    #endregion

    #region O-1: Override Stack LastFocus

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

    #endregion

    #region A-1: Authority suspend와 resume

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

    #endregion

    #region A-2: logical Focus containment

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

    #region A-3: native Focus 해제

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

    #region I-1: Screen Input Session policy rollback

        // ----------------------------------------------------------------------
        /// <summary>
        /// contribution 적용 callback이 실패하면 local enabled flag를 이전 값으로 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenInputSession_ContributionApply실패_LocalFlagRollback()
        {
            var session = new ScreenInputSession
            (
                new ScreenOptions("Input.Test"),
                (_, _, _) => { },
                _ => throw new InvalidOperationException("injected contribution failure")
            );

            Assert.IsTrue(session.IsContributionEnabled);

            Assert.Throws<InvalidOperationException>
            (
                () => session.SetContributionEnabled(false)
            );

            Assert.IsTrue(session.IsContributionEnabled);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Cursor policy 적용 callback이 실패하면 local owner flag를 이전 값으로 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScreenInputSession_CursorApply실패_LocalFlagRollback()
        {
            var session = new ScreenInputSession
            (
                new ScreenOptions("Cursor.Test"),
                (_, _, _) => { },
                _ => throw new InvalidOperationException("injected cursor failure")
            );

            Assert.IsFalse(session.IsCursorPolicyEnabled);

            Assert.Throws<InvalidOperationException>
            (
                () => session.SetCursorPolicyEnabled(true)
            );

            Assert.IsFalse(session.IsCursorPolicyEnabled);
        }

    #endregion

    #region S-1: Spotlight 활성화 재진입

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

    #endregion

    }
}
