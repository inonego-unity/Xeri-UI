/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowTransitionInterruptPolicy.cs
수정일 : 2026-09-20

# 설명
Xeri window 상태 전환 실행 중 새 전환 요청이 들어왔을 때의 처리 정책.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.UI.Window
{
   // ============================================================
   /// <summary>
   /// Xeri window 상태 전환 interrupt 정책.
   /// </summary>
   // ============================================================
   public enum XeriWindowTransitionInterruptPolicy
   {
      IgnoreSameTarget,
      CancelAndReplace,
      RejectWhileRunning,
   }
}
