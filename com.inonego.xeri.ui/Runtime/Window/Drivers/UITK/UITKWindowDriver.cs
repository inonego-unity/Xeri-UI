/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKWindowDriver.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 상태와 Core Presentation을 UITK VisualElement에 반영하는 driver.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// UITK VisualElement 기반 Xeri 윈도우 driver.
    /// </summary>
    // ============================================================
    public sealed class UITKWindowDriver : IXeriWindowDriver, IFocusScope
    {

    #region 필드

        private readonly VisualElement target = null;
        private readonly UITKPresentation presentation = null;

        private XeriWindowState visualState = XeriWindowState.Normal;

    #endregion

    #region 프로퍼티

        // ------------------------------------------------------------
        /// <summary>
        /// Window Root의 합성 Alpha State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha Alpha => presentation.Alpha;

        // ------------------------------------------------------------
        /// <summary>
        /// Window Root의 합성 Visibility State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationVisibility Visibility => presentation.Visibility;

        // ------------------------------------------------------------
        /// <summary>
        /// Window가 처음 활성화될 때 사용할 기본 Focus 대상.
        /// </summary>
        // ------------------------------------------------------------
        public object DefaultFocus => GetDefaultFocus();

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 위치.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 Pos
        {
            get => pos;
            set
            {
                pos = value;
                ApplyCurrentBounds();
            }
        }

        private Vector2 pos = Vector2.zero;

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 크기.
        /// </summary>
        // ------------------------------------------------------------
        public Vector2 Size
        {
            get => size;
            set
            {
                size = value;
                ApplyCurrentBounds();
            }
        }

        private Vector2 size = new Vector2(200f, 120f);

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 표시 상태.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowState State => state;

        private XeriWindowState state = XeriWindowState.Normal;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 위치와 크기를 하나의 bounds로 반환하거나 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public Rect Bounds
        {
            get => new Rect(pos, size);
            set => ApplyBounds(value);
        }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// UITK window driver를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public UITKWindowDriver(VisualElement target) : base()
        {
            this.target = target ?? throw new System.ArgumentNullException(nameof(target));
            this.target.focusable = true;
            presentation = new UITKPresentation(this.target);

            ApplyBounds(Bounds);
            CommitState(state);
            Visibility.Set(true);
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 VisualElement가 Window Root subtree에 속하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool ContainsFocus(object target)
        {
            if (target is not VisualElement element) return false;

            for (var current = element; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, this.target)) return true;
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 class와 상태 값을 대상에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public void CommitState(XeriWindowState next)
        {
            state = next;
            ApplyVisualState(next);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 값은 유지하고 상태 class만 대상에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ApplyVisualState(XeriWindowState next)
        {
            if (target == null)
            {
                visualState = next;
                return;
            }

            if (target is XeriWindowPanel panel)
            {
                visualState = next;
                panel.ApplyState(next);
                return;
            }

            if (visualState == next)
            {
                target.AddToClassList(GetStateClass(visualState));
                return;
            }

            target.RemoveFromClassList(GetStateClass(visualState));
            visualState = next;
            target.AddToClassList(GetStateClass(visualState));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 위치와 크기를 대상 style에 즉시 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ApplyBounds(Rect bounds)
        {
            pos = bounds.position;
            size = bounds.size;

            if (target == null) return;

            target.style.left = pos.x;
            target.style.top = pos.y;
            target.style.right = StyleKeyword.Auto;
            target.style.bottom = StyleKeyword.Auto;
            target.style.width = size.x;
            target.style.height = size.y;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 대상이 부모 영역을 채우도록 최대화 영역을 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ApplyMaximizedBounds()
        {
            if (target == null) return;

            target.style.left = 0f;
            target.style.top = 0f;
            target.style.right = 0f;
            target.style.bottom = 0f;
            target.style.width = StyleKeyword.Auto;
            target.style.height = StyleKeyword.Auto;
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Window Root를 기본 Focus 대상으로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private object GetDefaultFocus()
        {
            return target;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 상태에서 즉시 반영 가능한 bounds를 target에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyCurrentBounds()
        {
            if (target == null) return;
            if (state != XeriWindowState.Normal) return;

            ApplyBounds(Bounds);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태에 대응하는 USS class 이름을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetStateClass(XeriWindowState state)
        {
            return state switch
            {
                XeriWindowState.Minimized => "xeri-window--minimized",
                XeriWindowState.Maximized => "xeri-window--maximized",
                XeriWindowState.Closed    => "xeri-window--closed",
                _                         => "xeri-window--normal",
            };
        }

    #endregion

    }
}
