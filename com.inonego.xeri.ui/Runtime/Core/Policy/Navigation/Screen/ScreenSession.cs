/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenSession.cs
수정일 : 2026-09-17
# 설명
한 Screen의 공개 상태와 Stack, Hook, Transition 및 합성 Alpha 진행 상태를 묶는다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 한 Screen의 실행 수명과 상태 조회를 제공한다.
    /// </summary>
    // ============================================================
    public sealed class ScreenSession
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Screen stable string ID.
        /// </summary>
        // ------------------------------------------------------------
        public string ID => Options.ID;

        // ------------------------------------------------------------
        /// <summary>
        /// Screen 등록 정책.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenOptions Options { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Open 호출 인자.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenOpenParams OpenParams { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Screen 상태.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenState State { get; internal set; }

        // ------------------------------------------------------------
        /// <summary>
        /// Source 획득 뒤 Stack 실행 수명으로 수락됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        internal bool IsAccepted { get; set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 준비부터 종료까지 함께 이동하는 Screen 자원 소유자.
        /// </summary>
        // ------------------------------------------------------------
        internal ScreenSessionResources Resources { get; }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Screen lifecycle과 외부 Modifier를 합성하는 Presentation Alpha.
        /// </summary>
        // ----------------------------------------------------------------------
        internal PresentationAlpha Alpha { get; set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 진행 중인 Presentation Transition.
        /// </summary>
        // ------------------------------------------------------------
        internal PresentationTransitionHandle Transition { get; set; }

        // ------------------------------------------------------------
        /// <summary>
        /// Replace 요청에서 새 Screen이 대체하는 이전 Session.
        /// </summary>
        // ------------------------------------------------------------
        internal ScreenSession ReplacedSession { get; set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 닫기 완료 뒤 이전 Stack top을 복원할지 여부.
        /// </summary>
        // ------------------------------------------------------------
        internal bool RestorePreviousOnClose { get; set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 닫기 입력 해제 대기 중 현재 Screen의 Cursor 정책을 유지할지 여부.
        /// </summary>
        // ------------------------------------------------------------
        internal bool RetainCursorOnClose { get; set; }

        // ------------------------------------------------------------
        /// <summary>
        /// OnClosed 훅이 이미 호출됐는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        internal bool ClosedHookCalled { get; set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 OnClosing 훅이 이 Session에서 실행 중인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        internal bool IsClosingHookRunning { get; set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 늦은 Transition callback을 거부할 현재 세대값.
        /// </summary>
        // ------------------------------------------------------------
        internal int TransitionGeneration { get; set; }

        private readonly ScreenController controller = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Controller가 소유하는 준비 상태 Screen Session을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal ScreenSession
        (
            ScreenController controller,
            ScreenOptions options,
            ScreenOpenParams openParams,
            IScreenSource source,
            Lease layerUsage
        ) : base()
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            OpenParams = openParams;
            Resources = new ScreenSessionResources(source, layerUsage);
            State = ScreenState.Opening;
        }

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// 지정 Focus 대상이 현재 Screen 인스턴스의 표시 범위에 속하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal bool ContainsFocus(object target)
        {
            return Resources.Instance != null && Resources.Instance.Driver.ContainsFocus(target);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 Session이 Stack top일 때 같은 Controller 닫기 경로를 요청한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public bool Close()
        {
            return controller.Close(this);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부모 Screen 종료 시 역순으로 해제할 하위 표시 Handle을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public THandle RegisterChild<THandle>(THandle handle)
        where THandle : class, IDisposable
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (State == ScreenState.Closing || State == ScreenState.Closed)
            {
                throw new InvalidOperationException("종료 중인 Screen에는 하위 Handle을 등록할 수 없습니다.");
            }

            Resources.RegisterChild(handle);
            return handle;
        }

    #endregion

    }
}
