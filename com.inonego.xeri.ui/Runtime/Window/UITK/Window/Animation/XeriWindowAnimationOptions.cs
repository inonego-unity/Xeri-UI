/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowAnimationOptions.cs
수정일 : 2026-09-20

# 설명
Xeri window 상태 전환 animation 옵션.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI.Window
{
   // ============================================================
   /// <summary>
   /// Xeri window 상태 전환 animation 옵션.
   /// </summary>
   // ============================================================
   [Serializable]
   public struct XeriWindowAnimationOptions
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// animation 사용 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Enabled;

      // ------------------------------------------------------------
      /// <summary>
      /// animation 시간.
      /// </summary>
      // ------------------------------------------------------------
      public float Duration;

      // ------------------------------------------------------------
      /// <summary>
      /// animation 중 최소 opacity.
      /// </summary>
      // ------------------------------------------------------------
      public float HiddenOpacity;

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 animation 옵션을 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      public static XeriWindowAnimationOptions Default()
      {
         return new XeriWindowAnimationOptions
         {
            Enabled = false,
            Duration = 0.14f,
            HiddenOpacity = 0f,
         };
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 즉시 완료 옵션을 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      public static XeriWindowAnimationOptions Immediate()
      {
         return new XeriWindowAnimationOptions
         {
            Enabled = false,
            Duration = 0f,
            HiddenOpacity = 0f,
         };
      }

   #endregion

   }
}
