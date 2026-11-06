/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenInputSession.cs
수정일 : 2026-11-07
# 설명
한 Screen의 입력·Cursor 정책 수명과 active Context contribution 및 정책 적용 완료 경계를 표현한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 한 Screen의 입력 정책 수명.
    /// </summary>
    // ============================================================
    public sealed class ScreenInputSession
    {

    #region 내부 데이터

        private enum PolicyResolution { Pending, Applied, NotApplied }

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 정책 수명이 해제됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsReleased { get; private set; }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 입력 backend가 이 Session의 최초 정책 적용을 아직 확정하지 않았는지 여부.
        /// </summary>
        // ----------------------------------------------------------------------
        internal bool IsPolicyPending => policyResolution == PolicyResolution.Pending;
        internal bool IsPolicyApplied => policyResolution == PolicyResolution.Applied && !IsReleased;

        // ------------------------------------------------------------
        /// <summary>
        /// 닫기 입력 해제를 기다리는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsAwaitingRelease { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 해제 대기 중 이 Session의 Cursor 정책을 유지하는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool RetainsCursorWhileAwaitingRelease { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// Session을 생성한 Screen 정책.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenOptions Options { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Gameplay input policy 합성에 기여하는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsContributionEnabled { get; private set; } = true;

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 현재 Screen topology가 이 Session을 Cursor policy owner로 선택했는지 여부.
        /// </summary>
        // --------------------------------------------------------------------------------
        public bool IsCursorPolicyEnabled { get; private set; }

        private Action<ScreenInputSession, bool, bool> release = null;
        private Action<ScreenInputSession> contributionChanged = null;
        private Action validateContributionEnable = null;
        private Action onReleaseCompleted = null;
        private Action<bool> policyAppliedCallback = null;
        private PolicyResolution policyResolution = PolicyResolution.Applied;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// backend 해제 callback을 가진 입력 Session을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal ScreenInputSession
        (
            ScreenOptions options,
            Action<ScreenInputSession, bool, bool> release,
            Action<ScreenInputSession> contributionChanged = null,
            bool contributionEnabled = true,
            bool policyPending = false,
            Action validateContributionEnable = null
        ) : base()
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            this.release = release ?? throw new ArgumentNullException(nameof(release));
            this.contributionChanged = contributionChanged;
            this.validateContributionEnable = validateContributionEnable;
            IsContributionEnabled = contributionEnabled;
            policyResolution = policyPending ? PolicyResolution.Pending : PolicyResolution.Applied;
            IsCursorPolicyEnabled = false;
            IsReleased = false;
            IsAwaitingRelease = false;
            RetainsCursorWhileAwaitingRelease = false;
        }

    #endregion

    #region 입력 기여

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Context authority에 따라 global input contribution을 suspend/resume한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void SetContributionEnabled(bool enabled)
        {
            if (IsReleased || IsContributionEnabled == enabled) return;

            // 기존 Session의 재활성화는 최초 Acquire와 달리 정책 적용을 대기하지 않는다.
            if (enabled)
            {
                ValidateContributionActivation();
            }

            var previous = IsContributionEnabled;
            IsContributionEnabled = enabled;

            try
            {
                contributionChanged?.Invoke(this);
            }
            catch (Exception exception)
            {
                if (IsReleased)
                {
                    throw;
                }

                IsContributionEnabled = previous;

                try
                {
                    contributionChanged?.Invoke(this);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Screen input contribution 적용과 backend 롤백이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Context 권한 전환 전 입력 기여의 동기 활성화 가능 여부를 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ValidateContributionActivation()
        {
            if (IsReleased || IsContributionEnabled) return;

            validateContributionEnable?.Invoke();
        }

    #endregion

    #region Cursor 정책

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Screen Stack과 Context authority가 결정한 Cursor policy ownership을 갱신한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void SetCursorPolicyEnabled(bool enabled)
        {
            if (IsReleased || IsCursorPolicyEnabled == enabled) return;

            var previous = IsCursorPolicyEnabled;
            IsCursorPolicyEnabled = enabled;

            try
            {
                contributionChanged?.Invoke(this);
            }
            catch (Exception exception)
            {
                if (IsReleased)
                {
                    throw;
                }

                IsCursorPolicyEnabled = previous;

                try
                {
                    contributionChanged?.Invoke(this);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Screen Cursor policy 적용과 backend 롤백이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }
        }

    #endregion

    #region 정책 적용 완료

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 정책 적용 확정 뒤 한 번 이어서 수행할 작업을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void WhenPolicyApplied(Action<bool> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (!IsPolicyPending)
            {
                callback(policyResolution == PolicyResolution.Applied && !IsReleased);
                return;
            }

            if (policyAppliedCallback != null)
            {
                throw new InvalidOperationException("입력 정책 적용 완료 작업이 이미 등록됐습니다.");
            }

            policyAppliedCallback = callback;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// backend가 정책 적용 완료 또는 실패를 한 번 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void MarkPolicyApplied(bool applied)
        {
            if (!IsPolicyPending || IsReleased) return;

            policyResolution = applied ? PolicyResolution.Applied : PolicyResolution.NotApplied;
            var callback = policyAppliedCallback;
            policyAppliedCallback = null;
            callback?.Invoke(applied);
        }

    #endregion

    #region Session 반환과 종료

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 해제 대기 여부와 함께 backend에 Session 반환을 요청한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Release
        (
            bool waitForInputRelease,
            bool retainCursorWhileAwaitingRelease = true,
            Action onReleaseCompleted = null
        )
        {
            if (IsReleased) return;

            this.onReleaseCompleted += onReleaseCompleted;
            var retainCursor =
                retainCursorWhileAwaitingRelease &&
                IsCursorPolicyEnabled;

            try
            {
                release(this, waitForInputRelease, retainCursor);
            }
            catch
            {
                this.onReleaseCompleted -= onReleaseCompleted;
                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// backend가 Session을 입력 해제 대기 상태로 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void MarkAwaitingRelease(bool retainCursor)
        {
            if (IsReleased) return;

            IsAwaitingRelease = true;
            RetainsCursorWhileAwaitingRelease = retainCursor;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// backend가 Session 수명을 최종 해제 상태로 확정하고 필요할 때 완료 callback을 알린다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void MarkReleased(bool invokeCompletionCallback = true)
        {
            if (IsReleased) return;

            IsAwaitingRelease = false;
            RetainsCursorWhileAwaitingRelease = false;
            IsCursorPolicyEnabled = false;
            IsReleased = true;
            release = null;
            contributionChanged = null;
            validateContributionEnable = null;

            // 정책 적용 전에 수명이 끝난 Screen도 대기 중인 열기 절차를 정리할 수 있다.
            var policyCallback = policyAppliedCallback;
            policyAppliedCallback = null;
            policyResolution = PolicyResolution.NotApplied;
            var completionCallback = onReleaseCompleted;
            onReleaseCompleted = null;
            var errors = new ExceptionBuffer();

            if (policyCallback != null)
            {
                try
                {
                    policyCallback(false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (invokeCompletionCallback && completionCallback != null)
            {
                foreach (Action callback in completionCallback.GetInvocationList())
                {
                    try
                    {
                        callback.Invoke();
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException
            (
                "Screen input release 완료 callback 중 하나 이상이 실패했습니다."
            );
        }

    #endregion

    }
}
