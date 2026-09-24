/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriApplicationWindowManualPlayMode.cs
수정일 : 2026-09-24

# 설명
Application Window별 Child UIContext에서 Screen 전환, Modal stack, Focus authority와 teardown을 직접 확인한다.

# 테스트 구성
 A: sibling Application Window와 Screen 격리
 F: sibling Context authority와 LastFocus 복원
 M: Window-local nested Modal
 L: Modal이 열린 Application Window teardown

# 특이사항
[Explicit] 과 [Category("Manual")] 로 수동 실행 대상을 표시한다.
완료 조건이 충족되기 전에는 Space 입력으로 다음 단계에 진입하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;
using UnityEngine.TestTools;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// Application Window 수동 PlayMode 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_XeriApplicationWindowManualPlayMode
    {

    #region 필드

        private const string PREFIX = "Manual.Application";

        private XeriWindowManualRuntime manualRuntime = null;
        private XeriWindowSession first = null;
        private XeriWindowSession second = null;
        private XeriWindowManualScreenSource firstHome = null;
        private XeriWindowManualScreenSource firstDetail = null;
        private XeriWindowManualScreenSource secondHome = null;
        private XeriWindowManualScreenSource secondDetail = null;
        private XeriWindowManualModalHandle firstModal = null;
        private XeriWindowManualModalHandle nestedModal = null;
        private XeriWindowManualModalHandle secondModal = null;

    #endregion

    #region 구성

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 동일 Layout을 공유하는 두 Application Window와 각 Screen Registry를 구성한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void CreateApplicationSample(int stepCount)
        {
            manualRuntime = new XeriWindowManualRuntime(stepCount);
            var options = manualRuntime.CreateApplicationOptions(PREFIX);

            first = manualRuntime.Workspace.OpenApplicationWindow
            (
                "application-first",
                "Application A",
                new Vector2(110f, 90f),
                new Vector2(430f, 310f),
                options
            );
            second = manualRuntime.Workspace.OpenApplicationWindow
            (
                "application-second",
                "Application B",
                new Vector2(500f, 220f),
                new Vector2(400f, 290f),
                options
            );

            CreateFirstScreenSources();
            CreateSecondScreenSources();
            RegisterScreens(first, firstHome, firstDetail);
            RegisterScreens(second, secondHome, secondDetail);

            Assert.IsTrue(first.Context.Screens.Open($"{PREFIX}.Home").Accepted);
            Assert.IsTrue(second.Context.Screens.Open($"{PREFIX}.Home").Accepted);
        }

        private void CreateFirstScreenSources()
        {
            firstHome = new XeriWindowManualScreenSource
            (
                "Application A · Home",
                "Go Detail",
                () => first.Context.Screens.Replace($"{PREFIX}.Detail"),
                OpenFirstModal,
                () => first.Context.Screens.Close()
            );
            firstDetail = new XeriWindowManualScreenSource
            (
                "Application A · Detail",
                "Back Home",
                () => first.Context.Screens.Replace($"{PREFIX}.Home"),
                OpenFirstModal,
                () => first.Context.Screens.Close()
            );
        }

        private void CreateSecondScreenSources()
        {
            secondHome = new XeriWindowManualScreenSource
            (
                "Application B · Home",
                "Go Detail",
                () => second.Context.Screens.Replace($"{PREFIX}.Detail"),
                OpenSecondModal,
                () => second.Context.Screens.Close()
            );
            secondDetail = new XeriWindowManualScreenSource
            (
                "Application B · Detail",
                "Back Home",
                () => second.Context.Screens.Replace($"{PREFIX}.Home"),
                OpenSecondModal,
                () => second.Context.Screens.Close()
            );
        }
        private static void RegisterScreens
        (
            XeriWindowSession window,
            XeriWindowManualScreenSource home,
            XeriWindowManualScreenSource detail
        )
        {
            window.Context.RegisterScreen
            (
                new ScreenOptions
                (
                    $"{PREFIX}.Home",
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                home
            );
            window.Context.RegisterScreen
            (
                new ScreenOptions
                (
                    $"{PREFIX}.Detail",
                    openDuration: 0f,
                    closeDuration: 0f
                ),
                detail
            );
        }

        private void OpenFirstModal()
        {
            if (firstModal != null && !firstModal.IsDisposed) return;

            firstModal = XeriWindowApplicationManualUI.OpenModal
            (
                first.Context,
                $"{PREFIX}.Modal.A",
                "Application A · Modal A",
                () =>
                {
                    if (nestedModal != null && !nestedModal.IsDisposed) return;

                    nestedModal = XeriWindowApplicationManualUI.OpenModal
                    (
                        first.Context,
                        $"{PREFIX}.Modal.B",
                        "Application A · Modal B"
                    );
                }
            );
        }
        private void OpenSecondModal()
        {
            if (secondModal != null && !secondModal.IsDisposed) return;

            secondModal = XeriWindowApplicationManualUI.OpenModal
            (
                second.Context,
                $"{PREFIX}.Modal.A",
                "Application B · Modal"
            );
        }

    #endregion

    #region 픽스처

        [TearDown]
        public void TearDown()
        {
            nestedModal?.Dispose();
            firstModal?.Dispose();
            secondModal?.Dispose();
            nestedModal = null;
            firstModal = null;
            secondModal = null;

            manualRuntime?.Dispose();
            manualRuntime = null;
            first = null;
            second = null;
            firstHome = null;
            firstDetail = null;
            secondHome = null;
            secondDetail = null;
        }

    #endregion

    #region A-1: Screen 전환과 sibling 격리

        [Explicit]
        [Category("Manual")]
        [UnityTest]
        public IEnumerator TEST_XeriApplicationWindowManualPlayMode_Screen_수동확인()
        {
            CreateApplicationSample(4);

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    first.Context.Screens.Top?.ID == $"{PREFIX}.Home" &&
                    second.Context.Screens.Top?.ID == $"{PREFIX}.Home" &&
                    !ReferenceEquals
                    (
                        first.Context.Presentation.LayerRegistry,
                        second.Context.Presentation.LayerRegistry
                    ),
                "Application A/B에 각각 Home Screen이 보이는지 확인하세요.",
                "두 Window가 같은 ID의 Home Screen을 독립적으로 소유해야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    first.Context.Screens.Top?.ID == $"{PREFIX}.Detail" &&
                    second.Context.Screens.Top?.ID == $"{PREFIX}.Home",
                "Application A의 Go Detail 버튼을 클릭하세요.",
                "A만 Detail로 전환되고 B는 Home을 유지해야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    first.Context.Screens.Top?.ID == $"{PREFIX}.Detail" &&
                    second.Context.Screens.Top?.ID == $"{PREFIX}.Detail",
                "Application B의 Go Detail 버튼을 클릭하세요.",
                "A/B가 각각 자기 Detail Screen을 유지해야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    first.Context.Screens.Count == 0 &&
                    second.Context.Screens.Top?.ID == $"{PREFIX}.Detail",
                "Application A Detail의 Close Screen 버튼을 클릭하세요.",
                "A의 Screen만 닫히고 B의 Detail Screen은 그대로 살아 있어야 합니다."
            );
        }

    #endregion

    #region F-1: sibling Context authority와 LastFocus

        [Explicit]
        [Category("Manual")]
        [UnityTest]
        public IEnumerator TEST_XeriApplicationWindowManualPlayMode_FocusAuthority_수동확인()
        {
            CreateApplicationSample(3);
            first.Focus();
            manualRuntime.FocusDriver.Select(firstHome.ModalButton);

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    !first.Context.IsEffective &&
                    second.Context.IsEffective &&
                    ReferenceEquals
                    (
                        manualRuntime.FocusDriver.Current,
                        secondHome.NavigateButton
                    ),
                "Application B의 titlebar나 빈 영역을 클릭해 활성화하세요.",
                "B Context만 Effective가 되고 B Home의 기본 Focus가 복원되어야 합니다."
            );

            manualRuntime.FocusDriver.Select(secondHome.ModalButton);

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    first.Context.IsEffective &&
                    !second.Context.IsEffective &&
                    ReferenceEquals
                    (
                        manualRuntime.FocusDriver.Current,
                        firstHome.ModalButton
                    ),
                "Application A의 titlebar나 빈 영역을 클릭해 다시 활성화하세요.",
                "A Context가 Effective가 되고 A Home의 이전 Focus가 복원되어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    !first.Context.IsEffective &&
                    second.Context.IsEffective &&
                    ReferenceEquals
                    (
                        manualRuntime.FocusDriver.Current,
                        secondHome.ModalButton
                    ),
                "Application B를 다시 활성화하세요.",
                "B Context가 Effective가 되고 B Home의 이전 Focus가 복원되어야 합니다."
            );
        }

    #endregion

    #region M-1: Window-local nested Modal

        [Explicit]
        [Category("Manual")]
        [UnityTest]
        public IEnumerator TEST_XeriApplicationWindowManualPlayMode_Modal_수동확인()
        {
            CreateApplicationSample(4);

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    first.Context.Modals.Count == 1 &&
                    second.Context.Modals.Count == 0,
                "Application A의 Open Modal 버튼을 클릭하세요.",
                "Modal A가 Application A 안에서만 열려야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () => first.Context.Modals.Count == 2,
                "Modal A의 Open Nested 버튼을 클릭하세요.",
                "Application A의 Modal stack이 2개가 되어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    first.Context.Modals.Count == 1 &&
                    nestedModal != null &&
                    nestedModal.IsDisposed,
                "가장 위 Modal B의 Close 버튼을 클릭하세요.",
                "Modal B만 닫히고 Modal A가 다시 활성 상태여야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    first.Context.Modals.Count == 0 &&
                    first.Context.Screens.Top?.ID == $"{PREFIX}.Home",
                "Modal A의 Close 버튼을 클릭하세요.",
                "Modal stack이 비고 underlying Home Screen이 유지되어야 합니다."
            );
        }

    #endregion

    #region L-1: Modal 활성 상태 teardown

        [Explicit]
        [Category("Manual")]
        [UnityTest]
        public IEnumerator TEST_XeriApplicationWindowManualPlayMode_Teardown_수동확인()
        {
            CreateApplicationSample(2);

            yield return manualRuntime.HUD.WaitForStep
            (
                () => first.Context.Modals.Count == 1,
                "Application A의 Open Modal 버튼을 눌러 Modal A를 여세요.",
                "Window close 전 Modal A가 살아 있어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    first.IsDisposed &&
                    second != null &&
                    !second.IsDisposed &&
                    second.Context != null &&
                    !second.Context.IsDisposed,
                "Modal A가 열린 상태에서 Application A의 titlebar Close를 누르세요.",
                "A의 Child UI는 정리되고 Application B는 정상 생존해야 합니다."
            );
        }

    #endregion

    }
}
