/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriUnityToolbarTray.cs
수정일 : 2026-10-03

# 설명
Unity toolbar Tray host 테스트.

# 테스트 구성
 I: Fake toolbar 주입
 T: Tray 표시
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Tray;
using inonego.Xeri.UI.Window;
using inonego.Xeri.UI.Window.Editor;

namespace inonego.Xeri.UI.TEST.Window
{
    // ============================================================
    /// <summary>
    /// XeriUnityToolbarTray 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriUnityToolbarTray
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트용 window driver.
        /// </summary>
        // ============================================================
        private sealed class TestWindowDriver : IXeriWindowDriver
        {
            public PresentationAlpha Alpha { get; } = new();
            public PresentationVisibility Visibility { get; } = new();

            public Vector2 Pos { get; set; } = Vector2.zero;
            public Vector2 Size { get; set; } = new Vector2(200f, 120f);
            public XeriWindowState State { get; set; } = XeriWindowState.Normal;
            public XeriWindowState VisualState { get; private set; } = XeriWindowState.Normal;

            public Rect Bounds
            {
                get => new Rect(Pos, Size);
                set
                {
                    Pos = value.position;
                    Size = value.size;
                }
            }

            public void CommitState(XeriWindowState state)
            {
                State = state;
                ApplyVisualState(state);
            }

            public void ApplyVisualState(XeriWindowState state)
            {
                VisualState = state;
            }

            public void ApplyBounds(Rect bounds)
            {
                Bounds = bounds;
            }

            public void ApplyMaximizedBounds()
            {
                // NONE
            }
        }

        // ======================================================================
        /// <summary>
        /// public Registry event accessor와 조회 실패를 주입하는 테스트 double.
        /// </summary>
        // ======================================================================
        private sealed class FaultingRegistry : IXeriWindowRegistry
        {
            public XeriWindowHandle ActiveHandle => null;

            public IReadOnlyList<XeriWindowRecord> Records
            {
                get
                {
                    if (ThrowOnRecords)
                    {
                        throw new InvalidOperationException("injected registry records failure");
                    }

                    return Array.Empty<XeriWindowRecord>();
                }
            }

            public bool ThrowAfterCollectionAdd { get; set; }
            public bool ThrowAfterActiveAdd { get; set; }
            public bool ThrowOnRecords { get; set; }
            public int CollectionRemoveCount { get; private set; }
            public int ActiveRemoveCount { get; private set; }
            public int CollectionHandlerCount => collectionHandlers?.GetInvocationList().Length ?? 0;
            public int ActiveHandlerCount => activeHandlers?.GetInvocationList().Length ?? 0;

            private EventHandler collectionHandlers = null;
            private EventHandler<XeriWindowEventArgs> activeHandlers = null;

            public event EventHandler OnCollectionChange
            {
                add
                {
                    collectionHandlers += value;

                    if (ThrowAfterCollectionAdd)
                    {
                        throw new InvalidOperationException("injected collection add failure");
                    }
                }
                remove
                {
                    CollectionRemoveCount++;
                    collectionHandlers -= value;
                }
            }

            public event EventHandler<XeriWindowEventArgs> OnActiveChange
            {
                add
                {
                    activeHandlers += value;

                    if (ThrowAfterActiveAdd)
                    {
                        throw new InvalidOperationException("injected active add failure");
                    }
                }
                remove
                {
                    ActiveRemoveCount++;
                    activeHandlers -= value;
                }
            }

            public event EventHandler<XeriWindowEventArgs> OnRegister
            {
                add
                {
                    // NONE
                }
                remove
                {
                    // NONE
                }
            }

            public event EventHandler<XeriWindowEventArgs> OnUnregister
            {
                add
                {
                    // NONE
                }
                remove
                {
                    // NONE
                }
            }

            public event EventHandler OnOrderChange
            {
                add
                {
                    // NONE
                }
                remove
                {
                    // NONE
                }
            }

            public XeriWindowHandle Register(string id, XeriWindowController controller)
                => throw new NotSupportedException();

            public XeriWindowHandle Register
            (
                string id,
                XeriWindowController controller,
                XeriWindowRecord record
            )
                => throw new NotSupportedException();

            public bool Unregister(XeriWindowHandle handle) => false;
            public bool Contains(XeriWindowHandle handle) => false;

            public bool TryGetRecord
            (
                XeriWindowHandle handle,
                out XeriWindowRecord record
            )
            {
                record = null;
                return false;
            }

            public bool TryGetController
            (
                XeriWindowHandle handle,
                out XeriWindowController controller
            )
            {
                controller = null;
                return false;
            }

