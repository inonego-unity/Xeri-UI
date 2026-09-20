/* BLOCK_HEADER_BEGIN =======================================================================
파일명: XeriTrayReorderVisual.cs
수정일 : 2026-09-20

# 설명
Drag 중인 Tray entry visual을 축 제한 방식으로 이동시킨다.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Tray
{
    // ============================================================
    /// <summary>
    /// Tray reorder drag visual 처리기.
    /// </summary>
    // ============================================================
    public sealed class XeriTrayReorderVisual
    {

    #region 필드

        private const string REORDERING_CLASS = "xeri-tray-button--reordering";
        private const string PROXY_CLASS = "xeri-tray-button--reorder-proxy";

        private XeriTrayButton proxyButton = null;

    #endregion

    #region 생성자

        public XeriTrayReorderVisual() : base() {}

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 중인 button을 지정 축 방향으로만 이동시킨다.
        /// </summary>
        // ------------------------------------------------------------
        public void Move
        (
            XeriTrayReorderSession session,
            Vector2 currentPointerPos,
            IXeriTrayReorderTarget target
        )
        {
            if (session?.Button == null || target == null) return;

            UpdateProxy(session, target);
            session.Button.AddToClassList(REORDERING_CLASS);
            session.Button.visible = false;

            var delta = currentPointerPos - session.StartPointerPos;
            proxyButton.style.translate = target.ReorderAxis == XeriTrayReorderAxis.Horizontal
                ? new Translate(delta.x, 0f, 0f)
                : new Translate(0f, delta.y, 0f);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 중인 button offset을 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Clear(XeriTrayReorderSession session)
        {
            if (session?.Button == null) return;

            session.Button.style.translate = new Translate(0f, 0f, 0f);
            session.Button.visible = true;
            session.Button.RemoveFromClassList(REORDERING_CLASS);

            if (proxyButton != null)
            {
                proxyButton.RemoveFromHierarchy();
                proxyButton = null;
            }
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 중인 button을 대신 표시할 proxy를 생성하거나 위치를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UpdateProxy(XeriTrayReorderSession session, IXeriTrayReorderTarget target)
        {
            if (proxyButton == null)
            {
                proxyButton = new XeriTrayButton(session.Entry, target.ReorderOptions);
                proxyButton.AddToClassList(PROXY_CLASS);
                proxyButton.pickingMode = PickingMode.Ignore;
                target.EntryContainer.Add(proxyButton);
            }

            var bounds = session.Button.layout;

            proxyButton.style.position = Position.Absolute;
            proxyButton.style.left = bounds.x;
            proxyButton.style.top = bounds.y;
            proxyButton.style.width = bounds.width;
            proxyButton.style.height = bounds.height;
        }

    #endregion

    }
}
