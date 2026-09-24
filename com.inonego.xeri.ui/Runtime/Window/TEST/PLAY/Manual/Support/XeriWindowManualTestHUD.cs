/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowManualTestHUD.cs
수정일 : 2026-09-24

# 설명
Window 수동 테스트의 상단 고정 정보 영역, 하단 Window 영역과 단계 진행 gating을 제공한다.

# 테스트 구성
 H: 수동 테스트 HUD와 단계 진행 상태
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// Window 수동 테스트 공용 HUD.
    /// </summary>
    // ============================================================
    internal sealed class XeriWindowManualTestHUD
    {

    #region 필드

        public VisualElement TestInfoArea { get; }
        public VisualElement WindowArea { get; }

        private readonly int stepCount = 0;
        private readonly Label stepLabel = null;
        private readonly Label readinessLabel = null;
        private readonly Label guideLabel = null;
        private readonly Label conditionLabel = null;

        private int stepIndex = 0;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Root에 상단 정보 영역과 하단 Window 영역을 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowManualTestHUD(VisualElement root, int stepCount)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (stepCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepCount));
            }

            this.stepCount = stepCount;

            TestInfoArea = new VisualElement
            {
                name = "TEST_XeriWindow_TestInfoArea",
            };
            TestInfoArea.AddToClassList("xeri-window-manual__test-info");

            var statusRow = new VisualElement();
            statusRow.AddToClassList("xeri-window-manual__status-row");
            stepLabel = new Label($"STEP 00/{stepCount:00}");
            stepLabel.AddToClassList("xeri-window-manual__step");

            readinessLabel = new Label("진행 가능: 확인 중");
            readinessLabel.AddToClassList("xeri-window-manual__readiness");

            statusRow.Add(stepLabel);
            statusRow.Add(readinessLabel);

            guideLabel = new Label();
            guideLabel.AddToClassList("xeri-window-manual__guide");

            conditionLabel = new Label();
            conditionLabel.AddToClassList("xeri-window-manual__condition");

            TestInfoArea.Add(statusRow);
            TestInfoArea.Add(guideLabel);
            TestInfoArea.Add(conditionLabel);

            WindowArea = new VisualElement
            {
                name = "TEST_XeriWindow_WindowArea",
            };
            WindowArea.AddToClassList("xeri-window-manual__window-area");

            root.Add(TestInfoArea);
            root.Add(WindowArea);
        }

    #endregion

    #region H-1: 단계 진행

        // ----------------------------------------------------------------------
        /// <summary>
        /// 완료 조건이 충족된 상태에서 Space 입력이 들어올 때만 다음 단계로 진행한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public IEnumerator WaitForStep
        (
            Func<bool> canProceed,
            string guide,
            string condition
        )
        {
            if (canProceed == null)
            {
                throw new ArgumentNullException(nameof(canProceed));
            }

            stepIndex++;

            while (true)
            {
                var ready = canProceed();
                RefreshStatus(ready, guide, condition);

                if (ready && IsSpaceKeyPressed())
                {
                    break;
                }

                yield return null;
            }

            yield return null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상단 option 영역에 추가 UI를 배치한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddOptionArea(VisualElement optionArea)
        {
            if (optionArea == null)
            {
                throw new ArgumentNullException(nameof(optionArea));
            }

            TestInfoArea.Add(optionArea);
        }

    #endregion

    #region 내부 처리

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 단계와 진행 가능 여부를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshStatus(bool canProceed, string guide, string condition)
        {
            stepLabel.text = $"STEP {stepIndex:00}/{stepCount:00}";
            guideLabel.text = guide;
            conditionLabel.text = $"완료 조건 · {condition}";
            readinessLabel.text = canProceed
                ? "진행 가능 · YES · Space 키로 다음 단계"
                : "진행 가능 · NO · 아래 동작을 먼저 완료하세요";

            readinessLabel.EnableInClassList
            (
                "xeri-window-manual__readiness--ready",
                canProceed
            );
            readinessLabel.EnableInClassList
            (
                "xeri-window-manual__readiness--blocked",
                !canProceed
            );
        }

        private static bool IsSpaceKeyPressed()
        {
        #if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        #else
            return Input.GetKeyDown(KeyCode.Space);
        #endif
        }

    #endregion

    }
}
