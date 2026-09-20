/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriUITKWindowStateTransitioner.cs
수정일 : 2026-09-20

# 설명
Xeri window 상태 전환 lifecycle과 UITK animator를 연결한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
   // ============================================================
   /// <summary>
   /// UITK animation을 사용하는 Xeri window 상태 전환 transitioner.
   /// </summary>
   // ============================================================
   public sealed class XeriUITKWindowStateTransitioner : IXeriWindowStateTransitioner
   {

   #region 필드

      private readonly IXeriWindowStateAnimator animator = null;

      private XeriWindowStateTransitionRequest currentRequest = null;
      private XeriWindowTransitionStatus status = XeriWindowTransitionStatus.Idle;
      private XeriWindowState? pendingState = null;
      private int transitionID = 0;

   #endregion

   #region 프로퍼티

      // ------------------------------------------------------------
      /// <summary>
      /// 현재 전환 실행 상태.
      /// </summary>
      // ------------------------------------------------------------
      public XeriWindowTransitionStatus Status => status;

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 실행 중 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool IsRunning => status == XeriWindowTransitionStatus.Running;

      // ------------------------------------------------------------
      /// <summary>
      /// 진행 중 전환 목표 상태.
      /// </summary>
      // ------------------------------------------------------------
      public XeriWindowState? PendingState => pendingState;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// UITK window state transitioner를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public XeriUITKWindowStateTransitioner(IXeriWindowStateAnimator animator) : base()
      {
         this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 상태 전환을 시작한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool Transition(XeriWindowStateTransitionRequest request)
      {
         if (request == null) throw new ArgumentNullException(nameof(request));
         if (request.Driver == null) throw new ArgumentNullException(nameof(request.Driver));

         if (!ResolveRunningRequest(request)) return false;

         currentRequest = request;
         pendingState = request.NextState;
         status = XeriWindowTransitionStatus.Running;
         transitionID++;

         var id = transitionID;
         var context = new XeriWindowStateTransitionContext(request);

         PrepareDriver(request);

         if (!request.Animate)
         {
            Complete(id);
            return true;
         }

         animator.Play
         (
            context,
            () => Complete(id),
            exception => Fail(id, exception)
         );

         return true;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 진행 중 상태 전환을 취소한다.
      /// </summary>
      // ------------------------------------------------------------
      public void Cancel(bool restoreVisual)
      {
         if (!IsRunning) return;

         animator.Cancel(restoreVisual);

         var request = currentRequest;
         ClearRunningState();
         request?.OnCancel?.Invoke();
      }

   #endregion

   #region 내부 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 진행 중 전환과 새 요청의 충돌 정책을 처리한다.
      /// </summary>
      // ------------------------------------------------------------
      private bool ResolveRunningRequest(XeriWindowStateTransitionRequest request)
      {
         if (!IsRunning) return true;

         if
         (
             pendingState == request.NextState &&
             request.InterruptPolicy == XeriWindowTransitionInterruptPolicy.IgnoreSameTarget
         )
         {
            return false;
         }

         if (request.InterruptPolicy == XeriWindowTransitionInterruptPolicy.RejectWhileRunning)
         {
            return false;
         }

         Cancel(false);
         return true;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Visual animation 시작 전에 표시 여부만 준비한다.
      /// </summary>
      // ------------------------------------------------------------
      private static void PrepareDriver(XeriWindowStateTransitionRequest request)
      {
         // 표시되는 상태로 들어가는 animation은 완료 전에도 element가 보일 수 있어야 한다.
         if (request.NextState != XeriWindowState.Minimized && request.NextState != XeriWindowState.Closed)
         {
            request.Driver.SetVisible(true);
         }

         // 완료 상태 값은 유지하되, 전환 중 필요한 테두리/핸들 시각 상태는 즉시 바꾼다.
         request.Driver.ApplyVisualState(request.NextState);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Animation 완료 후 최종 driver primitive를 반영한다.
      /// </summary>
      // ------------------------------------------------------------
      private void Complete(int id)
      {
         if (id != transitionID) return;

         var request = currentRequest;

         ApplyCompletedState(request);

         ClearRunningState();
         request.OnComplete?.Invoke();
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Animation 실패 시 이전 완료 상태로 되돌리고 running 고착을 방지한다.
      /// </summary>
      // ------------------------------------------------------------
      private void Fail(int id, Exception exception)
      {
         if (id != transitionID) return;

         var request = currentRequest;
         RollbackState(request);
         ClearRunningState();
         request?.OnError?.Invoke(exception);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Animation 완료 후 driver의 실제 완료 상태를 확정한다.
      /// </summary>
      // ------------------------------------------------------------
      private static void ApplyCompletedState(XeriWindowStateTransitionRequest request)
      {
         if (request.NextState == XeriWindowState.Maximized)
         {
            request.Driver.ApplyMaximizedBounds();
         }
         else if (request.TargetBounds.HasValue)
         {
            request.Driver.ApplyBounds(request.TargetBounds.Value);
         }

         request.Driver.CommitState(request.NextState);

         if (request.NextState == XeriWindowState.Minimized || request.NextState == XeriWindowState.Closed)
         {
            request.Driver.SetVisible(false);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Animation 실패 시 driver를 전환 전 완료 상태로 되돌린다.
      /// </summary>
      // ------------------------------------------------------------
      private static void RollbackState(XeriWindowStateTransitionRequest request)
      {
         if (request == null) return;

         request.Driver.CommitState(request.PreviousState);
         request.Driver.SetVisible
         (
            request.PreviousState != XeriWindowState.Minimized &&
            request.PreviousState != XeriWindowState.Closed
         );
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 전환 실행 상태를 초기화한다.
      /// </summary>
      // ------------------------------------------------------------
      private void ClearRunningState()
      {
         currentRequest = null;
         pendingState = null;
         status = XeriWindowTransitionStatus.Idle;
      }

   #endregion

   }
}
