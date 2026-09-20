/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowThemeResolver.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 theme resolver 테스트.

# 테스트 구성
 R: Theme 조회
========================================================================= BLOCK_HEADER_END */

using System;
using System.Reflection;

using UnityEngine;

using NUnit;
using NUnit.Framework;

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

    #region 헬퍼

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트용 theme asset을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriWindowThemeAsset CreateTheme(string id)
        {
            var theme = ScriptableObject.CreateInstance<XeriWindowThemeAsset>();
            var field = typeof(XeriWindowThemeAsset).GetField
            (
                "id",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            field.SetValue(theme, id);

            return theme;
        }

    #endregion

    #region R-1: 조회 성공

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

    #endregion

    #region R-2: 조회 실패

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

    #endregion

    #region R-3: 중복 등록

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

    #endregion

    }
}
