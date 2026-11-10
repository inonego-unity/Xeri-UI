/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriEditorWindowTrayToolbar.cs
수정일 : 2026-11-10

# 설명
EditorWindow 내부에서 공통 XeriTrayPanel을 배치하는 toolbar view.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

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
    /// EditorWindow 내부용 Tray toolbar.
    /// </summary>
    // ============================================================
    public sealed class XeriEditorWindowTrayToolbar : VisualElement, IDisposable
    {

    #region Tray 연결 상태

        // ------------------------------------------------------------
        /// <summary>
        /// Toolbar 안에 배치된 공통 Tray panel.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayPanel TrayPanel => trayPanel;

        private readonly XeriTrayPanel trayPanel = null;

        private readonly XeriWindowTraySource source = null;
        private readonly XeriTrayController controller = null;
        private bool isDisposed = false;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// EditorWindow 내부 Tray toolbar를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriEditorWindowTrayToolbar
        (
            IXeriWindowRegistry registry,
            XeriWindowTrayStateMask includedStates = XeriWindowTrayStateMask.All,
            XeriTrayOptions options = null
        )
        {
            name = "xeri-editor-window-tray-toolbar";
            AddToClassList("xeri-editor-window-tray-toolbar");

            trayPanel = new XeriTrayPanel();
            hierarchy.Add(trayPanel);

            source = new XeriWindowTraySource(registry, includedStates);

            try
            {
                controller = new XeriTrayController(source, trayPanel, options);
                controller.OnEntrySelect += OnTrayEntrySelect;
                controller.OnEntryClose += OnTrayEntryClose;
                trayPanel.OnEntryReorder += OnTrayEntryReorder;
                RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
                controller.Reload();
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };

                UnregisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);

                if (controller != null)
                {
                    try
                    {
                        controller.OnEntrySelect -= OnTrayEntrySelect;
                        controller.OnEntryClose -= OnTrayEntryClose;
                        trayPanel.OnEntryReorder -= OnTrayEntryReorder;
                        controller.Dispose();
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }
                }

                try
                {
                    source.Dispose();
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "EditorWindow Tray Toolbar 조립과 롤백이 실패했습니다.",
                    errors
                );
            }
        }

    #endregion

    #region Tray 새로 고침

        // ------------------------------------------------------------
        /// <summary>
        /// Tray 표시를 즉시 다시 그린다.
        /// </summary>
        // ------------------------------------------------------------
        public void Reload()
        {
            controller.Reload();
        }

    #endregion

    #region Tray 입력과 Panel 분리

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

        // ------------------------------------------------------------
        /// <summary>
        /// Toolbar가 Panel에서 분리되면 소유 구독을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDetachedFromPanel(DetachFromPanelEvent eventData)
        {
            Dispose();
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
                UnregisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }

            try
            {
                controller.OnEntrySelect -= OnTrayEntrySelect;
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }

            try
            {
                controller.OnEntryClose -= OnTrayEntryClose;
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }

            try
            {
                trayPanel.OnEntryReorder -= OnTrayEntryReorder;
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }

            try
            {
                controller.Dispose();
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }

            try
            {
                source.Dispose();
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }

            if ((errors?.Count ?? 0) == 0) return;

            if ((errors?.Count ?? 0) == 1)
            {
                throw errors[0];
            }

            throw new AggregateException
            (
                "EditorWindow Tray Toolbar 해제 중 하나 이상의 정리가 실패했습니다.",
                errors
            );
        }

    #endregion

    }
}
