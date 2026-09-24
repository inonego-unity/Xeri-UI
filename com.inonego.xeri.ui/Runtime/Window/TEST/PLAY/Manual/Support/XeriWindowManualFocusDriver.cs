/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowManualFocusDriver.cs
수정일 : 2026-09-24

# 설명
Window 수동 테스트에서 native focus 결과를 관찰할 최소 FocusDriver 구현을 제공한다.

# 테스트 구성
 F: 수동 테스트 Focus 상태
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.TEST.Window
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// 수동 Window 테스트용 FocusDriver.
    /// </summary>
    // ============================================================
    internal sealed class XeriWindowManualFocusDriver : FocusDriverBehaviour
    {

    #region 필드

        public override object Current => current;
        private object current = null;

        public object Fallback { get; } = new object();

    #endregion

    #region FocusDriverBehaviour

        public override bool CanSelect(object target)
        {
            return target != null;
        }

        public override bool IsValid(object target)
        {
            return target != null;
        }

        public override void Select(object target)
        {
            current = target;
        }

        public override object FindFallback()
        {
            return Fallback;
        }

    #endregion

    }
}
