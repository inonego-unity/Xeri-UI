/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowStateTransitionRule.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 상태 전환 가능 여부와 다음 상태를 계산한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 상태 전환 규칙.
    /// </summary>
    // ============================================================
    internal static class XeriWindowStateTransitionRule
    {

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 상태와 요청으로부터 다음 상태를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static bool TryResolveNextState
        (
            XeriWindowState currentState,
            XeriWindowStateCommandRequest request,
            out XeriWindowState nextState
        )
        {
            nextState = currentState;

            if (currentState == XeriWindowState.Closed) return false;

            switch (request.Kind)
            {
                case XeriWindowStateCommandKind.Minimize:
                    nextState = XeriWindowState.Minimized;
                    return currentState != XeriWindowState.Minimized;

                case XeriWindowStateCommandKind.Maximize:
                    nextState = XeriWindowState.Maximized;
                    return currentState != XeriWindowState.Maximized;

                case XeriWindowStateCommandKind.ShowNormal:
                    nextState = XeriWindowState.Normal;
                    return currentState != XeriWindowState.Normal;

                case XeriWindowStateCommandKind.Restore:
                    nextState = XeriWindowState.Normal;
                    return currentState == XeriWindowState.Minimized;

                case XeriWindowStateCommandKind.Close:
                    nextState = XeriWindowState.Closed;
                    return true;

                default:
                    return false;
            }
        }

    #endregion

    }
}
