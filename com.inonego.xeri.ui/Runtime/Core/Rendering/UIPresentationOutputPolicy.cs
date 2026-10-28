/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIPresentationOutputPolicy.cs
수정일 : 2026-10-18

# 설명
UIRuntime이 소유한 프로젝트 단위 출력 정책을 선택적 SRP 렌더링 통합부에 노출한다.
Core는 URP/HDRP를 참조하지 않으며, Runtime이 없으면 Native 기본값을 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Native UI를 카메라 후처리 전후 어느 시점에 합성할지 지정한다.
    /// </summary>
    // ============================================================
    public enum UIPresentationPostProcessing
    {
        Unaffected = 0,
        Affected = 1,
    }

    // ============================================================
    /// <summary>
    /// UIRuntime 설정에 의해 소유되는 앱 단위 고정밀 출력 정책.
    /// </summary>
    // ============================================================
    public static class UIPresentationOutputPolicy
    {

    #region 현재 출력 정책

        private static UISettingsAsset settings;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 UIRuntime이 설정의 소유권을 획득했는지 판정한다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool IsConfigured => settings != null;

        // ------------------------------------------------------------
        /// <summary>
        /// 프로젝트의 High Precision Output 선택값.
        /// </summary>
        // ------------------------------------------------------------
        public static bool HighPrecisionOutput =>
            settings != null && settings.HighPrecisionOutput;

        // ------------------------------------------------------------
        /// <summary>
        /// 후처리 정책. Runtime이 없으면 Unaffected를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static UIPresentationPostProcessing PostProcessing =>
            settings != null
                ? settings.UIPostProcessing
                : UIPresentationPostProcessing.Unaffected;

        // ------------------------------------------------------------
        /// <summary>
        /// Native 기본 출력이 아닌 카메라 합성 정책을 두 SRP에 함께 제공한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static bool TryGetComposition(out bool highPrecision, out bool affected)
        {
            var owner = settings;
            highPrecision = owner != null && owner.HighPrecisionOutput;
            affected = owner != null && owner.UIPostProcessing == UIPresentationPostProcessing.Affected;
            return highPrecision || affected;
        }

    #endregion

    #region 설정 소유권과 초기화

        // ------------------------------------------------------------
        /// <summary>
        /// UIRuntime SettingsAsset 하나의 출력 정책 소유권을 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static void Bind(UISettingsAsset owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (settings != null && !ReferenceEquals(settings, owner))
            {
                throw new InvalidOperationException
                (
                    "Another Xeri UI SettingsAsset already owns the output policy."
                );
            }

            settings = owner;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 동일한 설정 소유자만 출력 정책을 반환할 수 있다.
        /// </summary>
        // ------------------------------------------------------------
        internal static void Unbind(UISettingsAsset owner)
        {
            if (ReferenceEquals(settings, owner))
            {
                settings = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            settings = null;
        }

    #endregion

    }
}
