/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationAppearance.cs
수정일 : 2026-10-05

# 설명
Validation 수명 동안 공통 색상과 기존 Window theme 선택을 모든 스타일 root에 전파한다.
명시적으로 연결된 Root에 선택을 적용하고 변경된 값만 PlayerPrefs에 저장한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// Validation의 색상과 창 장식 선택을 연결한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationAppearance : IDisposable
    {

    #region 상태

        private const string PREFS_KEY = "Xeri.Validation.Appearance.";
        private readonly XeriValidationAssets assets;
        private readonly HashSet<VisualElement> roots = new();

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 공통 색상 선택.
        /// </summary>
        // ------------------------------------------------------------
        internal string Palette { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 Window theme의 표시 이름.
        /// </summary>
        // ------------------------------------------------------------
        internal string WindowStyle { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 조밀한 콘텐츠 간격 사용 여부.
        /// </summary>
        // ------------------------------------------------------------
        internal bool Compact { get; private set; }

    #endregion

    #region 연결과 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 외관 선택을 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationAppearance
        (
            XeriValidationAssets assets
        ) : base()
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            Palette = PlayerPrefs.GetString(PREFS_KEY + "Palette", "Dark");
            WindowStyle = PlayerPrefs.GetString(PREFS_KEY + "Window", "Windows");
            Compact = PlayerPrefs.GetInt(PREFS_KEY + "Compact", 0) != 0;
            // 잘못된 저장값이 UI 표시와 실제 적용을 갈라놓지 않도록 정규화한다.
            Palette = Palette == "Light" ? "Light" : "Dark";
            if (WindowStyle != "Mac" && WindowStyle != "Minimal")
            {
                WindowStyle = "Windows";
            }

        }

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 Root에 외관을 연결하고 명시적인 해제 수명을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal Lease Bind(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (!roots.Add(root))
            {
                throw new InvalidOperationException("같은 Root의 외관 수명을 중복 등록할 수 없습니다.");
            }

            assets.ApplySharedStyles(root);
            Apply(root);
            return new Lease(() => roots.Remove(root));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 남은 외관 연결 참조를 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            roots.Clear();
        }

    #endregion

    #region 선택 적용

        // ------------------------------------------------------------
        /// <summary>
        /// 열린 UI를 즉시 갱신하고 다음 실행에 사용할 선택을 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Set(string palette, string windowStyle, bool compact)
        {
            if (Palette == palette && WindowStyle == windowStyle && Compact == compact) return;

            Palette = palette;
            WindowStyle = windowStyle;
            Compact = compact;
            // 동일한 root 경로로 기존 화면과 새 화면의 계약을 유지한다.
            foreach (var root in roots)
            {
                Apply(root);
            }

            PlayerPrefs.SetString(PREFS_KEY + "Palette", Palette);
            PlayerPrefs.SetString(PREFS_KEY + "Window", WindowStyle);
            PlayerPrefs.SetInt(PREFS_KEY + "Compact", Compact ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 공통 팔레트와 밀도, 기존 창 장식 API를 함께 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Apply(VisualElement root)
        {
            root.AddToClassList("xeri-validation-theme");
            root.AddToClassList("xeri-desktop-theme");
            root.EnableInClassList("xeri-desktop-theme--light", Palette == "Light");
            root.EnableInClassList("xeri-desktop-theme--compact", Compact);
            root.EnableInClassList("xeri-validation-theme--light", Palette == "Light");
            root.EnableInClassList("xeri-validation-theme--compact", Compact);
            if (root is XeriWindowPanel panel)
            {
                panel.ApplyTheme(WindowStyle.ToLowerInvariant());
            }
        }

    #endregion

    }
}
