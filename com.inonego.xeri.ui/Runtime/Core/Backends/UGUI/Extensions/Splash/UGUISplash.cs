/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUISplash.cs
수정일 : 2026-11-07

# 설명
RectTransform을 기존 UGUI Presentation Layer에 연결하고
SplashSession에 CanvasGroup Alpha와 Layer 연결 수명을 이전한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Runtime;
using System.Runtime.CompilerServices;

using UnityEngine;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UGUI Splash의 Presentation 조립 진입점.
    /// </summary>
    // ============================================================
    public static class UGUISplash
    {

    #region 소유권

        // Root의 부모가 외부에서 변경되더라도 동일 콘텐츠의 중복 소유를 허용하지 않는다.
        private static readonly ConditionalWeakTable<RectTransform, object> activeRoots = new();

    #endregion

    #region 표시

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/>분리된 RectTransform을 대상 Layer에 연결해 Splash를 재생한다.
        /// <br/>Fade 대상에는 CanvasGroup이 있어야 하며 종료 시 Alpha를 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static SplashSession Show
        (
            UIContext context,
            PresentationTarget target,
            RectTransform root,
            SplashParams parameters,
            RectTransform fadeTarget = null,
            Action<SplashResult> onEnded = null
        )
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var runtime = context.Runtime;

            if (!runtime.IsInitialized || runtime.IsReleasing || runtime.IsReleased)
            {
                throw new InvalidOperationException("Splash를 표시할 UI Runtime이 활성 상태가 아닙니다.");
            }

            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (root.parent != null)
            {
                throw new InvalidOperationException("Splash Root는 다른 계층에 연결되지 않아야 합니다.");
            }

            if (activeRoots.TryGetValue(root, out _))
            {
                throw new InvalidOperationException("이미 표시 중인 Splash Root는 다시 소유할 수 없습니다.");
            }

            fadeTarget = fadeTarget != null ? fadeTarget : root;

            if (!ReferenceEquals(root, fadeTarget) && !fadeTarget.IsChildOf(root))
            {
                throw new ArgumentException("Fade 대상은 Splash Root 또는 그 자식이어야 합니다.", nameof(fadeTarget));
            }

            var canvasGroup = fadeTarget.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                throw new InvalidOperationException("UGUI Splash Fade 대상에 CanvasGroup이 필요합니다.");
            }

            PresentationLayerLease layerLease = null;
            Lease attachment = null;
            Lease restore = null;
            SplashSession session = null;

            try
            {
                layerLease = context.AcquireLayer(target);

                if (layerLease.Layer is not IPresentationLayerDriver<RectTransform> layer ||
                    layer.Root == null)
                {
                    throw new InvalidOperationException($"Splash Target '{target}'은 UGUI Layer가 아닙니다.");
                }

                // 계층 이동보다 먼저 소유권을 등록해 외부 Reparent 후에도 중복 연결을 거부한다.
                // 부착 콜백이 변경하기 전에 호출자의 표시 상태를 보관한다.
                var originalAlpha = canvasGroup.alpha;
                var wasActive = root.gameObject.activeSelf;
                restore = new Lease
                (
                    () =>
                    {
                        try
                        {
                            if (canvasGroup != null)
                            {
                                canvasGroup.alpha = originalAlpha;
                            }
                        }
                        finally
                        {
                            if (root != null)
                            {
                                root.gameObject.SetActive(wasActive);
                            }
                        }
                    }
                );

                activeRoots.Add(root, new object());
                attachment = new Lease
                (
                    () =>
                    {
                        try
                        {
                            if (root != null)
                            {
                                root.SetParent(null, false);
                            }
                        }
                        finally
                        {
                            activeRoots.Remove(root);
                        }
                    }
                );
                root.SetParent(layer.Root, false);

                var presentation = new UGUIPresentation(fadeTarget.gameObject, canvasGroup);
                session = new SplashSession
                (
                    runtime,
                    presentation.Alpha,
                    parameters,
                    restore,
                    attachment,
                    layerLease,
                    onEnded
                );

                restore = null;
                attachment = null;
                layerLease = null;

                // 부착 콜백이 활성 상태를 바꿀 수 있으므로 재생 시점의 실제 상태를 사용한다.
                // wasActive는 종료 시 원상복원에만 사용한다.
                if (!root.gameObject.activeSelf)
                {
                    presentation.Alpha.Base = 0f;
                    root.gameObject.SetActive(true);
                }

                session.Start();
                return session;
            }
            catch (Exception exception)
            {
                var errors = new ExceptionBuffer(exception);
                TryRelease(session, ref errors);
                TryRelease(attachment, ref errors);
                TryRelease(restore, ref errors);
                TryRelease(layerLease, ref errors);

                if (errors.Count == 1)
                {
                    throw;
                }

                throw errors.CreateAggregateException("UGUI Splash 조립과 롤백이 실패했습니다.");
            }
        }

        private static void TryRelease(IDisposable lifetime, ref ExceptionBuffer errors)
        {
            if (lifetime == null)
            {
                return;
            }

            try
            {
                lifetime.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

    #endregion

    }
}
