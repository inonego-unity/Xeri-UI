/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowWorkspace.cs
수정일 : 2026-09-24

# 설명
XeriWindowWorkspace의 기본 Window 수명, View Source, rollback과 Record 복원 계약을 검증한다.

# 테스트 구성
 W: Window Open/Close와 Workspace Screen 수명
 V: View Source와 UI Session 수명
 D: Open 실패 rollback
 R: Record 검증과 복원
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
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

    #region W-1: Window Open과 Close 수명

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

    #region V-1: View Source와 UI Session 수명

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

    #endregion

    #region D-1: Open 실패 rollback

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

    #endregion

    #region R-1: View attach 검증

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

    #region R-2: Record option 복원

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

    #region R-3: Normal bounds 복원

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

    #region R-4: 완료 상태 primitive 복원

        [Test]
        public void TEST_XeriWindowWorkspace_ApplyRecordState_Maximized_표시와Bounds_적용()
        {
            var driver = new TestWindowDriver();

            XeriWindowWorkspace.ApplyRecordState(driver, XeriWindowState.Maximized);

            Assert.AreEqual(XeriWindowState.Maximized, driver.State);
            Assert.IsTrue(driver.Visibility.Modified);
            Assert.IsTrue(driver.MaximizedApplied);
        }

        [Test]
        public void TEST_XeriWindowWorkspace_ApplyRecordState_Minimized_숨김()
        {
            var driver = new TestWindowDriver();

            XeriWindowWorkspace.ApplyRecordState(driver, XeriWindowState.Minimized);

            Assert.AreEqual(XeriWindowState.Minimized, driver.State);
            Assert.IsFalse(driver.Visibility.Modified);
        }

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

    }
}
