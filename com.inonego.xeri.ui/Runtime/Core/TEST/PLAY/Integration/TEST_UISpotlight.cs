/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UISpotlight.cs
수정일 : 2026-11-10

# 설명
실제 Canvas와 Runtime Panel에서 Spotlight 표시·클리핑·입력·수명주기 계약을 검증한다.

# 테스트 구성
 A: Spotlight 테스트 구성
 B: Spotlight 요청 불변성
 C: UGUI 표시와 수명
 D: UGUI 클리핑과 입력
 E: UGUI 메시 갱신
 F: UITK 표시와 수명
 G: UITK Scroll과 Clipping
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using UGUIGraphicRaycaster = UnityEngine.UI.GraphicRaycaster;
using UGUIImage = UnityEngine.UI.Image;

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
    /// UGUI·UITK Spotlight의 런타임 계약 검증.
    /// </summary>
    // ============================================================
    public sealed class TEST_UISpotlight
    {

    #region A: Spotlight 테스트 구성

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 Driver의 private 직렬화 참조를 설정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField
            (
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.IsNotNull(field, $"{target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 UITK Panel과 Presentation Output을 조립한다.
        /// </summary>
        // ------------------------------------------------------------
        private static UITKPresentationOutput CreatePanelOutput
        (
            GameObject host,
            PanelSettings settings
        )
        {
            host.SetActive(false);
            host.AddComponent<PanelRenderer>();
            var output = host.AddComponent<UITKPresentationOutput>();
            output.Initialize(settings, 0);
            host.SetActive(true);
            return output;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// EventSystem의 실제 Canvas Raycaster로 첫 입력 대상 조회.
        /// </summary>
        // ------------------------------------------------------------
        private static GameObject RaycastFirst
        (
            UGUIGraphicRaycaster raycaster,
            EventSystem eventSystem,
            Vector2 screenPoint
        )
        {
            var pointer = new PointerEventData(eventSystem)
            {
                position = screenPoint,
            };
            var results = new List<RaycastResult>();
            raycaster.Raycast(pointer, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }

    #endregion

    #region B: Spotlight 요청 불변성

        // ------------------------------------------------------------
        /// <summary>
        /// 외부 원본 목록과 공개 목록에서 Spotlight 요청을 변경할 수 없다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_SpotlightParams_대상목록_불변계약유지()
        {
            var host = new GameObject("Spotlight Target", typeof(RectTransform));

            try
            {
                var rect = host.GetComponent<RectTransform>();
                var uguiSource = new[]
                {
                    new UGUISpotlightTarget(rect),
                };
                var uguiParams = new UGUISpotlightParams(uguiSource);
                uguiSource[0] = default;

                Assert.AreSame(rect, uguiParams.Targets[0].Target);
                Assert.Throws<NotSupportedException>
                (
                    () => ((IList<UGUISpotlightTarget>)uguiParams.Targets)[0] = default
                );

                var element = new VisualElement();
                var ancestor = new VisualElement();
                var clipSource = new[]
                {
                    new UITKSpotlightClip(ancestor),
                };
                var target = new UITKSpotlightTarget(element, clips: clipSource);
                clipSource[0] = default;

                Assert.AreSame(ancestor, target.Clips[0].Element);
                Assert.Throws<NotSupportedException>
                (
                    () => ((IList<UITKSpotlightClip>)target.Clips)[0] = default
                );

                var uitkSource = new[]
                {
                    target,
                };
                var uitkParams = new UITKSpotlightParams(uitkSource);
                uitkSource[0] = default;

                Assert.AreSame(element, uitkParams.Targets[0].Target);
                Assert.Throws<NotSupportedException>
                (
                    () => ((IList<UITKSpotlightTarget>)uitkParams.Targets)[0] = default
                );
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

    #endregion

    #region C: UGUI 표시와 수명

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

                // CanvasGroup이 완전히 투명해지면 대상 구멍과 입력 잠금도 해제된다.
                var group = target.AddComponent<CanvasGroup>();
                group.alpha = 0.0f;
                yield return null;

                Assert.AreEqual(0, graphic.HoleCount);
                Assert.IsFalse(graphic.raycastTarget);

                group.enabled = false;
                yield return null;

                Assert.AreEqual(1, graphic.HoleCount);
                Assert.IsTrue(graphic.raycastTarget);

                group.enabled = true;
                yield return null;

                Assert.AreEqual(0, graphic.HoleCount);
                Assert.IsFalse(graphic.raycastTarget);

                group.alpha = 1.0f;
                yield return null;

                Assert.IsTrue(graphic.enabled);
                Assert.AreEqual(1, graphic.HoleCount);
                Assert.IsTrue(graphic.raycastTarget);

                // 조상 Group의 Alpha도 표시 중 동적으로 상속된다.
                var parent = new GameObject
                (
                    "Spotlight Visibility Group",
                    typeof(RectTransform),
                    typeof(CanvasGroup)
                );
                parent.transform.SetParent(host.transform, false);
                target.transform.SetParent(parent.transform, false);
                var parentGroup = parent.GetComponent<CanvasGroup>();
                parentGroup.alpha = 0.0f;
                yield return null;

                Assert.AreEqual(0, graphic.HoleCount);
                Assert.IsFalse(graphic.raycastTarget);

                parentGroup.alpha = 1.0f;
                yield return null;

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

    #endregion

    #region D: UGUI 클리핑과 입력

        // --------------------------------------------------------------------------------
        /// <summary>
        /// RectMask2D padding으로 가려진 대상 영역이 Spotlight 구멍과 입력 통과 영역에 남지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUISpotlight_RectMaskPadding_표시와입력영역일치()
        {
            var canvasObject = new GameObject
            (
                "Spotlight Canvas",
                typeof(RectTransform),
                typeof(Canvas)
            );
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var maskObject = new GameObject
            (
                "Spotlight Mask",
                typeof(RectTransform),
                typeof(UnityEngine.UI.RectMask2D)
            );
            maskObject.transform.SetParent(canvasObject.transform, false);
            var maskRect = maskObject.GetComponent<RectTransform>();
            maskRect.sizeDelta = new Vector2(100.0f, 100.0f);
            maskObject.GetComponent<UnityEngine.UI.RectMask2D>().padding =
                new Vector4(25.0f, 0.0f, 0.0f, 0.0f);

            var targetObject = new GameObject("Spotlight Target", typeof(RectTransform));
            targetObject.transform.SetParent(maskObject.transform, false);
            var target = targetObject.GetComponent<RectTransform>();
            target.sizeDelta = maskRect.sizeDelta;

            var overlay = new GameObject
            (
                "Spotlight Overlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UGUISpotlightGraphic),
                typeof(UGUISpotlightDriver)
            );
            overlay.transform.SetParent(canvasObject.transform, false);
            overlay.GetComponent<RectTransform>().sizeDelta = new Vector2(400.0f, 300.0f);
            var graphic = overlay.GetComponent<UGUISpotlightGraphic>();
            var driver = overlay.GetComponent<UGUISpotlightDriver>();
            SetField(driver, "root", overlay);
            SetField(driver, "graphic", graphic);

            try
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                driver.Show
                (
                    new UGUISpotlightParams
                    (
                        new[]
                        {
                            new UGUISpotlightTarget(target),
                        }
                    )
                );

                var clippedPoint = RectTransformUtility.WorldToScreenPoint
                (
                    null,
                    maskRect.TransformPoint(new Vector3(-40.0f, 0.0f))
                );
                var visiblePoint = RectTransformUtility.WorldToScreenPoint
                (
                    null,
                    maskRect.TransformPoint(Vector3.zero)
                );

                Assert.AreEqual(1, graphic.HoleCount);
                Assert.IsTrue(graphic.IsRaycastLocationValid(clippedPoint, null));
                Assert.IsFalse(graphic.IsRaycastLocationValid(visiblePoint, null));

                // 중첩 RectMask2D를 적용한 뒤 한 마스크를 해제하면 표시와 Raycast가 함께 갱신된다.
                var nestedObject = new GameObject
                (
                    "Nested Spotlight Mask",
                    typeof(RectTransform),
                    typeof(UnityEngine.UI.RectMask2D)
                );
                nestedObject.transform.SetParent(maskRect, false);
                var nestedRect = nestedObject.GetComponent<RectTransform>();
                nestedRect.sizeDelta = new Vector2(60.0f, 80.0f);
                nestedRect.anchoredPosition = new Vector2(20.0f, 0.0f);
                target.SetParent(nestedRect, false);
                yield return null;
                Canvas.ForceUpdateCanvases();

                var nestedClippedPoint = RectTransformUtility.WorldToScreenPoint
                (
                    null,
                    maskRect.TransformPoint(new Vector3(-15.0f, 0.0f))
                );
                Assert.AreEqual(1, graphic.HoleCount);
                Assert.IsTrue(graphic.IsRaycastLocationValid(nestedClippedPoint, null));
                Assert.IsFalse(graphic.IsRaycastLocationValid(visiblePoint, null));

                nestedObject.GetComponent<UnityEngine.UI.RectMask2D>().enabled = false;
                yield return null;
                Assert.IsFalse(graphic.IsRaycastLocationValid(nestedClippedPoint, null));

                // 비활성 Canvas 안에 있는 대상은 GameObject가 활성 상태여도 보이지 않는다.
                var nestedCanvasObject = new GameObject
                (
                    "Nested Spotlight Canvas",
                    typeof(RectTransform),
                    typeof(Canvas)
                );
                nestedCanvasObject.transform.SetParent(nestedRect, false);
                var nestedCanvas = nestedCanvasObject.GetComponent<Canvas>();
                target.SetParent(nestedCanvasObject.transform, false);
                yield return null;
                Assert.AreEqual(1, graphic.HoleCount);

                nestedCanvas.enabled = false;
                yield return null;
                Assert.AreEqual(0, graphic.HoleCount);
                Assert.IsFalse(graphic.raycastTarget);

                nestedCanvas.enabled = true;
                yield return null;
                Assert.AreEqual(1, graphic.HoleCount);

                // 전체 클리핑 밖으로 이동한 대상은 전체 입력 차단 없이 해제된다.
                target.anchoredPosition = new Vector2(600.0f, 0.0f);
                yield return null;
                Assert.AreEqual(0, graphic.HoleCount);
                Assert.IsFalse(graphic.raycastTarget);
            }
            finally
            {
                driver.Hide();
                UnityEngine.Object.Destroy(canvasObject);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 정렬 경계와 대상 자체 maskable 설정을 실제 UGUI 클리핑과 일치시킨다.
        /// <br/> Graphic 영역 밖의 구멍은 전체 입력 차단 없이 제거한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUISpotlight_정렬경계와가시영역_구멍일치()
        {
            var host = new GameObject("Spotlight Canvas", typeof(RectTransform), typeof(Canvas));
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var maskObject = new GameObject
            (
                "Clipping Parent",
                typeof(RectTransform),
                typeof(UnityEngine.UI.RectMask2D)
            );
            maskObject.transform.SetParent(host.transform, false);
            maskObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 100f);

            var nestedObject = new GameObject
            (
                "Sorted Canvas",
                typeof(RectTransform),
                typeof(Canvas)
            );
            nestedObject.transform.SetParent(maskObject.transform, false);
            var nestedCanvas = nestedObject.GetComponent<Canvas>();

            var targetObject = new GameObject
            (
                "Target Graphic",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image)
            );
            targetObject.transform.SetParent(nestedObject.transform, false);
            var target = targetObject.GetComponent<RectTransform>();
            target.sizeDelta = new Vector2(40f, 40f);
            target.anchoredPosition = new Vector2(80f, 0f);
            var targetImage = targetObject.GetComponent<UnityEngine.UI.Image>();

            var overlay = new GameObject
            (
                "Spotlight Overlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UGUISpotlightGraphic),
                typeof(UGUISpotlightDriver)
            );
            overlay.transform.SetParent(host.transform, false);
            overlay.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 200f);
            var graphic = overlay.GetComponent<UGUISpotlightGraphic>();
            var driver = overlay.GetComponent<UGUISpotlightDriver>();
            SetField(driver, "root", overlay);
            SetField(driver, "graphic", graphic);

            try
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                driver.Show
                (
                    new UGUISpotlightParams
                    (
                        new[]
                        {
                            new UGUISpotlightTarget(target),
                        }
                    )
                );

                Assert.AreEqual(0, graphic.HoleCount);
                Assert.IsFalse(graphic.raycastTarget);

                nestedCanvas.overrideSorting = true;
                yield return null;

                Assert.AreEqual(1, graphic.HoleCount);
                var targetPoint = RectTransformUtility.WorldToScreenPoint
                (
                    null,
                    target.TransformPoint(Vector3.zero)
                );
                Assert.IsFalse(graphic.IsRaycastLocationValid(targetPoint, null));

                nestedCanvas.overrideSorting = false;
                targetImage.maskable = false;
                yield return null;

                Assert.AreEqual(1, graphic.HoleCount);

                targetImage.maskable = true;
                yield return null;

                Assert.AreEqual(0, graphic.HoleCount);

                target.anchoredPosition = Vector2.zero;
                yield return null;

                Assert.AreEqual(1, graphic.HoleCount);

                target.anchoredPosition = new Vector2(500f, 0f);
                nestedCanvas.overrideSorting = true;
                yield return null;

                Assert.AreEqual(0, graphic.HoleCount);
                Assert.IsFalse(graphic.raycastTarget);
            }
            finally
            {
                driver.Hide();
                UnityEngine.Object.Destroy(host);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 실제 GraphicRaycaster가 대상 구멍 안은 통과시키고 dim 바깥을 차단한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUISpotlight_실제Raycaster_대상입력통과()
        {
            var host = new GameObject
            (
                "Raycast Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UGUIGraphicRaycaster)
            );
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var raycaster = host.GetComponent<UGUIGraphicRaycaster>();
            var eventObject = new GameObject("Spotlight EventSystem", typeof(EventSystem));
            var eventSystem = eventObject.GetComponent<EventSystem>();

            var targetObject = new GameObject
            (
                "Target Graphic",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UGUIImage)
            );
            targetObject.transform.SetParent(host.transform, false);
            var target = targetObject.GetComponent<RectTransform>();
            target.sizeDelta = new Vector2(60f, 60f);
            target.anchoredPosition = new Vector2(40f, 0f);

            var overlay = new GameObject
            (
                "Spotlight Overlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UGUISpotlightGraphic),
                typeof(UGUISpotlightDriver)
            );
            overlay.transform.SetParent(host.transform, false);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.sizeDelta = new Vector2(300f, 200f);
            var driver = overlay.GetComponent<UGUISpotlightDriver>();
            SetField(driver, "root", overlay);
            SetField(driver, "graphic", overlay.GetComponent<UGUISpotlightGraphic>());

            try
            {
                yield return null;
                driver.Show
                (
                    new UGUISpotlightParams
                    (
                        new[]
                        {
                            new UGUISpotlightTarget(target),
                        }
                    )
                );
                yield return null;
                Canvas.ForceUpdateCanvases();

                var passPoint = RectTransformUtility.WorldToScreenPoint
                (
                    null,
                    target.TransformPoint(Vector3.zero)
                );
                var dimPoint = RectTransformUtility.WorldToScreenPoint
                (
                    null,
                    overlayRect.TransformPoint(new Vector3(-90f, 0f))
                );
                Assert.AreSame(targetObject, RaycastFirst(raycaster, eventSystem, passPoint));
                Assert.AreSame(overlay, RaycastFirst(raycaster, eventSystem, dimPoint));
            }
            finally
            {
                driver.Hide();
                UnityEngine.Object.Destroy(eventObject);
                UnityEngine.Object.Destroy(host);
            }
        }

    #endregion

    #region E: UGUI 메시 갱신

        // ----------------------------------------------------------------------
        /// <summary>
        /// 동일한 구멍과 Raycast 상태는 Graphic 메시 재생성을 요청하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UGUISpotlightGraphic_동일상태_메시갱신중복방지()
        {
            var host = new GameObject
            (
                "Spotlight Graphic",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UGUISpotlightGraphic)
            );

            try
            {
                var graphic = host.GetComponent<UGUISpotlightGraphic>();
                var dirtyCount = 0;
                graphic.RegisterDirtyVerticesCallback(() => dirtyCount++);
                graphic.SetHoles(null, false);
                Assert.IsFalse(graphic.raycastTarget);

                // 빈 구멍은 차단 요청이 있어도 입력을 막지 않는다.
                graphic.SetHoles(null, true);
                Assert.IsFalse(graphic.raycastTarget);
                Assert.IsFalse(graphic.IsRaycastLocationValid(Vector2.zero, null));

                var holes = new[]
                {
                    new Rect(0f, 0f, 50f, 50f),
                };

                graphic.SetHoles(holes, true);
                var firstCount = dirtyCount;
                graphic.SetHoles(holes, true);
                Assert.AreEqual(firstCount, dirtyCount);

                // Graphic의 실제 Raycast 설정이 입력 차단의 단일 상태다.
                graphic.raycastTarget = false;
                Assert.IsFalse(graphic.IsRaycastLocationValid(Vector2.zero, null));
                graphic.SetHoles(holes, true);
                Assert.IsTrue(graphic.raycastTarget);
                Assert.AreEqual(firstCount, dirtyCount);

                // 입력 차단 정책만 바뀌면 기존 dim 메시를 다시 만들지 않는다.
                graphic.SetHoles(holes, false);
                Assert.AreEqual(firstCount, dirtyCount);
                Assert.IsFalse(graphic.raycastTarget);

                var changedHoles = new[]
                {
                    new Rect(0f, 0f, 30f, 30f),
                };
                graphic.SetHoles(changedHoles, false);
                Assert.Greater(dirtyCount, firstCount);

                graphic.ClearHoles();
                var clearedCount = dirtyCount;
                graphic.ClearHoles();
                Assert.AreEqual(clearedCount, dirtyCount);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

    #endregion

    #region F: UITK 표시와 수명

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
            Lease passThroughLease = null;

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
                Assert.AreSame(target, target.panel.Pick(target.worldBound.center));
                Assert.AreSame(element, element.panel.Pick(element.LocalToWorld(dimPoint)));

                // 입력 차단이 없는 새 요청은 Picking을 해제하고 종료 시 이전 정책을 복원한다.
                passThroughLease = spotlight.Show
                (
                    element,
                    new UITKSpotlightParams
                    (
                        new[]
                        {
                            new UITKSpotlightTarget(target),
                        },
                        blocksOutsideInput: false
                    )
                );
                yield return null;

                Assert.AreEqual(1, element.HoleCount);
                Assert.AreEqual(PickingMode.Ignore, element.pickingMode);
                Assert.IsFalse(element.ContainsPoint(dimPoint));

                passThroughLease.Dispose();
                passThroughLease = null;
                yield return null;

                Assert.AreEqual(PickingMode.Position, element.pickingMode);
                Assert.IsTrue(element.ContainsPoint(dimPoint));

                target.style.display = DisplayStyle.None;
                var refreshDeadline = Time.realtimeSinceStartupAsDouble + 1.0;
                while (element.HoleCount != 0 && Time.realtimeSinceStartupAsDouble < refreshDeadline)
                {
                    yield return null;
                }

                Assert.AreEqual(0, element.HoleCount);
                Assert.AreEqual(PickingMode.Ignore, element.pickingMode);
            }
            finally
            {
                passThroughLease?.Dispose();
                lease?.Dispose();
                spotlight.Dispose();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(panelSettings);
            }
        }

    #endregion

    #region G: UITK Scroll과 Clipping

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
                var refreshDeadline = Time.realtimeSinceStartupAsDouble + 1.0;
                while
                (
                    !spotlight.ContainsPoint(newPoint) &&
                    Time.realtimeSinceStartupAsDouble < refreshDeadline
                )
                {
                    yield return null;
                }

                Assert.IsTrue(spotlight.ContainsPoint(newPoint));
                Assert.IsFalse
                (
                    spotlight.ContainsPoint(target.ChangeCoordinatesTo(spotlight, new Vector2(50f, 10f)))
                );

                scroll.scrollOffset = new Vector2(0f, 250f);
                // Panel 예약은 밀리초 기준이므로 빠른 batch 프레임 수로 갱신을 가정하지 않는다.
                refreshDeadline = Time.realtimeSinceStartupAsDouble + 1.0;
                while (spotlight.HoleCount != 0 && Time.realtimeSinceStartupAsDouble < refreshDeadline)
                {
                    yield return null;
                }

                Assert.AreEqual(0, spotlight.HoleCount);
                Assert.AreEqual(PickingMode.Ignore, spotlight.pickingMode);

                scroll.scrollOffset = Vector2.zero;
                refreshDeadline = Time.realtimeSinceStartupAsDouble + 1.0;
                while (spotlight.HoleCount != 1 && Time.realtimeSinceStartupAsDouble < refreshDeadline)
                {
                    yield return null;
                }

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
        /// 일반 overflow hidden 부모가 가린 영역에는 Spotlight 구멍을 만들지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKSpotlight_일반OverflowClip_표시와입력영역일치()
        {
            var host = new GameObject("Clipped Spotlight Panel");
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            var output = CreatePanelOutput(host, settings);
            var container = new VisualElement();
            container.style.width = 400.0f;
            container.style.height = 300.0f;

            var clip = new VisualElement();
            clip.style.position = Position.Absolute;
            clip.style.left = 40.0f;
            clip.style.top = 30.0f;
            clip.style.width = 100.0f;
            clip.style.height = 80.0f;
            clip.style.overflow = Overflow.Hidden;

            var target = new VisualElement();
            target.style.position = Position.Absolute;
            target.style.left = 70.0f;
            target.style.top = 10.0f;
            target.style.width = 100.0f;
            target.style.height = 40.0f;
            clip.Add(target);

            var spotlight = new UITKSpotlightElement();
            container.Add(clip);
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

                var visiblePoint = target.ChangeCoordinatesTo(spotlight, new Vector2(10.0f, 20.0f));
                var clippedPoint = target.ChangeCoordinatesTo(spotlight, new Vector2(70.0f, 20.0f));
                Assert.AreEqual(1, spotlight.HoleCount);
                Assert.IsFalse(spotlight.ContainsPoint(visiblePoint));
                Assert.IsTrue(spotlight.ContainsPoint(clippedPoint));

                clip.style.visibility = Visibility.Hidden;
                var refreshDeadline = Time.realtimeSinceStartupAsDouble + 1.0;
                while (spotlight.HoleCount != 0 && Time.realtimeSinceStartupAsDouble < refreshDeadline)
                {
                    yield return null;
                }

                Assert.AreEqual(0, spotlight.HoleCount);
                Assert.AreEqual(PickingMode.Ignore, spotlight.pickingMode);
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
        /// 실제 USS clipping ancestor를 전달하면 구멍과 입력 통과 영역을 제한한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKSpotlight_USSClip_명시경계반영()
        {
            var host = new GameObject("USS Spotlight Panel");
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            var output = CreatePanelOutput(host, settings);
            var sheet = Resources.Load<StyleSheet>("Xeri/Bar/UITKBar");
            Assert.IsNotNull(sheet);
            var container = new VisualElement();
            container.style.width = 400.0f;
            container.style.height = 300.0f;
            container.styleSheets.Add(sheet);

            var clip = new VisualElement();
            clip.AddToClassList("xeri-bar");
            clip.style.position = Position.Absolute;
            clip.style.left = 40.0f;
            clip.style.top = 30.0f;

            var target = new VisualElement();
            target.style.position = Position.Absolute;
            target.style.left = 130.0f;
            target.style.top = 0.0f;
            target.style.width = 100.0f;
            target.style.height = 40.0f;
            clip.Add(target);

            var spotlight = new UITKSpotlightElement();
            container.Add(clip);
            container.Add(spotlight);

            try
            {
                yield return null;
                yield return null;
                output.RequireRoot().Add(container);
                yield return null;
                yield return null;

                Assert.AreEqual(StyleKeyword.Null, clip.style.overflow.keyword);
                Assert.AreEqual(160.0f, clip.resolvedStyle.width, 0.5f);
                Assert.AreEqual(20.0f, clip.resolvedStyle.height, 0.5f);
                spotlight.Show
                (
                    new UITKSpotlightParams
                    (
                        new[]
                        {
                            new UITKSpotlightTarget
                            (
                                target,
                                clips: new[]
                                {
                                    new UITKSpotlightClip(clip),
                                }
                            ),
                        }
                    )
                );

                var visiblePoint = target.ChangeCoordinatesTo(spotlight, new Vector2(10.0f, 10.0f));
                var clippedPoint = target.ChangeCoordinatesTo(spotlight, new Vector2(60.0f, 10.0f));
                Assert.AreEqual(1, spotlight.HoleCount);
                Assert.IsFalse(spotlight.ContainsPoint(visiblePoint));
                Assert.IsTrue(spotlight.ContainsPoint(clippedPoint));

                // 다른 계층의 clip을 잘못 전달해도 활성 Spotlight는 교체되지 않는다.
                var unrelated = new VisualElement();
                var invalid = new UITKSpotlightParams
                (
                    new[]
                    {
                        new UITKSpotlightTarget
                        (
                            target,
                            clips: new[]
                            {
                                new UITKSpotlightClip(unrelated),
                            }
                        ),
                    }
                );
                Assert.Throws<InvalidOperationException>(() => spotlight.Show(invalid));
                Assert.IsFalse(spotlight.ContainsPoint(visiblePoint));

                // 내용 영역 기준 클리핑을 명시하면 padding 영역은 구멍에서 제외한다.
                clip.style.paddingLeft = 20.0f;
                target.style.left = 0.0f;
                yield return null;
                yield return null;
                spotlight.Show
                (
                    new UITKSpotlightParams
                    (
                        new[]
                        {
                            new UITKSpotlightTarget
                            (
                                target,
                                clips: new[]
                                {
                                    new UITKSpotlightClip(clip, OverflowClipBox.ContentBox),
                                }
                            ),
                        }
                    )
                );

                var paddedPoint = target.ChangeCoordinatesTo(spotlight, new Vector2(10.0f, 10.0f));
                var contentPoint = target.ChangeCoordinatesTo(spotlight, new Vector2(30.0f, 10.0f));
                Assert.AreEqual(1, spotlight.HoleCount);
                Assert.IsTrue(spotlight.ContainsPoint(paddedPoint));
                Assert.IsFalse(spotlight.ContainsPoint(contentPoint));

                // 표시 중 Target이 선언된 clip 조상 밖으로 이동하면 구멍을 폐기한다.
                container.Add(target);
                var refreshDeadline = Time.realtimeSinceStartupAsDouble + 1.0;
                while (spotlight.HoleCount != 0 && Time.realtimeSinceStartupAsDouble < refreshDeadline)
                {
                    yield return null;
                }

                Assert.AreEqual(0, spotlight.HoleCount);
                Assert.AreEqual(PickingMode.Ignore, spotlight.pickingMode);

                clip.Add(target);
                refreshDeadline = Time.realtimeSinceStartupAsDouble + 1.0;
                while (spotlight.HoleCount != 1 && Time.realtimeSinceStartupAsDouble < refreshDeadline)
                {
                    yield return null;
                }

                Assert.AreEqual(1, spotlight.HoleCount);
            }
            finally
            {
                spotlight.Hide();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(settings);
            }
        }

    #endregion

    }
}