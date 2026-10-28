/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UITKPresentationOutput.cs
수정일 : 2026-10-18

# 설명
UITK Output 경계에서 생성·대여 및 Native Root 재연결의 소유권을 검증한다.

# 테스트 구성
 L: 설정과 생성·대여 수명
 R: Root 재연결, 추가 스타일 반환 및 원래 스타일 보존
========================================================================= BLOCK_HEADER_END */

using System;
using System.Reflection;

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    // ============================================================
    /// <summary>
    /// Native 출력 경계의 원상 복원 계약을 검증한다.
    /// </summary>
    // ============================================================
    public sealed class TEST_UITKPresentationOutput
    {

    #region 픽스처

        private GameObject host;
        private PanelSettings settings;
        private UITKPresentationOutput output;
        private StyleSheet baseline;

        [SetUp]
        public void Setup()
        {
            host = new GameObject("Borrowed UITK Output");
            host.SetActive(false);
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            UITKTestPanelSettings.ApplyDefaultRuntimeTheme(settings);
            settings.sortingOrder = 37f;
            host.AddComponent<PanelRenderer>().panelSettings = settings;
            output = host.AddComponent<UITKPresentationOutput>();
            output.InitializeBorrowed(73);
            baseline = Resources.Load<StyleSheet>("Xeri/UI/Core/UIRuntimeBaseline");
            Assert.IsNotNull(baseline);
        }

        [TearDown]
        public void Teardown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(settings);
        }

        private void Reload(VisualElement root)
        {
            // Unity가 전달하는 Root 교체를 재현하고 Output의 관찰 가능한 결과를 검사한다.
            var callback = typeof(UITKPresentationOutput).GetMethod
            (
                "HandleUIReload", BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(callback);
            callback.Invoke
            (
                output,
                new object[]
                {
                    output.PanelRenderer, root, 1,
                }
            );
        }

    #endregion

    #region L-1: 설정 수명

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Borrowed PanelSettings 참조를 유지하고 임시 sorting order와 중복 초기화 금지를
        /// <br/> 확인한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_대여설정정체성과순서복원()
        {
            Assert.AreSame(settings, output.PanelRenderer.panelSettings);
            Assert.IsNull(output.RuntimePanelSettings);
            Assert.AreEqual(73f, settings.sortingOrder);
            Assert.Throws<InvalidOperationException>(() => output.InitializeBorrowed(90));
            output.ReleaseOutputState();
            output.ReleaseOutputState();
            Assert.AreSame(settings, output.PanelRenderer.panelSettings);
            Assert.AreEqual(37f, settings.sortingOrder);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 생성한 PanelSettings를 해제하고 Borrowed 설정으로 재초기화할 때 설정 참조와
        /// <br/> sorting order를 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_생성설정해제와재초기화()
        {
            output.ReleaseOutputState();
            output.Initialize(settings, 91);
            var generated = output.RuntimePanelSettings;
            Assert.AreNotSame(settings, generated);
            Assert.AreEqual(91f, generated.sortingOrder);
            Assert.Throws<InvalidOperationException>(() => output.Initialize(settings, 92));
            output.ReleaseOutputState();
            Assert.IsTrue(generated == null);
            Assert.IsNull(output.PanelRenderer.panelSettings);
            Assert.AreEqual(37f, settings.sortingOrder);

            output.PanelRenderer.panelSettings = settings;
            output.InitializeBorrowed(12);
            Assert.AreEqual(12f, settings.sortingOrder);
            output.ReleaseOutputState();
            Assert.AreEqual(37f, settings.sortingOrder);
            Assert.AreSame(settings, host.GetComponent<PanelRenderer>().panelSettings);
        }

    #endregion

    #region R-1: 루트와 스타일 수명

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Output이 추가한 기본 class와 stylesheet만 반환하고 authored class는 보존한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_추가한기본스타일만원상복원()
        {
            var root = new VisualElement();
            root.AddToClassList("authored");
            Reload(root);
            Reload(root);
            Assert.AreSame(root, output.Root);
            Assert.IsTrue(root.ClassListContains("xeri-ui"));
            Assert.IsTrue(root.styleSheets.Contains(baseline));
            output.ReleaseOutputState();
            Assert.IsFalse(root.ClassListContains("xeri-ui"));
            Assert.IsFalse(root.styleSheets.Contains(baseline));
            Assert.IsTrue(root.ClassListContains("authored"));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Root에 이미 있던 기본 class와 stylesheet를 Output 해제 뒤에도 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_기존클래스와스타일시트보존()
        {
            var root = new VisualElement();
            root.AddToClassList("xeri-ui");
            root.styleSheets.Add(baseline);
            Reload(root);
            output.ReleaseOutputState();
            Assert.IsTrue(root.ClassListContains("xeri-ui"));
            Assert.IsTrue(root.styleSheets.Contains(baseline));
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Root 교체 시 이전 Root의 스타일을 반환하고 새 Root에 Layer를 연결한 뒤 해제 시
        /// <br/> 분리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_루트교체에서스타일반환과레이어재연결()
        {
            var original = new VisualElement();
            var replacement = new VisualElement();
            var layer = new VisualElement();
            Reload(original);
            output.AttachLayerRoot(layer);
            Assert.AreSame(original, layer.parent);
            Reload(replacement);
            Assert.IsFalse(original.ClassListContains("xeri-ui"));
            Assert.IsFalse(original.styleSheets.Contains(baseline));
            Assert.AreSame(replacement, output.Root);
            Assert.AreSame(replacement, layer.parent);
            Assert.IsTrue(replacement.styleSheets.Contains(baseline));
            output.ReleaseOutputState();
            Assert.IsNull(layer.parent);
            Assert.IsFalse(replacement.styleSheets.Contains(baseline));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Output 초기화와 해제가 authored Root의 배경색과 투명도를 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UITKPresentationOutput_기존배경색과투명도보존()
        {
            var root = new VisualElement();
            var color = new Color(0.12f, 0.35f, 0.68f, 0.82f);
            root.style.backgroundColor = color;
            root.style.opacity = 0.47f;
            Reload(root);
            Assert.AreEqual(color, root.style.backgroundColor.value);
            Assert.AreEqual(0.47f, root.style.opacity.value);
            output.ReleaseOutputState();
            Assert.AreEqual(color, root.style.backgroundColor.value);
            Assert.AreEqual(0.47f, root.style.opacity.value);
        }

    #endregion

    }
}
