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
    public sealed class XeriTrayController
    {

    #region 필드

        private readonly IXeriTraySource source = null;
        private readonly IXeriTrayRenderer renderer = null;
        private readonly XeriTrayOptions options = null;

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
            this.source   = source;
            this.renderer = renderer;
            this.options  = options ?? XeriTrayOptions.Default();

            Bind();
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
            IReadOnlyList<XeriTrayEntry> entries = source != null
                ? source.GetEntries()
                : Array.Empty<XeriTrayEntry>();

            renderer?.Reload(entries, options);
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
            if (source != null)
            {
                source.OnReloadRequired += OnSourceReloadRequired;
            }

            if (renderer != null)
            {
                renderer.OnEntrySelect += OnRendererEntrySelect;
                renderer.OnEntryClose  += OnRendererEntryClose;
            }
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
            OnReloadRequired?.Invoke(this, EventArgs.Empty);

            Reload();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Renderer의 entry 선택 입력을 외부 이벤트로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnRendererEntrySelect(object sender, XeriTrayEventArgs e)
        {
            OnEntrySelect?.Invoke(this, e);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Renderer의 entry 닫기 입력을 취소 가능한 이벤트로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnRendererEntryClose(object sender, XeriTrayEventArgs e)
        {
            var cancelEventArgs = new XeriTrayCancelEventArgs(e.Entry);

            OnPreEntryClose?.Invoke(this, cancelEventArgs);
        }

    #endregion

    }
}
