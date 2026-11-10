/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UIBootstrapperModuleAsset.cs
수정일 : 2026-11-11

# 설명
UI Bootstrapper Module이 Initial Scene의 scene-owned UIRuntime을 fallback Host보다 우선하는 계약을 검증한다.

# 테스트 구성
 A: 씬 소유 Runtime 우선 및 감지
========================================================================= BLOCK_HEADER_END */

using System;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.SceneManagement;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Application;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.TEST
{
    // ============================================================
    /// <summary>
    /// Scene Runtime 우선 정책을 검증하는 Bootstrapper 테스트.
    /// </summary>
    // ============================================================
    public class TEST_UIBootstrapperModuleAsset
    {

    #region 테스트 Module

        private sealed class Startup : IApplicationModule
        {
            private readonly UIBootstrapperModuleAsset module;

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트할 UI Bootstrapper를 Application Module로 연결한다.
            /// </summary>
            // ------------------------------------------------------------
            public Startup(UIBootstrapperModuleAsset module)
            {
                this.module = module;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Application의 시작 요청을 Bootstrapper에 전달한다.
            /// </summary>
            // ------------------------------------------------------------
            public Task StartAsync(ApplicationContext context, CancellationToken cancellationToken)
            {
                return module.StartAsync(context, cancellationToken);
            }
        }

    #endregion

    #region A: 씬 소유 Runtime 우선 및 감지

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Initial Scene이 자체 Runtime을 제공하면
        /// <br/> Host Prefab과 Settings 없이도 fallback Host를 생성하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task TEST_UIBootstrapperModuleAsset_SceneOwnedRuntime_StartAsync에서FallbackHost생략()
        {
            var gameObject = new GameObject("Scene-owned UIRuntime");
            var module = ScriptableObject.CreateInstance<UIBootstrapperModuleAsset>();
            gameObject.SetActive(false);

            try
            {
                var runtime = gameObject.AddComponent<UIRuntime>();

                Assert.IsFalse(runtime.IsInitialized);
                Assert.IsFalse(UIRuntime.TryCurrent(out var current) && current != null && current.IsInitialized);
                Assert.AreEqual(SceneManager.GetActiveScene(), gameObject.scene);
                Assert.IsTrue(UIBootstrapperModuleAsset.HasSceneOwnedRuntime(gameObject.scene));
                var application = new ApplicationRuntime();

                try
                {
                    IApplicationModule[] modules =
                    {
                        new Startup(module),
                    };
                    await application.StartAsync(modules);
                    Assert.IsFalse(runtime.IsInitialized);
                }
                finally
                {
                    await application.StopAsync();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(module);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 유효하지 않은 Scene은 scene-owned Runtime을 가진 것으로 처리하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIBootstrapperModuleAsset_InvalidScene_미감지()
        {
            Assert.IsFalse
            (
                UIBootstrapperModuleAsset.HasSceneOwnedRuntime(default)
            );
        }

    #endregion

    }
}
