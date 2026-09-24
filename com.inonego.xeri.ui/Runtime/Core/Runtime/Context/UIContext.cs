/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIContext.cs
수정일 : 2026-09-29

# 설명
독립 Screen/Modal/Focus/Input state와 PresentationSession 참조를 소유한다.
Context lifetime과 Base/Override authority를 분리하며 Parent가 Child Context lifetime을 재귀적으로 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using inonego;
using inonego.Xeri;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// 독립 UI state domain과 재귀 Child Context lifetime을 소유한다.
    /// </summary>
    // ======================================================================
    public sealed class UIContext : IDisposable
    {

    #region 필드

        public bool IsDisposing { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool IsEffective => owner.IsEffectiveContext(this);
        public bool IsOnActivePath => owner.IsContextOnActivePath(this);

        public PresentationSession Presentation { get; private set; }
        public ScreenRegistry ScreenRegistry { get; }
        public ScreenController Screens { get; }
        public ModalController Modals { get; }

        internal UIContext Parent => parent;

        private UIContext parent = null;

        internal int ChildCount => children.Count;

        private readonly List<UIContext> children = new();

        private readonly UIRuntime owner = null;
        private readonly FocusController focusController = null;

    #endregion

    #region 생성자

        internal UIContext
        (
            UIRuntime owner,
            UIContext parent,
            PresentationSession presentation,
            IPresentationTransitioner transitioner,
            IFocusDriver focusDriver,
            IScreenInputDriver inputDriver
        )
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.parent = parent;
            Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));

            if (Presentation.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(presentation));
            }

            focusController = new FocusController
            (
                focusDriver ?? throw new ArgumentNullException(nameof(focusDriver))
            );
            ScreenRegistry = new ScreenRegistry();
            Screens = new ScreenController
            (
                ScreenRegistry,
                Presentation,
                transitioner ?? throw new ArgumentNullException(nameof(transitioner)),
                focusController,
                inputDriver ?? throw new ArgumentNullException(nameof(inputDriver))
            );
            Screens.OnStackChanged += HandleScreenStackChanged;
            Modals = new ModalController(this);

            // Runtime이 active path를 계산하기 전까지 새 Child contribution은 global policy에 참여하지 않는다.
            if (parent != null)
            {
                focusController.Suspend();
                Screens.SetInputContributionEnabled(false);
            }

            Screens.Activate();
        }

    #endregion

    #region Common API

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 Context에 Screen 정책과 Source를 등록하고 등록 수명을 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public ScreenRegistrationHandle RegisterScreen
        (
            ScreenOptions options,
            IScreenSource source
        )
        {
            ThrowIfUnavailable();
            return ScreenRegistry.Register(options, source);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 현재 Context의 PresentationSession에서 지정 Presentation View를 획득한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public Lease<TView> AcquirePresentation<TView>
        (
            string presentationID,
            IPresentationSource<TView> source
        )
        where TView : class
        {
            ThrowIfUnavailable();
            return PresentationLease.Acquire(Presentation, presentationID, source);
        }

    #endregion

    #region Child Context

        public UIContext CreateChild()
        {
            return CreateChildCore(Presentation);
        }

        internal UIContext CreateChild(PresentationSession presentation)
        {
            if (presentation == null)
            {
                throw new ArgumentNullException(nameof(presentation));
            }

            if (!ReferenceEquals(presentation.Parent, Presentation))
            {
                throw new InvalidOperationException
                (
                    "Child UIContext는 Parent Context Presentation의 직접 Child Session만 사용할 수 있습니다."
                );
            }

            return CreateChildCore(presentation);
        }

        private UIContext CreateChildCore(PresentationSession presentation)
        {
            ThrowIfUnavailable();
            owner.ThrowIfContextCreationUnavailable();

            if (presentation.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(presentation));
            }

            var child = new UIContext
            (
                owner,
                this,
                presentation,
                owner.Transitioner,
                owner.FocusDriver,
                owner.InputDriver
            );
            children.Add(child);
            owner.RefreshContextAuthority();
            return child;
        }

    #endregion

    #region Context Authority

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Context를 peer activation의 Base Context로 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetBaseAuthority()
        {
            ThrowIfUnavailable();
            owner.SetBaseContext(this);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Context를 일시 Context Override Stack top으로 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        public Lease PushAuthorityOverride()
        {
            ThrowIfUnavailable();
            return owner.PushContextOverride(this);
        }

        internal void SetAuthorityState
        (
            bool onActivePath,
            bool isEffective,
            bool hasCursorAuthority
        )
        {
            if (IsDisposing || IsDisposed) return;

            Screens.SetInputContributionEnabled(onActivePath);
            Screens.SetCursorPolicyEnabled(hasCursorAuthority);

            if (isEffective)
            {
                focusController.Resume();
            }
            else
            {
                focusController.Suspend();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Screen Stack 변경을 Runtime의 Context/Cursor authority 계산에 반영한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void HandleScreenStackChanged()
        {
            if (IsDisposing || IsDisposed) return;

            owner.RefreshContextAuthority();
        }

    #endregion

    #region Focus Scope

        public FocusScopeHandle RegisterFocusScope(IFocusScope scope)
        {
            ThrowIfUnavailable();
            return focusController.RegisterScope(scope);
        }

        public void SetPrimaryFocusScope(FocusScopeHandle handle)
        {
            ThrowIfUnavailable();
            focusController.SetPrimary(handle);
        }

        public Lease PushFocusOverride(FocusScopeHandle handle)
        {
            ThrowIfUnavailable();
            return focusController.PushOverride(handle);
        }

        internal void HandleNativeFocusChanged(object target)
        {
            if (IsDisposing || IsDisposed || !IsEffective) return;

            focusController.HandleNativeFocusChanged(target);
        }

    #endregion

    #region Tree 조회

        internal bool Contains(UIContext context)
        {
            var current = context;

            while (current != null)
            {
                if (ReferenceEquals(current, this)) return true;

                current = current.parent;
            }

            return false;
        }

        internal void AppendPathTo(List<UIContext> target)
        {
            if (parent != null)
            {
                parent.AppendPathTo(target);
            }

            target.Add(this);
        }

    #endregion

    #region 해제

        internal List<Exception> DisposeFromRuntime()
        {
            return Release
            (
                removeFromParent: false,
                allowRoot: true,
                restoreAuthority: false
            );
        }

        private List<Exception> DisposeFromParent()
        {
            return Release
            (
                removeFromParent: false,
                allowRoot: false,
                restoreAuthority: true
            );
        }

        private List<Exception> Release
        (
            bool removeFromParent,
            bool allowRoot,
            bool restoreAuthority
        )
        {
            var errors = new List<Exception>();

            if (IsDisposed || IsDisposing) return errors;

            if (parent == null && !allowRoot)
            {
                throw new InvalidOperationException
                (
                    "Main UI Context는 UIRuntime.Shutdown으로만 종료할 수 있습니다."
                );
            }

            IsDisposing = true;

            if (removeFromParent)
            {
                parent.children.Remove(this);
            }

            try
            {
                owner.ReleaseContextAuthority(this, restoreAuthority);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            while (children.Count > 0)
            {
                var index = children.Count - 1;
                var child = children[index];
                children.RemoveAt(index);
                errors.AddRange(child.DisposeFromParent());
            }

            DisposeOwned(Modals, errors);
            Screens.OnStackChanged -= HandleScreenStackChanged;

            try
            {
                errors.AddRange(Screens.Shutdown());
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                focusController.Clear();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            DisposeOwned(ScreenRegistry, errors);
            parent = null;
            IsDisposing = false;
            IsDisposed = true;
            return errors;
        }

        private void ThrowIfUnavailable()
        {
            if (IsDisposing || IsDisposed)
            {
                throw new ObjectDisposedException(nameof(UIContext));
            }
        }

        private static void DisposeOwned(IDisposable owned, ICollection<Exception> errors)
        {
            try
            {
                owned.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

    #endregion

    #region IDisposable

        public void Dispose()
        {
            if (IsDisposed || IsDisposing) return;

            if (parent == null)
            {
                throw new InvalidOperationException
                (
                    "Main UI Context는 UIRuntime.Shutdown으로만 종료할 수 있습니다."
                );
            }

            var errors = Release
            (
                removeFromParent: true,
                allowRoot: false,
                restoreAuthority: true
            );

            if (errors.Count > 0)
            {
                throw new AggregateException("UI Context 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
