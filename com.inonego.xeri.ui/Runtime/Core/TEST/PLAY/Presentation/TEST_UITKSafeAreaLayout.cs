/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UITKSafeAreaLayout.cs
수정일 : 2026-11-11

# 설명
실제 Panel에서 Safe Area Root 재연결과 부모 이동·반전·회전·비균일 스케일의 안전한 배치를 검증한다.

# 테스트 구성
 A: 테스트 callback과 관찰 지원
 B: Output Root 교체와 실제 Panel 좌표
 C: Root 알림 중 활성 상태 변경
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Reflection;

using UnityEngine;
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
    /// UITK Safe Area Layout의 Root 이벤트 수명을 검증한다.
    /// </summary>
    // ============================================================
    public sealed class TEST_UITKSafeAreaLayout
    {

    #region A: 테스트 callback과 관찰 지원

        private const float PANEL_STYLE_TOLERANCE = 0.5f;
        private const float SCREEN_BOUNDARY_TOLERANCE = 1f;
        private const float UNCHANGED_BOUND_TOLERANCE = 0.01f;
        private const double LAYOUT_UPDATE_TIMEOUT_SECONDS = 1d;

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField
            (
                name, BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        private static VisualElement GetObservedRoot(UITKSafeAreaLayout layout)
        {
            var field = typeof(UITKSafeAreaLayout).GetField
            (
                "observedRoot", BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(field);
            return field.GetValue(layout) as VisualElement;
        }

        private static void Reload
        (
            UITKPresentationOutput output, PanelRenderer renderer,
            VisualElement root, int version
        )
        {
            var method = typeof(UITKPresentationOutput).GetMethod
            (
                "HandleUIReload", BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(method);
            method.Invoke
            (
                output,
                new object[]
                {
                    renderer, root, version,
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 투영된 Root의 네 꼭짓점이 화면 Safe Area 안에 있는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AssertScreenSafeAreaContains
        (
            VisualElement root, bool expectFullCoverage = false
        )
        {
            var area = Screen.safeArea;
            var panel = root.panel;
            Assert.IsNotNull(panel);
            Assert.Greater(root.layout.width, 0f);
            Assert.Greater(root.layout.height, 0f);

            var panelTopLeft = RuntimePanelUtils.ScreenToPanel
            (
                panel, new Vector2(area.xMin, Screen.height - area.yMax)
            );
            var panelBottomRight = RuntimePanelUtils.ScreenToPanel
            (
                panel, new Vector2(area.xMax, Screen.height - area.yMin)
            );
            var min = Vector2.Min(panelTopLeft, panelBottomRight);
            var max = Vector2.Max(panelTopLeft, panelBottomRight);
            var corners = new[]
            {
                root.LocalToWorld(Vector2.zero),
                root.LocalToWorld(new Vector2(root.layout.width, 0f)),
                root.LocalToWorld(new Vector2(root.layout.width, root.layout.height)),
                root.LocalToWorld(new Vector2(0f, root.layout.height)),
            };
            var touchesBoundary = false;

            foreach (var corner in corners)
            {
                Assert.That(corner.x, Is.InRange(min.x - SCREEN_BOUNDARY_TOLERANCE, max.x + SCREEN_BOUNDARY_TOLERANCE));
                Assert.That(corner.y, Is.InRange(min.y - SCREEN_BOUNDARY_TOLERANCE, max.y + SCREEN_BOUNDARY_TOLERANCE));

                if
                (
                    Mathf.Abs(corner.x - min.x) <= SCREEN_BOUNDARY_TOLERANCE ||
                    Mathf.Abs(corner.x - max.x) <= SCREEN_BOUNDARY_TOLERANCE ||
                    Mathf.Abs(corner.y - min.y) <= SCREEN_BOUNDARY_TOLERANCE ||
                    Mathf.Abs(corner.y - max.y) <= SCREEN_BOUNDARY_TOLERANCE
                )
                {
                    touchesBoundary = true;
                }
            }

            Assert.IsTrue(touchesBoundary, "최대 안전 영역은 화면 Safe Area 경계에 접해야 합니다.");

            if (expectFullCoverage)
            {
                var actualMin = corners[0];
                var actualMax = corners[0];

                foreach (var corner in corners)
                {
                    actualMin = Vector2.Min(actualMin, corner);
                    actualMax = Vector2.Max(actualMax, corner);
                }

                Assert.That(actualMin.x, Is.EqualTo(min.x).Within(SCREEN_BOUNDARY_TOLERANCE));
                Assert.That(actualMin.y, Is.EqualTo(min.y).Within(SCREEN_BOUNDARY_TOLERANCE));
                Assert.That(actualMax.x, Is.EqualTo(max.x).Within(SCREEN_BOUNDARY_TOLERANCE));
                Assert.That(actualMax.y, Is.EqualTo(max.y).Within(SCREEN_BOUNDARY_TOLERANCE));
            }
        }

    #endregion

    #region B: Output Root 교체와 실제 Panel 좌표

        // ------------------------------------------------------------
        /// <summary>
        /// 출력 Root 교체와 반환에 따라 Safe Area 관찰 대상도 교체·해제된다.
        /// </summary>
        // ------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKSafeAreaLayout_OutputRoot교체와반환_관찰대상갱신()
        {
            var host = new GameObject("Safe Area Root Rebind");
            host.SetActive(false);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);

            try
            {
                var renderer = host.AddComponent<PanelRenderer>();
                renderer.panelSettings = settings;
                var output = host.AddComponent<UITKPresentationOutput>();
                output.InitializeBorrowed(31);
                var layout = host.AddComponent<UITKSafeAreaLayout>();
                SetField(layout, "rootName", "Safe Area");
                host.SetActive(true);

                var firstRoot = new VisualElement();
                var first = new VisualElement
                {
                    name = "Safe Area",
                };
                firstRoot.Add(first);
                Reload(output, renderer, firstRoot, 1);
                Assert.AreSame(first, GetObservedRoot(layout));

                var secondRoot = new VisualElement();
                var second = new VisualElement
                {
                    name = "Safe Area",
                };
                secondRoot.Add(second);
                Reload(output, renderer, secondRoot, 2);
                Assert.AreSame(second, layout.SafeAreaRoot);
                Assert.AreSame(second, GetObservedRoot(layout));

                output.ReleaseOutputState();
                Assert.IsNull(GetObservedRoot(layout));
                layout.Dispose();
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(settings);
            }

            yield return null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 Panel에서 부모 이동·반전·회전·스케일의 안전한 영역을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKSafeAreaLayout_실제Panel연결_SafeArea좌표반영()
        {
            var host = new GameObject("Safe Area Live Panel");
            host.SetActive(false);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);

            try
            {
                var renderer = host.AddComponent<PanelRenderer>();
                renderer.panelSettings = settings;
                var output = host.AddComponent<UITKPresentationOutput>();
                output.InitializeBorrowed(33);
                host.SetActive(true);

                yield return null;
                yield return null;

                var panelRoot = output.RequireRoot();
                Assert.IsNotNull(panelRoot.panel);

                var parent = new VisualElement();
                parent.style.width = 520f;
                parent.style.height = 300f;
                var safeArea = new VisualElement
                {
                    name = "Safe Area",
                };
                parent.Add(safeArea);
                panelRoot.Add(parent);
                yield return null;

                var layout = host.AddComponent<UITKSafeAreaLayout>();
                layout.enabled = false;
                SetField(layout, "rootName", "Safe Area");
                var notifications = 0;
                layout.OnLayoutChanged += () => notifications++;
                layout.enabled = true;

                Assert.AreSame(safeArea, layout.SafeAreaRoot);
                Assert.AreSame(safeArea, GetObservedRoot(layout));
                Assert.AreSame(panelRoot.panel, safeArea.panel);
                Assert.Greater(notifications, 0);
                Assert.AreEqual(Position.Absolute, safeArea.style.position.value);

                var area = Screen.safeArea;
                var panel = safeArea.panel;
                var topLeft = parent.WorldToLocal
                (
                    RuntimePanelUtils.ScreenToPanel
                    (
                        panel, new Vector2(area.xMin, Screen.height - area.yMax)
                    )
                );
                var bottomRight = parent.WorldToLocal
                (
                    RuntimePanelUtils.ScreenToPanel
                    (
                        panel, new Vector2(area.xMax, Screen.height - area.yMin)
                    )
                );
                var bounds = parent.contentRect;

                Assert.That
                (
                    safeArea.style.left.value.value,
                    Is.EqualTo(topLeft.x - bounds.xMin).Within(PANEL_STYLE_TOLERANCE)
                );
                Assert.That
                (
                    safeArea.style.top.value.value,
                    Is.EqualTo(topLeft.y - bounds.yMin).Within(PANEL_STYLE_TOLERANCE)
                );
                Assert.That
                (
                    safeArea.style.right.value.value,
                    Is.EqualTo(bounds.xMax - bottomRight.x).Within(PANEL_STYLE_TOLERANCE)
                );
                Assert.That
                (
                    safeArea.style.bottom.value.value,
                    Is.EqualTo(bounds.yMax - bottomRight.y).Within(PANEL_STYLE_TOLERANCE)
                );

                // 부모가 이동해도 같은 Panel의 Safe Area 좌표를 다시 반영한다.
                var originalParentX = parent.worldBound.x;
                var originalLeft = safeArea.style.left.value.value;
                var priorNotifications = notifications;
                parent.style.translate = new Translate(64f, 0f);
                var deadline = Time.realtimeSinceStartupAsDouble + LAYOUT_UPDATE_TIMEOUT_SECONDS;
                while
                (
                    (Mathf.Abs(parent.worldBound.x - originalParentX) < 1f ||
                     notifications == priorNotifications) &&
                    Time.realtimeSinceStartupAsDouble < deadline
                )
                {
                    yield return null;
                }

                Assert.Greater(parent.worldBound.x, originalParentX + 1f);
                Assert.Greater(notifications, priorNotifications);
                var movedTopLeft = parent.WorldToLocal
                (
                    RuntimePanelUtils.ScreenToPanel
                    (
                        panel, new Vector2(area.xMin, Screen.height - area.yMax)
                    )
                );
                Assert.That
                (
                    safeArea.style.left.value.value,
                    Is.EqualTo(movedTopLeft.x - parent.contentRect.xMin).Within(PANEL_STYLE_TOLERANCE)
                );
                Assert.Less(safeArea.style.left.value.value, originalLeft - 1f);
                yield return null;
                AssertScreenSafeAreaContains(safeArea);

                // 경계 사각형이 같아도 부모의 좌표계가 반전되면 Safe Area를 다시 계산한다.
                var previousBound = parent.worldBound;
                var previousTransform = parent.worldTransform;
                var previousLeft = safeArea.style.left.value.value;
                priorNotifications = notifications;
                parent.style.scale = new Scale(new Vector3(-1f, -1f, 1f));
                deadline = Time.realtimeSinceStartupAsDouble + LAYOUT_UPDATE_TIMEOUT_SECONDS;
                while
                (
                    (parent.worldTransform == previousTransform ||
                     notifications == priorNotifications) &&
                    Time.realtimeSinceStartupAsDouble < deadline
                )
                {
                    yield return null;
                }

                Assert.AreNotEqual(previousTransform, parent.worldTransform);
                Assert.That(parent.worldBound.x, Is.EqualTo(previousBound.x).Within(UNCHANGED_BOUND_TOLERANCE));
                Assert.That(parent.worldBound.y, Is.EqualTo(previousBound.y).Within(UNCHANGED_BOUND_TOLERANCE));
                Assert.That(parent.worldBound.size, Is.EqualTo(previousBound.size));
                Assert.Greater(notifications, priorNotifications);
                yield return null;
                AssertScreenSafeAreaContains(safeArea);
                Assert.That
                (
                    Mathf.Abs(safeArea.style.left.value.value - previousLeft),
                    Is.GreaterThan(1f)
                );

                // 회전, 비균일 스케일과 비중앙 피벗이 겹쳐도 실제 네 꼭짓점이 화면 안에 머문다.
                previousTransform = parent.worldTransform;
                priorNotifications = notifications;
                parent.style.scale = new Scale(new Vector3(1.35f, 0.75f, 1f));
                parent.style.rotate = new Rotate(new Angle(32f));
                parent.style.transformOrigin = new TransformOrigin
                (
                    Length.Percent(20f), Length.Percent(80f)
                );
                deadline = Time.realtimeSinceStartupAsDouble + LAYOUT_UPDATE_TIMEOUT_SECONDS;
                while
                (
                    (parent.worldTransform == previousTransform ||
                     notifications == priorNotifications) &&
                    Time.realtimeSinceStartupAsDouble < deadline
                )
                {
                    yield return null;
                }

                Assert.AreNotEqual(previousTransform, parent.worldTransform);
                Assert.Greater(notifications, priorNotifications);
                yield return null;
                AssertScreenSafeAreaContains(safeArea);

                // 90도 회전은 대각선 두 점만으로도 오판할 수 있는 경계다.
                priorNotifications = notifications;
                parent.style.rotate = new Rotate(new Angle(90f));
                deadline = Time.realtimeSinceStartupAsDouble + LAYOUT_UPDATE_TIMEOUT_SECONDS;
                while
                (
                    notifications == priorNotifications &&
                    Time.realtimeSinceStartupAsDouble < deadline
                )
                {
                    yield return null;
                }

                Assert.Greater(notifications, priorNotifications);
                yield return null;
                AssertScreenSafeAreaContains(safeArea, expectFullCoverage: true);

                layout.Dispose();
                output.ReleaseOutputState();
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(settings);
            }

            yield return null;
        }

    #endregion

    #region C: Root 알림 중 활성 상태 변경

        // ------------------------------------------------------------
        /// <summary>
        /// Root 알림 중 비활성화되면 관찰 구독을 다시 만들지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UITKSafeAreaLayout_Root알림중비활성화_관찰재등록방지()
        {
            var host = new GameObject("Safe Area Disable Reentry");
            host.SetActive(false);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);

            try
            {
                var renderer = host.AddComponent<PanelRenderer>();
                renderer.panelSettings = settings;
                var output = host.AddComponent<UITKPresentationOutput>();
                output.InitializeBorrowed(32);
                var layout = host.AddComponent<UITKSafeAreaLayout>();
                SetField(layout, "rootName", "Safe Area");
                host.SetActive(true);
                layout.enabled = false;

                output.RootUpdated += root =>
                {
                    if (root != null)
                    {
                        layout.enabled = false;
                    }
                };
                layout.enabled = true;

                var rootElement = new VisualElement();
                var safeArea = new VisualElement
                {
                    name = "Safe Area",
                };
                rootElement.Add(safeArea);
                Reload(output, renderer, rootElement, 1);

                Assert.IsFalse(layout.enabled);
                Assert.IsNull(GetObservedRoot(layout));
                layout.enabled = true;
                Assert.AreSame(safeArea, GetObservedRoot(layout));
                output.ReleaseOutputState();
                Assert.IsNull(GetObservedRoot(layout));
                layout.Dispose();
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(settings);
            }

            yield return null;
        }

    #endregion

    }
}