            public bool TryGetHandle
            (
                string id,
                out XeriWindowHandle handle
            )
            {
                handle = null;
                return false;
            }

            public void Focus(XeriWindowHandle handle)
            {
                // NONE
            }

            public void Deactivate(XeriWindowHandle handle)
            {
                // NONE
            }

            public void BringToFront(XeriWindowHandle handle)
            {
                // NONE
            }

            public void SendToBack(XeriWindowHandle handle)
            {
                // NONE
            }

            public void SetStackLayer
            (
                XeriWindowHandle handle,
                XeriWindowStackLayer stackLayer
            )
            {
                // NONE
            }

            public void ShowNormal(XeriWindowHandle handle)
            {
                // NONE
            }

            public void Restore(XeriWindowHandle handle)
            {
                // NONE
            }

            public void Close(XeriWindowHandle handle)
            {
                // NONE
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트용 controller를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriWindowController CreateController()
        {
            return new XeriWindowController(new TestWindowDriver());
        }

    #endregion

    #region I-1: 가짜 툴바 주입

        // ----------------------------------------------------------------------
        /// <summary>
        /// Install은 지정된 fake toolbar root에 공통 TrayPanel을 한 번만 주입한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_Install_FakeRoot_TrayPanel_한번만_주입()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            var toolbarTray = new XeriUnityToolbarTray();

            var first = toolbarTray.Install(root, registry);
            var second = toolbarTray.Install(root, registry);

            Assert.IsTrue(first);
            Assert.IsTrue(second);
            Assert.AreEqual(1, root.Query("xeri-unity-toolbar-tray").ToList().Count);
            Assert.IsTrue(toolbarTray.TrayPanel.ClassListContains("xeri-tray--unity-toolbar"));
        }

    #endregion

    #region I-2: 중복 호스트

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 이미 다른 host가 주입한 Tray에는 두 번째 Controller binding을 만들지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_Install_다른Host_중복거부()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            var firstHost = new XeriUnityToolbarTray();
            var secondHost = new XeriUnityToolbarTray();

            Assert.IsTrue(firstHost.Install(root, registry));
            Assert.IsFalse(secondHost.Install(root, registry));

            Assert.IsNotNull(firstHost.TrayPanel);
            Assert.IsNull(secondHost.TrayPanel);
            Assert.AreEqual(1, root.Query("xeri-unity-toolbar-tray").ToList().Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// owner host Dispose 후에는 Panel이 제거되어 새 host가 다시 설치할 수 있다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_Dispose_OwnerPanel제거_재설치가능()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            var firstHost = new XeriUnityToolbarTray();
            var secondHost = new XeriUnityToolbarTray();

            Assert.IsTrue(firstHost.Install(root, registry));
            firstHost.Dispose();

            Assert.AreEqual(0, root.Query("xeri-unity-toolbar-tray").ToList().Count);
            Assert.IsTrue(secondHost.Install(root, registry));
            Assert.AreEqual(1, root.Query("xeri-unity-toolbar-tray").ToList().Count);
        }

    #endregion

    #region I-3: 옵션

        // ----------------------------------------------------------------------
        /// <summary>
        /// Toolbar 전용 class를 별도로 적용하면서 caller의 Tray options를 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_Install_Options_보존()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            var toolbarTray = new XeriUnityToolbarTray();
            var options = new XeriTrayOptions
            {
                UssClass = "custom-tray",
                Reorderable = true,
                ReorderAxis = XeriTrayReorderAxis.Vertical,
                AnimateReorder = false,
            };

            Assert.IsTrue(toolbarTray.Install(root, registry, options: options));

            Assert.IsTrue(toolbarTray.TrayPanel.ClassListContains("custom-tray"));
            Assert.IsTrue
            (
                toolbarTray.TrayPanel.ClassListContains("xeri-tray--unity-toolbar")
            );
            Assert.IsTrue(toolbarTray.TrayPanel.Reorderable);
            Assert.AreEqual
            (
                XeriTrayReorderAxis.Vertical,
                toolbarTray.TrayPanel.ReorderAxis
            );
        }

    #endregion

