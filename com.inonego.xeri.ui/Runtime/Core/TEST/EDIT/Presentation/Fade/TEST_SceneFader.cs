/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_SceneFader.cs
수정일 : 2026-09-20

# 설명
SceneFader의 안정 상태, 완료 정리 실패와 Transition 시작 실패 롤백을 검증한다.

# 테스트 구성
 N: 정상 Cover·Reveal 수명
 C: 요청 교체와 종료 정리
 R: Reveal 반환 실패의 Terminal 처리
 X: Transition 실패 전달과 롤백
 I: Overlay 초기화 실패 롤백
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// SceneFader의 실패 원자성과 Overlay 소유권 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_SceneFader
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// Alpha와 색상을 메모리에 적용하는 Fade backend.
        /// </summary>
        // ============================================================
        private sealed class TestFadeDriver : ISceneFadeDriver, IPresentationAlphaTarget
        {
            public PresentationAlpha Alpha { get; }
            public PresentationVisibility Visibility => null;


            public Color Color { get; private set; }
            public bool FailNextApply { get; set; }
            public int ApplyCount { get; private set; }
            public float AppliedAlpha { get; private set; }

            public TestFadeDriver() : base()
            {
                Alpha = new PresentationAlpha(this, 0.0f);
            }

            bool IPresentationAlphaTarget.IsValid => true;
            float IPresentationAlphaTarget.Alpha => AppliedAlpha;

            void IPresentationAlphaTarget.SetAlpha(float alpha)
            {
                ApplyCount++;

                if (FailNextApply)
                {
                    FailNextApply = false;
                    throw new InvalidOperationException("injected fade apply failure");
                }

                AppliedAlpha = alpha;
            }

            public void SetColor(Color color)
            {
                Color = color;
            }
        }

        // ============================================================
        /// <summary>
        /// 하나의 Fade Driver를 획득하고 반환 실패를 주입하는 Source.
        /// </summary>
        // ============================================================
        private sealed class TestFadeSource : IPresentationSource<ISceneFadeDriver>
        {
            public TestFadeDriver Driver { get; } = new TestFadeDriver();
            public bool FailNextRelease { get; set; }
            public int AcquireCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public ISceneFadeDriver Acquire(IPresentationLayerDriver layer)
            {
                AcquireCount++;
                return Driver;
            }

            public void Release(ISceneFadeDriver view)
            {
                if (FailNextRelease)
                {
                    FailNextRelease = false;
                    throw new InvalidOperationException("injected release failure");
                }

                ReleaseCount++;
            }
        }

        // ======================================================================
        /// <summary>
        /// 요청 값을 즉시 적용하고 완료 callback을 동기로 호출하는 Transition backend.
        /// </summary>
        // ======================================================================
        private sealed class ImmediateTransitioner : IPresentationTransitioner
        {
            public PresentationTransitionHandle Play
            (
                PresentationTransitionParams parameters,
                Action onCompleted,
                Action<Exception> onFailed
            )
            {
                parameters.Target.Set(parameters.EndValue);
                var handle = new PresentationTransitionHandle(null);
                handle.Complete();
                onCompleted?.Invoke();
                return handle;
            }

            public void Dispose()
            {
                // NONE
            }
        }

        // ============================================================
        /// <summary>
        /// Transition 시작을 동기 실패시키는 backend.
        /// </summary>
        // ============================================================
        private sealed class ThrowingTransitioner : IPresentationTransitioner
        {
            public PresentationTransitionHandle Play
            (
                PresentationTransitionParams parameters,
                Action onCompleted,
                Action<Exception> onFailed
            )
            {
                throw new InvalidOperationException("injected transition failure");
            }

            public void Dispose()
            {
                // NONE
            }
        }

        // ======================================================================
        /// <summary>
        /// Fade Transition 완료와 늦은 callback 시점을 직접 제어하는 backend.
        /// </summary>
        // ======================================================================
        private sealed class ManualTransitioner : IPresentationTransitioner
        {
            // ============================================================
            /// <summary>
            /// 한 Fade Transition 요청의 Handle과 완료·실패 callback.
            /// </summary>
            // ============================================================
            public sealed class Request
            {
                public PresentationTransitionParams Params { get; }
                public PresentationTransitionHandle Handle { get; set; }
                public Action Completed { get; }
                public Action<Exception> Failed { get; }

                public Request
                (
                    PresentationTransitionParams parameters,
                    Action completed,
                    Action<Exception> failed
                ) : base()
                {
                    Params = parameters;
                    Completed = completed;
                    Failed = failed;
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 아직 완료 callback을 발생시키지 않은 요청 수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count => requests.Count;

            private readonly List<Request> requests = new List<Request>();

            // ------------------------------------------------------------
            /// <summary>
            /// Fade Transition 요청을 보관한다.
            /// </summary>
            // ------------------------------------------------------------
            public PresentationTransitionHandle Play
            (
                PresentationTransitionParams parameters,
                Action onCompleted,
                Action<Exception> onFailed
            )
            {
                var request = new Request(parameters, onCompleted, onFailed);
                request.Handle = new PresentationTransitionHandle(null);
                requests.Add(request);
                return request.Handle;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 가장 오래된 요청을 정상 완료한다.
            /// </summary>
            // ------------------------------------------------------------
            public Request CompleteNext()
            {
                var request = TakeNext();
                var completed = request.Handle.Complete();

                if (completed)
                {
                    request.Params.Target.Set(request.Params.EndValue);
                }

                request.Completed?.Invoke();
                return request;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 가장 오래된 요청을 비동기 실패시키고 실패 callback을 발생시킨다.
            /// </summary>
            // ------------------------------------------------------------
            public Request FailNext(Exception exception)
            {
                var request = TakeNext();
                request.Handle.Fail();
                request.Failed?.Invoke(exception);
                return request;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 취소된 요청의 늦은 완료 callback을 다시 발생시킨다.
            /// </summary>
            // ------------------------------------------------------------
            public static void InvokeLateCompletion(Request request)
            {
                request?.Completed?.Invoke();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 가장 오래된 요청을 목록에서 제거한다.
            /// </summary>
            // ------------------------------------------------------------
            private Request TakeNext()
            {
                if (requests.Count == 0)
                {
                    throw new InvalidOperationException("완료할 Fade Transition 요청이 없습니다.");
                }

                var request = requests[0];
                requests.RemoveAt(0);
                return request;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 backend의 남은 요청을 제거한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Dispose()
            {
                requests.Clear();
            }
        }

        private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

        // ----------------------------------------------------------------------
        /// <summary>
        /// Scene Fade 테스트에 사용할 실제 UGUI PresentationSession을 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static PresentationTestScope CreatePresentationScope()
        {
            return PresentationTestScope.CreateUGUI
            (
                "Fade",
                ("Fade", 0)
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 Asset의 private 직렬화 필드를 설정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SetField
        (
            object target,
            string name,
            object value
        )
        {
            var field = target.GetType().GetField
            (
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

    #endregion

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트에서 만든 Unity Object를 역순 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void TearDown()
        {
            for (var i = ownedObjects.Count - 1; i >= 0; i--)
            {
                if (ownedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }

            ownedObjects.Clear();
        }

    #endregion

    #region N-1: 정상 Cover·Reveal

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Cover가 Overlay를 유지하고 Reveal 완료만 정확히 한 번 반환하며,
        /// <br/> 각 요청의 완료 callback이 정확히 한 번 호출되는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_정상CoverReveal_Overlay유지후한번반환()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var fader = new SceneFader
            (
                presentation.Session,
                "Fade",
                source,
                new ImmediateTransitioner()
            );
            var coveredCount = 0;
            var revealedCount = 0;

            fader.Cover
            (
                new SceneFadeParams(Color.black, 0.0f),
                () => coveredCount++
            );

            Assert.AreEqual(SceneFadeState.Covered, fader.State);
            Assert.AreEqual(1.0f, source.Driver.AppliedAlpha);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(0, source.ReleaseCount);
            Assert.AreEqual(1, coveredCount);

            fader.Reveal
            (
                new SceneFadeParams(Color.black, 0.0f),
                () => revealedCount++
            );

            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.AreEqual(0.0f, source.Driver.AppliedAlpha);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(1, revealedCount);

            fader.Dispose();
            Assert.AreEqual(1, source.ReleaseCount);

        }

    #endregion

    #region C-1: 요청 교체

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 진행 중 Cover 교체가 같은 Overlay를 사용하고 마지막 요청 callback만 호출하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_Cover교체_같은Overlay와마지막요청만완료()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var fader = new SceneFader
            (
                presentation.Session,
                "Fade",
                source,
                transitioner
            );
            var firstCompletedCount = 0;
            var secondCompletedCount = 0;

            fader.Cover
            (
                new SceneFadeParams(Color.black, 1.0f),
                () => firstCompletedCount++
            );
            fader.Cover
            (
                new SceneFadeParams(Color.red, 1.0f),
                () => secondCompletedCount++
            );

            Assert.AreEqual(SceneFadeState.Covering, fader.State);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(2, transitioner.Count);

            var cancelled = transitioner.CompleteNext();
            ManualTransitioner.InvokeLateCompletion(cancelled);

            Assert.AreEqual(SceneFadeState.Covering, fader.State);
            Assert.AreEqual(0, firstCompletedCount);
            Assert.AreEqual(0, secondCompletedCount);

            transitioner.CompleteNext();

            Assert.AreEqual(SceneFadeState.Covered, fader.State);
            Assert.AreEqual(Color.red, source.Driver.Color);
            Assert.AreEqual(0, firstCompletedCount);
            Assert.AreEqual(1, secondCompletedCount);

            fader.Dispose();

        }

    #endregion

    #region C-2: 상태별 종료

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Covering·Covered·Revealing 어느 상태에서도
        /// <br/> Dispose가 Overlay를 한 번 반환하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [TestCase(SceneFadeState.Covering)]
        [TestCase(SceneFadeState.Covered)]
        [TestCase(SceneFadeState.Revealing)]
        public void TEST_SceneFader_상태별Dispose_Overlay한번반환(SceneFadeState targetState)
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var fader = new SceneFader
            (
                presentation.Session,
                "Fade",
                source,
                transitioner
            );

            fader.Cover(new SceneFadeParams(Color.black, 1.0f));

            if (targetState != SceneFadeState.Covering)
            {
                transitioner.CompleteNext();
            }

            if (targetState == SceneFadeState.Revealing)
            {
                fader.Reveal(new SceneFadeParams(Color.black, 1.0f));
            }

            Assert.AreEqual(targetState, fader.State);

            fader.Dispose();

            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);

            fader.Dispose();
            Assert.AreEqual(1, source.ReleaseCount);

        }

    #endregion

    #region R-1: Reveal 반환 실패

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Reveal 반환 실패 뒤 기존 Overlay 소유권이 Terminal이며 새 Cover만 재획득하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_Reveal반환실패_기존OverlayTerminal()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var fader = new SceneFader
            (
                presentation.Session,
                "Fade",
                source,
                new ImmediateTransitioner()
            );

            fader.Cover(new SceneFadeParams(Color.black, 0.0f));
            source.FailNextRelease = true;

            Assert.Throws<InvalidOperationException>
            (
                () => fader.Reveal(new SceneFadeParams(Color.black, 0.0f))
            );
            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.AreEqual(0.0f, source.Driver.AppliedAlpha);
            Assert.IsNotNull(fader.LastFailure);
            Assert.AreEqual(0, source.ReleaseCount);

            Assert.Throws<InvalidOperationException>
            (
                () => fader.Reveal(new SceneFadeParams(Color.black, 0.0f))
            );
            Assert.AreEqual(1, source.AcquireCount);

            fader.Cover(new SceneFadeParams(Color.black, 0.0f));

            Assert.AreEqual(SceneFadeState.Covered, fader.State);
            Assert.AreEqual(2, source.AcquireCount);

            fader.Dispose();
            Assert.AreEqual(1, source.ReleaseCount);

        }

    #endregion

    #region X-1: 비동기 실패 전달

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 비동기 Cover 실패가 완료 callback 없이 요청의 실패 callback에 전달되고
        /// <br/> 마지막 안정 상태와 Overlay 소유권을 복원하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_Cover비동기실패_요청실패Callback과Clear복원()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var fader = new SceneFader
            (
                presentation.Session,
                "Fade",
                source,
                transitioner
            );
            var completedCount = 0;
            Exception reportedFailure = null;
            var failure = new InvalidOperationException("injected async transition failure");

            fader.Cover
            (
                new SceneFadeParams(Color.black, 1.0f),
                () => completedCount++,
                exception => reportedFailure = exception
            );
            transitioner.FailNext(failure);

            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.AreEqual(0, completedCount);
            Assert.AreSame(failure, reportedFailure);
            Assert.AreSame(failure, fader.LastFailure);
            Assert.AreEqual(1, source.ReleaseCount);

            fader.Dispose();

        }

    #endregion

    #region X-2: 시작 실패

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Cover 시작 실패가 요청 callback 없이 Clear 상태와 Overlay 소유권을 복원하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_Cover시작실패_Clear상태와Overlay반환()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var fader = new SceneFader
            (
                presentation.Session,
                "Fade",
                source,
                new ThrowingTransitioner()
            );
            Exception reportedFailure = null;

            Assert.Throws<InvalidOperationException>
            (
                () => fader.Cover
                (
                    new SceneFadeParams(Color.black, 0.2f),
                    onFailed: exception => reportedFailure = exception
                )
            );
            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsNull(reportedFailure);

            fader.Dispose();

        }

    #endregion

    #region I-1: Overlay 초기화 실패

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 실제 Fade Overlay 초기화 실패가 Layer 사용과 View를 반환하고,
        /// <br/> 다음 독립 획득을 허용하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_Overlay초기화실패_Clear복원후재획득()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var fader = new SceneFader
            (
                presentation.Session,
                "Fade",
                source,
                new ImmediateTransitioner()
            );
            source.Driver.FailNextApply = true;

            Assert.Throws<InvalidOperationException>
            (
                () => fader.Cover(new SceneFadeParams(Color.black, 0.0f))
            );
            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsNotNull(fader.LastFailure);

            fader.Cover(new SceneFadeParams(Color.black, 0.0f));

            Assert.AreEqual(SceneFadeState.Covered, fader.State);
            Assert.AreEqual(2, source.AcquireCount);

            fader.Dispose();

        }

    #endregion

    #region V-1: Transition 인자 검증

        // ------------------------------------------------------------
        /// <summary>
        /// Fade와 공통 Transition 생성자가 유한하지 않은 시간을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_PresentationTransitionParams_유한하지않은시간_생성거부()
        {
            var target = new TestFadeDriver();

            Assert.Throws<ArgumentOutOfRangeException>
            (
                () => new SceneFadeParams(Color.black, float.PositiveInfinity)
            );
            Assert.Throws<ArgumentOutOfRangeException>
            (
                () => new PresentationTransitionParams
                (
                    target.Alpha,
                    0.0f,
                    1.0f,
                    float.NaN,
                    true
                )
            );
        }

    #endregion

    }
}
