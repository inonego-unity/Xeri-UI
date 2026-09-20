/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriWindowStateTransitioner.cs
수정일 : 2026-09-20

# 설명
Xeri window 상태 전환 transitioner 테스트.

# 테스트 구성
 I: Immediate transitioner
 U: UITK transitioner
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window
{
   // ============================================================
   /// <summary>
   /// Xeri window 상태 전환 transitioner 테스트 클래스.
   /// </summary>
   // ============================================================
   public class TEST_XeriWindowStateTransitioner
   {

   #region 헬퍼

      // ============================================================
      /// <summary>
      /// 테스트용 window driver.
      /// </summary>
      // ============================================================
      private sealed class TestWindowDriver : IXeriWindowDriver
      {
         public Vector2 Pos { get; set; } = new Vector2(10f, 20f);
         public Vector2 Size { get; set; } = new Vector2(200f, 120f);
         public XeriWindowState State { get; set; } = XeriWindowState.Normal;
         public XeriWindowState VisualState { get; private set; } = XeriWindowState.Normal;
         public bool Visible { get; private set; } = true;
         public bool MaximizedBoundsApplied { get; private set; } = false;

         public Rect Bounds
         {
            get => new Rect(Pos, Size);
            set
            {
               Pos = value.position;
               Size = value.size;
            }
         }

         public void SetVisible(bool visible)
         {
            Visible = visible;
         }

         public void CommitState(XeriWindowState state)
         {
            State = state;
            ApplyVisualState(state);
         }

         public void ApplyVisualState(XeriWindowState state)
         {
            VisualState = state;
         }

         public void ApplyBounds(Rect bounds)
         {
            Bounds = bounds;
         }

         public void ApplyMaximizedBounds()
         {
            MaximizedBoundsApplied = true;
         }
      }

      // ============================================================
      /// <summary>
      /// 테스트용 animator.
      /// </summary>
      // ============================================================
      private sealed class TestAnimator : IXeriWindowStateAnimator
      {
         public bool IsRunning { get; private set; } = false;
         public XeriWindowStateTransitionContext LastContext { get; private set; }
         public Action CompleteAction { get; private set; } = null;
         public Action<Exception> ErrorAction { get; private set; } = null;

         public void Play
         (
            XeriWindowStateTransitionContext context,
            Action onComplete,
            Action<Exception> onError
         )
         {
            IsRunning = true;
            LastContext = context;
            CompleteAction = () =>
            {
               IsRunning = false;
               onComplete?.Invoke();
            };
            ErrorAction = exception =>
            {
               IsRunning = false;
               onError?.Invoke(exception);
            };
         }

         public void Cancel(bool restoreVisual)
         {
            IsRunning = false;
            CompleteAction = null;
            ErrorAction = null;
         }
      }

   #endregion

   #region I-1: Immediate

      // ------------------------------------------------------------
      /// <summary>
      /// Immediate transitioner는 Minimize를 즉시 완료하고 숨김 상태를 반영한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_XeriImmediateWindowStateTransitioner_Minimize_즉시_숨김()
      {
         var driver = new TestWindowDriver();
         var transitioner = new XeriImmediateWindowStateTransitioner();
         var completed = false;

         transitioner.Transition
         (
            new XeriWindowStateTransitionRequest
            {
               Driver = driver,
               PreviousState = XeriWindowState.Normal,
               NextState = XeriWindowState.Minimized,
               OnComplete = () => completed = true,
            }
         );

         Assert.AreEqual(XeriWindowState.Minimized, driver.State);
         Assert.IsFalse(driver.Visible);
         Assert.IsTrue(completed);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Immediate transitioner는 Maximize bounds primitive를 호출한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_XeriImmediateWindowStateTransitioner_Maximize_Bounds_반영()
      {
         var driver = new TestWindowDriver();
         var transitioner = new XeriImmediateWindowStateTransitioner();

         transitioner.Transition
         (
            new XeriWindowStateTransitionRequest
            {
               Driver = driver,
               PreviousState = XeriWindowState.Normal,
               NextState = XeriWindowState.Maximized,
            }
         );

         Assert.AreEqual(XeriWindowState.Maximized, driver.State);
         Assert.IsTrue(driver.Visible);
         Assert.IsTrue(driver.MaximizedBoundsApplied);
      }

   #endregion

   #region U-1: UITK

      // ------------------------------------------------------------
      /// <summary>
      /// UITK transitioner는 animation 완료 전까지 driver 완료 상태와 pending 상태를 분리한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_XeriUITKWindowStateTransitioner_Animation_완료전_DriverState_유지()
      {
         var driver = new TestWindowDriver();
         var animator = new TestAnimator();
         var transitioner = new XeriUITKWindowStateTransitioner(animator);

         transitioner.Transition
         (
            new XeriWindowStateTransitionRequest
            {
               Driver = driver,
               PreviousState = XeriWindowState.Normal,
               NextState = XeriWindowState.Maximized,
            }
         );

         Assert.IsTrue(transitioner.IsRunning);
         Assert.AreEqual(XeriWindowState.Maximized, transitioner.PendingState);
         Assert.AreEqual(XeriWindowState.Normal, driver.State);
         Assert.AreEqual(XeriWindowState.Maximized, driver.VisualState);
         Assert.IsTrue(driver.Visible);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// UITK transitioner는 animation 완료 후 최종 primitive와 완료 callback을 호출한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_XeriUITKWindowStateTransitioner_Animation_완료후_State_확정()
      {
         var driver = new TestWindowDriver();
         var animator = new TestAnimator();
         var transitioner = new XeriUITKWindowStateTransitioner(animator);
         var completed = false;

         transitioner.Transition
         (
            new XeriWindowStateTransitionRequest
            {
               Driver = driver,
               PreviousState = XeriWindowState.Normal,
               NextState = XeriWindowState.Maximized,
               OnComplete = () => completed = true,
            }
         );

         animator.CompleteAction.Invoke();

         Assert.IsFalse(transitioner.IsRunning);
         Assert.IsNull(transitioner.PendingState);
         Assert.IsTrue(driver.MaximizedBoundsApplied);
         Assert.AreEqual(XeriWindowState.Maximized, driver.State);
         Assert.AreEqual(XeriWindowState.Maximized, driver.VisualState);
         Assert.IsTrue(completed);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// UITK transitioner는 animation 실패 시 이전 완료 상태로 rollback한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_XeriUITKWindowStateTransitioner_Animation_실패시_State_Rollback()
      {
         var driver = new TestWindowDriver();
         var animator = new TestAnimator();
         var transitioner = new XeriUITKWindowStateTransitioner(animator);
         var errorRaised = false;

         transitioner.Transition
         (
            new XeriWindowStateTransitionRequest
            {
               Driver = driver,
               PreviousState = XeriWindowState.Normal,
               NextState = XeriWindowState.Maximized,
               OnError = _ => errorRaised = true,
            }
         );

         animator.ErrorAction.Invoke(new Exception("animation fail"));

         Assert.IsFalse(transitioner.IsRunning);
         Assert.IsNull(transitioner.PendingState);
         Assert.AreEqual(XeriWindowState.Normal, driver.State);
         Assert.AreEqual(XeriWindowState.Normal, driver.VisualState);
         Assert.IsFalse(driver.MaximizedBoundsApplied);
         Assert.IsTrue(driver.Visible);
         Assert.IsTrue(errorRaised);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// UITK transitioner는 animation 비활성 요청을 즉시 완료한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_XeriUITKWindowStateTransitioner_Animation_비활성_즉시완료()
      {
         var driver = new TestWindowDriver();
         var animator = new TestAnimator();
         var transitioner = new XeriUITKWindowStateTransitioner(animator);
         var completed = false;

         transitioner.Transition
         (
            new XeriWindowStateTransitionRequest
            {
               Driver = driver,
               PreviousState = XeriWindowState.Maximized,
               NextState = XeriWindowState.Normal,
               TargetBounds = new Rect(30f, 40f, 220f, 140f),
               Animate = false,
               OnComplete = () => completed = true,
            }
         );

         Assert.IsFalse(transitioner.IsRunning);
         Assert.IsNull(transitioner.PendingState);
         Assert.IsFalse(animator.IsRunning);
         Assert.AreEqual(XeriWindowState.Normal, driver.State);
         Assert.AreEqual(new Vector2(30f, 40f), driver.Pos);
         Assert.AreEqual(new Vector2(220f, 140f), driver.Size);
         Assert.IsTrue(completed);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 같은 target 상태 요청은 IgnoreSameTarget 정책에서 중복 실행되지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_XeriUITKWindowStateTransitioner_동일_Target_중복요청_무시()
      {
         var driver = new TestWindowDriver();
         var animator = new TestAnimator();
         var transitioner = new XeriUITKWindowStateTransitioner(animator);

         transitioner.Transition
         (
            new XeriWindowStateTransitionRequest
            {
               Driver = driver,
               PreviousState = XeriWindowState.Normal,
               NextState = XeriWindowState.Maximized,
               InterruptPolicy = XeriWindowTransitionInterruptPolicy.IgnoreSameTarget,
            }
         );
         var result = transitioner.Transition
         (
            new XeriWindowStateTransitionRequest
            {
               Driver = driver,
               PreviousState = XeriWindowState.Normal,
               NextState = XeriWindowState.Maximized,
               InterruptPolicy = XeriWindowTransitionInterruptPolicy.IgnoreSameTarget,
            }
         );

         Assert.IsFalse(result);
      }

   #endregion

   #region U-2: Animator

      // ------------------------------------------------------------
      /// <summary>
      /// UITK animator는 비활성 옵션에서 즉시 완료된다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_XeriUITKWindowStateAnimator_Disabled_즉시완료()
      {
         var panel = new XeriWindowPanel();
         var driver = new TestWindowDriver();
         var animator = new XeriUITKWindowStateAnimator
         (
            panel,
            XeriWindowAnimationOptions.Immediate()
         );
         var completed = false;

         animator.Play
         (
            new XeriWindowStateTransitionContext
            (
               new XeriWindowStateTransitionRequest
               {
                  Driver = driver,
                  PreviousState = XeriWindowState.Normal,
                  NextState = XeriWindowState.Minimized,
               }
            ),
            () => completed = true,
            _ => {}
         );

         Assert.IsFalse(animator.IsRunning);
         Assert.IsTrue(completed);
      }

   #endregion

   }
}
