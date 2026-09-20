/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_InputAndFocus.cs
수정일 : 2026-09-20

# 설명
UGUI Focus 소유권·유효성 경계와 Spotlight 요청 수명을 검증한다.

# 테스트 구성
 F: UGUI Layer 소유권과 비활성·파괴된 Selectable 거부
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
            var layerObject = new GameObject
            (
                "UGUI Layer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUILayerCanvas)
            );
            ownedObjects.Add(layerObject);
            var layer = layerObject.GetComponent<UGUILayerCanvas>();
            SetField(layer, "root", layerObject.GetComponent<RectTransform>());
            SetField(layer, "canvas", layerObject.GetComponent<Canvas>());
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
            var layerObject = new GameObject("UGUI Layer", typeof(RectTransform), typeof(UGUILayerCanvas));
            ownedObjects.Add(layerObject);
            var layer = layerObject.GetComponent<UGUILayerCanvas>();
            SetField(layer, "root", layerObject.GetComponent<RectTransform>());
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
