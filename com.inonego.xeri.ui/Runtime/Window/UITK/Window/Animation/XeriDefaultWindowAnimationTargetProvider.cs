/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriDefaultWindowAnimationTargetProvider.cs
수정일 : 2026-09-20

# 설명
Xeri window 기본 animation target bounds provider.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
   // ============================================================
   /// <summary>
   /// Xeri window 기본 animation target bounds provider.
   /// </summary>
   // ============================================================
   public sealed class XeriDefaultWindowAnimationTargetProvider : IXeriWindowAnimationTargetProvider
   {

   #region 필드

      private readonly VisualElement target = null;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Target element를 기준으로 provider를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public XeriDefaultWindowAnimationTargetProvider(VisualElement target) : base()
      {
         this.target = target;
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 상태 전환 목표 bounds를 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      public Rect GetTargetBounds(XeriWindowState nextState, Rect currentBounds)
      {
         if (nextState != XeriWindowState.Maximized) return currentBounds;
         if (target?.parent == null) return currentBounds;

         var width = target.parent.resolvedStyle.width;
         var height = target.parent.resolvedStyle.height;

         if (width <= 0f || height <= 0f)
         {
            width = target.parent.layout.width;
            height = target.parent.layout.height;
         }

         if (width <= 0f || height <= 0f) return currentBounds;

         return new Rect(0f, 0f, width, height);
      }

   #endregion

   }
}
