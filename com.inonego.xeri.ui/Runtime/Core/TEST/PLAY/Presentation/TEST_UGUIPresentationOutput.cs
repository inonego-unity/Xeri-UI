/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UGUIPresentationOutput.cs
수정일 : 2026-10-28

# 설명
PlayMode에서 Component 파괴 중 Scene-authored Canvas의 정렬 순서 복원을 검증한다.
EditMode의 DestroyImmediate는 동일한 Unity MonoBehaviour lifetime 경계가 아니다.

# 테스트 구성
 L: 대여 출력의 파괴와 순서 복원
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    // ======================================================================
    /// <summary>
    /// PlayMode Canvas Output destruction / borrowed state 계약 테스트.
    /// </summary>
    // ======================================================================
    public sealed class TEST_UGUIPresentationOutput
    {

    #region L-1: 대여 출력 파괴와 순서 복원

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Scene-authored Output component가 먼저 사라져도 Canvas 정렬 상태를 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [UnityTest]
        public IEnumerator TEST_UGUIPresentationOutput_OnDestroy_BorrowedOrder복원()
        {
            var gameObject = new GameObject
            (
                "Borrowed UGUI Output",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UGUIPresentationOutput)
            );
            var canvas = gameObject.GetComponent<Canvas>();
            var output = gameObject.GetComponent<UGUIPresentationOutput>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 37;

            try
            {
                yield return null;

                output.InitializeBorrowed(111);
                Assert.AreEqual(111, canvas.sortingOrder);
                UnityEngine.Object.Destroy(output);
                yield return null;

                Assert.AreEqual(37, canvas.sortingOrder);
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

    #endregion

    }
}
