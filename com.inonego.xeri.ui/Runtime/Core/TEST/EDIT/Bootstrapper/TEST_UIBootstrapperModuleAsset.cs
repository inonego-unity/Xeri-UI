/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UIBootstrapperModuleAsset.cs
수정일 : 2026-10-04

# 설명
UI Bootstrapper Module이 Initial Scene의 scene-owned UIRuntime을 fallback Host보다 우선하는 계약을 검증한다.

# 테스트 구성
 S: Scene-owned Runtime 감지
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.SceneManagement;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.TEST
{
    public class TEST_UIBootstrapperModuleAsset
    {

    #region S-1: 씬 소유 런타임

        // ----------------------------------------------------------------------
        /// <summary>
        /// active Scene에 UIRuntime이 있으면 scene-owned composition으로 감지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UIBootstrapperModuleAsset_SceneRuntime_존재_감지()
        {
            var gameObject = new GameObject("Scene-owned UIRuntime");

            try
            {
                gameObject.AddComponent<UIRuntime>();

                Assert.IsTrue
                (
                    UIBootstrapperModuleAsset.HasSceneOwnedRuntime
                    (
                        SceneManager.GetActiveScene()
                    )
                );
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
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
