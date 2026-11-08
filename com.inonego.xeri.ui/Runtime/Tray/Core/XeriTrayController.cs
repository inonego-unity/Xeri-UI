/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriTrayController.cs
수정일 : 2026-11-09

# 설명
Tray source와 renderer를 연결하고 entry 선택/닫기 흐름을 외부 이벤트로 전달한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray source와 renderer를 연결하는 controller.
    /// </summary>
    // ============================================================
    public sealed class XeriTrayController : IDisposable
    {

    #region 필드

        private readonly IXeriTraySource source = null;
        private readonly IXeriTrayRenderer renderer = null;
        private readonly XeriTrayOptions options = null;
        private bool isDisposed = false;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Tray entry 목록 재조회가 필요한 시점에 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler OnReloadRequired = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 선택 시 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriTrayEventArgs> OnEntrySelect = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 닫기 요청 전에 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriTrayCancelEventArgs> OnPreEntryClose = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 취소되지 않은 Entry 닫기 요청이 승인되면 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<XeriTrayEventArgs> OnEntryClose = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Source와 Renderer 이벤트를 연결하고 구독 수명을 소유한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriTrayController
        (
            IXeriTraySource source,
            IXeriTrayRenderer renderer,
            XeriTrayOptions options = null
        ) : base()
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            this.options = options ?? XeriTrayOptions.Default();

            source.OnReloadRequired += OnSourceReloadRequired;
            renderer.OnEntrySelect += OnRendererEntrySelect;
            renderer.OnEntryClose += OnRendererEntryClose;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Source에서 entry 목록을 읽어 renderer에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Reload()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(XeriTrayController));
            }

            IReadOnlyList<XeriTrayEntry> entries = source.GetEntries();

            // Source callback에서 수명이 끝나면 반환된 목록을 renderer에 전달하지 않는다.
            if (isDisposed) return;

            renderer.Reload(entries, options);
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// EventHandler 구독자를 독립적으로 호출하고 오류를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private void InvokeHandlers
        (
            EventHandler handlers,
            EventArgs eventArgs,
            ref ExceptionBuffer errors
        )
        {
            if (handlers == null) return;

            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, eventArgs);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Generic EventHandler 구독자를 독립적으로 호출하고 오류를 수집한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void InvokeHandlers<TEventArgs>
        (
            EventHandler<TEventArgs> handlers,
            TEventArgs eventArgs,
            ref ExceptionBuffer errors
        )
        where TEventArgs : EventArgs
        {
            if (handlers == null) return;

            foreach (EventHandler<TEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, eventArgs);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 수집된 Tray event 오류를 한 번 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ThrowEventErrors
        (
            string message,
            ExceptionBuffer errors
        )
        {
            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException(message);
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Source의 reload 요청을 외부에 알리고 renderer를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnSourceReloadRequired(object sender, EventArgs e)
        {
            if (isDisposed) return;

            var errors = new ExceptionBuffer();
            InvokeHandlers(OnReloadRequired, EventArgs.Empty, ref errors);

            if (!isDisposed)
            {
                try
                {
                    Reload();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            ThrowEventErrors("Tray reload 이벤트 처리", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Renderer의 entry 선택 입력을 외부 이벤트로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnRendererEntrySelect(object sender, XeriTrayEventArgs e)
        {
            if (isDisposed) return;

            var errors = new ExceptionBuffer();
            InvokeHandlers(OnEntrySelect, e, ref errors);
            ThrowEventErrors("Tray select 이벤트 처리", errors);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Renderer의 entry 닫기 입력을 취소 가능한 이벤트로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnRendererEntryClose(object sender, XeriTrayEventArgs e)
        {
            if (isDisposed) return;
            if (e?.Entry == null || !e.Entry.CanClose) return;

            var cancelEventArgs = new XeriTrayCancelEventArgs(e.Entry);
            var errors = new ExceptionBuffer();
            InvokeHandlers(OnPreEntryClose, cancelEventArgs, ref errors);

            if (errors.Count > 0)
            {
                ThrowEventErrors("Tray pre-close 이벤트 처리", errors);
                return;
            }

            if (isDisposed || cancelEventArgs.Cancel) return;

            InvokeHandlers(OnEntryClose, e, ref errors);
            ThrowEventErrors("Tray close 이벤트 처리", errors);
        }

    #endregion

    #region 수명 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Source와 Renderer 이벤트 구독을 해제하며 재호출은 무시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            renderer.OnEntryClose -= OnRendererEntryClose;
            renderer.OnEntrySelect -= OnRendererEntrySelect;
            source.OnReloadRequired -= OnSourceReloadRequired;
        }

    #endregion

    }
}
