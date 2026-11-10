/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriUnityToolbarTray.cs
수정일 : 2026-11-10

# 설명
Unity Editor 상단 toolbar에 공통 XeriTrayPanel을 주입하는 Editor 전용 host.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Tray;

namespace inonego.Xeri.UI.Window.Editor
{
    // ============================================================
    /// <summary>
    /// Unity Editor toolbar에 Xeri window tray를 붙이는 host.
    /// </summary>
    // ============================================================
    public sealed class XeriUnityToolbarTray : IDisposable
    {

    #region Tray 연결 상태

        private const string TRAY_ROOT_NAME = "xeri-unity-toolbar-tray";
        private const string TRAY_USS_CLASS = "xeri-tray--unity-toolbar";
        private const string TRAY_USS_PATH = "XeriUI/Tray/XeriTrayUnityToolbar";

        // ------------------------------------------------------------
        /// <summary>
        /// 주입된 공통 Tray panel.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayPanel TrayPanel => trayPanel;

        private XeriTrayPanel trayPanel = null;

        private XeriWindowTraySource source = null;
        private XeriTrayController controller = null;
        private bool isDisposed = false;

    #endregion

    #region Toolbar 설치와 갱신

        // ------------------------------------------------------------
        /// <summary>
        /// Unity Editor toolbar를 찾아 Tray를 주입한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool InstallToUnityToolbar
        (
            IXeriWindowRegistry registry,
            XeriWindowTrayStateMask includedStates = XeriWindowTrayStateMask.All,
            XeriTrayOptions options = null
        )
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(XeriUnityToolbarTray));
            }

            var toolbarRoot = FindUnityToolbarRoot();
            if (toolbarRoot == null) return false;

            return Install(toolbarRoot, registry, includedStates, options);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 toolbar root에 Tray를 한 번 설치한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Install
        (
            VisualElement toolbarRoot,
            IXeriWindowRegistry registry,
            XeriWindowTrayStateMask includedStates = XeriWindowTrayStateMask.All,
            XeriTrayOptions options = null
        )
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(XeriUnityToolbarTray));
            }

            if (toolbarRoot == null) return false;
            if (registry == null) return false;

            // 설치에 성공한 Host는 기존 Registry·옵션을 변경하지 않는다.
            if (controller != null || trayPanel != null) return false;

            var exists = toolbarRoot.Q<XeriTrayPanel>(TRAY_ROOT_NAME);
            if (exists != null)
            {
                return false;
            }

            try
            {
                trayPanel = new XeriTrayPanel
                {
                    name = TRAY_ROOT_NAME,
                };
                trayPanel.AddToClassList(TRAY_USS_CLASS);
                AddToolbarStyleSheet(trayPanel);
                toolbarRoot.Add(trayPanel);

                BindTray(registry, includedStates, options);
                return true;
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };

                try
                {
                    ReleaseBinding();
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                try
                {
                    trayPanel?.RemoveFromHierarchy();
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                trayPanel = null;

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "Unity Toolbar Tray 설치와 롤백이 실패했습니다.",
                    errors
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray 표시를 즉시 다시 그린다.
        /// </summary>
        // ------------------------------------------------------------
        public void Reload()
        {
            controller?.Reload();
        }

    #endregion

    #region Tray 바인딩과 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 공통 Tray panel과 window source를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BindTray
        (
            IXeriWindowRegistry registry,
            XeriWindowTrayStateMask includedStates,
            XeriTrayOptions options
        )
        {
            source = new XeriWindowTraySource(registry, includedStates);
            controller = new XeriTrayController(source, trayPanel, CreateOptions(options));

            controller.OnEntrySelect += OnTrayEntrySelect;
            controller.OnEntryClose += OnTrayEntryClose;
            trayPanel.OnEntryReorder += OnTrayEntryReorder;

            controller.Reload();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray Controller와 Source binding을 한 번 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReleaseBinding()
        {
            var currentController = controller;
            var currentSource = source;
            controller = null;
            source = null;

            List<Exception> errors = null;

            if (trayPanel != null)
            {
                try
                {
                    trayPanel.OnEntryReorder -= OnTrayEntryReorder;
                }
                catch (Exception exception)
                {
                    (errors ??= new List<Exception>()).Add(exception);
                }
            }

            if (currentController != null)
            {
                try
                {
                    currentController.OnEntrySelect -= OnTrayEntrySelect;
                }
                catch (Exception exception)
                {
                    (errors ??= new List<Exception>()).Add(exception);
                }

                try
                {
                    currentController.OnEntryClose -= OnTrayEntryClose;
                }
                catch (Exception exception)
                {
                    (errors ??= new List<Exception>()).Add(exception);
                }

                try
                {
                    currentController.Dispose();
                }
                catch (Exception exception)
                {
                    (errors ??= new List<Exception>()).Add(exception);
                }
            }

            if (currentSource != null)
            {
                try
                {
                    currentSource.Dispose();
                }
                catch (Exception exception)
                {
                    (errors ??= new List<Exception>()).Add(exception);
                }
            }

            if ((errors?.Count ?? 0) == 0) return;

            if ((errors?.Count ?? 0) == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "Unity Toolbar Tray binding 해제 중 하나 이상의 정리가 실패했습니다.",
                errors
            );
        }

    #endregion

    #region Toolbar 탐색과 표시 설정

        // ------------------------------------------------------------
        /// <summary>
        /// Unity toolbar root VisualElement를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static VisualElement FindUnityToolbarRoot()
        {
            var toolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null) return null;

            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            var toolbar = toolbars.FirstOrDefault() as EditorWindow;

            return toolbar != null ? toolbar.rootVisualElement : null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity toolbar 전용 Tray 표시 옵션을 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static XeriTrayOptions CreateOptions(XeriTrayOptions options)
        {
            var source = options ?? new XeriTrayOptions
            {
                VisibleContent = XeriTrayContent.Icon |
                                 XeriTrayContent.Badge |
                                 XeriTrayContent.StateMarker,
            };

            return new XeriTrayOptions
            {
                VisibleContent = source.VisibleContent,
                UssClass = source.UssClass,
                Reorderable = source.Reorderable,
                ReorderAxis = source.ReorderAxis,
                AnimateReorder = source.AnimateReorder,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity toolbar 전용 Tray USS를 panel에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AddToolbarStyleSheet(VisualElement element)
        {
            var styleSheet = Resources.Load<StyleSheet>(TRAY_USS_PATH);

            if (styleSheet == null)
            {
                throw new InvalidOperationException($"XeriUnityToolbarTray USS를 로드할 수 없습니다. Path: {TRAY_USS_PATH}");
            }

            element.styleSheets.Add(styleSheet);
        }

    #endregion

    #region Tray 입력 처리

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 선택을 Window 활성화 명령으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnTrayEntrySelect(object sender, XeriTrayEventArgs e)
        {
            source.Activate(e.Entry);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 닫기 입력을 close 명령으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnTrayEntryClose(object sender, XeriTrayEventArgs e)
        {
            source.Close(e.Entry);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tray reorder 요청을 Window Source의 표시 순서 변경에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnTrayEntryReorder(object sender, XeriTrayReorderEventArgs e)
        {
            if (e.Entry?.Payload is XeriWindowHandle handle)
            {
                source.MoveEntry(handle, e.TargetIndex);
            }
        }

    #endregion

    #region 수명 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Tray 입력과 Source·Controller 구독을 한 번 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            List<Exception> errors = null;

            try
            {
                ReleaseBinding();
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }

            try
            {
                trayPanel?.RemoveFromHierarchy();
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }

            trayPanel = null;

            if ((errors?.Count ?? 0) == 0) return;

            if ((errors?.Count ?? 0) == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "Unity Toolbar Tray 해제 중 하나 이상의 정리가 실패했습니다.",
                errors
            );
        }

    #endregion

    }
}
