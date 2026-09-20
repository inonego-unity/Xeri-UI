/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowStateTransitionRequest.cs
수정일 : 2026-09-20

# 설명
Xeri window 상태 전환 실행에 필요한 요청 데이터.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI.Window
{
   // ============================================================
   /// <summary>
   /// Xeri window 상태 전환 요청 데이터.
   /// </summary>
   // ============================================================
   public sealed class XeriWindowStateTransitionRequest
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 상태를 반영할 driver.
      /// </summary>
      // ------------------------------------------------------------
      public IXeriWindowDriver Driver = null;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 시작 시점의 완료 상태.
      /// </summary>
      // ------------------------------------------------------------
      public XeriWindowState PreviousState = XeriWindowState.Normal;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 목표 상태.
      /// </summary>
      // ------------------------------------------------------------
      public XeriWindowState NextState = XeriWindowState.Normal;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 목표 bounds.
      /// </summary>
      // ------------------------------------------------------------
      public Rect? TargetBounds = null;

      // ------------------------------------------------------------
      /// <summary>
      /// 진행 중 전환과 충돌했을 때의 처리 정책.
      /// </summary>
      // ------------------------------------------------------------
      public XeriWindowTransitionInterruptPolicy InterruptPolicy = XeriWindowTransitionInterruptPolicy.CancelAndReplace;

      // ------------------------------------------------------------
      /// <summary>
      /// Visual animation 사용 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Animate = true;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 완료 callback.
      /// </summary>
      // ------------------------------------------------------------
      public Action OnComplete = null;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 취소 callback.
      /// </summary>
      // ------------------------------------------------------------
      public Action OnCancel = null;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 실패 callback.
      /// </summary>
      // ------------------------------------------------------------
      public Action<Exception> OnError = null;

   #endregion

   }
}
