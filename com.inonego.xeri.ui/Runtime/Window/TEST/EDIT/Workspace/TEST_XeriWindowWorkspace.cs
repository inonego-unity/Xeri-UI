/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowWorkspace.cs
수정일 : 2026-11-09

# 설명
XeriWindowWorkspace의 기본 Window 수명, View Source, rollback과 Record 복원 계약을 검증한다.

# 테스트 구성
 W: Window 열기·닫기와 Workspace Screen 수명
 V: View Source와 UI Session 수명
 D: Open 실패 rollback
 R: Record 검증과 복원
 T: Workspace → Tray Source integration
 H: Theme ID 저장과 복원
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Tray;
using inonego.Xeri.UI.Window;

using static inonego.Xeri.UI.TEST.Window.XeriWindowWorkspaceTestSupport;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// XeriWindowWorkspace의 기본 수명과 복원 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_XeriWindowWorkspace
    {

    #region W-1: 창 열기와 닫기 수명

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 두 Window를 연 뒤 순서대로 닫으면 Session과 Workspace Screen이 생성 역순으로 정리된다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_OpenClose_Session과Screen수명_정리()
        {
            using var fixture = new WorkspaceFixture();

            var first = OpenWindow(fixture.Workspace, "first");
            var second = OpenWindow(fixture.Workspace, "second");

            Assert.AreEqual(2, fixture.Workspace.Count);
            Assert.IsFalse(first.IsApplication);
            Assert.IsFalse(second.IsApplication);
            Assert.IsNull(first.Context);
            Assert.IsNull(second.Context);
            Assert.IsFalse(first.IsDisposed);
            Assert.IsFalse(second.IsDisposed);
            Assert.IsTrue(first.Handle.IsValid);
            Assert.IsTrue(second.Handle.IsValid);
            Assert.AreEqual(1, fixture.WorkspacePlacementRoot.childCount);
            Assert.IsTrue(fixture.Workspace.TryGetSession(first.Handle, out var found));
            Assert.AreSame(first, found);

            first.Close();

            Assert.AreEqual(1, fixture.Workspace.Count);
            Assert.IsTrue(first.IsDisposed);
            Assert.IsFalse(first.Handle.IsValid);
            Assert.IsFalse(second.IsDisposed);
            Assert.AreEqual(1, fixture.WorkspacePlacementRoot.childCount);

            second.Close();

            Assert.AreEqual(0, fixture.Workspace.Count);
            Assert.IsTrue(second.IsDisposed);
            Assert.IsFalse(second.Handle.IsValid);
            Assert.AreEqual(0, fixture.WorkspacePlacementRoot.childCount);
        }

    #endregion

    #region T-1: 트레이 소스 통합

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 같은 Workspace에서 Tray Source마다 독립적인 Window 상태 projection을 사용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_CreateTraySource_상태별Projection_독립()
        {
            using var fixture = new WorkspaceFixture();
            using var taskbarSource = fixture.Workspace.CreateTraySource();
            using var minimizedSource = fixture.Workspace.CreateTraySource
            (
                XeriWindowTrayStateMask.Minimized
            );

            var window = OpenWindow(fixture.Workspace, "window");

            Assert.AreEqual(1, taskbarSource.GetEntries().Count);
            Assert.AreEqual(0, minimizedSource.GetEntries().Count);

            window.Controller.Maximize();

            Assert.AreEqual(1, taskbarSource.GetEntries().Count);
            Assert.AreEqual(0, minimizedSource.GetEntries().Count);

            window.Controller.Minimize();

            Assert.AreEqual(1, taskbarSource.GetEntries().Count);
            Assert.AreEqual(1, minimizedSource.GetEntries().Count);

            minimizedSource.Activate(minimizedSource.GetEntries()[0]);

            Assert.AreEqual(XeriWindowState.Maximized, window.Controller.State);
            Assert.AreEqual(1, taskbarSource.GetEntries().Count);
            Assert.AreEqual(0, minimizedSource.GetEntries().Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Workspace Source부터 concrete Controller/Panel까지
        /// <br/> 실제 production Tray 경로를 연결한다.
        /// <br/> Panel이 표시한 entry를 Activate하면 Minimized Window가
        /// <br/> Restore되고 Active entry가 다시 표시된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_TrayConcretePipeline_Reload와Activate연결()
        {
            using var fixture = new WorkspaceFixture();
            using var source = fixture.Workspace.CreateTraySource();
            var panel = new XeriTrayPanel();
            using var controller = new XeriTrayController(source, panel);
            controller.OnEntrySelect += (_, e) => source.Activate(e.Entry);
            var window = OpenWindow(fixture.Workspace, "window");

            controller.Reload();

            Assert.AreEqual(1, panel.GetEntryButtons().Count);
            Assert.AreEqual("window", panel.GetEntryButtons()[0].Entry.ID);

            window.Controller.Minimize();

            Assert.AreEqual(XeriWindowState.Minimized, window.Controller.State);
            Assert.AreEqual(1, panel.GetEntryButtons().Count);

            var entry = panel.GetEntryButtons()[0].Entry;
            source.Activate(entry);

            Assert.AreEqual(XeriWindowState.Normal, window.Controller.State);
            Assert.AreEqual(1, panel.GetEntryButtons().Count);
            Assert.IsTrue(panel.GetEntryButtons()[0].Entry.IsActive);
        }

    #endregion

    #region V-1: 뷰 소스와 UI 세션 수명

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Record Open부터 Capture와 Dispose까지 같은 UI Session이
        /// <br/> Load·Save·Release 수명을 통과한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_ViewSource_OpenCaptureDispose_대칭수명()
        {
            var resolver = new XeriUIViewResolver();
            var source = new TestViewSource("test.view");
            var uiSession = new TestSession();
            resolver.Register(source);

            using var fixture = new WorkspaceFixture(resolver);
            var window = fixture.Workspace.OpenWindow
            (
                new XeriWindowRecord
                {
                    ID = "source-window",
                    Title = "Source Window",
                    Pos = new Vector2(20f, 30f),
                    Size = new Vector2(320f, 220f),
                    NormalPos = new Vector2(20f, 30f),
                    NormalSize = new Vector2(320f, 220f),
                    ViewSourceID = source.ID,
                    ViewDataKey = "source-window.view",
                    UISession = uiSession,
                }
            );

            Assert.AreEqual(1, source.LoadCount);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(0, source.SaveCount);
            Assert.AreEqual(0, source.ReleaseCount);

            var records = fixture.Workspace.CaptureRecords();

            Assert.AreEqual(1, source.SaveCount);
            Assert.AreEqual(1, uiSession.Version);
            Assert.AreSame(uiSession, records[0].UISession);

            window.Dispose();

            Assert.IsTrue(window.IsDisposed);
            Assert.AreEqual(2, source.SaveCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreSame(uiSession, source.LastScope.UISession);

            window.Dispose();

            Assert.AreEqual(2, source.SaveCount);
            Assert.AreEqual(1, source.ReleaseCount);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Session 저장 기능이 없는 View Source도 열기·캡처·종료를 지원한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_ViewSource_세션저장없이_OpenCaptureDispose()
        {
            var resolver = new XeriUIViewResolver();
            var source = new TestViewOnlySource("plain.view");
            resolver.Register(source);

            using var fixture = new WorkspaceFixture(resolver);
            var window = fixture.Workspace.OpenWindow
            (
                new XeriWindowRecord
                {
                    ID = "plain-window",
                    Title = "Plain Window",
                    Pos = new Vector2(20f, 30f),
                    Size = new Vector2(320f, 220f),
                    NormalPos = new Vector2(20f, 30f),
                    NormalSize = new Vector2(320f, 220f),
                    ViewSourceID = source.ID,
                }
            );

            Assert.AreEqual(1, source.AcquireCount);

            var records = fixture.Workspace.CaptureRecords();

            Assert.AreEqual(1, records.Count);
            Assert.AreEqual(source.ID, records[0].ViewSourceID);

            window.Dispose();

            Assert.IsTrue(window.IsDisposed);
            Assert.AreEqual(1, source.ReleaseCount);
        }

    #endregion

    #region D-1: 열기 실패 롤백

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 등록 콜백에서 Window가 닫히면 열기 절차를 롤백하고 View를 반환한 뒤 같은 ID로 다시
        /// <br/> 열 수 있다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_등록중Close_미완료Window반환과재사용()
        {
            var resolver = new XeriUIViewResolver();
            var source = new TestViewSource("close.view");
            resolver.Register(source);
            var registry = new XeriWindowRegistry();
            using var fixture = new WorkspaceFixture(resolver, registry: registry);
            var closeOnRegister = true;
            XeriWindowHandle closedHandle = null;
            registry.OnRegister += (_, e) =>
            {
                if (!closeOnRegister) return;

                closedHandle = e.Handle;
                registry.TryGetController(e.Handle, out var controller);
                controller.Close();
            };
            var record = new XeriWindowRecord
            {
                ID = "close-on-register",
                Title = "Close on register",
                Pos = new Vector2(20f, 30f),
                Size = new Vector2(320f, 220f),
                NormalPos = new Vector2(20f, 30f),
                NormalSize = new Vector2(320f, 220f),
                ViewSourceID = source.ID,
            };

            Assert.Throws<InvalidOperationException>(() => fixture.Workspace.OpenWindow(record));

            Assert.IsNotNull(closedHandle);
            Assert.IsFalse(closedHandle.IsValid);
            Assert.AreEqual(0, registry.Records.Count);
            Assert.AreEqual(0, fixture.Workspace.Count);
            Assert.AreEqual(0, fixture.WorkspacePlacementRoot.childCount);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsNull(source.LastView.parent);

            closeOnRegister = false;
            var reopened = OpenWindow(fixture.Workspace, record.ID);
            Assert.IsFalse(reopened.IsDisposed);
            Assert.AreEqual(XeriWindowState.Normal, reopened.Controller.State);
            reopened.Close();
            Assert.AreEqual(0, registry.Records.Count);
            Assert.AreEqual(0, fixture.Workspace.Count);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// View 조립이 실패하면 획득한 View를 반환하고 빈 Workspace Screen도 남기지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_Open실패_View와Screen_Rollback()
        {
            var resolver = new XeriUIViewResolver();
            var source = new TestViewSource("invalid.view")
            {
                ReturnParentedView = true,
            };
            resolver.Register(source);

            using var fixture = new WorkspaceFixture(resolver);

            Assert.Throws<InvalidOperationException>
            (
                () => fixture.Workspace.OpenWindow
                (
                    new XeriWindowRecord
                    {
                        ID = "invalid-window",
                        Title = "Invalid Window",
                        Pos = new Vector2(20f, 30f),
                        Size = new Vector2(320f, 220f),
                        NormalPos = new Vector2(20f, 30f),
                        NormalSize = new Vector2(320f, 220f),
                        ViewSourceID = source.ID,
                    }
                )
            );

            Assert.AreEqual(0, fixture.Workspace.Count);
            Assert.AreEqual(1, source.LoadCount);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(0, source.SaveCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, fixture.WorkspacePlacementRoot.childCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> View 획득 callback에서 Workspace가 종료되면 뒤늦게 획득된 View를 반환하고
        /// <br/> 종료된 Workspace에 Window Session을 공개하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_View획득중Dispose_늦은View반환()
        {
            var resolver = new XeriUIViewResolver();
            var source = new TestViewSource("dispose.view");
            resolver.Register(source);

            using var fixture = new WorkspaceFixture(resolver);
            source.Acquiring = fixture.Workspace.Dispose;

            Assert.Throws<ObjectDisposedException>
            (
                () => fixture.Workspace.OpenWindow
                (
                    new XeriWindowRecord
                    {
                        ID = "dispose-view-window",
                        Title = "Dispose View Window",
                        Pos = new Vector2(20f, 30f),
                        Size = new Vector2(320f, 220f),
                        NormalPos = new Vector2(20f, 30f),
                        NormalSize = new Vector2(320f, 220f),
                        ViewSourceID = source.ID,
                    }
                )
            );

            Assert.IsTrue(fixture.Workspace.IsDisposed);
            Assert.AreEqual(0, fixture.Workspace.Count);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsNull(source.LastView.parent);
            Assert.AreEqual(0, fixture.WorkspacePlacementRoot.childCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Session constructor 내부 Drag factory callback에서
        /// <br/> Workspace가 종료되면 아직 추적되지 않은 provisional Session의
        /// <br/> Registry·View·binding을 직접 정리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_Session생성중Dispose_ProvisionalSession정리()
        {
            var dragFactory = new CallbackDragFactory();

            using var fixture = new WorkspaceFixture(dragFactory: dragFactory);
            var view = new VisualElement();
            dragFactory.Creating = (_, _) => fixture.Workspace.Dispose();

            Assert.Throws<ObjectDisposedException>
            (
                () => OpenWindow(fixture.Workspace, "provisional-window", view)
            );

            Assert.IsTrue(fixture.Workspace.IsDisposed);
            Assert.AreEqual(0, fixture.Workspace.Count);
            Assert.IsNull(view.parent);
            Assert.AreEqual(0, fixture.WorkspacePlacementRoot.childCount);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag Factory 실패 시 테마 구독을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_DragFactory_Theme변경후실패_구독해제()
        {
            var dragFactory = new CallbackDragFactory();
            XeriWindowPanel failedPanel = null;
            XeriWindowController failedController = null;
            dragFactory.Creating = (panel, controller) =>
            {
                failedPanel = panel;
                failedController = controller;
                panel.ApplyTheme(XeriWindowThemeClass.MacID);
                throw new InvalidOperationException("Drag Factory 실패");
            };

            var registry = new XeriWindowRegistry();
            using var fixture = new WorkspaceFixture
            (
                dragFactory: dragFactory,
                registry: registry
            );

            Assert.Throws<InvalidOperationException>
            (
                () => OpenWindow(fixture.Workspace, "drag-theme-failed")
            );

            Assert.IsNotNull(failedPanel);
            Assert.IsNotNull(failedController);
            Assert.AreEqual(XeriWindowThemeClass.MacID, failedController.ThemeID);
            Assert.AreEqual(0, fixture.Workspace.Count);
            Assert.AreEqual(0, registry.Records.Count);

            failedPanel.ApplyTheme(XeriWindowThemeClass.MinimalID);
            Assert.AreEqual(XeriWindowThemeClass.MacID, failedController.ThemeID);
        }

    #endregion

    #region R-1: 뷰 연결 검증

        // ------------------------------------------------------------
        /// <summary>
        /// 다른 hierarchy에 연결된 View는 Window 부착 대상으로 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_ValidateViewForAttach_Parent있음_거부()
        {
            var parent = new VisualElement();
            var view = new VisualElement();
            parent.Add(view);

            Assert.Throws<System.InvalidOperationException>
            (
                () => XeriWindowWorkspace.ValidateViewForAttach(view)
            );
            Assert.AreSame(parent, view.parent);
        }

    #endregion

    #region R-2: 레코드 옵션 복원

        // ----------------------------------------------------------------------
        /// <summary>
        /// 호출 옵션에서 StackLayer를 생략하면 Record의 AlwaysOnTop 설정을 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_RecordOption_생략_StackLayer_보존()
        {
            var record = new XeriWindowRecord
            {
                StackLayer = XeriWindowStackLayer.AlwaysOnTop,
            };

            var options = XeriWindowWorkspace.ResolveRecordOptions(record, null);

            Assert.AreEqual(XeriWindowStackLayer.AlwaysOnTop, options.StackLayer);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 호출 옵션에 StackLayer를 지정하면 Record의 값을 덮어쓴다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_RecordOption_명시_StackLayer_덮어씀()
        {
            var record = new XeriWindowRecord
            {
                StackLayer = XeriWindowStackLayer.AlwaysOnTop,
            };
            var options = XeriWindowOptions.Default();
            options.StackLayer = XeriWindowStackLayer.Normal;

            var resolved = XeriWindowWorkspace.ResolveRecordOptions(record, options);

            Assert.AreEqual(XeriWindowStackLayer.Normal, resolved.StackLayer);
        }

    #endregion

    #region R-3: 일반 경계 복원

        // ----------------------------------------------------------------------
        /// <summary>
        /// Maximized Record의 복원용 NormalBounds를 초기 Bounds로 선택한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_MaximizedRecord_NormalBounds_사용()
        {
            var record = new XeriWindowRecord
            {
                State = XeriWindowState.Maximized,
                Pos = new Vector2(10f, 20f),
                Size = new Vector2(800f, 600f),
                NormalPos = new Vector2(120f, 140f),
                NormalSize = new Vector2(320f, 240f),
            };

            var bounds = XeriWindowWorkspace.ResolveRecordNormalBounds(record);

            Assert.AreEqual(record.NormalPos, bounds.position);
            Assert.AreEqual(record.NormalSize, bounds.size);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Normal Record는 일반 Pos와 Size를 복원용 Bounds로 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_NormalRecord_PosSize_우선()
        {
            var record = new XeriWindowRecord
            {
                State = XeriWindowState.Normal,
                Pos = new Vector2(30f, 40f),
                Size = new Vector2(400f, 300f),
                NormalPos = new Vector2(1f, 2f),
                NormalSize = new Vector2(10f, 20f),
            };

            var bounds = XeriWindowWorkspace.ResolveRecordNormalBounds(record);

            Assert.AreEqual(record.Pos, bounds.position);
            Assert.AreEqual(record.Size, bounds.size);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Record bounds에 NaN 또는 Infinity가 있으면 복원을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_RecordBounds_NonFinite_거부()
        {
            var record = new XeriWindowRecord
            {
                State = XeriWindowState.Normal,
                Pos = new Vector2(float.NaN, 20f),
                Size = new Vector2(300f, 200f),
            };

            Assert.Throws<System.ArgumentOutOfRangeException>
            (
                () => XeriWindowWorkspace.ResolveRecordNormalBounds(record)
            );
        }

    #endregion

    #region R-4: 기록된 창 상태 적용

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Record의 Maximized 상태를 Driver와 Visibility에 적용하고 최대화 Bounds 처리를
        /// <br/> 호출한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_ApplyRecordState_Maximized_표시와Bounds_적용()
        {
            var driver = new TestWindowDriver();

            XeriWindowWorkspace.ApplyRecordState(driver, XeriWindowState.Maximized);

            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
            Assert.IsTrue(driver.Visibility.Modified);
            Assert.IsTrue(driver.MaximizedApplied);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Record의 Minimized 상태를 Driver에 적용하고 Visibility를 숨김으로 설정한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_ApplyRecordState_Minimized_숨김()
        {
            var driver = new TestWindowDriver();

            XeriWindowWorkspace.ApplyRecordState(driver, XeriWindowState.Minimized);

            Assert.AreEqual(XeriWindowState.Minimized, driver.State);
            Assert.IsFalse(driver.Visibility.Modified);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 복원 가능한 Record 상태가 아닌 Closed 상태의 적용을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_ApplyRecordState_Closed_거부()
        {
            var driver = new TestWindowDriver();

            Assert.Throws<System.InvalidOperationException>
            (
                () => XeriWindowWorkspace.ApplyRecordState
                (
                    driver,
                    XeriWindowState.Closed
                )
            );
        }

    #endregion

    #region H-1: 테마 ID 보존

        // ------------------------------------------------------------
        /// <summary>
        /// Window 테마 변경은 Registry와 Capture·복원 결과에 반영된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_CustomTheme_CaptureRestore_ID보존()
        {
            var theme = ScriptableObject.CreateInstance<XeriWindowThemeAsset>();

            try
            {
                XeriWindowWorkspaceTestSupport.SetField(theme, "id", "custom");
                var resolver = new XeriWindowThemeResolver();
                resolver.Register(theme);
                var registry = new XeriWindowRegistry();
                using var fixture = new WorkspaceFixture
                (
                    themeResolver: resolver,
                    registry: registry
                );
                string registeredThemeID = null;
                registry.OnRegister += (_, args) =>
                {
                    if (registry.TryGetRecord(args.Handle, out var observed))
                    {
                        registeredThemeID = observed.ThemeID;
                    }
                };
                var record = new XeriWindowRecord
                {
                    ID = "themed-window",
                    Title = "Themed",
                    Pos = new Vector2(20f, 30f),
                    Size = new Vector2(320f, 220f),
                    NormalPos = new Vector2(20f, 30f),
                    NormalSize = new Vector2(320f, 220f),
                    ThemeID = "custom",
                };
                var window = fixture.Workspace.OpenWindow(record, new Label("Content"));
                Assert.AreEqual("custom", window.Panel.ThemeID);
                Assert.AreEqual("custom", registeredThemeID);
                Assert.IsTrue(registry.TryGetRecord(window.Handle, out var registered));
                Assert.AreEqual("custom", registered.ThemeID);

                window.Panel.ApplyTheme(XeriWindowThemeClass.MacID);
                Assert.IsTrue(registry.TryGetRecord(window.Handle, out var changed));
                Assert.AreEqual(XeriWindowThemeClass.MacID, changed.ThemeID);

                window.Panel.ApplyTheme(" custom ");
                Assert.IsTrue(registry.TryGetRecord(window.Handle, out var resumed));
                Assert.AreEqual("custom", resumed.ThemeID);
                Assert.IsTrue(window.Panel.ClassListContains(XeriWindowThemeClass.Custom));

                var snapshot = fixture.Workspace.CaptureRecords()[0];

                Assert.AreEqual("custom", snapshot.ThemeID);
                window.Dispose();

                var restored = fixture.Workspace.OpenWindow(snapshot, new Label("Restored"));

                Assert.AreEqual("custom", restored.Panel.ThemeID);
                Assert.AreEqual(1, fixture.Workspace.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 등록 이벤트는 미등록 Theme ID의 Windows 적용 상태를 즉시 관찰한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_UnknownTheme_Register_적용ID조회()
        {
            var registry = new XeriWindowRegistry();
            using var fixture = new WorkspaceFixture(registry: registry);
            var record = new XeriWindowRecord
            {
                ID = "unknown-theme",
                Title = "Unknown Theme",
                Pos = new Vector2(10f, 15f),
                Size = new Vector2(300f, 220f),
                ThemeID = "missing",
            };
            string onRegisterTheme = null;
            registry.OnRegister += (_, args) =>
            {
                if (registry.TryGetRecord(args.Handle, out var observed))
                {
                    onRegisterTheme = observed.ThemeID;
                }
            };

            var window = fixture.Workspace.OpenWindow(record, new Label("View"));

            Assert.AreEqual("missing", record.ThemeID);
            Assert.AreEqual(XeriWindowThemeClass.WindowsID, window.Panel.ThemeID);
            Assert.AreEqual(XeriWindowThemeClass.WindowsID, onRegisterTheme);
            Assert.IsTrue(registry.TryGetRecord(window.Handle, out var snapshot));
            Assert.AreEqual(XeriWindowThemeClass.WindowsID, snapshot.ThemeID);
            Assert.AreEqual(XeriWindowThemeClass.WindowsID, fixture.Workspace.CaptureRecords()[0].ThemeID);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 초기화 중 변경한 테마가 Controller와 Registry에 반영된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriWindowWorkspace_DragFactory_Theme변경_등록상태일치()
        {
            var dragFactory = new CallbackDragFactory();
            dragFactory.Creating = (panel, _) =>
                panel.ApplyTheme(XeriWindowThemeClass.MacID);

            var registry = new XeriWindowRegistry();
            using var fixture = new WorkspaceFixture
            (
                dragFactory: dragFactory,
                registry: registry
            );

            var window = OpenWindow(fixture.Workspace, "drag-theme");

            Assert.AreEqual(XeriWindowThemeClass.MacID, window.Panel.ThemeID);
            Assert.AreEqual(XeriWindowThemeClass.MacID, window.Controller.ThemeID);
            Assert.IsTrue(registry.TryGetRecord(window.Handle, out var recorded));
            Assert.AreEqual(XeriWindowThemeClass.MacID, recorded.ThemeID);
            Assert.AreEqual
            (
                XeriWindowThemeClass.MacID,
                fixture.Workspace.CaptureRecords()[0].ThemeID
            );

            window.Panel.ApplyTheme(XeriWindowThemeClass.MinimalID);
            Assert.AreEqual(XeriWindowThemeClass.MinimalID, window.Controller.ThemeID);
            Assert.IsTrue(registry.TryGetRecord(window.Handle, out var updated));
            Assert.AreEqual(XeriWindowThemeClass.MinimalID, updated.ThemeID);
        }

    #endregion

    }
}
