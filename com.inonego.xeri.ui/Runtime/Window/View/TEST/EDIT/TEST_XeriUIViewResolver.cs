/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_XeriUIViewResolver.cs
수정일 : 2026-09-20

# 설명
공통 UI view source resolver 테스트.

# 테스트 구성
 R: Resolver 등록과 조회
 S: UI session 전달
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

            public XeriUIViewScope CreateScope = null;
            public XeriUIViewScope SaveScope = null;
            public XeriUIViewScope LoadScope = null;

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
            /// 테스트용 Label view를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public VisualElement CreateView(XeriUIViewScope scope)
            {
                CreateScope = scope;

                return new Label(ID);
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

    #region R-1: 조회 성공

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

    #endregion

    #region R-2: 조회 실패

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

    #endregion

    #region R-3: 조회 실패

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

    #endregion

    #region S-1: Null Session

        // ------------------------------------------------------------
        /// <summary>
        /// UI session이 null이어도 view source 호출 scope는 정상 전달된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriUIViewSource_CreateView_Null_UISession_허용()
        {
            var source = new TestViewSource("test.view");
            var root   = new VisualElement();
            var slot   = new VisualElement();
            var scope  = new XeriUIViewScope("test.view", "view-key", null, root, slot);

            var view = source.CreateView(scope);

            Assert.IsNotNull(view);
            Assert.AreSame(scope, source.CreateScope);
            Assert.IsNull(source.CreateScope.UISession);
        }

    #endregion

    #region S-2: Session Load

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
            var scope   = new XeriUIViewScope("test.view", "view-key", session, null, null);

            source.LoadSession(scope);

            Assert.AreSame(scope, source.LoadScope);
            Assert.AreEqual(1, session.LoadCount);
        }

    #endregion

    }
}
