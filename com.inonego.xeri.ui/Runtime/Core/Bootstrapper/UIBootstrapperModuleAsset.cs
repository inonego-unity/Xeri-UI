/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIBootstrapperModuleAsset.cs
수정일 : 2026-09-17
# 설명
Initial Scene 확정 뒤 App 단위 UI Host를 조립하고 Unity native UI output으로 초기화한다.
Application startup policy는 Host 내부의 프로젝트 composition에 위임한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Bootstrapper;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Runtime Host를 생성하는 Bootstrapper Module Asset.
    /// </summary>
    // ============================================================
    [CreateAssetMenu
    (
        fileName = "UI Bootstrapper Module",
        menuName = "Xeri/Bootstrapper/UI Module"
    )]
    public sealed class UIBootstrapperModuleAsset : BootstrapperModuleAsset
    {

    #region 실행 단계

        // ------------------------------------------------------------
        /// <summary>
        /// UI composition이 실행될 Initial Scene 이후 phase.
        /// </summary>
        // ------------------------------------------------------------
        public override BootstrapperModulePhase Phase => BootstrapperModulePhase.AfterInitialScene;

    #endregion

    #region 필드

        [SerializeField]
        private GameObject hostPrefab = null;

        [SerializeField]
        private UISettingsAsset settings = null;

    #endregion

    #region UI 초기화

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Host Prefab과 Settings 참조를 확인하고 새 Host를 생성해 Runtime을 초기화한다.
        /// <br/> 실패 시 기존 Host를 건드리지 않고 이번에 생성한 Host만 제거한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public override async Awaitable Init()
        {
            if (hostPrefab == null)
            {
                throw new InvalidOperationException("UI Host Prefab이 설정되지 않았습니다.");
            }

            if (settings == null)
            {
                throw new InvalidOperationException("UI Settings Asset이 설정되지 않았습니다.");
            }

            var instance = Instantiate(hostPrefab);

            try
            {
                var runtime = instance.GetComponent<UIRuntime>();

                if (runtime == null)
                {
                    throw new InvalidOperationException
                    (
                        "생성한 UI Host Prefab Root에 UIRuntime이 없습니다."
                    );
                }

                runtime.Initialize(settings);
            }
            catch
            {
                // 초기화에 실패한 Host가 Scene의 Runtime·EventSystem 구성을 막지 않도록 제거한다.
                instance.SetActive(false);
                Destroy(instance);
                throw;
            }
        }

    #endregion

    }
}
