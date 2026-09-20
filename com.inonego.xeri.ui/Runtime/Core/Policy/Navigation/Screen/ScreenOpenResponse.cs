/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenOpenResponse.cs
수정일 : 2026-09-17
# 설명
Screen Open 시작 결과와 성공 Session 또는 실패 정보를 불변 응답으로 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Screen Open 시작 응답.
    /// </summary>
    // ============================================================
    public readonly struct ScreenOpenResponse
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Open 결과 종류.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenOpenKind Kind { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Open이 수락됐을 때 생성된 Session.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenSession Session { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 수락되지 않았을 때의 진단 메시지.
        /// </summary>
        // ------------------------------------------------------------
        public string Error { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Source 또는 Transition 시작 실패의 원본 예외.
        /// </summary>
        // ------------------------------------------------------------
        public Exception Exception { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Open 시작이 수락됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool Accepted => Kind == ScreenOpenKind.Accepted;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Open 응답을 생성하고 Kind별 불변식을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private ScreenOpenResponse
        (
            ScreenOpenKind kind,
            ScreenSession session,
            string error,
            Exception exception
        ) : this()
        {
            if (!Enum.IsDefined(typeof(ScreenOpenKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (kind == ScreenOpenKind.Accepted)
            {
                if (session == null)
                {
                    throw new ArgumentNullException(nameof(session));
                }

                if (!string.IsNullOrEmpty(error) || exception != null)
                {
                    throw new ArgumentException("수락 응답에는 오류 정보를 포함할 수 없습니다.");
                }
            }
            else
            {
                if (session != null)
                {
                    throw new ArgumentException("실패 응답에는 Session을 포함할 수 없습니다.", nameof(session));
                }

                if (string.IsNullOrWhiteSpace(error))
                {
                    throw new ArgumentException("실패 응답에는 오류 메시지가 필요합니다.", nameof(error));
                }
            }

            if ((kind == ScreenOpenKind.Rejected || kind == ScreenOpenKind.Cancelled) && exception != null)
            {
                throw new ArgumentException("거부·취소 응답에는 Exception을 포함할 수 없습니다.", nameof(exception));
            }

            if
            (
                (kind == ScreenOpenKind.SourceFailed || kind == ScreenOpenKind.TransitionFailed) &&
                exception == null
            )
            {
                throw new ArgumentNullException(nameof(exception));
            }

            Kind = kind;
            Session = session;
            Error = error ?? "";
            Exception = exception;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 수락 응답을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static ScreenOpenResponse Accept(ScreenSession session)
        {
            return new ScreenOpenResponse(ScreenOpenKind.Accepted, session, "", null);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 사전 조건 또는 정책 거부 응답을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static ScreenOpenResponse Reject(string error)
        {
            return new ScreenOpenResponse(ScreenOpenKind.Rejected, null, error, null);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// OnOpening 취소 응답을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static ScreenOpenResponse Cancel(string error)
        {
            return new ScreenOpenResponse(ScreenOpenKind.Cancelled, null, error, null);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Source 획득·Bind 실패 응답을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static ScreenOpenResponse SourceFailure
        (
            string error,
            Exception exception
        )
        {
            return new ScreenOpenResponse
            (
                ScreenOpenKind.SourceFailed,
                null,
                error,
                exception
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Transition 시작 실패 응답을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static ScreenOpenResponse TransitionFailure
        (
            string error,
            Exception exception
        )
        {
            return new ScreenOpenResponse
            (
                ScreenOpenKind.TransitionFailed,
                null,
                error,
                exception
            );
        }

    #endregion

    }
}
