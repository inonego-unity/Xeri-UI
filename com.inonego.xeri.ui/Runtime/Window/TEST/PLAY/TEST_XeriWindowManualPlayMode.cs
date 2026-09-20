/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowManualPlayMode.cs
수정일 : 2026-09-20

# 설명
Xeri Window 시스템을 PlayMode 화면에서 직접 조작해 확인하는 수동 테스트.
각 단계를 정상 확인한 뒤 Space 키로 다음 단계로 진행한다.

# 테스트 구성
 M: 수동 확인

# 특이사항
[Explicit] 과 [Category("Manual")] 로 일반 테스트 실행에서 멈추지 않게 분리한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego.Xeri.UI.Tray;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// Xeri Window 수동 PlayMode 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriWindowManualPlayMode
    {

    #region 필드

        private const string MANUAL_TEST_USS_PATH = "XeriUI/TEST/Window/TEST_XeriWindowManualPlayMode";
        private const string MAIN_NORMAL_TITLE = "Main Window";
        private const string SECOND_NORMAL_TITLE = "Second Window";
        private const string MAIN_LONG_TITLE = "Main Window - Very Long Windows Style Title For Layout Check";
        private const string SECOND_LONG_TITLE = "Second Window - Very Long Mac Style Title For Layout Check";

        private GameObject cameraGO = null;
        private GameObject documentGO = null;
        private PanelSettings panelSettings = null;
        private XeriWindowCanvas canvas = null;
        private Label guideLabel = null;
        private XeriWindowPanel mainPanel = null;
        private XeriWindowPanel secondPanel = null;
        private XeriWindowHandle mainHandle = null;
        private XeriWindowHandle secondHandle = null;
        private XeriWindowTraySource traySource = null;
        private XeriTrayController trayController = null;
        private XeriTrayPanel trayPanel = null;
        private VisualElement trayLayer = null;
        private VisualElement optionBar = null;
        private VisualElement rootElement = null;
        private Button longTitleButton = null;
        private Button titleIconButton = null;
        private Texture2D mainIcon = null;
        private Texture2D secondIcon = null;
        private bool cancelNextClose = false;
        private bool useLongTitle = false;
        private bool showTitleIcon = false;

    #endregion

    #region 헬퍼

        // ------------------------------------------------------------
        /// <summary>
        /// Space키 입력을 체크한다.
        /// </summary>
        // ------------------------------------------------------------
        private bool IsSpaceKeyPressed()
        {
        #if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        #else
            return Input.GetKeyDown(KeyCode.Space);
        #endif
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Game View 경고가 나오지 않도록 테스트용 카메라를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CreateTestCamera()
        {
            cameraGO = new GameObject("TEST_XeriWindow_Camera");

            var testCamera = cameraGO.AddComponent<Camera>();
            testCamera.clearFlags      = CameraClearFlags.SolidColor;
            testCamera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            testCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// UITK 기반 XeriWindowCanvas 샘플을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CreateWindowSample()
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "TEST_XeriWindow_PanelSettings";
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            XeriWindowManualTestPanelSettings.ApplyDefaultRuntimeTheme(panelSettings);

            documentGO = new GameObject("TEST_XeriWindow_Document");
            var document = documentGO.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            rootElement = document.rootVisualElement;
            LoadManualTestStyleSheet(rootElement);
            rootElement.style.flexGrow = 1f;
            rootElement.style.backgroundColor = new Color(0.06f, 0.06f, 0.07f, 1f);

            guideLabel = CreateGuideLabel();
            rootElement.Add(guideLabel);

            canvas = new XeriWindowCanvas
            (
                null,
                null,
                CreateManualAnimationOptions()
            );
            rootElement.Add(canvas);

            CreateManualTray();
            ConfigureManualTrayLayout();

            mainHandle = canvas.AddWindow
            (
                "main",
                MAIN_NORMAL_TITLE,
                CreateContent("Main content"),
                new Vector2(180f, 150f),
                new Vector2(340f, 220f)
            );
            mainPanel = canvas.WindowLayer[0] as XeriWindowPanel;
            ApplyWindowTheme(mainPanel, XeriWindowThemeClass.Windows);
            ConfigureTrayRecord
            (
                mainHandle,
                "Windows style window",
                new Color(0.20f, 0.44f, 0.96f, 1f),
                "W"
            );

            secondHandle = canvas.AddWindow
            (
                "second",
                SECOND_NORMAL_TITLE,
                CreateContent("Second content"),
                new Vector2(440f, 260f),
                new Vector2(300f, 180f)
            );
            secondPanel = canvas.WindowLayer[1] as XeriWindowPanel;
            ApplyWindowTheme(secondPanel, XeriWindowThemeClass.Mac);
            ConfigureTrayRecord
            (
                secondHandle,
                "Mac style window",
                new Color(0.30f, 0.80f, 0.38f, 1f),
                "M"
            );

            CreateOptionBar();
            ApplyTitleOptions();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 화면 상단 안내 Label을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Label CreateGuideLabel()
        {
            var label = new Label();
            label.style.position = Position.Absolute;
            label.style.left = 0f;
            label.style.right = 0f;
            label.style.top = 32f;
            label.style.height = 32f;
            label.style.color = Color.white;
            label.style.fontSize = 18f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;

            return label;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 긴 제목과 title icon 표시를 즉시 바꿔 볼 수 있는 수동 확인 버튼을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private void CreateOptionBar()
        {
            optionBar = new VisualElement { name = "TEST_XeriWindow_OptionBar" };
            optionBar.style.position = Position.Absolute;
            optionBar.style.top = 72f;
            optionBar.style.left = 0f;
            optionBar.style.right = 0f;
            optionBar.style.height = 32f;
            optionBar.style.flexDirection = FlexDirection.Row;
            optionBar.style.alignItems = Align.Center;
            optionBar.style.justifyContent = Justify.Center;
            optionBar.pickingMode = PickingMode.Position;

            longTitleButton = CreateOptionButton();
            titleIconButton = CreateOptionButton();

            longTitleButton.clicked += ToggleLongTitle;
            titleIconButton.clicked += ToggleTitleIcon;

            optionBar.Add(longTitleButton);
            optionBar.Add(titleIconButton);
            rootElement.Add(optionBar);
            optionBar.BringToFront();

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
        /// 현재 option 상태를 window record와 panel titlebar에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyTitleOptions()
        {
            ApplyWindowTitleOptions
            (
                mainHandle,
                mainPanel,
                MAIN_NORMAL_TITLE,
                MAIN_LONG_TITLE
            );
            ApplyWindowTitleOptions
            (
                secondHandle,
                secondPanel,
                SECOND_NORMAL_TITLE,
                SECOND_LONG_TITLE
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
            XeriWindowHandle handle,
            XeriWindowPanel panel,
            string normalTitle,
            string longTitle
        )
        {
            if (handle == null || panel == null) return;
            if (!canvas.Registry.TryGetRecord(handle, out var record)) return;

            record.Title = useLongTitle ? longTitle : normalTitle;

            panel.ApplyTitle(record.Title);
            panel.ApplyTitleIcon(showTitleIcon ? record.Icon : null);
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
        /// Manual 테스트에서 눈으로 확인 가능한 window animation 옵션을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriWindowAnimationOptions CreateManualAnimationOptions()
        {
            return new XeriWindowAnimationOptions
            {
                Enabled = true,
                Duration = 0.18f,
                HiddenOpacity = 0f,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 window 내부 content를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static VisualElement CreateContent(string text)
        {
            var root = new VisualElement();
            root.style.flexGrow = 1f;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var label = new Label(text);
            label.style.color = Color.white;
            label.style.fontSize = 16f;

            root.Add(label);

            return root;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Manual test USS를 Resources에서 로드해 target에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void LoadManualTestStyleSheet(VisualElement target)
        {
            var styleSheet = Resources.Load<StyleSheet>(MANUAL_TEST_USS_PATH);

            if (styleSheet == null)
            {
                throw new InvalidOperationException($"Manual test USS load failed. Path: {MANUAL_TEST_USS_PATH}");
            }

            target.styleSheets.Add(styleSheet);
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

        // ------------------------------------------------------------
        /// <summary>
        /// Manual 테스트용 Tray source, controller, panel을 생성하고 canvas root에 붙인다.
        /// </summary>
        // ------------------------------------------------------------
        private void CreateManualTray()
        {
            trayLayer = new VisualElement { name = "TEST_XeriWindow_TrayLayer" };
            trayPanel = new XeriTrayPanel();
            traySource = new XeriWindowTraySource(canvas.Registry);
            trayController = new XeriTrayController
            (
                traySource, trayPanel,
                new XeriTrayOptions
                {
                    VisibleContent = XeriTrayContent.Icon,
                    UssClass = "xeri-tray--manual-test",
                    Reorderable = true,
                    ReorderAxis = XeriTrayReorderAxis.Horizontal,
                    ReorderMode = XeriTrayReorderMode.AxisLocked,
                    AnimateReorder = true,
                }
            );

            trayPanel.OnEntrySelect += OnTrayEntrySelect;
            trayPanel.OnEntryClose += OnTrayEntryClose;
            trayPanel.OnEntryReorder += OnTrayEntryReorder;

            trayLayer.Add(trayPanel);
            canvas.Root.Add(trayLayer);
            trayController.Reload();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Manual 테스트에서 tray entry를 식별할 수 있도록 임시 표시 정보를 지정한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ConfigureTrayRecord
        (
            XeriWindowHandle handle,
            string tooltip,
            Color iconColor,
            string badgeText
        )
        {
            if (!canvas.Registry.TryGetRecord(handle, out var record)) return;

            var icon = CreateTemporaryIcon(iconColor, badgeText);

            record.Tooltip = tooltip;
            record.Icon = icon;
            record.Badge = new XeriTrayBadge(badgeText, iconColor);

            if (handle == mainHandle)
            {
                mainIcon = icon;
                return;
            }

            if (handle == secondHandle)
            {
                secondIcon = icon;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Manual 테스트용 단색 tray icon을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Texture2D CreateTemporaryIcon(Color color, string mark)
        {
            const int SIZE = 24;

            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false)
            {
                name = "TEST_XeriWindow_TrayIcon",
                filterMode = FilterMode.Point,
            };

            var borderColor = Color.white;

            for (var y = 0; y < SIZE; y++)
            {
                for (var x = 0; x < SIZE; x++)
                {
                    var isBorder = x <= 1 || x >= SIZE - 2 || y <= 1 || y >= SIZE - 2;
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
        /// Space 입력을 기다린 뒤 성공 여부를 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private IEnumerator WaitForSpaceAndAssert(Func<bool> isSuccess, string guide, string message)
        {
            guideLabel.text = guide;

            while (!IsSpaceKeyPressed())
            {
                yield return null;
            }

            Assert.IsTrue(isSuccess(), message);
            yield return null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 handle의 controller를 가져온다.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowController GetController(XeriWindowHandle handle)
        {
            canvas.Registry.TryGetController(handle, out var controller);

            return controller;
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

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 생성된 테스트 오브젝트를 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void TearDown()
        {
            if (cameraGO != null) UnityEngine.Object.DestroyImmediate(cameraGO);
            if (documentGO != null) UnityEngine.Object.DestroyImmediate(documentGO);
            if (panelSettings != null) UnityEngine.Object.DestroyImmediate(panelSettings);
            if (mainIcon != null) UnityEngine.Object.DestroyImmediate(mainIcon);
            if (secondIcon != null) UnityEngine.Object.DestroyImmediate(secondIcon);

            traySource?.Dispose();
        }

    #endregion

    #region M-1: Window 수동 확인

        // ----------------------------------------------------------------------
        /// <summary>
        /// Xeri Window 기본 상호작용을 PlayMode 화면에서 순서대로 직접 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Explicit]
        [Category("Manual")]
        [UnityTest]
        public IEnumerator TEST_XeriWindowManualPlayMode_Window_수동확인()
        {
            CreateTestCamera();
            CreateWindowSample();

            var mainController = GetController(mainHandle);
            var secondController = GetController(secondHandle);
            var beginPos = mainController.Driver.Pos;
            var beginSize = mainController.Driver.Size;
            secondController.OnPreClose += OnSecondWindowPreClose;

            yield return WaitForSpaceAndAssert
            (
                () => canvas.WindowLayer.childCount == 2 &&
                      mainPanel.ClassListContains(XeriWindowThemeClass.Windows) &&
                      secondPanel.ClassListContains(XeriWindowThemeClass.Mac),
                "Windows style window와 Mac style window가 같이 보이면 Space 키를 누르세요.",
                "Windows/Mac window panel이 같이 생성되어야 합니다."
            );

            yield return WaitForSpaceAndAssert
            (
                () => mainController.Driver.Pos != beginPos,
                "Main Window의 titlebar를 드래그해 이동한 뒤 Space 키를 누르세요.",
                "Titlebar drag 후 window 위치가 변경되어야 합니다."
            );

            yield return WaitForSpaceAndAssert
            (
                () => mainController.Driver.Size != beginSize,
                "Main Window의 경계나 모서리를 드래그해 크기를 바꾼 뒤 Space 키를 누르세요.",
                "Window 경계 drag 후 window 크기가 변경되어야 합니다."
            );

            yield return WaitForSpaceAndAssert
            (
                () => mainController.Driver.State == XeriWindowState.Maximized,
                "Main Window의 maximize button을 눌러 최대화 animation이 끝난 뒤 Space 키를 누르세요.",
                "Maximize button 입력 후 window가 Maximized 상태여야 합니다."
            );

            yield return WaitForSpaceAndAssert
            (
                () => mainController.Driver.State == XeriWindowState.Normal,
                "Main Window의 maximize button을 다시 눌러 restore animation이 끝난 뒤 Space 키를 누르세요.",
                "Maximize button 재입력 후 window가 Normal 상태여야 합니다."
            );

            yield return WaitForSpaceAndAssert
            (
                () => mainController.Driver.State == XeriWindowState.Minimized &&
                      trayPanel.Q<VisualElement>("entry-container").childCount > 0,
                "Main Window의 minimize button을 눌러 minimize animation 후 Tray entry가 나타나면 Space 키를 누르세요.",
                "Minimize 후 Tray entry가 표시되어야 합니다."
            );

            yield return WaitForSpaceAndAssert
            (
                () => mainController.Driver.State == XeriWindowState.Normal,
                "Tray entry를 클릭해 Main Window를 복구한 뒤 Space 키를 누르세요.",
                "Tray entry 클릭 후 window가 Normal 상태여야 합니다."
            );

            yield return WaitForSpaceAndAssert
            (
                () => mainController.Driver.State == XeriWindowState.Minimized &&
                      secondController.Driver.State == XeriWindowState.Minimized &&
                      traySource.GetEntries().Count == 2 &&
                      traySource.GetEntries()[0].ID == "second" &&
                      traySource.GetEntries()[1].ID == "main",
                "두 Window를 모두 minimize한 뒤 Mac tray icon을 Windows tray icon 왼쪽으로 드래그하고 Space 키를 누르세요.",
                "Tray reorder 후 Tray entry 순서가 second, main이어야 합니다."
            );

            yield return WaitForSpaceAndAssert
            (
                () => mainController.Driver.State == XeriWindowState.Normal &&
                      secondController.Driver.State == XeriWindowState.Normal,
                "두 Tray entry를 클릭해 두 Window를 모두 복구한 뒤 Space 키를 누르세요.",
                "Tray entry 클릭 후 두 Window가 Normal 상태여야 합니다."
            );

            yield return WaitForSpaceAndAssert
            (
                () => canvas.Registry.ActiveHandle == secondHandle,
                "Second Window를 클릭해 앞으로 가져온 뒤 Space 키를 누르세요.",
                "Second Window 클릭 후 active handle이 변경되어야 합니다."
            );

            cancelNextClose = true;
            yield return WaitForSpaceAndAssert
            (
                () => secondController.Driver.State != XeriWindowState.Closed,
                "Second Window의 close button을 한 번 눌러도 취소되어 남아 있으면 Space 키를 누르세요.",
                "OnPreClose 취소 후 Second Window가 닫히지 않아야 합니다."
            );

            yield return WaitForSpaceAndAssert
            (
                () => secondController.Driver.State == XeriWindowState.Closed,
                "Second Window의 close button을 눌러 닫은 뒤 Space 키를 누르세요.",
                "Close button 입력 후 Second Window가 Closed 상태여야 합니다."
            );

            ApplyWindowTheme(mainPanel, XeriWindowThemeClass.Windows);
            yield return WaitForSpaceAndAssert
            (
                () => mainPanel.ClassListContains(XeriWindowThemeClass.Windows),
                "Main Window에 Windows theme class가 적용된 것을 확인한 뒤 Space 키를 누르세요.",
                "Windows theme class가 적용되어야 합니다."
            );

            ApplyWindowTheme(mainPanel, XeriWindowThemeClass.Mac);
            yield return WaitForSpaceAndAssert
            (
                () => mainPanel.ClassListContains(XeriWindowThemeClass.Mac),
                "Main Window에 Mac theme class가 적용된 것을 확인한 뒤 Space 키를 누르세요.",
                "Mac theme class가 적용되어야 합니다."
            );

            ApplyWindowTheme(mainPanel, XeriWindowThemeClass.Minimal);
            yield return WaitForSpaceAndAssert
            (
                () => mainPanel.ClassListContains(XeriWindowThemeClass.Minimal),
                "Main Window에 Minimal theme class가 적용된 것을 확인한 뒤 Space 키를 누르세요.",
                "Minimal theme class가 적용되어야 합니다."
            );
        }

    #endregion

    #region Tray 이벤트 핸들러

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