    #region I-4: 실패 원자성

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> public Registry event add가 등록 뒤 실패해도
        /// <br/> Source constructor가 남은 구독을 롤백한다.
        /// </summary>
        // ------------------------------------------------------------
        [TestCase(false)]
        [TestCase(true)]
        public void TEST_XeriWindowTraySource_EventAdd후실패_구독Rollback(bool failOnActive)
        {
            var registry = new FaultingRegistry
            {
                ThrowAfterCollectionAdd = !failOnActive,
                ThrowAfterActiveAdd = failOnActive,
            };

            Assert.Throws<InvalidOperationException>
            (
                () => new XeriWindowTraySource(registry)
            );

            Assert.AreEqual(0, registry.CollectionHandlerCount);
            Assert.AreEqual(0, registry.ActiveHandlerCount);
            Assert.AreEqual(1, registry.CollectionRemoveCount);
            Assert.AreEqual(failOnActive ? 1 : 0, registry.ActiveRemoveCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Toolbar 설치의 Reload 단계가 실패해도 Panel과 Registry 구독을 모두 롤백하고
        /// <br/> 같은 Host를 다시 설치할 수 있다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_Reload실패_전체Rollback후재설치()
        {
            var root = new VisualElement();
            var registry = new FaultingRegistry
            {
                ThrowOnRecords = true,
            };
            var toolbarTray = new XeriUnityToolbarTray();

            Assert.Throws<InvalidOperationException>
            (
                () => toolbarTray.Install(root, registry)
            );
            Assert.AreEqual(0, root.Query("xeri-unity-toolbar-tray").ToList().Count);
            Assert.AreEqual(0, registry.CollectionHandlerCount);
            Assert.AreEqual(0, registry.ActiveHandlerCount);

            registry.ThrowOnRecords = false;

            Assert.IsTrue(toolbarTray.Install(root, registry));
            Assert.AreEqual(1, root.Query("xeri-unity-toolbar-tray").ToList().Count);
            Assert.AreEqual(1, registry.CollectionHandlerCount);
            Assert.AreEqual(1, registry.ActiveHandlerCount);

            toolbarTray.Dispose();

            Assert.AreEqual(0, registry.CollectionHandlerCount);
            Assert.AreEqual(0, registry.ActiveHandlerCount);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 이미 설치된 같은 Host를 다른 Root에 다시 설치하면
        /// <br/> 기존 소유 Panel을 유지하고 새 Panel을 만들지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_다른Root재설치_기존소유유지()
        {
            var firstRoot = new VisualElement();
            var secondRoot = new VisualElement();
            var registry = new XeriWindowRegistry();
            var toolbarTray = new XeriUnityToolbarTray();

            Assert.IsTrue(toolbarTray.Install(firstRoot, registry));
            Assert.IsFalse(toolbarTray.Install(secondRoot, registry));

            Assert.AreSame(firstRoot, toolbarTray.TrayPanel.parent);
            Assert.AreEqual(1, firstRoot.Query("xeri-unity-toolbar-tray").ToList().Count);
            Assert.AreEqual(0, secondRoot.Query("xeri-unity-toolbar-tray").ToList().Count);

            toolbarTray.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> EditorWindow Toolbar constructor Reload 실패가
        /// <br/> Source/Controller Registry 구독을 남기지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriEditorWindowTrayToolbar_ConstructorReload실패_구독Rollback()
        {
            var registry = new FaultingRegistry
            {
                ThrowOnRecords = true,
            };

            Assert.Throws<InvalidOperationException>
            (
                () => new XeriEditorWindowTrayToolbar(registry)
            );
            Assert.AreEqual(0, registry.CollectionHandlerCount);
            Assert.AreEqual(0, registry.ActiveHandlerCount);

            registry.ThrowOnRecords = false;
            var toolbar = new XeriEditorWindowTrayToolbar(registry);

            Assert.AreEqual(1, registry.CollectionHandlerCount);
            Assert.AreEqual(1, registry.ActiveHandlerCount);

            toolbar.Dispose();

            Assert.AreEqual(0, registry.CollectionHandlerCount);
            Assert.AreEqual(0, registry.ActiveHandlerCount);
        }

    #endregion

    #region T-1: 트레이 표시

        // ------------------------------------------------------------
        /// <summary>
        /// Minimized window가 toolbar TrayPanel entry로 표시된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUnityToolbarTray_MinimizedWindow_TrayEntry_표시()
        {
            var root = new VisualElement();
            var registry = new XeriWindowRegistry();
            registry.Register("normal", CreateController());
            var handle = registry.Register("inventory", CreateController());
            var toolbarTray = new XeriUnityToolbarTray();

            registry.TryGetController(handle, out var controller);
            controller.Minimize();

            toolbarTray.Install
            (
                root,
                registry,
                XeriWindowTrayStateMask.Minimized
            );
            toolbarTray.Reload();

            var container = toolbarTray.TrayPanel.Q<VisualElement>("entry-container");

            Assert.IsNotNull(container);
            Assert.AreEqual(1, container.childCount);
        }

    #endregion

    }
}
