/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIContextAuthorityCoordinator.cs
수정일 : 2026-11-08

# 설명
Context Base/Override 선택과 경로 권한 적용, 실패 복구 및 재진입 상태를 관리한다.
UIRuntime이 소유하는 Coordinator이며 Input·Focus·Cursor 권한을 중복 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Context Authority 경로의 적용·반환과 안정화 이후 복구를 소유한다.
    /// </summary>
    // ============================================================
    internal sealed class UIContextAuthorityCoordinator
    {

    #region 필드

        private readonly UIRuntime runtime;
        private readonly InputSystemScreenInputDriver inputDriver;
        private readonly List<UIContext> contextOverrides = new();
        private readonly List<UIContext> activeContextPath = new();
        private readonly List<UIContext> authorityCleanupContexts = new();
        private readonly List<UIContext> nextContextPath = new();
        private readonly List<UIContext> authorityCleanupCandidates = new();

        private UIContext baseContext = null;
        private bool isRefreshingContextAuthority = false;
        private bool contextAuthorityRefreshRequested = false;
        private bool contextAuthorityRecoveryPending = false;

    #endregion

    #region 생성과 종료

        internal UIContextAuthorityCoordinator
        (
            UIRuntime runtime,
            InputSystemScreenInputDriver inputDriver
        )
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.inputDriver = inputDriver ?? throw new ArgumentNullException(nameof(inputDriver));
        }

        internal void SetInitialBase(UIContext context)
        {
            baseContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 종료 시 권한 상태를 비우되 진행 중인 재진입 순회는 보호한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ResetFromRuntime()
        {
            baseContext = null;
            contextOverrides.Clear();
            activeContextPath.Clear();
            authorityCleanupContexts.Clear();
            contextAuthorityRecoveryPending = false;

            if (!isRefreshingContextAuthority)
            {
                nextContextPath.Clear();
                authorityCleanupCandidates.Clear();
            }
        }

    #endregion

    #region 컨텍스트 권한 선택과 해제

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Context를 peer activation의 Base Context로 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SetBaseContext(UIContext context)
        {
            ValidateContextAuthorityTarget(context);

            if (ReferenceEquals(baseContext, context)) return;

            var previous = baseContext;
            baseContext = context;

            try
            {
                RefreshContextAuthority();
            }
            catch (Exception exception)
            {
                // 콜백이 더 새로운 Base를 선택했다면 실패한 이전 요청만 보고한다.
                if (!ReferenceEquals(baseContext, context))
                {
                    throw;
                }

                baseContext = previous;

                try
                {
                    RefreshContextAuthority();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Base Context authority 적용과 topology 롤백이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Context를 Context Override Stack top으로 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        internal Lease PushContextOverride(UIContext context)
        {
            ValidateContextAuthorityTarget(context);

            if (contextOverrides.Contains(context))
            {
                throw new InvalidOperationException("같은 UIContext를 Override Stack에 중복 추가할 수 없습니다.");
            }

            contextOverrides.Add(context);

            try
            {
                RefreshContextAuthority();
            }
            catch (Exception exception)
            {
                contextOverrides.Remove(context);

                try
                {
                    RefreshContextAuthority();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException
                    (
                        "Context Override authority 적용과 topology 롤백이 모두 실패했습니다.",
                        exception,
                        rollbackException
                    );
                }

                throw;
            }

            return new Lease(() => ReleaseContextOverride(context));
        }

        private void ReleaseContextOverride(UIContext context)
        {
            var index = contextOverrides.IndexOf(context);
            if (index < 0) return;

            contextOverrides.RemoveAt(index);
            RefreshContextAuthority();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 종료할 Context subtree를 Base/Override authority에서
        /// <br/> 제거하고 필요하면 살아 있는 Parent를 Base로 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ReleaseContextAuthority
        (
            UIContext context,
            bool restoreAuthority
        )
        {
            if (context == null) return;

            for (var index = contextOverrides.Count - 1; index >= 0; index--)
            {
                if (context.Contains(contextOverrides[index]))
                {
                    contextOverrides.RemoveAt(index);
                }
            }

            if (baseContext != null && context.Contains(baseContext))
            {
                baseContext = restoreAuthority
                    ? FindAuthorityParent(context.Parent)
                    : null;
            }

            RefreshContextAuthority();
        }

        internal bool IsEffectiveContext(UIContext context)
        {
            return context != null && ReferenceEquals(GetActiveContext(), context);
        }

        internal bool IsContextOnActivePath(UIContext context)
        {
            return context != null && activeContextPath.Contains(context);
        }

    #endregion

    #region 컨텍스트 권한 적용과 복구

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 정책이 안정화되면 중단됐던 Context 권한 복원을 재시도한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void HandleInputPolicySettled()
        {
            if (!contextAuthorityRecoveryPending || !runtime.IsInitialized) return;

            contextAuthorityRecoveryPending = false;
            RefreshContextAuthority();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Context Authority 변경을 한 번에 하나씩 적용한다.
        /// <br/> 적용 중 콜백이 topology를 바꾸면 중첩 실행 대신 최종 경로를 다시 계산한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal void RefreshContextAuthority()
        {
            contextAuthorityRefreshRequested = true;

            if (isRefreshingContextAuthority) return;

            isRefreshingContextAuthority = true;
            var errors = new ExceptionBuffer();

            try
            {
                while (contextAuthorityRefreshRequested && !runtime.IsReleased)
                {
                    contextAuthorityRefreshRequested = false;
                    ApplyContextAuthority(ref errors);
                }
            }
            finally
            {
                isRefreshingContextAuthority = false;

                if (runtime.IsReleased)
                {
                    nextContextPath.Clear();
                    authorityCleanupCandidates.Clear();
                }
            }

            if (errors.Count == 0) return;

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw errors.CreateAggregateException
            (
                "UI Runtime Context authority 갱신 중 하나 이상의 적용이 실패했습니다."
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 마지막으로 요청된 Context 경로를 계산하고 이전 권한을 먼저 반환한다.
        /// <br/> 콜백이 topology를 변경했다면 오래된 경로의 적용을 중단한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplyContextAuthority(ref ExceptionBuffer errors)
        {
            nextContextPath.Clear();
            var effective = GetRequestedAuthorityContext();
            effective?.AppendPathTo(nextContextPath);
            var cursorContext = FindCursorPolicyContext(nextContextPath);

            // 새 경로의 입력 기여가 적용 불가능하다면 기존 경로의 권한을 먼저 반환하지 않는다.
            // 최종 실제 적용에서도 검사를 반복해 외부 callback이 만든 상태 변화를 검증한다.
            for (var index = 0; index < nextContextPath.Count; index++)
            {
                if (contextAuthorityRefreshRequested || runtime.IsReleased) return;

                try
                {
                    nextContextPath[index].ValidateInputAuthorityActivation();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                    return;
                }
            }

            // 실패했던 반환은 성공할 때까지 추적하고, 이전 논리 경로도 정리 대상으로 합친다.
            authorityCleanupCandidates.Clear();
            AddUniqueContexts(authorityCleanupCandidates, authorityCleanupContexts);
            AddUniqueContexts(authorityCleanupCandidates, activeContextPath);

            for (var index = 0; index < authorityCleanupCandidates.Count; index++)
            {
                if (contextAuthorityRefreshRequested || runtime.IsReleased) return;

                var context = authorityCleanupCandidates[index];

                if (nextContextPath.Contains(context))
                {
                    authorityCleanupContexts.Remove(context);
                    continue;
                }

                try
                {
                    context.SetAuthorityState(false, false, false);
                    authorityCleanupContexts.Remove(context);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);

                    if (!context.IsDisposed && !context.IsDisposing)
                    {
                        if (!authorityCleanupContexts.Contains(context))
                        {
                            authorityCleanupContexts.Add(context);
                        }
                    }
                    else
                    {
                        authorityCleanupContexts.Remove(context);
                    }
                }
            }

            if (contextAuthorityRefreshRequested || runtime.IsReleased) return;

            // 이전 Context의 권한 반환이 완료되기 전에는 다음 경로에 입력·Focus
            // 소유권을 부여하지 않는다. 실패한 반환은 다음 Refresh에서 먼저 재시도한다.
            if (authorityCleanupContexts.Count > 0) return;

            activeContextPath.Clear();
            activeContextPath.AddRange(nextContextPath);

            for (var index = 0; index < activeContextPath.Count; index++)
            {
                if (contextAuthorityRefreshRequested || runtime.IsReleased) return;

                var context = activeContextPath[index];

                try
                {
                    context.SetAuthorityState
                    (
                        true,
                        ReferenceEquals(context, effective),
                        ReferenceEquals(context, cursorContext)
                    );
                }
                catch (Exception exception)
                {
                    errors.Add(exception);

                    // 적용 중 열린 Batch는 지금 롤백까지 막을 수 있으므로 안정화 뒤에 복구한다.
                    if (inputDriver != null && inputDriver.IsPolicyDeferred)
                    {
                        contextAuthorityRecoveryPending = true;
                    }

                    // 일부만 적용된 경로를 정리하고, 실패한 반환은 후속 갱신에 남긴다.
                    AbandonFailedContextActivation(ref errors);
                    return;
                }
            }

            contextAuthorityRecoveryPending = false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패한 활성화 경로의 backend 권한을 반환하고 미완료 반환을 추적한다.
        /// </summary>
        // ------------------------------------------------------------
        private void AbandonFailedContextActivation(ref ExceptionBuffer errors)
        {
            // 종료·입력 콜백이 활성 경로를 변경할 수 있으므로 현재 반환 대상을 고정한다.
            var abandonedPath = new List<UIContext>(activeContextPath);
            AddUniqueContexts(authorityCleanupContexts, abandonedPath);

            // 부분적으로 획득한 Input·Cursor·Focus도 반대 순서로 반환한다.
            for (var index = abandonedPath.Count - 1; index >= 0; index--)
            {
                if (runtime.IsReleased) break;

                var context = abandonedPath[index];

                try
                {
                    context.SetAuthorityState(false, false, false);
                    authorityCleanupContexts.Remove(context);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);

                    if (context.IsDisposed || context.IsDisposing)
                    {
                        authorityCleanupContexts.Remove(context);
                    }
                }
            }

            activeContextPath.Clear();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Context reference 목록을 중복 없이 대상 목록에 병합한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AddUniqueContexts
        (
            List<UIContext> target,
            IReadOnlyList<UIContext> source
        )
        {
            for (var index = 0; index < source.Count; index++)
            {
                var context = source[index];
                if (context == null || target.Contains(context)) continue;

                target.Add(context);
            }
        }

    #endregion

    #region 컨텍스트 권한 조회와 검증

        private UIContext GetRequestedAuthorityContext()
        {
            return contextOverrides.Count > 0
                ? contextOverrides[contextOverrides.Count - 1]
                : baseContext;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 활성 경로 끝에서 실제 Focus 권한을 보유한 Context만 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        internal UIContext GetActiveContext()
        {
            var count = activeContextPath.Count;
            if (count == 0) return null;

            var context = activeContextPath[count - 1];
            return context != null && context.HasFocusAuthority ? context : null;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Active Context path에서 가장 깊은 Screen Stack Cursor owner를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static UIContext FindCursorPolicyContext(IReadOnlyList<UIContext> path)
        {
            for (var index = path.Count - 1; index >= 0; index--)
            {
                var context = path[index];

                if (context != null && context.Screens.HasCursorPolicySource)
                {
                    return context;
                }
            }

            return null;
        }

        private void ValidateContextAuthorityTarget(UIContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            runtime.ThrowIfUnavailable();

            if (context.IsDisposing || context.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(context));
            }

            if (runtime.Main == null || !runtime.Main.Contains(context))
            {
                throw new InvalidOperationException("현재 UIRuntime Context Tree 밖의 Context에 authority를 줄 수 없습니다.");
            }
        }

        private static UIContext FindAuthorityParent(UIContext current)
        {
            while (current != null)
            {
                if (!current.IsDisposing && !current.IsDisposed)
                {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }

    #endregion

    }
}
