/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowStateTransitionContext.cs
수정일 : 2026-09-20

# 설명
Xeri window 상태 전환 시작 시점의 snapshot.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.UI.Window
{
   // ============================================================
   /// <summary>
   /// Xeri window 상태 전환 context.
   /// </summary>
   // ============================================================
   public readonly struct XeriWindowStateTransitionContext
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 상태를 반영할 driver.
      /// </summary>
      // ------------------------------------------------------------
      public readonly IXeriWindowDriver Driver;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 시작 시점의 완료 상태.
      /// </summary>
      // ------------------------------------------------------------
      public readonly XeriWindowState PreviousState;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 목표 상태.
      /// </summary>
      // ------------------------------------------------------------
      public readonly XeriWindowState NextState;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 시작 시점 bounds.
      /// </summary>
      // ------------------------------------------------------------
      public readonly Rect PreviousBounds;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 목표 bounds.
      /// </summary>
      // ------------------------------------------------------------
      public readonly Rect? TargetBounds;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 상태 전환 request에서 context를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public XeriWindowStateTransitionContext(XeriWindowStateTransitionRequest request)
      {
         Driver = request.Driver;
         PreviousState = request.PreviousState;
         NextState = request.NextState;
         PreviousBounds = request.Driver.Bounds;
         TargetBounds = request.TargetBounds;
      }

   #endregion

   }
}
