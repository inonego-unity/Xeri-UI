/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UGUIInteractionBlocker.cs
수정일 : 2026-11-10

# 설명
PlayMode Unity OnDisable/OnEnable 이벤트에서 Blocker 점유와 Root 표시의 재진입 계약을 검증한다.

# 테스트 구성
 A: 테스트 구성과 비활성 이벤트 재진입
 B: Host 활성 전환과 점유 수명
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;
using UnityEngine.TestTools;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// UGUI Blocker의 실제 Unity 비활성 이벤트에 따른 점유 수명 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_UGUIInteractionBlocker
    {

    #region A: 테스트 구성과 비활성 이벤트 재진입

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 Blocker의 Root와 CanvasGroup 참조를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SetBlockerReferences
        (
            UGUIInteractionBlocker blocker,
            GameObject root,
            CanvasGroup group
        )
        {
            var rootField = typeof(UGUIInteractionBlocker).GetField
            (
                "root", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            var groupField = typeof(UGUIInteractionBlocker).GetField
            (
                "canvasGroup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            Assert.IsNotNull(rootField);
            Assert.IsNotNull(groupField);
            rootField.SetValue(blocker, root);
            groupField.SetValue(blocker, group);
        }

        // ============================================================
        /// <summary>
        /// Root 비활성 콜백에서 다시 Blocker를 점유하는 테스트 Component.
        /// </summary>
        // ============================================================
        private sealed class BlockerDisableReentry : MonoBehaviour
        {

            // ------------------------------------------------------------
            /// <summary>
            /// Root 비활성 이벤트에서 호출할 재진입 콜백을 지정한다.
            /// </summary>
            // ------------------------------------------------------------
            public Action Disabled { get; set; }

            private void OnDisable()
            {
                Disabled?.Invoke();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 마지막 Lease 반환 중 새 점유가 발생하면 최신 Root와 raycast 상태를 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUIInteractionBlocker_Root비활성중재점유_최신Raycast유지()
        {
            var host = new GameObject("Blocker Host");
            var root = new GameObject("Blocker Root", typeof(CanvasGroup));
            root.transform.SetParent(host.transform, false);
            host.SetActive(false);

            try
            {
                var blocker = host.AddComponent<UGUIInteractionBlocker>();
                var group = root.GetComponent<CanvasGroup>();
                SetBlockerReferences(blocker, root, group);
                root.SetActive(false);
                var callback = root.AddComponent<BlockerDisableReentry>();
                host.SetActive(true);
                var first = blocker.Acquire();
                Lease next = null;
                callback.Disabled = () => next = blocker.Acquire();

                first.Dispose();

                Assert.IsNotNull(next);
                Assert.IsTrue(root.activeSelf);
                Assert.IsTrue(group.interactable);
                Assert.IsTrue(group.blocksRaycasts);

                callback.Disabled = null;
                next.Dispose();
                Assert.IsFalse(root.activeSelf);
                Assert.IsFalse(group.interactable);
                Assert.IsFalse(group.blocksRaycasts);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }

            yield return null;
        }

    #endregion

    #region B: Host 활성 전환과 점유 수명

        // ----------------------------------------------------------------------
        /// <summary>
        /// Host 비활성 중 Root와 raycast는 닫히고 재활성 시 기존 Lease가 복원된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUIInteractionBlocker_Host비활성후복귀_기존점유복원()
        {
            var host = new GameObject("Blocker Host");
            var root = new GameObject("Blocker Root", typeof(CanvasGroup));
            root.transform.SetParent(host.transform, false);
            host.SetActive(false);

            try
            {
                var blocker = host.AddComponent<UGUIInteractionBlocker>();
                var group = root.GetComponent<CanvasGroup>();
                SetBlockerReferences(blocker, root, group);
                host.SetActive(true);

                var lease = blocker.Acquire();

                host.SetActive(false);
                Assert.IsFalse(root.activeSelf);
                Assert.IsFalse(group.interactable);
                Assert.IsFalse(group.blocksRaycasts);

                host.SetActive(true);
                Assert.IsTrue(root.activeSelf);
                Assert.IsTrue(group.interactable);
                Assert.IsTrue(group.blocksRaycasts);

                lease.Dispose();
                Assert.IsFalse(root.activeSelf);
                Assert.IsFalse(group.interactable);
                Assert.IsFalse(group.blocksRaycasts);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }

            yield return null;
        }

    #endregion

    }
}
