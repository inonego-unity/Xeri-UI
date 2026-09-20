/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriImmediateWindowStateTransitioner.cs
수정일 : 2026-09-20

# 설명
애니메이션 없이 Xeri window 상태 전환을 즉시 완료하는 transitioner.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
   // ============================================================
   /// <summary>
   /// 애니메이션 없는 Xeri window 상태 전환 transitioner.
   /// </summary>
   // ============================================================
   public sealed class XeriImmediateWindowStateTransitioner : IXeriWindowStateTransitioner
   {

   #region 프로퍼티

      // ------------------------------------------------------------
      /// <summary>
      /// 현재 전환 실행 상태.
      /// </summary>
      // ------------------------------------------------------------
      public XeriWindowTransitionStatus Status => XeriWindowTransitionStatus.Idle;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 실행 중 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool IsRunning => false;

      // ------------------------------------------------------------
      /// <summary>
      /// 진행 중 전환 목표 상태.
      /// </summary>
      // ------------------------------------------------------------
      public XeriWindowState? PendingState => null;

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 상태 전환을 즉시 완료한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool Transition(XeriWindowStateTransitionRequest request)
      {
         if (request == null) throw new ArgumentNullException(nameof(request));
         if (request.Driver == null) throw new ArgumentNullException(nameof(request.Driver));

         try
         {
            ApplyImmediate(request);
            request.OnComplete?.Invoke();

            return true;
         }
         catch (Exception exception)
         {
            request.OnError?.Invoke(exception);
            throw;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 즉시 전환은 취소할 실행 상태가 없다.
      /// </summary>
      // ------------------------------------------------------------
      public void Cancel(bool restoreVisual) {}

   #endregion

   #region 내부 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 요청 상태에 맞는 driver primitive를 즉시 호출한다.
      /// </summary>
      // ------------------------------------------------------------
      private static void ApplyImmediate(XeriWindowStateTransitionRequest request)
      {
         var driver = request.Driver;
         var isHiddenState = request.NextState == XeriWindowState.Minimized ||
                             request.NextState == XeriWindowState.Closed;

         if (!isHiddenState)
         {
            driver.SetVisible(true);
         }

         driver.CommitState(request.NextState);

         if (request.NextState == XeriWindowState.Maximized)
         {
            driver.ApplyMaximizedBounds();
         }
         else if (request.TargetBounds.HasValue)
         {
            driver.ApplyBounds(request.TargetBounds.Value);
         }

         if (isHiddenState)
         {
            driver.SetVisible(false);
         }
      }

   #endregion

   }
}
