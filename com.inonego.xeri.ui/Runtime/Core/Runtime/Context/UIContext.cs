/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UIContext.cs
수정일 : 2026-09-17
# 설명
독립된 Screen Registry, Screen Stack, Modal Stack과 Focus 기록을 소유한다.
Parent가 Child Context의 수명을 재귀적으로 소유하며 Runtime 전역 backend는 비소유로 공유한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 독립 UI 상태와 재귀 Child 수명을 소유하는 Context.
    /// </summary>
    // ============================================================
    public sealed class UIContext : IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Context 종료가 진행 중인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposing { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// Context 종료가 완료됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Context가 실제 Focus Driver 적용 권한을 갖는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool HasFocus => owner.IsFocusedContext(this);

        // ------------------------------------------------------------
        /// <summary>
        /// Screen과 Overlay를 표시할 Presentation Layer Registry.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationLayerRegistry LayerRegistry { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Context의 Screen 등록 Registry.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenRegistry ScreenRegistry { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Context의 Screen 명령과 Stack Controller.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenController Screens { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Context의 Modal Stack Controller.
        /// </summary>
        // ------------------------------------------------------------
        public ModalController Modals { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Context를 직접 소유하는 Parent.
        /// </summary>
        // ------------------------------------------------------------
        internal UIContext Parent => parent;

        private UIContext parent = null;
        private readonly UIRuntime owner = null;
        private readonly FocusController focusController = null;
        private readonly List<UIContext> children = new List<UIContext>();

    #endregion

    #region 생성자

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Context 고정 Controller를 공용 backend와 지정 Layer Registry로 조립한다.
        /// <br/> Parent가 있는 Child는 명시적으로 Focus될 때까지 실제 Driver를 사용하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal UIContext
        (
            UIRuntime owner,
            UIContext parent,
            PresentationLayerRegistry layerRegistry,
            IPresentationTransitioner transitioner,
            IFocusDriver focusDriver,
            IScreenInputDriver inputDriver
        ) : base()
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.parent = parent;
            LayerRegistry = layerRegistry ?? throw new ArgumentNullException(nameof(layerRegistry));

            if (LayerRegistry.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(layerRegistry));
            }

            focusController = new FocusController
            (
                focusDriver ?? throw new ArgumentNullException(nameof(focusDriver))
            );
            ScreenRegistry = new ScreenRegistry(LayerRegistry);
            Screens = new ScreenController
            (
                ScreenRegistry,
                LayerRegistry,
                transitioner ?? throw new ArgumentNullException(nameof(transitioner)),
                focusController,
                inputDriver ?? throw new ArgumentNullException(nameof(inputDriver))
            );
            Modals = new ModalController();

            // Child는 자신의 Focus 기록만 준비하고 Runtime이 권한을 넘길 때까지 Driver 적용을 막는다.
            if (parent != null)
            {
                focusController.Unfocus(null);
            }

            Screens.Activate();
        }

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 같은 Runtime backend를 공유하고 독립 Controller 상태를 소유하는
        /// <br/> Child를 생성한다.
        /// <br/> Layer Registry를 생략하면 Parent와 같은 표시 공간을 사용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public UIContext CreateChild(PresentationLayerRegistry layerRegistry = null)
        {
            ThrowIfUnavailable();
            owner.ThrowIfContextCreationUnavailable();

            var selectedRegistry = layerRegistry ?? LayerRegistry;

            if (selectedRegistry.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(layerRegistry));
            }

            // 완전히 조립된 Child만 소유 목록에 공개해 생성 실패 시 부분 수명을 남기지 않는다.
            var child = new UIContext
            (
                owner,
                this,
                selectedRegistry,
                owner.Transitioner,
                owner.FocusDriver,
                owner.InputDriver
            );

            children.Add(child);
            return child;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이 Context의 Screen Focus 기록을 실제 Focus Driver에 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void Focus()
        {
            ThrowIfUnavailable();
            owner.FocusContext(this);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이 Context의 Focus 권한을 가장 가까운 살아 있는 Parent에 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void Unfocus()
        {
            ThrowIfUnavailable();
            owner.UnfocusContext(this);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 Top Screen 선택을 기록하고 실제 Focus Driver 적용을 중지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void SuspendFocus()
        {
            focusController.Unfocus(Screens.Top);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Top Screen 기록을 실제 Focus Driver에 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ResumeFocus()
        {
            focusController.Focus(Screens.Top);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 Top Screen 소유 범위의 안정화된 native Focus를 마지막 선택으로 기록한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void RecordFocus(object target)
        {
            if (IsDisposing || IsDisposed || !HasFocus) return;

            focusController.RecordCurrentFocus(Screens.Top, target);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 Focus 권한을 가진 Context의 Screen 선택 정책을 다시 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void RestoreFocus()
        {
            if (IsDisposing || IsDisposed || !HasFocus) return;

            // 안정화 뒤에도 native 선택이 없을 때만 마지막·기본·fallback 순서로 복원한다.
            focusController.Restore(Screens.Top);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Context가 이 Context Subtree에 속하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
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

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 종료가 Root Main과 전체 Child Tree를 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        internal List<Exception> DisposeFromRuntime()
        {
            return Release
            (
                removeFromParent: false,
                allowRoot: true,
                restoreFocus: false
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Parent가 소유 목록에서 먼저 제거한 Child를 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private List<Exception> DisposeFromParent()
        {
            return Release
            (
                removeFromParent: false,
                allowRoot: false,
                restoreFocus: true
            );
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Context를 소유 Tree에서 분리하고 Child와 고정 Controller를
        /// <br/> attempt-once로 정리한다.
        /// <br/> 시작된 종료는 결과와 관계없이 Terminal이며 전달 Registry는 해제하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private List<Exception> Release
        (
            bool removeFromParent,
            bool allowRoot,
            bool restoreFocus
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

            // 외부 callback 전에 Parent 소유 목록과 Focus 권한을 먼저 닫는다.
            if (removeFromParent)
            {
                parent.children.Remove(this);
            }

            try
            {
                owner.ReleaseContextFocus(this, restoreFocus);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            // Child callback이 형제를 정리해도 매번 남은 마지막 소유 항목만 가져온다.
            while (children.Count > 0)
            {
                var index = children.Count - 1;
                var child = children[index];
                children.RemoveAt(index);
                errors.AddRange(child.DisposeFromParent());
            }

            DisposeOwned(Modals, errors);

            try
            {
                errors.AddRange(Screens.Shutdown());
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

        // ------------------------------------------------------------
        /// <summary>
        /// Context가 새 명령을 받을 수 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfUnavailable()
        {
            if (IsDisposing || IsDisposed)
            {
                throw new ObjectDisposedException(nameof(UIContext));
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Context가 소유한 IDisposable 하나를 Terminal화하고 오류를 수집한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void DisposeOwned
        (
            IDisposable owned,
            List<Exception> errors
        )
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Child Context를 Parent에서 분리하고 전체 하위 UI 수명을 종료한다.
        /// <br/> Root Main은 Runtime이 최종 소유하므로 공개 Dispose를 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void Dispose()
        {
            if (IsDisposed || IsDisposing) return;

            var errors = Release
            (
                removeFromParent: true,
                allowRoot: false,
                restoreFocus: true
            );

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    "UI Context 종료 중 하나 이상의 정리가 실패했습니다.",
                    errors
                );
            }
        }

    #endregion

    }
}
