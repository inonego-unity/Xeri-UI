/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSplash.cs
수정일 : 2026-11-07

# 설명
VisualElement를 기존 Presentation Target에 연결하고 SplashSession에
Presentation Alpha, hierarchy와 Layer Lease 수명을 이전한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// UI Toolkit Splash의 Presentation 조립 진입점.
    /// </summary>
    // ============================================================
    public static class UITKSplash
    {

    #region 표시

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/>분리된 VisualElement를 대상 Layer에 연결하고 Splash를 재생한다.
        /// <br/>종료 시 계층을 분리하고 변경한 opacity를 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static SplashSession Show
        (
            UIContext context,
            PresentationTarget target,
            VisualElement root,
            SplashParams parameters,
            VisualElement fadeTarget = null,
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

            fadeTarget ??= root;

            if (!ReferenceEquals(root, fadeTarget) && !root.Contains(fadeTarget))
            {
                throw new ArgumentException("Fade 대상은 Splash Root 또는 그 자식이어야 합니다.", nameof(fadeTarget));
            }

            PresentationLayerLease layerLease = null;
            Lease attachment = null;
            Lease restore = null;
            SplashSession session = null;

            try
            {
                layerLease = context.AcquireLayer(target);

                if (layerLease.Layer is not IPresentationLayerDriver<VisualElement> layer)
                {
                    throw new InvalidOperationException($"Splash Target '{target}'은 UI Toolkit Layer가 아닙니다.");
                }

                // AttachToPanelEvent가 원본 스타일을 변경하기 전에 호출자 상태를 보관한다.
                var originalOpacity = fadeTarget.style.opacity;
                var originalDisplay = root.style.display;
                restore = new Lease
                (
                    () =>
                    {
                        try
                        {
                            fadeTarget.style.opacity = originalOpacity;
                        }
                        finally
                        {
                            root.style.display = originalDisplay;
                        }
                    }
                );

                // 목표 Alpha는 SplashParams 소유이므로 USS 계산 시점을 추측하지 않는다.
                attachment = UITKPresentationLayer.Attach(layer, root);
                fadeTarget.style.opacity = 0f;
                root.style.display = DisplayStyle.Flex;

                session = new SplashSession
                (
                    runtime,
                    new UITKPresentation(fadeTarget).Alpha,
                    parameters,
                    restore,
                    attachment,
                    layerLease,
                    onEnded
                );

                restore = null;
                attachment = null;
                layerLease = null;

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

                throw errors.CreateAggregateException("Splash 조립과 롤백이 실패했습니다.");
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
