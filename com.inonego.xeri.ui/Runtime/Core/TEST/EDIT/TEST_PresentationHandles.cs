/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_PresentationHandles.cs
수정일 : 2026-09-20

# 설명
Modal·Drag Visual·Alpha·Visibility·Overlay의 해제와 UGUI 초기 표시 계약을 검증한다.

# 테스트 구성
 M: Modal top 복원과 Terminal 정리
 D: Drag Visual 외부 파괴
 A: Alpha Modifier reactive 반영
 V: UGUI 표시 구성 검증
 C: 중첩 Core 요청과 Overlay 롤백
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
    using inonego.Xeri.Serializable;
    using inonego.Xeri.UI;

    // ============================================================
    /// <summary>
    /// Presentation Handle의 정상 해제와 Terminal 실패 처리 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_PresentationHandles
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// Modal top 상태를 기록하는 backend.
        /// </summary>
        // ============================================================
        private sealed class TestModalDriver : IModalInteractionDriver
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 현재 top 적용 상태.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsTop { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// top 상태를 기록한 뒤 호출할 테스트 callback.
            /// </summary>
            // ------------------------------------------------------------
            public Action<bool> TopChanged { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// top 상태를 적용한다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetTop(bool isTop)
            {
                IsTop = isTop;
                TopChanged?.Invoke(isTop);
            }
        }

        // ============================================================
        /// <summary>
        /// Modality Policy 테스트에 사용할 최소 Presentation.
        /// </summary>
        // ============================================================
        private sealed class TestPresentation : IPresentation
        {
            public PresentationAlpha Alpha => null;
            public PresentationVisibility Visibility => null;
        }

        // ============================================================
        /// <summary>
        /// Dispose 호출을 기록한 뒤 예외를 던지는 Handle.
        /// </summary>
        // ============================================================
        private sealed class ThrowingHandle : IDisposable
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Dispose 호출 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int DisposeCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Dispose 부작용 뒤 실패를 주입한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Dispose()
            {
                DisposeCount++;
                throw new InvalidOperationException("injected owned handle failure");
            }
        }

        // ============================================================
        /// <summary>
        /// Alpha 상태를 기록하는 테스트 Target.
        /// </summary>
        // ============================================================
        private sealed class TestAlphaTarget : IPresentationAlphaTarget
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 현재 Target 유효 여부.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsValid { get; set; } = true;

            // ------------------------------------------------------------
            /// <summary>
            /// 현재 적용된 Alpha.
            /// </summary>
            // ------------------------------------------------------------
            public float Alpha { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Alpha 적용 호출 수.
            /// </summary>
            // ------------------------------------------------------------
            public int SetCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 초기 Alpha로 테스트 Target을 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestAlphaTarget(float alpha)
            {
                Alpha = alpha;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Alpha를 기록한다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetAlpha(float alpha)
            {
                Alpha = alpha;
                SetCount++;
            }
        }

        // ============================================================
        /// <summary>
        /// Visibility 상태를 기록하는 테스트 Target.
        /// </summary>
        // ============================================================
        private sealed class TestVisibilityTarget : IPresentationVisibilityTarget
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Target은 수명 동안 항상 유효하다.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsValid => true;

            // ------------------------------------------------------------
            /// <summary>
            /// 현재 표시 상태.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsVisible { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 표시 상태 적용 호출 수.
            /// </summary>
            // ------------------------------------------------------------
            public int SetCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 표시 상태를 기록한 뒤 호출할 테스트 callback.
            /// </summary>
            // ------------------------------------------------------------
            public Action<bool> VisibilityChanged { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 기준 표시 상태를 지정한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestVisibilityTarget(bool visible) : base()
            {
                IsVisible = visible;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 표시 상태를 기록한다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetVisible(bool visible)
            {
                IsVisible = visible;
                SetCount++;
                VisibilityChanged?.Invoke(visible);
            }
        }

        // ============================================================
        /// <summary>
        /// Overlay Layer 등록에 사용할 테스트 backend.
        /// </summary>
        // ============================================================
        private sealed class TestLayerDriver : IPresentationLayerDriver<RectTransform>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer Root.
            /// </summary>
            // ------------------------------------------------------------
            public RectTransform Root { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 Root를 사용하는 backend를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestLayerDriver(RectTransform root) : base()
            {
                Root = root;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Asset과 Root 존재 여부를 검증한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool Validate
            (
                PresentationLayerAsset asset,
                out string error
            )
            {
                error = asset == null || Root == null ? "invalid" : "";
                return string.IsNullOrEmpty(error);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Layer 순서는 별도로 기록하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetOrder(int order)
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트에서는 별도 활성 상태를 기록하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public void SetActive(bool active)
            {
                // NONE
            }
        }

        // ============================================================
        /// <summary>
        /// 획득 단계에서 실패하는 Presentation Source.
        /// </summary>
        // ============================================================
        private sealed class FailingPresentationSource : IPresentationSource<object>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Overlay 획득 실패를 주입한다.
            /// </summary>
            // ------------------------------------------------------------
            public object Acquire(IPresentationLayerDriver layer)
            {
                throw new InvalidOperationException("injected overlay acquire failure");
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 획득되지 않은 View는 반환되지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release(object view)
            {
                throw new InvalidOperationException("unreachable");
            }
        }

        // ======================================================================
        /// <summary>
        /// 필수 View가 없는 GameObject를 반환하고 반환 정리도 실패시키는 Provider.
        /// </summary>
        // ======================================================================
        private sealed class FailingReleaseProvider : IGameObjectProvider
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 획득 인스턴스에 적용할 기본 부모.
            /// </summary>
            // ------------------------------------------------------------
            public Transform Parent
            {
                get;
                set;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 획득할 테스트 인스턴스.
            /// </summary>
            // ------------------------------------------------------------
            public GameObject Instance { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 인스턴스를 반환하기 전에 실행할 테스트 callback.
            /// </summary>
            // ------------------------------------------------------------
            public Action Acquiring { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 누적 반환 시도 수.
            /// </summary>
            // ------------------------------------------------------------
            public int ReleaseCount { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Provider 반환 진입 시 인스턴스의 활성 상태.
            /// </summary>
            // ------------------------------------------------------------
            public bool? LastReleasedActiveSelf { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 반환 호출에서 예외를 발생시킬지 여부.
            /// </summary>
            // ------------------------------------------------------------
            public bool FailOnRelease { get; set; } = true;

            // ------------------------------------------------------------
            /// <summary>
            /// 지정된 테스트 인스턴스를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public GameObject Acquire(bool worldPositionStays = true)
            {
                Acquiring?.Invoke();
                return Instance;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 이 Fixture에서 지원하지 않는 비동기 획득을 거부한다.
            /// </summary>
            // ------------------------------------------------------------
            public Awaitable<GameObject> AcquireAsync(bool worldPositionStays = true)
            {
                throw new NotSupportedException();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 반환 시도를 기록하고 설정된 실패를 발생시킨다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release
            (
                GameObject gameObject,
                bool worldPositionStays = true
            )
            {
                ReleaseCount++;
                LastReleasedActiveSelf = gameObject.activeSelf;

                if (FailOnRelease)
                {
                    throw new InvalidOperationException("injected overlay provider release failure");
                }
            }
        }

        private readonly List<UnityEngine.Object> ownedObjects =
            new List<UnityEngine.Object>();

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 Presentation Layer Asset을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private PresentationLayerAsset CreateLayerAsset()
        {
            var asset = ScriptableObject.CreateInstance<PresentationLayerAsset>();
            SetField(asset, "id", "Overlay");
            SetField(asset, "order", 0);
            ownedObjects.Add(asset);
            return asset;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// private 직렬화 필드를 설정한다.
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

            Assert.IsNotNull(field, $"{target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }

    #endregion

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트에서 만든 GameObject를 역순 제거한다.
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

    #region M-1: Modal top 복원

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이미 Stack에 있는 Modal Driver의 중복 Open을 상태 변경 전에 거부한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ModalController_동일Driver중복Open_기존Top유지()
        {
            var controller = new ModalController();
            var driver = new TestModalDriver();
            var handle = controller.Open(new TestPresentation(), driver);

            Assert.Throws<InvalidOperationException>(() => controller.Open(new TestPresentation(), driver));
            Assert.AreEqual(1, controller.Count);
            Assert.IsTrue(driver.IsTop);

            handle.Dispose();
            controller.Dispose();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Modal Driver 비활성화 callback에서 같은 Driver를 다시 열지 못하게 하고,
        /// <br/> 기존 Modal의 표시 소유권만 한 번 종료한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ModalController_해제중같은DriverOpen_기존해제만완료()
        {
            var controller = new ModalController();
            var driver = new TestModalDriver();
            var childDisposeCount = 0;
            var child = new Lease(() => childDisposeCount++);
            Exception nestedException = null;
            var handle = controller.Open(new TestPresentation(), driver, child);
            driver.TopChanged = isTop =>
            {
                if (isTop) return;

                nestedException = Assert.Throws<InvalidOperationException>
                (
                    () => controller.Open(new TestPresentation(), driver)
                );
            };

            handle.Dispose();

            Assert.IsNotNull(nestedException);
            Assert.IsTrue(handle.IsDisposed);
            Assert.AreEqual(0, controller.Count);
            Assert.AreEqual(1, childDisposeCount);
            controller.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 소유 lifetime 정리가 실패해도 이전 top을 복원하고,
        /// <br/> 현재 ModalSession을 Terminal화하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ModalController_소유Lifetime정리실패_이전Top복원과SessionTerminal()
        {
            var controller = new ModalController();
            var previousDriver = new TestModalDriver();
            var currentDriver = new TestModalDriver();
            var child = new ThrowingHandle();
            var previous = controller.Open(new TestPresentation(), previousDriver);
            var current = controller.Open(new TestPresentation(), currentDriver, child);

            Assert.Throws<AggregateException>(current.Dispose);
            Assert.AreEqual(1, controller.Count);
            Assert.IsTrue(previousDriver.IsTop);
            Assert.IsFalse(currentDriver.IsTop);
            Assert.IsTrue(current.IsDisposed);
            Assert.AreEqual(1, child.DisposeCount);

            Assert.DoesNotThrow(current.Dispose);

            Assert.IsTrue(current.IsDisposed);
            Assert.AreEqual(1, child.DisposeCount);
            Assert.AreEqual(1, controller.Count);

            previous.Dispose();
            controller.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Controller 종료가 남은 ModalSession과 자식 Lease를 함께
        /// <br/> Terminal로 종료하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ModalController_Controller종료_남은SessionTerminal()
        {
            var controller = new ModalController();
            var driver = new TestModalDriver();
            var childDisposeCount = 0;
            var child = new Lease(() => childDisposeCount++);
            var handle = controller.Open(new TestPresentation(), driver, child);

            Assert.DoesNotThrow(controller.Dispose);
            Assert.AreEqual(0, controller.Count);
            Assert.IsTrue(handle.IsDisposed);
            Assert.IsFalse(driver.IsTop);
            Assert.AreEqual(1, childDisposeCount);

            Assert.DoesNotThrow(handle.Dispose);
            Assert.AreEqual(1, childDisposeCount);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 새 Modal 활성화 중 Controller가 종료되면 Handle을 반환하지 않고,
        /// <br/> 성공하지 않은 Open의 자식 소유권은 호출자에게 남기는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ModalController_Open중Dispose_Handle미반환과자식소유권유지()
        {
            var controller = new ModalController();
            var driver = new TestModalDriver();
            var childDisposeCount = 0;
            var child = new Lease(() => childDisposeCount++);
            driver.TopChanged = isTop =>
            {
                if (isTop)
                {
                    controller.Dispose();
                }
            };

            Assert.Throws<ObjectDisposedException>
            (
                () => controller.Open(new TestPresentation(), driver, child)
            );

            Assert.AreEqual(0, controller.Count);
            Assert.IsFalse(driver.IsTop);
            Assert.AreEqual(0, childDisposeCount);

            child.Dispose();
            Assert.AreEqual(1, childDisposeCount);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Modal 활성화 callback의 중첩 Open을 거부하고,
        /// <br/> 바깥 Modal 하나만 top으로 공개하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ModalController_Open재진입_중첩Open거부하고단일Top유지()
        {
            var controller = new ModalController();
            var outerDriver = new TestModalDriver();
            var nestedDriver = new TestModalDriver();
            Exception nestedException = null;
            outerDriver.TopChanged = isTop =>
            {
                if (!isTop) return;

                nestedException = Assert.Throws<InvalidOperationException>
                (
                    () => controller.Open(new TestPresentation(), nestedDriver)
                );
            };

            var handle = controller.Open(new TestPresentation(), outerDriver);

            Assert.IsNotNull(nestedException);
            Assert.AreEqual(1, controller.Count);
            Assert.IsTrue(outerDriver.IsTop);
            Assert.IsFalse(nestedDriver.IsTop);

            handle.Dispose();
            controller.Dispose();
        }

    #endregion

    #region A-1: Alpha Modifier reactive 반영

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 등록된 Modifier 내부 값 변경이 Presentation Alpha backend에 즉시 반영되는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_PresentationAlpha_Modifier상태변경_자동Backend반영()
        {
            var target = new TestAlphaTarget(1.0f);
            var alpha = new PresentationAlpha(target);
            var modifier = new NumericFModifier
            (
                NumericFOperation.MUL,
                1.0f
            );
            alpha.AddModifier("a", modifier);

            modifier.Value = 0.5f;

            Assert.AreEqual(0.5f, alpha.Modified);
            Assert.AreEqual(0.5f, target.Alpha);

            Assert.IsTrue(alpha.RemoveModifier("a"));

            Assert.AreEqual(1.0f, alpha.Modified);
            Assert.AreEqual(1.0f, target.Alpha);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Composite Alpha의 Base·Modified 결과를 계산한다.
        /// <br/> 계산 결과가 child local Modified와 곱셈 합성되는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationAlpha_Composite_BaseModified와ChildModified_곱셈합성()
        {
            var firstTarget = new TestAlphaTarget(0.5f);
            var secondTarget = new TestAlphaTarget(0.5f);
            var first = new PresentationAlpha(firstTarget);
            var second = new PresentationAlpha(secondTarget);
            var childModifier = new NumericFModifier
            (
                NumericFOperation.MUL,
                0.5f
            );
            var groupModifier = new NumericFModifier
            (
                NumericFOperation.MUL,
                0.5f
            );
            var group = new PresentationGroup
            (
                new IPresentation[]
                {
                    new Presentation(first),
                    new Presentation(second),
                }
            );

            first.AddModifier("child", childModifier);
            group.Alpha.Set(0.8f);
            group.Alpha.AddModifier("fade", groupModifier);
            group.Apply();

            Assert.AreEqual(0.8f, group.Alpha.Base);
            Assert.AreEqual(0.4f, group.Alpha.Modified);
            Assert.AreEqual(0.5f, first.Base);
            Assert.AreEqual(0.25f, first.Modified);
            Assert.AreEqual(1, first.Modifiers.Count);
            Assert.AreEqual(0.1f, firstTarget.Alpha);
            Assert.AreEqual(0.2f, secondTarget.Alpha);

            Assert.IsTrue(group.Alpha.RemoveModifier("fade"));

            Assert.AreEqual(0.8f, group.Alpha.Modified);
            Assert.AreEqual(0.1f, firstTarget.Alpha);
            Assert.AreEqual(0.2f, secondTarget.Alpha);

            group.Apply();

            Assert.AreEqual(0.2f, firstTarget.Alpha);
            Assert.AreEqual(0.4f, secondTarget.Alpha);
            Assert.AreEqual(0.5f, first.Base);
            Assert.AreEqual(0.25f, first.Modified);
            Assert.AreEqual(1, first.Modifiers.Count);

            group.Clear();

            Assert.AreEqual(0.2f, firstTarget.Alpha);
            Assert.AreEqual(0.4f, secondTarget.Alpha);
        }

    #endregion

    #region C-1: 중첩 Core 요청

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 독립적인 두 hide Modifier가 같은 Presentation에 겹치면
        /// <br/> 하나를 해제해도 나머지 hide가 최종 Visibility를 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_PresentationVisibility_중첩Hide_마지막해제까지숨김유지()
        {
            var target = new TestVisibilityTarget(true);
            var visibility = new PresentationVisibility(target);
            visibility.AddModifier
            (
                "first",
                new BooleanModifier(BooleanOperation.AND, false)
            );
            visibility.AddModifier
            (
                "second",
                new BooleanModifier(BooleanOperation.AND, false)
            );

            Assert.IsFalse(target.IsVisible);

            Assert.IsTrue(visibility.RemoveModifier("first"));

            Assert.IsFalse(target.IsVisible);

            Assert.IsTrue(visibility.RemoveModifier("second"));

            Assert.IsTrue(target.IsVisible);
            Assert.IsTrue(visibility.Modified);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Base Visibility 변경은 활성 hide Modifier를 우회하지 않고,
        /// <br/> 해제 뒤 최신 Base를 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationVisibility_Base변경중Hide활성_해제후최신Base복원()
        {
            var target = new TestVisibilityTarget(true);
            var visibility = new PresentationVisibility(target);
            visibility.AddModifier
            (
                "hide",
                new BooleanModifier(BooleanOperation.AND, false)
            );

            visibility.Set(false);
            visibility.Set(true);

            Assert.IsFalse(target.IsVisible);
            Assert.IsFalse(visibility.Modified);

            Assert.IsTrue(visibility.RemoveModifier("hide"));

            Assert.IsTrue(target.IsVisible);
            Assert.IsTrue(visibility.Modified);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 같은 Presentation이 서로 다른 Tree에 동시에 포함될 수 있다.
        /// <br/> backend 결과는 마지막으로 Apply한 Tree 경로를 기준으로 계산한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationGroup_여러Tree_마지막ApplyTree기준으로합성()
        {
            var target = new TestVisibilityTarget(true);
            var presentation = new Presentation(visibilityTarget: target);
            var firstGroup = new PresentationGroup
            (
                new[]
                {
                    presentation,
                }
            );
            var secondGroup = new PresentationGroup
            (
                new[]
                {
                    presentation,
                }
            );
            firstGroup.Visibility.Set(false);
            secondGroup.Visibility.Set(true);

            firstGroup.Apply();
            Assert.IsFalse(target.IsVisible);

            secondGroup.Apply();
            Assert.IsTrue(target.IsVisible);

            firstGroup.Apply();
            Assert.IsFalse(target.IsVisible);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Group Apply는 현재 Member만 순회한다.
        /// <br/> topology 변경 자체는 backend를 수정하지 않는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_PresentationGroup_TreeApply_GroupModified_현재Member만합성()
        {
            var firstTarget = new TestAlphaTarget(1.0f);
            var secondTarget = new TestAlphaTarget(1.0f);
            var first = new Presentation(alphaTarget: firstTarget);
            var second = new Presentation(alphaTarget: secondTarget);
            var group = new PresentationGroup
            (
                new[]
                {
                    first,
                }
            );
            var modifier = new NumericFModifier(NumericFOperation.MUL, 0.5f);

            group.Alpha.Set(0.8f);
            group.Alpha.AddModifier("fade", modifier);
            group.Apply();

            Assert.AreEqual(0.8f, group.Alpha.Base);
            Assert.AreEqual(0.4f, group.Alpha.Modified);
            Assert.AreEqual(1.0f, first.Alpha.Base);
            Assert.AreEqual(1.0f, first.Alpha.Modified);
            Assert.AreEqual(0.4f, firstTarget.Alpha);
            Assert.AreEqual(1.0f, secondTarget.Alpha);

            Assert.IsTrue(group.Add(second));
            Assert.AreEqual(1.0f, secondTarget.Alpha);

            group.Apply();
            Assert.AreEqual(0.4f, secondTarget.Alpha);

            Assert.IsTrue(group.Remove(first));
            Assert.AreEqual(0.4f, firstTarget.Alpha);

            modifier.Value = 0.25f;
            Assert.AreEqual(0.2f, group.Alpha.Modified);
            Assert.AreEqual(0.4f, firstTarget.Alpha);
            Assert.AreEqual(0.4f, secondTarget.Alpha);

            group.Apply();

            Assert.AreEqual(0.4f, firstTarget.Alpha);
            Assert.AreEqual(0.2f, secondTarget.Alpha);

            Assert.IsTrue(group.Alpha.RemoveModifier("fade"));
            Assert.AreEqual(0.2f, secondTarget.Alpha);

            group.Apply();
            Assert.AreEqual(0.8f, secondTarget.Alpha);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 중첩 Group Apply는 부모에서 자식으로 Alpha 곱셈과 Visibility AND를 전달한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_PresentationGroup_중첩Composite_Alpha곱셈과VisibilityAND()
        {
            var alphaTarget = new TestAlphaTarget(0.8f);
            var visibilityTarget = new TestVisibilityTarget(true);
            var leaf = new Presentation(alphaTarget, visibilityTarget);
            var childGroup = new PresentationGroup
            (
                new[]
                {
                    leaf,
                }
            );
            var rootGroup = new PresentationGroup
            (
                new[]
                {
                    childGroup,
                }
            );

            childGroup.Alpha.Set(0.5f);
            rootGroup.Alpha.Set(0.5f);
            childGroup.Visibility.Set(false);
            rootGroup.Apply();

            Assert.AreEqual(0.2f, alphaTarget.Alpha);
            Assert.IsFalse(visibilityTarget.IsVisible);

            childGroup.Visibility.Set(true);
            Assert.IsFalse(visibilityTarget.IsVisible);

            rootGroup.Apply();
            Assert.IsTrue(visibilityTarget.IsVisible);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Group Apply는 one-shot Tree 평가다.
        /// <br/> 이후 leaf local 변경은 backend를 다시 쓰고 재Apply 시 Tree 결과로 돌아간다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_PresentationGroup_Apply이후Leaf변경_재Apply전까지Local값적용()
        {
            var target = new TestAlphaTarget(1.0f);
            var leaf = new Presentation(alphaTarget: target);
            var group = new PresentationGroup
            (
                new[]
                {
                    leaf,
                }
            );
            group.Alpha.Set(0.5f);

            group.Apply();
            Assert.AreEqual(0.5f, target.Alpha);

            leaf.Alpha.Set(0.8f);

            Assert.AreEqual(0.8f, leaf.Alpha.Base);
            Assert.AreEqual(0.8f, leaf.Alpha.Modified);
            Assert.AreEqual(0.8f, target.Alpha);

            group.Apply();

            Assert.AreEqual(0.4f, target.Alpha);
            Assert.AreEqual(0.8f, leaf.Alpha.Base);
            Assert.AreEqual(0.8f, leaf.Alpha.Modified);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Presentation Group Composite에 자기 자신 또는 간접 순환을 추가하지 못하게 한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_PresentationGroup_Composite순환참조_거부()
        {
            var first = new PresentationGroup();
            var second = new PresentationGroup();

            Assert.Throws<InvalidOperationException>(() => first.Add(first));
            Assert.IsTrue(first.Add(second));
            Assert.Throws<InvalidOperationException>(() => second.Add(first));

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(0, second.Count);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 하나의 Apply Tree 안에서 같은 Presentation이 두 경로에 있으면 적용을 거부한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_PresentationGroup_한Tree내중복Presentation_Apply거부()
        {
            var target = new TestAlphaTarget(1.0f);
            var leaf = new Presentation(alphaTarget: target);
            var firstBranch = new PresentationGroup
            (
                new[]
                {
                    leaf,
                }
            );
            var secondBranch = new PresentationGroup
            (
                new[]
                {
                    leaf,
                }
            );
            var root = new PresentationGroup
            (
                new IPresentation[]
                {
                    firstBranch,
                    secondBranch,
                }
            );

            Assert.Throws<InvalidOperationException>(root.Apply);
            Assert.AreEqual(1.0f, target.Alpha);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Presentation 획득 실패가 Layer 소비 수명을 남기지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_PresentationLease_획득실패_Layer소비수명롤백()
        {
            var parent = new GameObject("Layer Parent");
            var root = new GameObject("Overlay Layer", typeof(RectTransform));
            root.transform.SetParent(parent.transform, false);
            ownedObjects.Add(parent);
            ownedObjects.Add(root);
            var registry = new PresentationLayerRegistry();
            var layerHandle = registry.Register
            (
                CreateLayerAsset(),
                new TestLayerDriver(root.GetComponent<RectTransform>())
            );

            Assert.Throws<InvalidOperationException>
            (
                () => PresentationLease.Acquire<object>
                (
                    registry,
                    "Overlay",
                    new FailingPresentationSource()
                )
            );

            Assert.IsFalse(layerHandle.HasConsumers);
            Assert.DoesNotThrow(layerHandle.Dispose);
            registry.Dispose();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// View 반환 실패 뒤 Source 종료가 같은 Provider 반환을 반복하지 않는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_GameObjectProviderPresentationSource_View반환실패_Source종료에서반복하지않음()
        {
            var parent = new GameObject("Overlay Parent", typeof(RectTransform));
            var instance = new GameObject("Overlay View");
            instance.AddComponent<UGUIScreenDriver>();
            ownedObjects.Add(parent);
            ownedObjects.Add(instance);
            var provider = new FailingReleaseProvider
            {
                Instance = instance,
            };
            var source = new GameObjectProviderPresentationSource<UGUIScreenDriver>(provider);
            var view = source.Acquire
            (
                new TestLayerDriver(parent.GetComponent<RectTransform>())
            );

            Assert.Throws<InvalidOperationException>(() => source.Release(view));
            Assert.AreEqual(1, provider.ReleaseCount);

            provider.FailOnRelease = false;
            Assert.DoesNotThrow(source.Dispose);
            Assert.AreEqual(1, provider.ReleaseCount);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Provider 반환이 지연 Destroy를 사용해도 Overlay는 반환 전에 즉시 비활성화된다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_GameObjectProviderPresentationSource_View반환_제공자호출전비활성()
        {
            var parent = new GameObject("Overlay Parent", typeof(RectTransform));
            var instance = new GameObject("Overlay View");
            instance.AddComponent<UGUIScreenDriver>();
            ownedObjects.Add(parent);
            ownedObjects.Add(instance);
            var provider = new FailingReleaseProvider
            {
                Instance = instance,
                FailOnRelease = false,
            };
            var source = new GameObjectProviderPresentationSource<UGUIScreenDriver>(provider);
            var view = source.Acquire
            (
                new TestLayerDriver(parent.GetComponent<RectTransform>())
            );

            source.Release(view);

            Assert.AreEqual(false, provider.LastReleasedActiveSelf);
            Assert.IsFalse(instance.activeSelf);
            source.Dispose();
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Source가 먼저 물리 소유권을 종료해도 남은 Overlay Handle은
        /// <br/> 오류 없이 Layer 사용을 끝낸다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_GameObjectProviderPresentationSource_Source선종료_남은HandleTerminal()
        {
            var parent = new GameObject("Layer Parent");
            var root = new GameObject("Overlay Layer", typeof(RectTransform));
            var instance = new GameObject("Overlay View");
            root.transform.SetParent(parent.transform, false);
            instance.AddComponent<UGUIScreenDriver>();
            ownedObjects.Add(parent);
            ownedObjects.Add(root);
            ownedObjects.Add(instance);
            var provider = new FailingReleaseProvider
            {
                Instance = instance,
                FailOnRelease = false,
            };
            var source = new GameObjectProviderPresentationSource<UGUIScreenDriver>(provider);
            var registry = new PresentationLayerRegistry();
            var layerHandle = registry.Register
            (
                CreateLayerAsset(),
                new TestLayerDriver(root.GetComponent<RectTransform>())
            );
            var handle = PresentationLease.Acquire<UGUIScreenDriver>
            (
                registry,
                "Overlay",
                source
            );

            source.Dispose();

            Assert.AreEqual(1, provider.ReleaseCount);
            Assert.DoesNotThrow(handle.Dispose);
            Assert.IsTrue(handle.IsDisposed);
            Assert.IsFalse(layerHandle.HasConsumers);
            Assert.AreEqual(1, provider.ReleaseCount);

            layerHandle.Dispose();
            registry.Dispose();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Provider 획득 callback에서 Source가 종료되면
        /// <br/> 뒤늦게 반환된 인스턴스를 한 번 반환하고,
        /// <br/> 종료된 Source 소유 목록에 View를 공개하지 않는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_GameObjectProviderPresentationSource_획득중Dispose_미확정Instance한번반환()
        {
            var parent = new GameObject("Overlay Parent", typeof(RectTransform));
            var instance = new GameObject("Overlay View");
            instance.AddComponent<UGUIScreenDriver>();
            ownedObjects.Add(parent);
            ownedObjects.Add(instance);
            var provider = new FailingReleaseProvider
            {
                Instance = instance,
                FailOnRelease = false,
            };
            GameObjectProviderPresentationSource<UGUIScreenDriver> source = null;
            provider.Acquiring = () => source.Dispose();
            source = new GameObjectProviderPresentationSource<UGUIScreenDriver>(provider);

            Assert.Throws<ObjectDisposedException>
            (
                () => source.Acquire
                (
                    new TestLayerDriver(parent.GetComponent<RectTransform>())
                )
            );

            Assert.AreEqual(1, provider.ReleaseCount);
            Assert.DoesNotThrow(source.Dispose);
            Assert.AreEqual(1, provider.ReleaseCount);
        }

    #endregion

    #region M-2: 정상 Modal Stack

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Modal Stack이 마지막 Modal만 top으로 두고 해제 시 이전 top을 복원하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_ModalController_정상Stack_마지막Top과이전Top복원()
        {
            var controller = new ModalController();
            var firstDriver = new TestModalDriver();
            var secondDriver = new TestModalDriver();
            var first = controller.Open(new TestPresentation(), firstDriver);
            var second = controller.Open(new TestPresentation(), secondDriver);

            Assert.IsFalse(firstDriver.IsTop);
            Assert.IsTrue(secondDriver.IsTop);

            second.Dispose();

            Assert.IsTrue(firstDriver.IsTop);
            Assert.IsFalse(secondDriver.IsTop);

            first.Dispose();
            Assert.AreEqual(0, controller.Count);
            controller.Dispose();
        }

    #endregion

    #region D-1: Drag Visual 직접 Root

        // ----------------------------------------------------------------------
        /// <summary>
        /// 직접 Root Begin이 종료 시 원래 부모와 sibling을 복원하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualHandle_직접Root_원래Hierarchy복원()
        {
            var originalParent = new GameObject("Original", typeof(RectTransform));
            var sibling = new GameObject("Sibling", typeof(RectTransform));
            var dragRoot = new GameObject("Drag Root", typeof(RectTransform));
            var target = new GameObject("Target", typeof(RectTransform));
            ownedObjects.Add(originalParent);
            ownedObjects.Add(sibling);
            ownedObjects.Add(dragRoot);
            ownedObjects.Add(target);
            sibling.transform.SetParent(originalParent.transform, false);
            target.transform.SetParent(originalParent.transform, false);
            var rect = target.GetComponent<RectTransform>();
            var originalSibling = rect.GetSiblingIndex();
            var controller = new DragVisualController();
            var handle = controller.Begin
            (
                rect,
                dragRoot.GetComponent<RectTransform>()
            );

            handle.Dispose();

            Assert.AreSame(originalParent.transform, rect.parent);
            Assert.AreEqual(originalSibling, rect.GetSiblingIndex());
            controller.Dispose();
        }

    #endregion

    #region V-1: UGUI backend 구성

        // --------------------------------------------------------------------------------
        /// <summary>
        /// CanvasGroup 없는 UGUI Modal interaction backend가 명시적으로 실패하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UGUIModalInteractionDriver_CanvasGroup누락_명시적실패()
        {
            var gameObject = new GameObject("Modal Driver");
            ownedObjects.Add(gameObject);
            var driver = gameObject.AddComponent<UGUIModalInteractionDriver>();

            Assert.Throws<InvalidOperationException>(() => driver.SetTop(true));
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// UGUI Modal top 상태는 interaction과 raycast만 적용하고 시각 표현을 소유하지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UGUIModalInteractionDriver_정상Top_Interaction만동기화()
        {
            var gameObject = new GameObject("Modal Driver", typeof(CanvasGroup));
            ownedObjects.Add(gameObject);
            var driver = gameObject.AddComponent<UGUIModalInteractionDriver>();
            var canvasGroup = gameObject.GetComponent<CanvasGroup>();
            SetField(driver, "canvasGroup", canvasGroup);

            driver.SetTop(true);

            Assert.IsTrue(canvasGroup.interactable);
            Assert.IsTrue(canvasGroup.blocksRaycasts);

            driver.SetTop(false);

            Assert.IsFalse(canvasGroup.interactable);
            Assert.IsFalse(canvasGroup.blocksRaycasts);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 표시 참조가 모두 없는 UGUI Blocker가 점유 Handle을 반환하지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_UGUIInteractionBlocker_표시참조누락_명시적실패()
        {
            var gameObject = new GameObject("Interaction Blocker");
            ownedObjects.Add(gameObject);
            var blocker = gameObject.AddComponent<UGUIInteractionBlocker>();

            Assert.Throws<InvalidOperationException>(() => blocker.Acquire());
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 중첩 Blocker 하나를 해제해도 남은 점유가 유지되고 마지막 해제만 숨기는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_UGUIInteractionBlocker_중첩점유_마지막해제만비활성()
        {
            var gameObject = new GameObject("Interaction Blocker");
            var root = new GameObject("Blocker Root");
            root.transform.SetParent(gameObject.transform, false);
            ownedObjects.Add(gameObject);
            ownedObjects.Add(root);
            var blocker = gameObject.AddComponent<UGUIInteractionBlocker>();
            SetField(blocker, "root", root);
            root.SetActive(false);

            var first = blocker.Acquire();
            var second = blocker.Acquire();
            first.Dispose();

            Assert.IsTrue(root.activeSelf);

            second.Dispose();

            Assert.IsFalse(root.activeSelf);
        }

    #endregion

    }
}
