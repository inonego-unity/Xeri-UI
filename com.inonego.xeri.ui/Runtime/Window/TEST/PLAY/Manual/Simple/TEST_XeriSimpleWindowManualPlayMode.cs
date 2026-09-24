/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriSimpleWindowManualPlayMode.cs
수정일 : 2026-09-24

# 설명
Xeri Window 시스템을 PlayMode 화면에서 직접 조작해 확인하는 수동 테스트.
상단 고정 HUD에 단계, 완료 조건과 진행 가능 여부를 표시하고 하단 Window 영역만 테스트한다.
완료 조건이 충족된 상태에서 Space 키를 눌렀을 때만 다음 단계로 진행한다.

# 테스트 구성
 C: ContentRoot mini-app 입력
 G: Window geometry와 maximize
 T: Minimize와 Tray
 F: Focus와 Close
 V: Theme 시각 확인

# 특이사항
[Explicit] 과 [Category("Manual")] 로 수동 실행 대상을 표시한다.
조건 미충족 상태의 Space 입력은 실패나 단계 진행으로 처리하지 않는다.
CLI 자동 실행은 -testCategory "!Manual" 로 Manual category를 명시적으로 제외한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.TEST;
using inonego.Xeri.UI.Tray;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// Simple Window 수동 PlayMode 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_XeriSimpleWindowManualPlayMode
    {

    #region 필드

        private const string MAIN_NORMAL_TITLE = "Main Window";
        private const string SECOND_NORMAL_TITLE = "Second Window";
        private const string MAIN_LONG_TITLE = "Main Window - Very Long Windows Style Title For Layout Check";
        private const string SECOND_LONG_TITLE = "Second Window - Very Long Mac Style Title For Layout Check";

        private XeriWindowManualRuntime manualRuntime = null;
        private XeriWindowWorkspace workspace = null;
        private XeriWindowSession mainSession = null;
        private XeriWindowSession secondSession = null;
        private XeriWindowPanel mainPanel = null;
        private XeriWindowPanel secondPanel = null;
        private XeriWindowHandle mainHandle = null;
        private XeriWindowHandle secondHandle = null;
        private XeriWindowTraySource traySource = null;
        private XeriTrayController trayController = null;
        private XeriTrayPanel trayPanel = null;
        private VisualElement trayLayer = null;
        private VisualElement optionBar = null;
        private Button longTitleButton = null;
        private Button titleIconButton = null;
        private Texture2D mainIcon = null;
        private Texture2D secondIcon = null;
        private XeriWindowTaskBoardTestApp taskBoardApp = null;
        private XeriWindowPreferencesTestApp preferencesApp = null;

        private bool cancelNextClose = false;
        private bool useLongTitle = false;
        private bool showTitleIcon = false;

    #endregion

    #region 구성

        // ------------------------------------------------------------
        /// <summary>
        /// 공용 Runtime 위에 Simple Window 두 개와 mini-app을 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CreateWindowSample(int stepCount)
        {
            manualRuntime = new XeriWindowManualRuntime(stepCount);
            workspace = manualRuntime.Workspace;

            var mainColor = new Color(0.20f, 0.44f, 0.96f, 1f);
            mainIcon = CreateTemporaryIcon(mainColor, "W");
            taskBoardApp = XeriWindowManualTestApps.CreateTaskBoard();
            mainSession = AddManualWindow
            (
                "main",
                MAIN_NORMAL_TITLE,
                "Windows style window",
                mainIcon,
                new XeriTrayBadge("W", mainColor),
                taskBoardApp.Root,
                new Vector2(180f, 150f),
                new Vector2(420f, 300f),
                out mainPanel
            );
            mainHandle = mainSession.Handle;
            ApplyWindowTheme(mainPanel, XeriWindowThemeClass.Windows);

            var secondColor = new Color(0.30f, 0.80f, 0.38f, 1f);
            secondIcon = CreateTemporaryIcon(secondColor, "M");
            preferencesApp = XeriWindowManualTestApps.CreatePreferences();
            secondSession = AddManualWindow
            (
                "second",
                SECOND_NORMAL_TITLE,
                "Mac style window",
                secondIcon,
                new XeriTrayBadge("M", secondColor),
                preferencesApp.Root,
                new Vector2(440f, 260f),
                new Vector2(380f, 270f),
                out secondPanel
            );
            secondHandle = secondSession.Handle;
            ApplyWindowTheme(secondPanel, XeriWindowThemeClass.Mac);

            CreateManualTray();
            ConfigureManualTrayLayout();
            CreateOptionBar();
            ApplyTitleOptions();
        }


        // ----------------------------------------------------------------------
        /// <summary>
        /// production Workspace.OpenWindow 경로로 Manual Window 하나를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private XeriWindowSession AddManualWindow
        (
            string id,
            string title,
            string tooltip,
            Texture2D icon,
            XeriTrayBadge badge,
            VisualElement view,
            Vector2 pos,
            Vector2 size,
            out XeriWindowPanel panel
        )
        {
            var options = XeriWindowOptions.Default();
            var record = new XeriWindowRecord
            {
                ID = id,
                Title = title,
                Tooltip = tooltip,
                Icon = icon,
                Badge = badge,
                Pos = pos,
                Size = size,
                NormalPos = pos,
                NormalSize = size,
                StackLayer = options.StackLayer,
            };
            var session = workspace.OpenWindow(record, view, options);
            panel = manualRuntime.FindWindowPanel(title);

            return session;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 긴 제목과 title icon 표시를 즉시 바꿔 볼 수 있는 수동 확인 버튼을 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void CreateOptionBar()
        {
            optionBar = new VisualElement
            {
                name = "TEST_XeriWindow_OptionBar",
            };
            optionBar.AddToClassList("xeri-window-manual__option-bar");
            optionBar.pickingMode = PickingMode.Position;

            longTitleButton = CreateOptionButton();
            titleIconButton = CreateOptionButton();

            longTitleButton.clicked += ToggleLongTitle;
            titleIconButton.clicked += ToggleTitleIcon;

            optionBar.Add(longTitleButton);
            optionBar.Add(titleIconButton);
            manualRuntime.HUD.AddOptionArea(optionBar);

            RefreshOptionButtons();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 수동 확인 option button을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Button CreateOptionButton()
        {
            var button = new Button();
            button.style.height = 26f;
            button.style.minWidth = 112f;
            button.style.marginLeft = 4f;
            button.style.marginRight = 4f;
            button.style.paddingLeft = 10f;
            button.style.paddingRight = 10f;
            button.style.borderTopLeftRadius = 4f;
            button.style.borderTopRightRadius = 4f;
            button.style.borderBottomLeftRadius = 4f;
            button.style.borderBottomRightRadius = 4f;
            button.style.fontSize = 12f;

            return button;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 긴 title 표시 여부를 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ToggleLongTitle()
        {
            useLongTitle = !useLongTitle;

            ApplyTitleOptions();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// title icon 표시 여부를 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ToggleTitleIcon()
        {
            showTitleIcon = !showTitleIcon;

            ApplyTitleOptions();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 option 상태를 panel titlebar에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyTitleOptions()
        {
            ApplyWindowTitleOptions
            (
                mainPanel,
                MAIN_NORMAL_TITLE,
                MAIN_LONG_TITLE,
                mainIcon
            );
            ApplyWindowTitleOptions
            (
                secondPanel,
                SECOND_NORMAL_TITLE,
                SECOND_LONG_TITLE,
                secondIcon
            );

            RefreshOptionButtons();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 window의 title과 title icon 표시 상태를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyWindowTitleOptions
        (
            XeriWindowPanel panel,
            string normalTitle,
            string longTitle,
            Texture2D icon
        )
        {
            if (panel == null) return;

            panel.ApplyTitle(useLongTitle ? longTitle : normalTitle);
            panel.ApplyTitleIcon(showTitleIcon ? icon : null);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Option button text와 색을 현재 상태에 맞춘다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshOptionButtons()
        {
            RefreshOptionButton(longTitleButton, "긴 제목", useLongTitle);
            RefreshOptionButton(titleIconButton, "아이콘", showTitleIcon);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 단일 option button 표시 상태를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void RefreshOptionButton(Button button, string label, bool active)
        {
            if (button == null) return;

            button.text = active ? $"{label} ON" : $"{label} OFF";
            button.style.color = Color.white;
            button.style.backgroundColor = active
                ? new Color(0.20f, 0.44f, 0.96f, 1f)
                : new Color(0.20f, 0.20f, 0.22f, 1f);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Manual 테스트에서 tray가 화면 하단 dock처럼 보이도록 배치한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ConfigureManualTrayLayout()
        {
            trayLayer.BringToFront();
            trayLayer.style.position = Position.Absolute;
            trayLayer.style.height = 56f;
            trayLayer.style.left = 0f;
            trayLayer.style.right = 0f;
            trayLayer.style.bottom = 16f;
            trayLayer.style.flexDirection = FlexDirection.Row;
            trayLayer.style.alignItems = Align.Center;
            trayLayer.style.justifyContent = Justify.Center;
            trayLayer.pickingMode = PickingMode.Position;

            trayPanel.style.height = 44f;
            trayPanel.style.minWidth = 180f;
            trayPanel.style.minHeight = 44f;
            trayPanel.style.flexShrink = 0f;
            trayPanel.pickingMode = PickingMode.Position;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Manual 테스트용 Tray source, controller, panel을 생성하고 화면 Root에 붙인다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void CreateManualTray()
        {
            trayLayer = new VisualElement
            {
                name = "TEST_XeriWindow_TrayLayer",
            };
            trayPanel = new XeriTrayPanel();
            traySource = workspace.CreateTraySource();
            trayController = new XeriTrayController
            (
                traySource, trayPanel,
                new XeriTrayOptions
                {
                    VisibleContent = XeriTrayContent.Icon,
                    UssClass = "xeri-tray--manual-test",
                    Reorderable = true,
                    ReorderAxis = XeriTrayReorderAxis.Horizontal,
                    AnimateReorder = true,
                }
            );

            trayController.OnEntrySelect += OnTrayEntrySelect;
            trayController.OnEntryClose += OnTrayEntryClose;
            trayPanel.OnEntryReorder += OnTrayEntryReorder;

            trayLayer.Add(trayPanel);
            manualRuntime.HUD.WindowArea.Add(trayLayer);
            trayController.Reload();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Manual 테스트용 단색 tray icon을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Texture2D CreateTemporaryIcon(Color color, string mark)
        {
            const int size = 24;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "TEST_XeriWindow_TrayIcon",
                filterMode = FilterMode.Point,
            };

            var borderColor = Color.white;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var isBorder = x <= 1 || x >= size - 2 || y <= 1 || y >= size - 2;
                    texture.SetPixel(x, y, isBorder ? borderColor : color);
                }
            }

            if (!string.IsNullOrEmpty(mark))
            {
                DrawIconMark(texture, mark[0], borderColor);
            }

            texture.Apply();

            return texture;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 작은 아이콘 중앙에 M/W 식별 표시를 그린다.
        /// </summary>
        // ------------------------------------------------------------
        private static void DrawIconMark(Texture2D texture, char mark, Color color)
        {
            if (mark == 'M')
            {
                for (var y = 7; y <= 16; y++)
                {
                    texture.SetPixel(7, y, color);
                    texture.SetPixel(16, y, color);
                }

                for (var i = 0; i <= 4; i++)
                {
                    texture.SetPixel(8 + i, 15 - i, color);
                    texture.SetPixel(15 - i, 15 - i, color);
                }

                return;
            }

            for (var y = 7; y <= 16; y++)
            {
                texture.SetPixel(7, y, color);
                texture.SetPixel(16, y, color);
            }

            for (var x = 8; x <= 15; x++)
            {
                texture.SetPixel(x, 8, color);
                texture.SetPixel(x, 15, color);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 handle의 controller를 가져온다.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowController GetController(XeriWindowHandle handle)
        {
            if (workspace.TryGetSession(handle, out var session))
            {
                return session.Controller;
            }

            return null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트용 theme class를 main window에 순서대로 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyWindowTheme(XeriWindowPanel panel, string themeClass)
        {
            if (panel == null) return;

            panel.ApplyTheme(themeClass);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 한 번만 close 요청을 취소한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnSecondWindowPreClose(object sender, XeriWindowCancelEventArgs e)
        {
            if (!cancelNextClose) return;

            e.Cancel = true;
            cancelNextClose = false;
        }

    #endregion

    #region L-1: Simple Window fixture lifetime

        // ------------------------------------------------------------
        /// <summary>
        /// Simple Window 수동 테스트 전용 자원을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void TearDown()
        {
            if (trayController != null)
            {
                trayController.OnEntrySelect -= OnTrayEntrySelect;
                trayController.OnEntryClose -= OnTrayEntryClose;
            }

            if (trayPanel != null)
            {
                trayPanel.OnEntryReorder -= OnTrayEntryReorder;
            }

            trayController?.Dispose();
            traySource?.Dispose();
            trayController = null;
            traySource = null;
            trayPanel = null;
            trayLayer = null;

            manualRuntime?.Dispose();
            manualRuntime = null;
            workspace = null;
            mainSession = null;
            secondSession = null;
            mainHandle = null;
            secondHandle = null;
            mainPanel = null;
            secondPanel = null;
            taskBoardApp = null;
            preferencesApp = null;

            if (mainIcon != null)
            {
                UnityEngine.Object.DestroyImmediate(mainIcon);
                mainIcon = null;
            }

            if (secondIcon != null)
            {
                UnityEngine.Object.DestroyImmediate(secondIcon);
                secondIcon = null;
            }
        }

    #endregion

    #region C-1: ContentRoot mini-app 입력

        [Explicit]
        [Category("Manual")]
        [UnityTest]
        public IEnumerator TEST_XeriSimpleWindowManualPlayMode_Content_수동확인()
        {
            CreateWindowSample(3);

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    manualRuntime.HUD.WindowArea.Query<XeriWindowPanel>().ToList().Count == 2 &&
                    mainPanel.ClassListContains(XeriWindowThemeClass.Windows) &&
                    secondPanel.ClassListContains(XeriWindowThemeClass.Mac),
                "두 Window와 각 mini-app이 같이 보이는지 확인하세요.",
                "Windows/Mac Window 2개와 Task Board/Preferences가 표시되어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () => taskBoardApp.Status.text != "2 tasks",
                "Main Window의 Task Board에서 내용을 입력하고 Add 버튼을 누르세요.",
                "Task Board 상태가 2 tasks에서 변경되어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () => preferencesApp.Status.text != "Saved",
                "Second Window의 Preferences 값을 바꾸고 Apply 버튼을 누르세요.",
                "Preferences 상태가 Saved에서 적용 결과로 변경되어야 합니다."
            );
        }

    #endregion

    #region G-1: Window geometry와 maximize

        [Explicit]
        [Category("Manual")]
        [UnityTest]
        public IEnumerator TEST_XeriSimpleWindowManualPlayMode_Geometry_수동확인()
        {
            CreateWindowSample(4);
            var mainController = GetController(mainHandle);
            var beginPos = mainController.Driver.Pos;
            var beginSize = mainController.Driver.Size;

            yield return manualRuntime.HUD.WaitForStep
            (
                () => mainController.Driver.Pos != beginPos,
                "Main Window의 titlebar를 드래그해 이동하세요.",
                "Window 위치가 최초 위치에서 변경되어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () => mainController.Driver.Size != beginSize,
                "Main Window의 경계나 모서리를 드래그해 크기를 바꾸세요.",
                "Window 크기가 최초 크기에서 변경되어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () => mainController.Driver.State == XeriWindowState.Maximized,
                "Main Window를 최대화하고 상단 테스트 HUD가 계속 보이는지 확인하세요.",
                "Window가 Maximized 상태이고 HUD는 Window 영역 밖에 유지되어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () => mainController.Driver.State == XeriWindowState.Normal,
                "Maximize 버튼을 다시 눌러 Window를 복원하세요.",
                "Window가 Normal 상태로 복원되어야 합니다."
            );
        }

    #endregion

    #region T-1: Minimize와 Tray

        [Explicit]
        [Category("Manual")]
        [UnityTest]
        public IEnumerator TEST_XeriSimpleWindowManualPlayMode_Tray_수동확인()
        {
            CreateWindowSample(4);
            var mainController = GetController(mainHandle);
            var secondController = GetController(secondHandle);

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    mainController.Driver.State == XeriWindowState.Minimized &&
                    trayPanel.Q<VisualElement>("entry-container").childCount > 0,
                "Main Window를 minimize해 Tray entry를 만드세요.",
                "Main Window가 Minimized이고 Tray entry가 표시되어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () => mainController.Driver.State == XeriWindowState.Normal,
                "Main Window의 Tray entry를 클릭해 복구하세요.",
                "Main Window가 Normal 상태로 복원되어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    mainController.Driver.State == XeriWindowState.Minimized &&
                    secondController.Driver.State == XeriWindowState.Minimized &&
                    traySource.GetEntries().Count == 2 &&
                    traySource.GetEntries()[0].ID == "second" &&
                    traySource.GetEntries()[1].ID == "main",
                "두 Window를 minimize한 뒤 Mac tray icon을 왼쪽으로 reorder하세요.",
                "Tray entry 순서가 second, main이어야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    mainController.Driver.State == XeriWindowState.Normal &&
                    secondController.Driver.State == XeriWindowState.Normal,
                "두 Tray entry를 클릭해 Window를 모두 복구하세요.",
                "두 Window가 모두 Normal 상태여야 합니다."
            );
        }

    #endregion

    #region F-1: Focus와 Close

        [Explicit]
        [Category("Manual")]
        [UnityTest]
        public IEnumerator TEST_XeriSimpleWindowManualPlayMode_FocusClose_수동확인()
        {
            CreateWindowSample(3);
            var firstController = GetController(mainHandle);
            var secondController = GetController(secondHandle);
            secondController.OnPreClose += OnSecondWindowPreClose;
            mainSession.Focus();

            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    ((IFocusScope)secondController.Driver).ContainsFocus
                    (
                        manualRuntime.FocusDriver.Current
                    ),
                "Second Window를 클릭해 앞으로 가져오세요.",
                "Second Window의 Focus Scope가 활성 상태여야 합니다."
            );

            cancelNextClose = true;
            yield return manualRuntime.HUD.WaitForStep
            (
                () =>
                    !cancelNextClose &&
                    secondController.Driver.State != XeriWindowState.Closed,
                "Second Window의 Close 버튼을 한 번 누르세요.",
                "OnPreClose가 요청을 취소하고 Window는 열린 상태여야 합니다."
            );

            yield return manualRuntime.HUD.WaitForStep
            (
                () => secondController.Driver.State == XeriWindowState.Closed,
                "Second Window의 Close 버튼을 다시 눌러 닫으세요.",
                "Second Window가 Closed 상태여야 합니다."
            );
        }

    #endregion

    #region V-1: Theme 시각 확인

        [Explicit]
        [Category("Manual")]
        [UnityTest]
        public IEnumerator TEST_XeriSimpleWindowManualPlayMode_Theme_수동확인()
        {
            CreateWindowSample(3);

            ApplyWindowTheme(mainPanel, XeriWindowThemeClass.Windows);
            yield return manualRuntime.HUD.WaitForStep
            (
                () => mainPanel.ClassListContains(XeriWindowThemeClass.Windows),
                "Main Window의 Windows theme를 시각적으로 확인하세요.",
                "Windows theme class가 적용되어야 합니다."
            );

            ApplyWindowTheme(mainPanel, XeriWindowThemeClass.Mac);
            yield return manualRuntime.HUD.WaitForStep
            (
                () => mainPanel.ClassListContains(XeriWindowThemeClass.Mac),
                "Main Window의 Mac theme를 시각적으로 확인하세요.",
                "Mac theme class가 적용되어야 합니다."
            );

            ApplyWindowTheme(mainPanel, XeriWindowThemeClass.Minimal);
            yield return manualRuntime.HUD.WaitForStep
            (
                () => mainPanel.ClassListContains(XeriWindowThemeClass.Minimal),
                "Main Window의 Minimal theme를 시각적으로 확인하세요.",
                "Minimal theme class가 적용되어야 합니다."
            );
        }

    #endregion

    #region Tray 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 선택을 window show normal 명령으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnTrayEntrySelect(object sender, XeriTrayEventArgs e)
        {
            traySource.Restore(e.Entry);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 닫기 입력을 window close 명령으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnTrayEntryClose(object sender, XeriTrayEventArgs e)
        {
            traySource.Close(e.Entry);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry reorder 요청을 source order 변경으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnTrayEntryReorder(object sender, XeriTrayReorderEventArgs e)
        {
            if (e?.Entry?.Payload is not XeriWindowHandle handle) return;

            traySource.MoveEntry(handle, e.TargetIndex);
        }

    #endregion

    }
}
