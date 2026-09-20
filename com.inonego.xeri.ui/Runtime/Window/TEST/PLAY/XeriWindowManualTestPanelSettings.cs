/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowManualTestPanelSettings.cs
수정일 : 2026-09-20

# 설명
Xeri Window 수동 테스트에서 임시 PanelSettings를 에셋 생성 상태와 맞추는 테스트 헬퍼.

# 특이사항
ScriptableObject.CreateInstance<PanelSettings>()는 기본 ThemeStyleSheet를 직렬화하지 않으므로,
수동 테스트용 transient PanelSettings에 Unity 기본 runtime theme을 명시적으로 연결한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Reflection;

using UnityEngine.UIElements;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// Xeri Window 수동 테스트용 transient PanelSettings 보정 헬퍼.
    /// </summary>
    // ============================================================
    public static class XeriWindowManualTestPanelSettings
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
        public static void ApplyDefaultRuntimeTheme(PanelSettings target)
        {
            if (target == null) return;

            var theme = GetDefaultRuntimeTheme();
            if (theme != null)
            {
                target.themeStyleSheet = theme;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity runtime panel이 사용하는 기본 ThemeStyleSheet를 가져온다.
        /// </summary>
        // ------------------------------------------------------------
        private static ThemeStyleSheet GetDefaultRuntimeTheme()
        {
            var field = typeof(PanelSettings).GetField
            (
                DEFAULT_THEME_GETTER_FIELD_NAME,
                BindingFlags.NonPublic | BindingFlags.Static
            );
            if (field == null) return null;

            var getter = field.GetValue(null) as Func<ThemeStyleSheet>;

            return getter != null ? getter.Invoke() : null;
        }

    #endregion

    }
}
