/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SpotlightRequestStack.cs
수정일 : 2026-09-17
# 설명
backend별 중첩 Spotlight 요청을 최신 표시 우선으로 합성하고 해제 시 이전 요청을 복원한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Spotlight 요청과 backend 표시 수명을 소유한다.
    /// </summary>
    // ============================================================
    internal sealed class SpotlightRequestStack<TParams> : IDisposable
    where TParams : class
    {

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// 한 backend에 적용할 순서화된 표시 요청.
        /// </summary>
        // ============================================================
        private sealed class Request
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 표시 호출 인자.
            /// </summary>
            // ------------------------------------------------------------
            public TParams Params { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Spotlight 요청을 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public Request(TParams parameters) : base()
            {
                Params = parameters ?? throw new ArgumentNullException(nameof(parameters));
            }
        }

    #endregion

    #region 필드

        private readonly Dictionary<ISpotlightDriver<TParams>, List<Request>> requests =
            new Dictionary<ISpotlightDriver<TParams>, List<Request>>();
        private bool isDisposed = false;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// backend에 Spotlight 요청을 표시하고 소유 Lease를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public Lease Show
        (
            ISpotlightDriver<TParams> driver,
            TParams parameters
        )
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(SpotlightRequestStack<TParams>));
            }

            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            if (!driver.IsValid)
            {
                throw new InvalidOperationException("Spotlight backend가 유효하지 않습니다.");
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (!requests.TryGetValue(driver, out var list))
            {
                list = new List<Request>();
                requests.Add(driver, list);
            }

            var request = new Request(parameters);
            list.Add(request);

            try
            {
                // backend callback보다 먼저 요청을 공개해 재진입 종료가 현재 표시를 숨길 수 있게 한다.
                driver.Show(parameters);

                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(SpotlightRequestStack<TParams>));
                }

                return new Lease(() => Release(driver, request));
            }
            catch (Exception exception)
            {
                if (isDisposed)
                {
                    throw;
                }

                try
                {
                    Release(driver, request);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        "Spotlight 표시와 요청 롤백이 모두 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 요청을 제거하고 이전 최신 요청 또는 숨김 상태를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Release
        (
            ISpotlightDriver<TParams> driver,
            Request request
        )
        {
            if (isDisposed) return;
            if (!requests.TryGetValue(driver, out var list)) return;

            var index = list.IndexOf(request);

            if (index < 0) return;

            var wasTop = index == list.Count - 1;
            var nextParams = wasTop && index > 0
                ? list[index - 1].Params
                : null;

            list.RemoveAt(index);

            if (list.Count == 0)
            {
                ReleaseDriver(driver);
                return;
            }

            if (!wasTop) return;

            driver.Show(nextParams);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> backend의 마지막 Spotlight 표시를 종료한다.
        /// <br/> 전체 종료에서는 원본 목록을 순회한 뒤 한 번에 비우도록 등록 제거를 생략한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ReleaseDriver
        (
            ISpotlightDriver<TParams> driver,
            bool removeFromRequests = true
        )
        {
            if (removeFromRequests)
            {
                requests.Remove(driver);
            }

            driver.Hide();
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 backend의 Spotlight를 숨기고 표시 요청을 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            var errors = new List<Exception>();

            try
            {
                // 종료 상태에서는 Lease 해제가 목록을 변경하지 않으므로 원본 backend 목록을 직접 순회한다.
                foreach (var driver in requests.Keys)
                {
                    try
                    {
                        ReleaseDriver(driver, removeFromRequests: false);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }
            finally
            {
                requests.Clear();
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Spotlight 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
