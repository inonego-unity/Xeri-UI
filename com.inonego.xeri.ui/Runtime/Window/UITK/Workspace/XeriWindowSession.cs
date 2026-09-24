/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowSession.cs
수정일 : 2026-10-05

# 설명
한 Xeri Window의 Registry 등록, 입력 binding, Panel과 View Source 반환 수명을 묶는다.
Application Window에서는 Child PresentationSession과 Child UIContext를 소유한다.
Simple Window에서는 Workspace UIContext에 등록한 persistent Focus record를 Window lifetime 동안 유지한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// 한 Window의 실행 수명과 공개 명령 진입점을 제공한다.
    /// </summary>
    // ============================================================
    public sealed class XeriWindowSession : IDisposable
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Registry에 등록된 Window Handle.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowHandle Handle { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Window 고유 상태와 명령을 관리하는 Controller.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowController Controller { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Window 기능 옵션.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowOptions Options => Controller.Options;

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Application Window가 소유한 Child UIContext. Simple Window는 null이다.
        /// <br/> 수명은 Window Session이 소유하며 직접 Dispose할 수 없다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public UIContext Context => childContext;

        private UIContext childContext = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 UIContext와 Window-local Surface를 사용하는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsApplication => childContext != null;

        internal FocusScopeHandle FocusScope => focusScope;

        private FocusScopeHandle focusScope = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Workspace 소유권이 종료됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsDisposed => owner == null;

        private XeriWindowWorkspace owner = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Window의 표시 Root. 종료 뒤에는 null을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public XeriWindowPanel Panel => panel;
        private XeriWindowPanel panel = null;
        private IXeriUIViewSource viewSource = null;
        private XeriUIViewScope viewScope = null;
        private VisualElement view = null;

        private PresentationSession childPresentation = null;

        private XeriWindowControlManipulator controlManipulator = null;
        private XeriWindowResizeManipulator resizeManipulator = null;
        private XeriWindowTitleBarManipulator titleBarManipulator = null;

        private bool isPanelFocusBound = false;
        private bool isControllerBound = false;
        private bool isReleased = false;
        private bool isContentReleased = false;
        private readonly List<IDisposable> children = new();

    #endregion

    #region 생성자

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Window 표시, 입력 binding과 선택적 View Source 수명을 하나의 Session으로 묶는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal XeriWindowSession
        (
            XeriWindowWorkspace owner,
            XeriWindowHandle handle,
            XeriWindowController controller,
            XeriWindowPanel panel,
            IXeriUIViewSource viewSource,
            XeriUIViewScope viewScope,
            VisualElement view,
            UIContext workspaceContext,
            XeriWindowApplicationOptions applicationOptions,
            IXeriWindowDragFactory dragFactory
        )
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.panel = panel ?? throw new ArgumentNullException(nameof(panel));
            this.viewSource = viewSource;
            this.viewScope = viewScope;
            this.view = view;

            try
            {
                var context = workspaceContext ?? throw new ArgumentNullException(nameof(workspaceContext));

                if (applicationOptions != null)
                {
                    InitializeApplication(context, applicationOptions);
                }
                else
                {
                    InitializeSimpleFocus(context);
                }

                Bind(dragFactory ?? throw new ArgumentNullException(nameof(dragFactory)));
            }
            catch (Exception exception)
            {
                var errors = ReleaseApplication();

                if (errors.Count == 0)
                {
                    throw;
                }

                errors.Insert(0, exception);
                throw new AggregateException("Window Session 조립과 롤백이 실패했습니다.", errors);
            }
        }

    #endregion

    #region 명령

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Window를 Workspace의 활성 Window로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        public void Focus()
        {
            owner?.Focus(this);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Focus를 바꾸지 않고 같은 StackLayer의 가장 앞으로 이동한다.
        /// </summary>
        // ------------------------------------------------------------
        public void BringToFront()
        {
            owner?.BringToFront(this);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Focus를 바꾸지 않고 같은 StackLayer의 가장 뒤로 이동한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SendToBack()
        {
            owner?.SendToBack(this);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window의 화면 정렬 StackLayer를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetStackLayer(XeriWindowStackLayer stackLayer)
        {
            owner?.SetStackLayer(this, stackLayer);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window 고유 Close transition을 요청한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Close()
        {
            if (owner == null) return;

            Controller.Close();
        }

    #endregion

    #region 콘텐츠 수명

        // ------------------------------------------------------------
        /// <summary>
        /// Context 반환 전에 함께 종료할 콘텐츠 수명을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public THandle RegisterChild<THandle>(THandle handle)
        where THandle : class, IDisposable
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (IsDisposed || isReleased || isContentReleased)
            {
                throw new ObjectDisposedException(nameof(XeriWindowSession));
            }

            children.Add(handle);
            return handle;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Context 선행 종료에도 동일한 콘텐츠 소유권을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReleaseContent()
        {
            var errors = new List<Exception>();
            ReleaseChildren(errors);
            if (errors.Count > 0)
            {
                throw new AggregateException("Window 콘텐츠 정리가 실패했습니다.", errors);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 콘텐츠를 역순 반환하며 실패한 항목도 한 번만 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReleaseChildren(List<Exception> errors)
        {
            if (isContentReleased) return;

            isContentReleased = true;
            // 콜백 재진입 전에 소유권을 제거하고 나머지 콘텐츠도 끝까지 정리한다.
            while (children.Count > 0)
            {
                var index = children.Count - 1;
                var child = children[index];
                children.RemoveAt(index);
                try
                {
                    child.Dispose();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

    #endregion

    #region 내부 수명

        // ------------------------------------------------------------
        /// <summary>
        /// 살아 있는 View Source의 현재 UI Session을 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SaveViewSession()
        {
            if (isReleased) return;
            if (viewSource == null || viewScope == null) return;

            viewSource.SaveSession(viewScope);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Workspace 추적에 공개되기 전 실패한 provisional Session을 직접 Terminal 정리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal List<Exception> ReleaseUnaccepted
        (
            XeriWindowContainer container,
            IXeriWindowRegistry registry
        )
        {
            owner = null;
            return Release(container, registry);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Workspace 소유권을 종료하고 입력·표시·View 자원을 attempt-once로 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal List<Exception> Release
        (
            XeriWindowContainer container,
            IXeriWindowRegistry registry
        )
        {
            var errors = new List<Exception>();

            if (isReleased) return errors;

            isReleased = true;
            Unbind(errors);
            ReleaseChildren(errors);
            errors.AddRange(ReleaseApplication());

            var currentPanel = panel;
            var currentViewSource = viewSource;
            var currentViewScope = viewScope;
            var currentView = view;
            panel = null;
            viewSource = null;
            viewScope = null;
            view = null;

            if (currentViewSource != null && currentViewScope != null)
            {
                try
                {
                    currentViewSource.SaveSession(currentViewScope);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            try
            {
                container?.DetachWindow(Handle);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                registry?.Unregister(Handle);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                currentPanel?.AttachView(null);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (currentViewSource != null && currentViewScope != null && currentView != null)
            {
                try
                {
                    currentViewSource.ReleaseView(currentViewScope, currentView);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            return errors;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Window ContentRoot에 Child PresentationSession과 독립 Child Context를 생성한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void InitializeApplication
        (
            UIContext workspaceContext,
            XeriWindowApplicationOptions options
        )
        {
            try
            {
                childPresentation = workspaceContext.Presentation.CreateChild
                (
                    options.Layout,
                    panel.ContentRoot
                );
                childPresentation.SetOwnerControlledLifetime();
                childContext = workspaceContext.CreateChild(childPresentation);
                childContext.RegisterChild(new Lease(ReleaseContent));
            }
            catch (Exception exception)
            {
                var errors = ReleaseApplication();

                if (errors.Count == 0)
                {
                    throw;
                }

                errors.Insert(0, exception);
                throw new AggregateException
                (
                    "Application Window UI 조립과 롤백이 실패했습니다.",
                    errors
                );
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Simple Window의 Focus Scope record를 Workspace Context에 persistent 등록한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void InitializeSimpleFocus(UIContext workspaceContext)
        {
            if (Controller.Driver is not IFocusScope scope)
            {
                throw new InvalidOperationException
                (
                    $"Window '{Handle.ID}' Driver가 Core Focus Scope를 제공하지 않습니다."
                );
            }

            focusScope = workspaceContext.RegisterFocusScope(scope);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Application Window의 Child Context, Surface와 Registry를 역순 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private List<Exception> ReleaseApplication()
        {
            var errors = new List<Exception>();
            var context = childContext;
            var presentation = childPresentation;
            var currentFocusScope = focusScope;
            childContext = null;
            childPresentation = null;
            focusScope = null;

            try
            {
                context?.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                currentFocusScope?.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                presentation?.DisposeFromOwner();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            return errors;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window 입력 binding과 Controller 이벤트를 Session에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Bind(IXeriWindowDragFactory dragFactory)
        {
            controlManipulator = new XeriWindowControlManipulator(panel, Controller);
            resizeManipulator = new XeriWindowResizeManipulator(panel, Controller);

            try
            {
                controlManipulator.Attach();
                resizeManipulator.Attach();
                titleBarManipulator = dragFactory.CreateTitleBarDrag(panel, Controller);

                if (titleBarManipulator == null)
                {
                    throw new InvalidOperationException
                    (
                        "Window Drag Factory가 null TitleBar Manipulator를 반환했습니다."
                    );
                }

                titleBarManipulator.Attach();

                panel.RegisterCallback<PointerDownEvent>
                (
                    OnPanelPointerDown,
                    TrickleDown.TrickleDown
                );
                isPanelFocusBound = true;

                Controller.OnClose += OnControllerClose;
                Controller.OnStateChange += OnControllerStateChange;
                isControllerBound = true;
            }
            catch (Exception bindException)
            {
                var errors = new List<Exception>();
                Unbind(errors);

                if (errors.Count > 0)
                {
                    errors.Insert(0, bindException);

                    throw new AggregateException
                    (
                        "Window Session 입력 binding과 롤백이 함께 실패했습니다.",
                        errors
                    );
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window 입력 binding과 Controller 이벤트를 한 번 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Unbind(List<Exception> errors)
        {
            if (isControllerBound)
            {
                Controller.OnClose -= OnControllerClose;
                Controller.OnStateChange -= OnControllerStateChange;
                isControllerBound = false;
            }

            if (isPanelFocusBound && panel != null)
            {
                try
                {
                    panel.UnregisterCallback<PointerDownEvent>
                    (
                        OnPanelPointerDown,
                        TrickleDown.TrickleDown
                    );
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }

                isPanelFocusBound = false;
            }

            try
            {
                titleBarManipulator?.Detach();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                resizeManipulator?.Detach();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                controlManipulator?.Detach();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            titleBarManipulator = null;
            resizeManipulator = null;
            controlManipulator = null;
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Panel pointer 입력을 Workspace Focus 요청으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnPanelPointerDown(PointerDownEvent evt)
        {
            Focus();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window Close 완료를 Session 수명 종료로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnControllerClose(object sender, XeriWindowEventArgs e)
        {
            Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window 상태 변경을 Workspace Focus 정책에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnControllerStateChange
        (
            object sender,
            ValueChangeEventArgs<XeriWindowState> e
        )
        {
            owner?.HandleStateChanged(this, e.Current);
        }

    #endregion

    #region 수명 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Workspace에서 Window Session 소유권을 한 번 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            var currentOwner = owner;
            if (currentOwner == null) return;

            owner = null;
            currentOwner.Release(this);
        }

    #endregion

    }
}
