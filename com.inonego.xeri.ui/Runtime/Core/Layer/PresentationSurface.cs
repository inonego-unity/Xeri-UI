/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationSurface.cs
수정일 : 2026-10-03

# 설명
Embedded/shared-output Session의 Layer hierarchy containment와 root lifetime만 소유한다.
Layer registration·consumer lifetime·Focus binding은 PresentationSession이 단일하게 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    internal abstract class PresentationSurface : IDisposable
    {

    #region 내부 데이터

        private sealed class OwnedLayer
        {
            public PresentationPlanLayer Plan = null;
            public IPresentationLayerDriver Driver = null;
            public Action Release = null;
        }

    #endregion

    #region 필드

        public bool IsDisposed { get; private set; }
        public int Count => layers.Count;

        private readonly List<OwnedLayer> layers = new();

    #endregion

    #region 레이어 구성

        // ----------------------------------------------------------------------
        /// <summary>
        /// Embedded Layer hierarchy와 release lifetime을 Surface에 추가한다.
        /// </summary>
        // ----------------------------------------------------------------------
        protected void AddLayer
        (
            PresentationPlanLayer plan,
            IPresentationLayerDriver driver,
            Action release
        )
        {
            ThrowIfDisposed();

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            if (release == null)
            {
                throw new ArgumentNullException(nameof(release));
            }

            if (!driver.Validate(out var error))
            {
                throw new InvalidOperationException
                (
                    $"Presentation Surface Layer '{plan.LayerID}' 구성이 유효하지 않습니다. {error}"
                );
            }

            var owned = new OwnedLayer
            {
                Plan = plan,
                Driver = driver,
                Release = release,
            };

            try
            {
                layers.Add(owned);
                ApplyCurrentLayerOrder();
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };

                layers.Remove(owned);
                TryCleanup(release, errors);

                // Ordering 적용이 일부 진행된 뒤 실패했을 수 있으므로 생존 Layer 순서를 다시 확정한다.
                try
                {
                    ApplyCurrentLayerOrder();
                }
                catch (Exception rollbackException)
                {
                    errors.Add(rollbackException);
                }

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    $"Presentation Surface Layer '{plan.LayerID}' 추가와 롤백이 실패했습니다.",
                    errors
                );
            }
        }

        public bool TryGetLayer(string layerID, out IPresentationLayerDriver driver)
        {
            if (!IsDisposed && !string.IsNullOrWhiteSpace(layerID))
            {
                for (var index = 0; index < layers.Count; index++)
                {
                    if
                    (
                        string.Equals
                        (
                            layers[index].Plan.LayerID,
                            layerID,
                            StringComparison.Ordinal
                        )
                    )
                    {
                        driver = layers[index].Driver;
                        return true;
                    }
                }
            }

            driver = null;
            return false;
        }

        internal IPresentationLayerDriver GetLayerDriver(int index)
        {
            ThrowIfDisposed();
            return layers[index].Driver;
        }

        private void ApplyCurrentLayerOrder()
        {
            var drivers = new List<IPresentationLayerDriver>(layers.Count);

            for (var index = 0; index < layers.Count; index++)
            {
                drivers.Add(layers[index].Driver);
            }

            ApplyLayerOrder(drivers);
        }

    #endregion

    #region 백엔드 경계

        protected abstract void ApplyLayerOrder
        (
            IReadOnlyList<IPresentationLayerDriver> orderedDrivers
        );

        protected abstract void ReleaseSurfaceRoot();

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private static void TryCleanup
        (
            Action cleanup,
            ICollection<Exception> errors
        )
        {
            if (cleanup == null) return;

            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

    #endregion

    #region 수명 해제

        public void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
            var errors = new List<Exception>();

            for (var index = layers.Count - 1; index >= 0; index--)
            {
                TryCleanup(layers[index].Release, errors);
            }

            layers.Clear();
            TryCleanup(ReleaseSurfaceRoot, errors);

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    "Presentation Surface 해제가 실패했습니다.",
                    errors
                );
            }
        }

    #endregion

    }
}
