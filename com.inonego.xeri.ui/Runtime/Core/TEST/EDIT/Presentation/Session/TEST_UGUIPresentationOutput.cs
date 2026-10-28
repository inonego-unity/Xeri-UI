/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_UGUIPresentationOutput.cs
수정일 : 2026-10-28

# 설명
UGUI Native Output의 Generated/Borrowed 수명·중복 획득·authoring 순서 복원을 검증한다.
UITKPresentationOutput과 동일한 생성/대여/반환 계약을 보존한다.

# 테스트 구성
 L: 생성·대여 출력의 순서와 해제 수명
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UI;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    // ======================================================================
    /// <summary>
    /// UGUI Output의 Native Canvas order ownership 경계를 검증한다.
    /// </summary>
    // ======================================================================
    public sealed class TEST_UGUIPresentationOutput
    {

    #region 헬퍼

        private static UGUIPresentationOutput CreateOutput()
        {
            var gameObject = new GameObject
            (
                "UGUI Output Lifetime",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UGUIPresentationOutput)
            );

            var canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 37;
            return gameObject.GetComponent<UGUIPresentationOutput>();
        }

    #endregion

    #region L-1: 생성·대여 출력의 순서와 해제

        // ----------------------------------------------------------------------
        /// <summary>
        /// Scene-authored Canvas 정체성과 원래 정렬 순서를 빌렸다가 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UGUIPresentationOutput_BorrowedOrder_복원과중복해제()
        {
            var output = CreateOutput();
            var canvas = output.Canvas;

            try
            {
                output.InitializeBorrowed(108);
                Assert.AreSame(canvas, output.Canvas);
                Assert.AreEqual(108, canvas.sortingOrder);

                Assert.Throws<InvalidOperationException>
                (
                    () => output.Initialize(120)
                );
                Assert.Throws<InvalidOperationException>
                (
                    () => output.InitializeBorrowed(121)
                );

                output.ReleaseOutputState();
                output.ReleaseOutputState();
                Assert.AreEqual(37, canvas.sortingOrder);

                output.InitializeBorrowed(76);
                Assert.AreEqual(76, canvas.sortingOrder);
                output.ReleaseOutputState();
                Assert.AreEqual(37, canvas.sortingOrder);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output.gameObject);
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Generated Output은 LayerOrder만 부여하고 authored prefab 소유권을 주장하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UGUIPresentationOutput_GeneratedRelease_순서복원강제하지않음()
        {
            var output = CreateOutput();

            try
            {
                output.Initialize(92);
                Assert.AreEqual(92, output.Canvas.sortingOrder);

                output.ReleaseOutputState();
                Assert.AreEqual(92, output.Canvas.sortingOrder);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output.gameObject);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 명시적 해제 뒤 바꾼 Canvas 순서를 Component 파괴가 덮어쓰지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UGUIPresentationOutput_명시해제후Component파괴_Order유지()
        {
            var output = CreateOutput();
            var gameObject = output.gameObject;
            var canvas = output.Canvas;

            try
            {
                output.InitializeBorrowed(111);
                Assert.AreEqual(111, canvas.sortingOrder);
                output.ReleaseOutputState();
                Assert.AreEqual(37, canvas.sortingOrder);
                canvas.sortingOrder = 73;
                UnityEngine.Object.DestroyImmediate(output);
                Assert.AreEqual(73, canvas.sortingOrder);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

    #endregion

    }
}
