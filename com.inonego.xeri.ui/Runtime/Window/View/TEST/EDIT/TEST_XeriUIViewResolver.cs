/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriUIViewResolver.cs
수정일 : 2026-09-21

# 설명
공통 UI view source resolver 테스트.

# 테스트 구성
 R: Resolver stable ID 등록과 조회
 V: View Source scope와 UI session 전달
========================================================================= BLOCK_HEADER_END */

using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Window;

namespace inonego.Xeri.UI.TEST.Window.View
{
    // ============================================================
    /// <summary>
    /// 공통 UI view resolver 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriUIViewResolver
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트용 UI session.
        /// </summary>
        // ============================================================
        private sealed class TestSession : IXeriUISession
        {
            public int LoadCount = 0;
        }

        // ============================================================
        /// <summary>
        /// 테스트용 view source.
        /// </summary>
        // ============================================================
        private sealed class TestViewSource : IXeriUIViewSource
        {

        #region 필드

            public string ID => id;

            private readonly string id = string.Empty;

            public XeriUIViewScope AcquireScope = null;
            public XeriUIViewScope ReleaseScope = null;
            public XeriUIViewScope SaveScope = null;
            public XeriUIViewScope LoadScope = null;
            public VisualElement ReleasedView = null;

        #endregion

        #region 생성자

            public TestViewSource(string id) : base()
            {
                this.id = id;
            }

        #endregion

        #region 메서드

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트용 Label view를 획득한다.
            /// </summary>
            // ------------------------------------------------------------
            public VisualElement AcquireView(XeriUIViewScope scope)
            {
                AcquireScope = scope;

                return new Label(ID);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 반환된 view와 scope를 저장한다.
            /// </summary>
            // ------------------------------------------------------------
            public void ReleaseView(XeriUIViewScope scope, VisualElement view)
            {
                ReleaseScope = scope;
                ReleasedView = view;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 전달된 scope를 저장한다.
            /// </summary>
            // ------------------------------------------------------------
            public void SaveSession(XeriUIViewScope scope)
            {
                SaveScope = scope;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 전달된 scope를 저장하고 테스트 session을 갱신한다.
            /// </summary>
            // ------------------------------------------------------------
            public void LoadSession(XeriUIViewScope scope)
            {
                LoadScope = scope;

                if (scope.UISession is TestSession session)
                {
                    session.LoadCount++;
                }
            }

        #endregion

        }

    #endregion

    #region R-1: Resolver stable ID 등록과 조회

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 view source는 stable ID로 다시 조회된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUIViewResolver_Register_TryGetViewSource_조회_성공()
        {
            var resolver = new XeriUIViewResolver();
            var source   = new TestViewSource("test.view");

            resolver.Register(source);

            var found = resolver.TryGetViewSource("test.view", out var result);

            Assert.IsTrue(found);
            Assert.AreSame(source, result);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 ID를 중복 등록하면 예외를 발생시킨다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUIViewResolver_Register_중복_ID_예외()
        {
            var resolver = new XeriUIViewResolver();

            resolver.Register(new TestViewSource("test.view"));

            Assert.Throws<System.InvalidOperationException>
            (
                () => resolver.Register(new TestViewSource("test.view"))
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 등록되지 않은 ID는 조회 실패를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUIViewResolver_TryGetViewSource_없는_ID_조회_실패()
        {
            var resolver = new XeriUIViewResolver();

            var found = resolver.TryGetViewSource("missing.view", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 공백-only View Source ID는 stable key로 등록할 수 없다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUIViewResolver_Register_WhitespaceID_거부()
        {
            var resolver = new XeriUIViewResolver();

            Assert.Throws<System.ArgumentException>
            (
                () => resolver.Register(new TestViewSource("   "))
            );
            Assert.IsFalse
            (
                resolver.TryGetViewSource("   ", out _)
            );
        }

    #endregion

    #region V-1: View Source scope와 UI session

        // ----------------------------------------------------------------------
        /// <summary>
        /// UI session이 null이어도 view source 호출 scope는 정상 전달된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUIViewSource_AcquireView_NullUISession_허용()
        {
            var source = new TestViewSource("test.view");
            var scope = new XeriUIViewScope("test.view", "view-key", null);

            var view = source.AcquireView(scope);

            Assert.IsNotNull(view);
            Assert.AreSame(scope, source.AcquireScope);
            Assert.IsNull(source.AcquireScope.UISession);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// LoadSession은 scope의 UI session을 view source에 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUIViewSource_LoadSession_UISession_전달()
        {
            var source  = new TestViewSource("test.view");
            var session = new TestSession();
            var scope = new XeriUIViewScope("test.view", "view-key", session);

            source.LoadSession(scope);

            Assert.AreSame(scope, source.LoadScope);
            Assert.AreEqual(1, session.LoadCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// ReleaseView는 생성한 View와 같은 Scope를 Source 반환 경계에 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriUIViewSource_ReleaseView_Scope와View_전달()
        {
            var source = new TestViewSource("test.view");
            var scope = new XeriUIViewScope
            (
                "test.view",
                "view-key",
                null
            );
            var view = source.AcquireView(scope);

            source.ReleaseView(scope, view);

            Assert.AreSame(scope, source.ReleaseScope);
            Assert.AreSame(view, source.ReleasedView);
        }

    #endregion

    }
}
