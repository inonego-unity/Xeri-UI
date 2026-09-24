/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationPreferences.cs
수정일 : 2026-10-05

# 설명
Validation Desktop의 form/control natural application.
Unity 기본 DropdownField, TextField, Toggle, Slider와 Button을 Xeri runtime baseline 위에서 실제 입력할 수 있게 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// 공통 외관 선택과 기본 폼 컨트롤을 제공한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationPreferences : IDisposable
    {

    #region 필드와 상태

        // ------------------------------------------------------------
        /// <summary>
        /// Window에 연결할 콘텐츠 Root.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Root { get; }

        private readonly DropdownField theme = null;
        private readonly DropdownField palette = null;
        private readonly XeriValidationAppearance appearance;
        private readonly TextField displayName = null;
        private readonly Toggle compactMode = null;
        private readonly Slider scale = null;
        private readonly Button applyButton = null;
        private readonly Label status = null;

        private bool isDisposed = false;

    #endregion

    #region 화면 연결

        // ------------------------------------------------------------
        /// <summary>
        /// 공통 외관 선택과 기본 폼 컨트롤을 제공한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationPreferences
        (
            XeriValidationAssets assets,
            XeriValidationAppearance appearance
        )
        {
            this.appearance = appearance;
            if (assets == null || assets.PreferencesTemplate == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            Root = XeriValidationUI.CloneRoot
            (
                assets.PreferencesTemplate,
                "PreferencesRoot"
            );
            assets.ApplyAppStyles(Root);
            theme = XeriValidationUI.Require<DropdownField>(Root, "ThemeField");
            palette = XeriValidationUI.Require<DropdownField>(Root, "PaletteField");
            displayName = XeriValidationUI.Require<TextField>(Root, "DisplayNameField");
            compactMode = XeriValidationUI.Require<Toggle>(Root, "CompactModeField");
            scale = XeriValidationUI.Require<Slider>(Root, "ScaleField");
            applyButton = XeriValidationUI.Require<Button>(Root, "PreferencesApply");
            status = XeriValidationUI.Require<Label>(Root, "PreferencesStatus");

            theme.choices = new List<string>
            {
                "Windows",
                "Mac",
                "Minimal",
            };
            theme.value = appearance.WindowStyle;
            palette.choices = new List<string>
            {
                "Dark", "Light",
            };
            palette.value = appearance.Palette;
            displayName.value = "Validation User";
            compactMode.value = appearance.Compact;
            scale.lowValue = 0.8f;
            scale.highValue = 1.4f;
            scale.value = 1.0f;
            applyButton.clicked += Apply;
            theme.RegisterValueChangedCallback(OnThemeChanged);
            palette.RegisterValueChangedCallback(OnThemeChanged);
            status.text = $"{appearance.Palette} · {appearance.WindowStyle}";
        }

    #endregion

    #region 외관 선택 적용

        // ------------------------------------------------------------
        /// <summary>
        /// 선택 즉시 전체 workspace에 실제 테마를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnThemeChanged(ChangeEvent<string> evt)
        {
            Apply();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선택한 외관을 적용하고 표시를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Apply()
        {
            appearance.Set(palette.value, theme.value, compactMode.value);
            status.text =
                $"{appearance.Palette} · {appearance.WindowStyle}";
        }

    #endregion

    #region 연결 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 이벤트 연결과 소유한 수명을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            applyButton.clicked -= Apply;
            theme.UnregisterValueChangedCallback(OnThemeChanged);
            palette.UnregisterValueChangedCallback(OnThemeChanged);
            Root.RemoveFromHierarchy();
        }

    #endregion

    }
}
