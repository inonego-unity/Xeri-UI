/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriWindowAnimationTargetProvider.cs
수정일 : 2026-09-20

# 설명
Xeri window 상태 전환 animation 목표 bounds를 제공하는 계약.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI.Window
{
   // ============================================================
   /// <summary>
   /// Xeri window animation 목표 bounds provider.
   /// </summary>
   // ============================================================
   public interface IXeriWindowAnimationTargetProvider
   {

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 상태 전환 목표 bounds를 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      Rect GetTargetBounds(XeriWindowState nextState, Rect currentBounds);

   #endregion

   }
}
