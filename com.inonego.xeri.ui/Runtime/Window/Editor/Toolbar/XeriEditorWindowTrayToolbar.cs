/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriEditorWindowTrayToolbar.cs
수정일 : 2026-10-03

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

    #region 필드

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
        ) : base()
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

    #region 메서드

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

    #region 이벤트 핸들러

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
            var errors = new List<Exception>();

            try
            {
                UnregisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                controller.OnEntrySelect -= OnTrayEntrySelect;
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                controller.OnEntryClose -= OnTrayEntryClose;
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                controller.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                source.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
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
