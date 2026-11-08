/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_SceneFader.cs
수정일 : 2026-11-08

# 설명
SceneFader의 안정 상태, 외부 Transition 취소와 시작 실패 롤백을 검증한다.

# 테스트 구성
 N: 정상 Cover·Reveal 수명
 C: 요청 교체와 종료 정리
 R: Reveal 반환 실패의 Terminal 처리
 X: Transition 취소·실패 전달과 롤백
 I: Overlay 초기화 실패 롤백
 V: 전환 인자 검증
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
            public Action<Color> OnSetColor { get; set; }
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
                OnSetColor?.Invoke(color);
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
            public Action Acquiring { get; set; }
            public Action Releasing { get; set; }
            public int AcquireCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public ISceneFadeDriver Acquire(IPresentationLayerDriver layer)
            {
                AcquireCount++;
                Acquiring?.Invoke();
                return Driver;
            }

            public void Release(ISceneFadeDriver view)
            {
                Releasing?.Invoke();

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
            public Request LatestRequest => requests[requests.Count - 1];

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
            /// 진행 중 요청을 외부에서 취소한다.
            /// </summary>
            // ------------------------------------------------------------
            public Request CancelNext()
            {
                var request = TakeNext();
                request.Handle.Cancel();
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Fade Layer를 System destination으로 노출하는 테스트 Host를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static PresentationHost CreatePresentationHost(PresentationTestScope presentation)
        {
            return new PresentationHost
            (
                presentation.Session,
                new[]
                {
                    new PresentationDestinationDefinition
                    (
                        PresentationDestinationID.System,
                        "Fade"
                    ),
                }
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

    #region N-1: 정상 덮기·드러내기

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
            var host = CreatePresentationHost(presentation);
            var fader = new SceneFader
            (
                host,
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
            var host = CreatePresentationHost(presentation);
            var fader = new SceneFader
            (
                host,
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
            var host = CreatePresentationHost(presentation);
            var fader = new SceneFader
            (
                host,
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

    #region R-1: 드러내기 반환 실패

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
            var host = CreatePresentationHost(presentation);
            var fader = new SceneFader
            (
                host,
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
            var host = CreatePresentationHost(presentation);
            var fader = new SceneFader
            (
                host,
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Reveal 실패가 마지막 성공 Covered 상태의 Alpha뿐 아니라 색상도 함께 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_Reveal비동기실패_마지막Covered색상복원()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var host = CreatePresentationHost(presentation);
            var fader = new SceneFader
            (
                host,
                source,
                transitioner
            );
            var failure = new InvalidOperationException("injected reveal failure");

            fader.Cover(new SceneFadeParams(Color.black, 1.0f));
            transitioner.CompleteNext();
            fader.Reveal(new SceneFadeParams(Color.red, 1.0f));
            transitioner.FailNext(failure);

            Assert.AreEqual(SceneFadeState.Covered, fader.State);
            Assert.AreEqual(Color.black, source.Driver.Color);
            Assert.AreEqual(1.0f, source.Driver.AppliedAlpha);
            Assert.AreSame(failure, fader.LastFailure);

            fader.Dispose();
        }

    #endregion

    #region X-2: 외부 취소

        // ------------------------------------------------------------
        /// <summary>
        /// 외부 취소된 Cover 요청은 Clear 상태로 복원하고 Layer를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_외부취소Cover_Clear복원()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var host = CreatePresentationHost(presentation);
            var fader = new SceneFader(host, source, transitioner);
            Exception reportedFailure = null;
            var completedCount = 0;

            fader.Cover
            (
                new SceneFadeParams(Color.black, 1.0f),
                onCompleted: () => completedCount++,
                onFailed: exception => reportedFailure = exception
            );

            var cancelled = transitioner.CancelNext();
            ManualTransitioner.InvokeLateCompletion(cancelled);

            Assert.AreEqual(0, completedCount);
            Assert.IsTrue(cancelled.Handle.IsCancelled);
            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.IsInstanceOf<OperationCanceledException>(reportedFailure);
            Assert.AreSame(reportedFailure, fader.LastFailure);

            fader.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 외부 취소된 Reveal 요청은 마지막 성공 Covered 상태로 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_외부취소Reveal_Covered복원()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var host = CreatePresentationHost(presentation);
            var fader = new SceneFader(host, source, transitioner);
            Exception reportedFailure = null;

            fader.Cover(new SceneFadeParams(Color.black, 1.0f));
            transitioner.CompleteNext();
            fader.Reveal
            (
                new SceneFadeParams(Color.red, 1.0f),
                onFailed: exception => reportedFailure = exception
            );

            transitioner.CancelNext();

            Assert.AreEqual(SceneFadeState.Covered, fader.State);
            Assert.AreEqual(Color.black, source.Driver.Color);
            Assert.AreEqual(1.0f, source.Driver.AppliedAlpha);
            Assert.AreEqual(0, source.ReleaseCount);
            Assert.IsInstanceOf<OperationCanceledException>(reportedFailure);

            fader.Dispose();
            Assert.AreEqual(1, source.ReleaseCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 취소 이벤트 안에서 새 Cover가 시작되면 이전 Cover가 새 요청을 덮어쓰지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_취소중재진입_최신Cover유지()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var fader = new SceneFader(CreatePresentationHost(presentation), source, transitioner);
            var discardedCompleted = 0;
            var latestCompleted = 0;

            fader.Cover(new SceneFadeParams(Color.black, 1.0f));
            var original = transitioner.LatestRequest;
            original.Handle.OnCancelled += () => fader.Cover
            (
                new SceneFadeParams(Color.red, 1.0f),
                onCompleted: () => latestCompleted++
            );

            fader.Cover
            (
                new SceneFadeParams(Color.green, 1.0f),
                onCompleted: () => discardedCompleted++
            );

            Assert.IsTrue(original.Handle.IsCancelled);
            Assert.AreEqual(2, transitioner.Count);
            Assert.AreEqual(Color.red, source.Driver.Color);
            Assert.AreEqual(SceneFadeState.Covering, fader.State);

            transitioner.CompleteNext();
            Assert.AreEqual(SceneFadeState.Covering, fader.State);

            transitioner.CompleteNext();
            Assert.AreEqual(SceneFadeState.Covered, fader.State);
            Assert.AreEqual(1, latestCompleted);
            Assert.AreEqual(0, discardedCompleted);

            fader.Dispose();
        }

    #endregion

    #region X-3: 시작 실패

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
            var host = CreatePresentationHost(presentation);
            var fader = new SceneFader
            (
                host,
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

    #region I-1: 오버레이 초기화 실패

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
            var host = CreatePresentationHost(presentation);
            var fader = new SceneFader
            (
                host,
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

    #region H-1: 전환 취소 알림

        // ------------------------------------------------------------
        /// <summary>
        /// 취소 동작과 구독자 오류가 있어도 나머지 취소 구독자는 호출한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_PresentationTransitionHandle_취소오류_전체구독자호출()
        {
            var handle = new PresentationTransitionHandle
            (
                () => throw new InvalidOperationException("cancel failure")
            );
            var notified = 0;
            handle.OnCancelled += () => throw new ArgumentException("observer failure");
            handle.OnCancelled += () => notified++;

            var failure = Assert.Throws<AggregateException>(() => handle.Cancel());

            Assert.IsTrue(handle.IsCancelled);
            Assert.AreEqual(2, failure.InnerExceptions.Count);
            Assert.AreEqual(1, notified);

            handle.Cancel();
            Assert.AreEqual(1, notified);
        }

    #endregion

    #region X-4: 획득·요청·롤백 재진입

        // ----------------------------------------------------------------------
        /// <summary>
        /// Source가 획득 중 Dispose를 재진입하면 반환된 Driver와 Layer를 정리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_획득중Dispose_임시자원반환()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var fader = new SceneFader(CreatePresentationHost(presentation), source, transitioner);
            source.Acquiring = fader.Dispose;

            Assert.Throws<ObjectDisposedException>
            (
                () => fader.Cover(new SceneFadeParams(Color.black, 1f))
            );

            Assert.AreEqual(1, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.IsNull(fader.LastFailure);
            Assert.AreEqual(0, transitioner.Count);
            Assert.Throws<ObjectDisposedException>
            (
                () => fader.Cover(new SceneFadeParams(Color.black, 0f))
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 색상 적용 중 새 요청이 시작되면 이전 요청은 Transition을 시작하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_색상적용중재진입_최신요청만전환()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var fader = new SceneFader(CreatePresentationHost(presentation), source, transitioner);
            var earlierCompleted = 0;
            var latestCompleted = 0;

            source.Driver.OnSetColor = color =>
            {
                if (color != Color.red) return;

                source.Driver.OnSetColor = null;
                fader.Cover
                (
                    new SceneFadeParams(Color.blue, 1f),
                    onCompleted: () => latestCompleted++
                );
            };

            fader.Cover
            (
                new SceneFadeParams(Color.red, 1f),
                onCompleted: () => earlierCompleted++
            );

            Assert.AreEqual(1, transitioner.Count);
            Assert.AreEqual(Color.blue, source.Driver.Color);
            transitioner.CompleteNext();
            Assert.AreEqual(SceneFadeState.Covered, fader.State);
            Assert.AreEqual(1, latestCompleted);
            Assert.AreEqual(0, earlierCompleted);
            fader.Dispose();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 색상 적용 callback에서 Dispose 후 예외가 나도 종료된 소유권을 다시 복구하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_색상적용중Dispose오류_이미해제된소유권유지()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var fader = new SceneFader(CreatePresentationHost(presentation), source, transitioner);
            source.Driver.OnSetColor = _ =>
            {
                fader.Dispose();
                throw new InvalidOperationException("injected color callback failure");
            };

            var exception = Assert.Throws<InvalidOperationException>
            (
                () => fader.Cover(new SceneFadeParams(Color.black, 1f))
            );

            StringAssert.Contains("color callback failure", exception.Message);
            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.IsNull(fader.LastFailure);
            Assert.AreEqual(1, source.ReleaseCount);
            Assert.AreEqual(0, transitioner.Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 실패 롤백의 색상 복원 콜백이 새 전환을 시작하면 이전 Alpha 복원을 중단한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_롤백색상복원재진입_새Reveal의Alpha보존()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var fader = new SceneFader(CreatePresentationHost(presentation), source, transitioner);

            fader.Cover(new SceneFadeParams(Color.black, 1f));
            transitioner.CompleteNext();
            fader.Reveal(new SceneFadeParams(Color.red, 1f));
            transitioner.LatestRequest.Params.Target.Set(0.4f);

            source.Driver.OnSetColor = color =>
            {
                if (color != Color.black) return;

                source.Driver.OnSetColor = null;
                fader.Reveal(new SceneFadeParams(Color.blue, 1f));
            };

            transitioner.FailNext(new InvalidOperationException("injected reveal failure"));

            Assert.AreEqual(SceneFadeState.Revealing, fader.State);
            Assert.AreEqual(Color.blue, source.Driver.Color);
            Assert.AreEqual(0.4f, source.Driver.AppliedAlpha, 0.0001f);
            Assert.IsNull(fader.LastFailure);
            Assert.AreEqual(1, transitioner.Count);
            Assert.AreEqual(0, source.ReleaseCount);

            transitioner.CompleteNext();
            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.AreEqual(1, source.ReleaseCount);
            fader.Dispose();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Reveal 반환 중 시작된 새 Cover 실패를 이전 Reveal이 지우지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_반환중새Cover실패_최신LastFailure유지()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var fader = new SceneFader(CreatePresentationHost(presentation), source, transitioner);
            var failure = new InvalidOperationException("injected reentrant acquire failure");
            Exception observedFailure = null;
            var completed = 0;

            fader.Cover(new SceneFadeParams(Color.black, 1f));
            transitioner.CompleteNext();
            source.Releasing = () =>
            {
                source.Releasing = null;
                source.Acquiring = () => throw failure;

                try
                {
                    fader.Cover(new SceneFadeParams(Color.blue, 1f));
                }
                catch (Exception exception)
                {
                    observedFailure = exception;
                }
                finally
                {
                    source.Acquiring = null;
                }
            };

            fader.Reveal
            (
                new SceneFadeParams(Color.black, 1f),
                onCompleted: () => completed++
            );
            transitioner.CompleteNext();

            Assert.AreSame(failure, observedFailure);
            Assert.AreSame(failure, fader.LastFailure);
            Assert.AreEqual(SceneFadeState.Clear, fader.State);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(2, source.AcquireCount);
            Assert.AreEqual(1, source.ReleaseCount);

            fader.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Reveal 반환 중 새 Cover가 시작된 뒤 예외가 나도
        /// <br/> 이전 요청만 실패시키고 새 전환은 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_반환중재진입오류_새Cover와이전실패통지()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var transitioner = new ManualTransitioner();
            var fader = new SceneFader(CreatePresentationHost(presentation), source, transitioner);
            var failure = new InvalidOperationException("injected release callback failure");
            Exception reportedFailure = null;
            var revealCompleted = 0;

            fader.Cover(new SceneFadeParams(Color.black, 1f));
            transitioner.CompleteNext();

            source.Releasing = () =>
            {
                source.Releasing = null;
                fader.Cover(new SceneFadeParams(Color.blue, 1f));
                throw failure;
            };

            fader.Reveal
            (
                new SceneFadeParams(Color.black, 1f),
                onCompleted: () => revealCompleted++,
                onFailed: exception => reportedFailure = exception
            );
            transitioner.CompleteNext();

            Assert.AreSame(failure, reportedFailure);
            Assert.AreEqual(0, revealCompleted);
            Assert.AreEqual(SceneFadeState.Covering, fader.State);
            Assert.AreEqual(Color.blue, source.Driver.Color);
            Assert.IsNull(fader.LastFailure);
            Assert.AreEqual(1, transitioner.Count);

            transitioner.CompleteNext();
            Assert.AreEqual(SceneFadeState.Covered, fader.State);
            fader.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 동기 Reveal 반환 중 새 Cover가 시작된 뒤 예외가 나도
        /// <br/> 호출자에게 실패를 전하고 새 요청은 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_SceneFader_동기반환중재진입오류_호출자예외와새Cover보존()
        {
            using var presentation = CreatePresentationScope();
            var source = new TestFadeSource();
            var fader = new SceneFader
            (
                CreatePresentationHost(presentation),
                source,
                new ImmediateTransitioner()
            );
            var failure = new InvalidOperationException("injected synchronous release failure");
            Exception reportedFailure = null;

            fader.Cover(new SceneFadeParams(Color.black, 0f));
            source.Releasing = () =>
            {
                source.Releasing = null;
                fader.Cover(new SceneFadeParams(Color.blue, 0f));
                throw failure;
            };

            var observed = Assert.Throws<InvalidOperationException>
            (
                () => fader.Reveal
                (
                    new SceneFadeParams(Color.black, 0f),
                    onFailed: exception => reportedFailure = exception
                )
            );

            Assert.AreSame(failure, observed);
            Assert.IsNull(reportedFailure);
            Assert.AreEqual(SceneFadeState.Covered, fader.State);
            Assert.AreEqual(Color.blue, source.Driver.Color);
            Assert.IsNull(fader.LastFailure);
            Assert.AreEqual(2, source.AcquireCount);

            fader.Dispose();
        }

    #endregion

    #region V-1: 전환 인자 검증

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
