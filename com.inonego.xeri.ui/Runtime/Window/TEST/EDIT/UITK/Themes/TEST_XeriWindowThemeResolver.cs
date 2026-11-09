/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowThemeResolver.cs
수정일 : 2026-11-10

# 설명
Xeri 커스텀 윈도우 theme resolver 테스트.

# 테스트 구성
 R: Theme 조회
 A: 등록 테마의 Window USS 적용
========================================================================= BLOCK_HEADER_END */

using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// Xeri window theme resolver 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowThemeResolver
    {

    #region 필드

        private readonly List<UnityEngine.Object> ownedAssets = new();

    #endregion

    #region 수명

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in ownedAssets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            ownedAssets.Clear();
        }

    #endregion

    #region 헬퍼

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트용 theme asset을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowThemeAsset CreateTheme(string id)
        {
            var theme = ScriptableObject.CreateInstance<XeriWindowThemeAsset>();
            var field = typeof(XeriWindowThemeAsset).GetField
            (
                "id",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            field.SetValue(theme, id);
            ownedAssets.Add(theme);
            return theme;
        }

    #endregion

    #region R-1: 테마 고정 ID 등록과 조회

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 theme은 stable ID로 조회된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowThemeResolver_Register_TryGetTheme_조회_성공()
        {
            var resolver = new XeriWindowThemeResolver();
            var theme = CreateTheme("windows");

            resolver.Register(theme);

            var found = resolver.TryGetTheme("windows", out var result);

            Assert.IsTrue(found);
            Assert.AreSame(theme, result);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 등록되지 않은 theme ID는 자동 fallback 없이 조회 실패를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowThemeResolver_TryGetTheme_없는_ID_조회_실패()
        {
            var resolver = new XeriWindowThemeResolver();

            var found = resolver.TryGetTheme("missing", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 theme ID를 중복 등록하면 예외가 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowThemeResolver_Register_중복_ID_예외()
        {
            var resolver = new XeriWindowThemeResolver();

            resolver.Register(CreateTheme("windows"));

            Assert.Throws<System.InvalidOperationException>
            (
                () => resolver.Register(CreateTheme("windows"))
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 등록 후 Asset ID가 변경되면 이전 ID 조회는 실패한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowThemeResolver_RegisteredAsset_ID변경_이전ID무효()
        {
            var resolver = new XeriWindowThemeResolver();
            var asset = CreateTheme("original");
            resolver.Register(asset);

            var field = typeof(XeriWindowThemeAsset).GetField
            (
                "id",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            field.SetValue(asset, "changed");

            Assert.IsFalse(resolver.TryGetTheme("original", out var found));
            Assert.IsNull(found);
            Assert.IsFalse(resolver.TryGetTheme("changed", out _));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Theme ID 공백은 등록과 조회에서 동일하게 정규화한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowThemeResolver_Register_TrimID_일치()
        {
            var resolver = new XeriWindowThemeResolver();
            var theme = CreateTheme(" custom ");

            resolver.Register(theme);

            Assert.AreEqual("custom", theme.ID);
            Assert.IsTrue(resolver.TryGetTheme("custom ", out var found));
            Assert.AreSame(theme, found);
            Assert.Throws<InvalidOperationException>
            (
                () => resolver.Register(CreateTheme("custom"))
            );
        }

    #endregion

    #region A-1: 등록된 테마의 Window USS 적용

        // ----------------------------------------------------------------------
        /// <summary>
        /// 등록 테마의 USS가 Window Panel에 적용되고 내장 테마로 돌아가면 제거된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowThemeResolver_CustomStyle_ApplyTheme_전환()
        {
            var resolver = new XeriWindowThemeResolver();
            var theme = CreateTheme("custom");
            var baseStyle = ScriptableObject.CreateInstance<StyleSheet>();
            var themeStyle = ScriptableObject.CreateInstance<StyleSheet>();
            ownedAssets.Add(baseStyle);
            ownedAssets.Add(themeStyle);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(XeriWindowThemeAsset).GetField("windowStyleSheet", flags)
                .SetValue(theme, baseStyle);
            typeof(XeriWindowThemeAsset).GetField("windowThemeStyleSheet", flags)
                .SetValue(theme, themeStyle);
            resolver.Register(theme);

            var container = new XeriWindowContainer(new XeriWindowRegistry(), resolver);
            var panel = container.CreatePanel
            (
                new XeriWindowRecord
                {
                    ThemeID = "custom",
                },
                XeriWindowOptions.Default()
            );

            Assert.AreEqual("custom", panel.ThemeID);
            Assert.IsTrue(panel.ClassListContains(XeriWindowThemeClass.Custom));
            Assert.IsTrue(panel.ClassListContains(XeriWindowThemeClass.Windows));
            Assert.IsTrue(panel.styleSheets.Contains(baseStyle));
            Assert.IsTrue(panel.styleSheets.Contains(themeStyle));

            panel.ApplyTheme(XeriWindowThemeClass.MacID);

            Assert.AreEqual(XeriWindowThemeClass.MacID, panel.ThemeID);
            Assert.IsFalse(panel.styleSheets.Contains(baseStyle));
            Assert.IsFalse(panel.styleSheets.Contains(themeStyle));
            Assert.IsTrue(panel.ClassListContains(XeriWindowThemeClass.Mac));
            Assert.IsFalse(panel.ClassListContains(XeriWindowThemeClass.Custom));
            container.Dispose();
        }

    #endregion

    }
}
