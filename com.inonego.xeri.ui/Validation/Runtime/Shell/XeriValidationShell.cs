/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriValidationShell.cs
수정일 : 2026-10-07

# 설명
통합 Validation Desktop의 Window Workspace, Taskbar, Monitor, Checklist와 launcher를 조립한다.
각 앱과 Lab은 독립 controller로 생성하고 Shell은 open/activate 및 top-level lifetime만 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// Desktop 조작과 Window 수명을 연결한다.
    /// </summary>
    // ============================================================
    internal sealed class XeriValidationShell : IDisposable
    {

    #region 필드와 상태

        private const float COMPACT_DESKTOP_WIDTH = 1180f;
        private const float NARROW_DESKTOP_WIDTH = 760f;
        private const float SHORT_DESKTOP_HEIGHT = 640f;

        private readonly UIRuntime runtime = null;
        private readonly XeriValidationAssets assets = null;
        private readonly XeriValidationDesktopAuthoring desktop = null;
        private readonly Dictionary<string, XeriWindowSession> slots = new();

        private XeriWindowWorkspace workspace = null;
        private XeriWindowRegistry registry = null;
        private XeriValidationAppearance appearance = null;
        private XeriValidationChecklist checklist = null;
        private XeriValidationTaskbarBinding taskbar = null;
        private XeriValidationSystemMonitor monitor = null;
        private VisualElement desktopRoot = null;

        private Button coreLabButton = null;
        private Button windowLabButton = null;
        private Button modalLabButton = null;
        private Button taskBoardButton = null;
        private Button preferencesButton = null;
        private Button applicationAButton = null;
        private Button applicationBButton = null;
        private Button monitorToggle = null;
        private Button checklistToggle = null;

        private bool monitorExpanded = true;
        private bool checklistExpanded = false;
        private bool isInitialized = false;
        private bool isDisposed = false;

    #endregion

    #region 조립과 Desktop 연결

        // ------------------------------------------------------------
        /// <summary>
        /// Desktop 조작과 Window 수명을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriValidationShell
        (
            UIRuntime runtime,
            XeriValidationAssets assets,
            XeriValidationDesktopAuthoring desktop
        ) : base()
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window와 Taskbar 연결을 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            if (!runtime.IsInitialized)
            {
                throw new InvalidOperationException("Validation Shell보다 UIRuntime이 먼저 초기화되어야 합니다.");
            }

            desktopRoot = desktop.Output?.Root ??
                throw new InvalidOperationException("Validation Desktop UITK Root가 없습니다.");

            try
            {
                registry = new XeriWindowRegistry();
                appearance = new XeriValidationAppearance(assets);
                appearance.Bind(desktopRoot);
                workspace = new XeriWindowWorkspace
                (
                    runtime.Main,
                    PresentationTarget.Host(XeriValidationIDs.WindowDestination),
                    registry: registry,
                    animationOptions: XeriWindowAnimationOptions.Default()
                );
                checklist = new XeriValidationChecklist(runtime.Main, assets, appearance);
                taskbar = new XeriValidationTaskbarBinding
                (
                    runtime.Main,
                    workspace,
                    registry,
                    checklist
                );
                monitor = new XeriValidationSystemMonitor
                (
                    runtime.Main,
                    runtime,
                    registry,
                    checklist
                );
    
                BindDesktop();
                checklist.SetExpanded(checklistExpanded);
                monitor.SetExpanded(monitorExpanded);
                checklist.Mark(XeriValidationChecklist.Item.Host, "Scene-authored Desktop");
                checklist.Mark(XeriValidationChecklist.Item.Taskbar, "All live states");
                isInitialized = true;
            }
            catch (Exception exception)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(exception, cleanup);
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Desktop 입력과 크기 변경을 구독한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BindDesktop()
        {
            coreLabButton = XeriValidationUI.Require<Button>(desktopRoot, "LaunchCoreLab");
            windowLabButton = XeriValidationUI.Require<Button>(desktopRoot, "LaunchWindowLab");
            modalLabButton = XeriValidationUI.Require<Button>(desktopRoot, "LaunchModalLab");
            taskBoardButton = XeriValidationUI.Require<Button>(desktopRoot, "LaunchTaskBoard");
            preferencesButton = XeriValidationUI.Require<Button>(desktopRoot, "LaunchPreferences");
            applicationAButton = XeriValidationUI.Require<Button>(desktopRoot, "LaunchApplicationA");
            applicationBButton = XeriValidationUI.Require<Button>(desktopRoot, "LaunchApplicationB");
            monitorToggle = XeriValidationUI.Require<Button>(desktopRoot, "ToggleMonitor");
            checklistToggle = XeriValidationUI.Require<Button>(desktopRoot, "ToggleChecklist");

            desktopRoot.RegisterCallback<GeometryChangedEvent>(OnDesktopGeometryChanged);
            ApplyDesktopLayout(desktopRoot.contentRect.size);

            coreLabButton.clicked += OpenCoreLab;
            windowLabButton.clicked += OpenWindowLab;
            modalLabButton.clicked += OpenModalLab;
            taskBoardButton.clicked += OpenTaskBoard;
            preferencesButton.clicked += OpenPreferences;
            applicationAButton.clicked += OpenApplicationA;
            applicationBButton.clicked += OpenApplicationB;
            monitorToggle.clicked += ToggleMonitor;
            checklistToggle.clicked += ToggleChecklist;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Desktop 입력과 크기 변경 구독을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnbindDesktop()
        {
            desktopRoot?.UnregisterCallback<GeometryChangedEvent>(OnDesktopGeometryChanged);
            if (coreLabButton != null)
            {
                coreLabButton.clicked -= OpenCoreLab;
            }

            if (windowLabButton != null)
            {
                windowLabButton.clicked -= OpenWindowLab;
            }

            if (modalLabButton != null)
            {
                modalLabButton.clicked -= OpenModalLab;
            }

            if (taskBoardButton != null)
            {
                taskBoardButton.clicked -= OpenTaskBoard;
            }

            if (preferencesButton != null)
            {
                preferencesButton.clicked -= OpenPreferences;
            }

            if (applicationAButton != null)
            {
                applicationAButton.clicked -= OpenApplicationA;
            }

            if (applicationBButton != null)
            {
                applicationBButton.clicked -= OpenApplicationB;
            }

            if (monitorToggle != null)
            {
                monitorToggle.clicked -= ToggleMonitor;
            }

            if (checklistToggle != null)
            {
                checklistToggle.clicked -= ToggleChecklist;
            }
        }

    #endregion

    #region Desktop 배치

        // ------------------------------------------------------------
        /// <summary>
        /// 화면 크기 변경을 Desktop 배치에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDesktopGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyDesktopLayout(evt.newRect.size);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Desktop viewport 크기에 맞는 responsive shell class를 갱신한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplyDesktopLayout(Vector2 size)
        {
            if (desktopRoot == null)
            {
                return;
            }

            desktopRoot.EnableInClassList
            (
                "xeri-validation-desktop--compact",
                size.x > 0f && size.x < COMPACT_DESKTOP_WIDTH
            );
            desktopRoot.EnableInClassList
            (
                "xeri-validation-desktop--narrow",
                size.x > 0f && size.x < NARROW_DESKTOP_WIDTH
            );
            desktopRoot.EnableInClassList
            (
                "xeri-validation-desktop--short",
                size.y > 0f && size.y < SHORT_DESKTOP_HEIGHT
            );
        }

    #endregion

    #region Lab과 Application 열기

        // ------------------------------------------------------------
        /// <summary>
        /// Core Lab을 열거나 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenCoreLab()
        {
            ActivateOrCreate
            (
                XeriValidationIDs.CoreLabWindow,
                () =>
                {
                    var content = new XeriValidationCoreLab(runtime, assets, appearance, checklist);
                    return OpenDirectWindow
                    (
                        XeriValidationIDs.CoreLabWindow,
                        "Core Lab",
                        content.Root,
                        content,
                        new Vector2(210f, 140f),
                        new Vector2(640f, 500f),
                        XeriWindowThemeClass.MinimalID
                    );
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window Lab을 열거나 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenWindowLab()
        {
            ActivateOrCreate
            (
                XeriValidationIDs.WindowLabWindow,
                () =>
                {
                    var content = new XeriValidationWindowLab
                    (
                        workspace,
                        registry,
                        assets,
                        appearance,
                        checklist
                    );
                    return OpenDirectWindow
                    (
                        XeriValidationIDs.WindowLabWindow,
                        "Window Lab",
                        content.Root,
                        content,
                        new Vector2(260f, 170f),
                        new Vector2(680f, 500f),
                        XeriWindowThemeClass.WindowsID
                    );
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Modal Lab을 열거나 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenModalLab()
        {
            ActivateOrCreate
            (
                XeriValidationIDs.ModalLabWindow,
                () =>
                {
                    var content = new XeriValidationModalLab
                    (
                        runtime.Main,
                        assets,
                        appearance,
                        checklist
                    );
                    return OpenDirectWindow
                    (
                        XeriValidationIDs.ModalLabWindow,
                        "Modal Lab",
                        content.Root,
                        content,
                        new Vector2(340f, 210f),
                        new Vector2(560f, 390f),
                        XeriWindowThemeClass.MacID
                    );
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Task Board를 열거나 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenTaskBoard()
        {
            ActivateOrCreate
            (
                XeriValidationIDs.TaskBoardWindow,
                () =>
                {
                    var content = new XeriValidationTaskBoard(assets, checklist);
                    return OpenDirectWindow
                    (
                        XeriValidationIDs.TaskBoardWindow,
                        "Task Board",
                        content.Root,
                        content,
                        new Vector2(180f, 130f),
                        new Vector2(600f, 450f),
                        XeriWindowThemeClass.WindowsID
                    );
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Preferences를 열거나 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenPreferences()
        {
            ActivateOrCreate
            (
                XeriValidationIDs.PreferencesWindow,
                () =>
                {
                    var content = new XeriValidationPreferences(assets, appearance);
                    return OpenDirectWindow
                    (
                        XeriValidationIDs.PreferencesWindow,
                        "Preferences",
                        content.Root,
                        content,
                        new Vector2(480f, 190f),
                        new Vector2(520f, 430f),
                        XeriWindowThemeClass.WindowsID
                    );
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Application A를 열거나 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenApplicationA()
        {
            ActivateOrCreate
            (
                XeriValidationIDs.ApplicationAWindow,
                () => OpenApplication
                (
                    XeriValidationIDs.ApplicationAWindow,
                    "Application A",
                    new Vector2(210f, 145f)
                )
            );
            RefreshApplicationIsolation();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Application B를 열거나 활성화한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OpenApplicationB()
        {
            ActivateOrCreate
            (
                XeriValidationIDs.ApplicationBWindow,
                () => OpenApplication
                (
                    XeriValidationIDs.ApplicationBWindow,
                    "Application B",
                    new Vector2(520f, 245f)
                )
            );
            RefreshApplicationIsolation();
        }

    #endregion

    #region Window 생성과 소유권

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 Context를 가진 Application Window를 연다.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowSession OpenApplication
        (
            string id,
            string title,
            Vector2 pos
        )
        {
            var options = CreateValidationWindowOptions();
            var session = workspace.OpenApplicationWindow
            (
                id,
                title,
                pos,
                new Vector2(580f, 420f),
                new XeriWindowApplicationOptions(assets.ApplicationLayout),
                options
            );

            XeriValidationApplication content = null;

            try
            {
                content = new XeriValidationApplication(session, assets, appearance, title);
                session.RegisterChild(content);
                content = null;
                return CreateSlot(id, session);
            }
            catch (Exception exception)
            {
                throw ReleaseFailedWindow(exception, session, content);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Desktop Context에 연결된 Window를 연다.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowSession OpenDirectWindow
        (
            string id,
            string title,
            VisualElement view,
            IDisposable content,
            Vector2 pos,
            Vector2 size,
            string themeID
        )
        {
            var record = new XeriWindowRecord
            {
                ID = id,
                Title = title,
                Tooltip = $"Xeri UI Validation · {title}",
                Pos = pos,
                Size = size,
                NormalPos = pos,
                NormalSize = size,
                ThemeID = themeID,
            };

            XeriWindowSession session = null;
            try
            {
                session = workspace.OpenWindow
                (
                    record,
                    view,
                    CreateValidationWindowOptions()
                );
                if (content != null)
                {
                    session.RegisterChild(content);
                    content = null;
                }

                return CreateSlot(id, session);
            }
            catch (Exception exception)
            {
                throw ReleaseFailedWindow(exception, session, content);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 생성 실패 시 남은 소유권을 모두 반환하고 원인 예외를 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        private static AggregateException ReleaseFailedWindow
        (
            Exception cause,
            XeriWindowSession session,
            IDisposable content
        )
        {
            var errors = new List<Exception>
            {
                cause,
            };
            var resources = new IDisposable[]
            {
                content, session,
            };

            foreach (var owned in resources)
            {
                try
                {
                    owned?.Dispose();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            return new AggregateException("Validation Window 생성이 실패했습니다.", errors);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Validation content가 최소한의 header/body 구조를 유지하도록 공통
        /// <br/> resize 하한을 적용한 Window 옵션을 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static XeriWindowOptions CreateValidationWindowOptions()
        {
            var options = XeriWindowOptions.Default();
            options.MinSize = new Vector2(360f, 280f);
            return options;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window와 콘텐츠 종료 경로를 함께 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        private XeriWindowSession CreateSlot
        (
            string id,
            XeriWindowSession session
        )
        {
            // 창 수명에 외관 바인딩과 추적 해제를 연결한다.
            session.RegisterChild(new Lease(() => slots.Remove(id)));
            session.RegisterChild(appearance.Bind(session.Panel));
            checklist?.Mark(XeriValidationChecklist.Item.WindowOpen, session.Handle.ID);
            return session;
        }

    #endregion

    #region 활성화와 Context 확인

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 Window를 활성화하거나 새로 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ActivateOrCreate
        (
            string id,
            Func<XeriWindowSession> create
        )
        {
            if (workspace != null && workspace.Count > 0)
            {
                checklist?.Mark
                (
                    XeriValidationChecklist.Item.DesktopInput,
                    "Launcher behind window"
                );
            }

            if
            (
                slots.TryGetValue(id, out var current) &&
                !current.IsDisposed
            )
            {
                if (current.Controller.EffectiveState == XeriWindowState.Minimized)
                {
                    current.Controller.Restore();
                }

                current.Focus();
                return;
            }

            if (current != null)
            {
                current.Dispose();
                slots.Remove(id);
            }

            var created = create();
            slots.Add(id, created);
            created.Focus();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Application 간 독립 상태를 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RefreshApplicationIsolation()
        {
            if
            (
                slots.TryGetValue(XeriValidationIDs.ApplicationAWindow, out var first) &&
                slots.TryGetValue(XeriValidationIDs.ApplicationBWindow, out var second) &&
                first.Context != null &&
                second.Context != null &&
                !ReferenceEquals(first.Context, second.Context)
            )
            {
                checklist?.Mark
                (
                    XeriValidationChecklist.Item.ApplicationIsolation,
                    "A/B child contexts"
                );
            }
        }

    #endregion

    #region 진행 표시

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 모니터 표시를 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ToggleMonitor()
        {
            monitorExpanded = !monitorExpanded;
            monitor.SetExpanded(monitorExpanded);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 검증 체크리스트 표시를 전환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ToggleChecklist()
        {
            checklistExpanded = !checklistExpanded;
            checklist.SetExpanded(checklistExpanded);
        }

    #endregion

    #region 전체 정리

        // ------------------------------------------------------------
        /// <summary>
        /// 구독과 소유 수명을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            var errors = new List<Exception>();

            // 입력 구독부터 끊고 Window의 콘텐츠를 Context와 함께 반환한다.
            try
            {
                UnbindDesktop();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            // 종료 중인 Runtime을 읽지 않도록 상태 관찰부터 끊는다.
            var resources = new IDisposable[]
            {
                monitor, workspace, taskbar, checklist, appearance,
            };

            foreach (var owned in resources)
            {
                try
                {
                    owned?.Dispose();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            slots.Clear();
            workspace = null;
            monitor = null;
            taskbar = null;
            checklist = null;
            appearance = null;
            desktopRoot = null;
            if (errors.Count > 0)
            {
                throw new AggregateException("Validation Shell 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
