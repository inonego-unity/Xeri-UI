/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIBootstrapperModuleAsset.cs
수정일 : 2026-11-11
# 설명
Initial Scene 확정 뒤 App 단위 UI Host를 조립하고 Unity native UI output으로 초기화한다.
Application startup policy는 Host 내부의 프로젝트 composition에 위임한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.SceneManagement;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Application;
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
        public override Task StartAsync(ApplicationContext context, CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Scene 소유 UI가 있으면 별도 Application Host를 만들지 않는다.
            if
            (
                HasSceneOwnedRuntime(SceneManager.GetActiveScene()) ||
                (
                    UIRuntime.TryCurrent(out var current) &&
                    current != null &&
                    current.IsInitialized
                )
            )
            {
                return Task.CompletedTask;
            }

            if (hostPrefab == null)
            {
                throw new InvalidOperationException("UI Host Prefab이 설정되지 않았습니다.");
            }

            if (settings == null)
            {
                throw new InvalidOperationException("UI Settings Asset이 설정되지 않았습니다.");
            }

            var instance = Instantiate(hostPrefab);
            var ownership = new Lease(() => ReleaseHost(instance));

            try
            {
                // UI Host는 Application 서비스보다 먼저 종료되어야 한다.
                context.Scope.Own(ownership);
                cancellationToken.ThrowIfCancellationRequested();

                var runtime = instance.GetComponent<UIRuntime>();
                if (runtime == null)
                {
                    throw new InvalidOperationException
                    (
                        "생성한 UI Host Prefab Root에 UIRuntime이 없습니다."
                    );
                }

                runtime.Initialize(settings);
                return Task.CompletedTask;
            }
            catch
            {
                ownership.Release();
                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// UI Runtime의 비활성화 종료 계약을 실행하고 Host 파괴를 예약한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ReleaseHost(GameObject host)
        {
            if (host == null) return;

            host.SetActive(false);
            Destroy(host);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// active Initial Scene이 자체 UIRuntime composition을 포함하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal static bool HasSceneOwnedRuntime(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            var runtimes = FindObjectsByType<UIRuntime>
            (
                FindObjectsInactive.Include
            );

            foreach (var runtime in runtimes)
            {
                if
                (
                    runtime != null &&
                    runtime.gameObject.scene == scene
                )
                {
                    return true;
                }
            }

            return false;
        }

    #endregion

    }
}
