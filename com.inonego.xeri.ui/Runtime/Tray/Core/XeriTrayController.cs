/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriTrayController.cs
수정일 : 2026-09-20

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
        private bool isSourceBound = false;
        private bool isSelectBound = false;
        private bool isCloseBound = false;
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
        /// Tray controller를 생성한다.
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

            try
            {
                Bind();
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };
                Unbind(errors);

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "Tray Controller 이벤트 연결과 롤백이 실패했습니다.",
                    errors
                );
            }
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

            renderer.Reload(entries, options);
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Source와 renderer 이벤트를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Bind()
        {
            isSourceBound = true;
            source.OnReloadRequired += OnSourceReloadRequired;

            isSelectBound = true;
            renderer.OnEntrySelect += OnRendererEntrySelect;

            isCloseBound = true;
            renderer.OnEntryClose += OnRendererEntryClose;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Source와 renderer 이벤트 연결을 독립적으로 한 번씩 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Unbind(List<Exception> errors)
        {
            if (isCloseBound)
            {
                isCloseBound = false;

                try
                {
                    renderer.OnEntryClose -= OnRendererEntryClose;
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (isSelectBound)
            {
                isSelectBound = false;

                try
                {
                    renderer.OnEntrySelect -= OnRendererEntrySelect;
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (isSourceBound)
            {
                isSourceBound = false;

                try
                {
                    source.OnReloadRequired -= OnSourceReloadRequired;
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// EventHandler 구독자를 독립적으로 호출하고 오류를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private void InvokeHandlers
        (
            EventHandler handlers,
            EventArgs eventArgs,
            List<Exception> errors
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
            List<Exception> errors
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
            List<Exception> errors
        )
        {
            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException(message, errors);
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

            var errors = new List<Exception>();
            InvokeHandlers(OnReloadRequired, EventArgs.Empty, errors);

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

            var errors = new List<Exception>();
            InvokeHandlers(OnEntrySelect, e, errors);
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
            var errors = new List<Exception>();
            InvokeHandlers(OnPreEntryClose, cancelEventArgs, errors);

            if (errors.Count > 0)
            {
                ThrowEventErrors("Tray pre-close 이벤트 처리", errors);
                return;
            }

            if (isDisposed || cancelEventArgs.Cancel) return;

            InvokeHandlers(OnEntryClose, e, errors);
            ThrowEventErrors("Tray close 이벤트 처리", errors);
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// Source와 Renderer 이벤트 연결을 한 번 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            var errors = new List<Exception>();
            Unbind(errors);

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    "Tray Controller 이벤트 해제가 실패했습니다.",
                    errors
                );
            }
        }

    #endregion

    }
}
