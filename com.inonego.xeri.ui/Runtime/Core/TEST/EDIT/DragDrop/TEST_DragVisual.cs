/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_DragVisual.cs
수정일 : 2026-09-20

# 설명
기존 DraggableUI와 UGUI Drag Visual의 좌표·Layer Usage·종료 수명 연동을 검증한다.

# 테스트 구성
 L: Presentation Layer Usage와 pose 복원
 B: DraggableUI Begin·End·Cancel 연결
 R: Runtime 종료 안전 경계
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.DragDrop;
using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.UI.TEST.Core
{
    // ============================================================
    /// <summary>
    /// UGUI Drag Visual과 기존 Drag_Drop 연동 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_DragVisual
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// Transform 부모 변경 callback을 테스트 흐름에 전달한다.
        /// </summary>
        // ============================================================
        [ExecuteAlways]
        private sealed class TransformParentChangedRelay : MonoBehaviour
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 부모 변경 직후 호출할 테스트 callback.
            /// </summary>
            // ------------------------------------------------------------
            public Action Changed { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// Unity 부모 변경 메시지를 테스트 callback에 전달한다.
            /// </summary>
            // ------------------------------------------------------------
            private void OnTransformParentChanged()
            {
                Changed?.Invoke();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 직렬화 private 필드에 테스트 값을 지정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SetField
        (
            object target,
            string fieldName,
            object value
        )
        {
            var field = target.GetType().GetField
            (
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.IsNotNull(field, $"필드 '{fieldName}'를 찾지 못했습니다.");
            field.SetValue(target, value);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// EditMode에서 DraggableUI의 Unity lifecycle 초기화를 테스트용으로 수행한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void InitializeDraggable(DraggableUI draggable)
        {
            var method = typeof(DraggableUI).GetMethod
            (
                "InitializeRuntime",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.IsNotNull(method, "DraggableUI 초기화 진입점을 찾지 못했습니다.");
            method.Invoke(draggable, null);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// EventSystem Pointer 입력 데이터를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static PointerEventData CreateEventData(Vector2 position)
        {
            return new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = 1,
                position = position,
            };
        }

    #endregion

    #region 픽스처

        private readonly List<UnityEngine.Object> ownedObjects =
            new List<UnityEngine.Object>();

        private PresentationTestScope presentation = null;
        private DragVisualController controller = null;
        private IDisposable binding = null;
        private RectTransform originalParent = null;
        private RectTransform layerRoot = null;
        private RectTransform target = null;
        private DraggableUI draggable = null;

        // ----------------------------------------------------------------------
        /// <summary>
        /// 서로 다른 좌표계의 원래 부모와 Drag Layer, DraggableUI를 준비한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            ownedObjects.Add(eventSystemObject);

            var canvasRoot = new GameObject("Canvas Root", typeof(RectTransform));
            ownedObjects.Add(canvasRoot);

            var originalObject = new GameObject("Original Parent", typeof(RectTransform));
            ownedObjects.Add(originalObject);
            originalParent = originalObject.GetComponent<RectTransform>();
            originalParent.SetParent(canvasRoot.transform, false);
            originalParent.anchoredPosition = new Vector2(-120.0f, 40.0f);
            originalParent.localScale = new Vector3(1.5f, 0.75f, 1.0f);

            var targetObject = new GameObject
            (
                "Draggable",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(DraggableUI)
            );
            ownedObjects.Add(targetObject);
            target = targetObject.GetComponent<RectTransform>();
            target.SetParent(originalParent, false);
            target.anchorMin = new Vector2(0.2f, 0.3f);
            target.anchorMax = new Vector2(0.7f, 0.8f);
            target.pivot = new Vector2(0.4f, 0.6f);
            target.anchoredPosition3D = new Vector3(25.0f, 35.0f, 7.0f);
            target.sizeDelta = new Vector2(70.0f, 90.0f);
            target.localRotation = Quaternion.Euler(0.0f, 0.0f, 12.0f);
            target.localScale = new Vector3(1.1f, 0.9f, 1.0f);
            draggable = targetObject.GetComponent<DraggableUI>();

            // EditMode에서는 Unity가 Awake를 호출하지 않으므로 같은 초기화 진입점을 사용한다.
            InitializeDraggable(draggable);

            presentation = PresentationTestScope.CreateUGUI
            (
                "Drag",
                ("Drag", 0)
            );
            Assert.IsTrue
            (
                presentation.Session.TryAcquirePlacement
                (
                    "Drag",
                    out var placement,
                    out var usage
                )
            );
            usage.Dispose();
            layerRoot = ((IPresentationLayerDriver<RectTransform>)placement).Root;
            layerRoot.anchoredPosition = new Vector2(80.0f, -60.0f);
            layerRoot.localScale = new Vector3(0.5f, 2.0f, 1.0f);
            controller = new DragVisualController(presentation.Session);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 연결과 Layer 소유권을 역순으로 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void TearDown()
        {
            binding?.Dispose();
            controller?.Dispose();
            presentation?.Dispose();
            presentation = null;

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

    #region L-1: Layer Usage와 pose 복원

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Layer 기반 Begin과 Dispose가 Usage와 전체 RectTransform pose를 대칭으로 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualController_BeginDispose_LayerUsage와Pose복원()
        {
            var originalSibling = target.GetSiblingIndex();
            var parameters = new DragVisualParams(target, "Drag");

            var handle = controller.Begin(parameters);

            Assert.AreSame(layerRoot, target.parent);
            Assert.IsTrue(presentation.Session.LayerRegistry.HasConsumers);

            handle.Dispose();

            Assert.IsTrue(handle.IsDisposed);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
            Assert.AreSame(originalParent, target.parent);
            Assert.AreEqual(originalSibling, target.GetSiblingIndex());
            Assert.AreEqual(new Vector2(0.2f, 0.3f), target.anchorMin);
            Assert.AreEqual(new Vector2(0.7f, 0.8f), target.anchorMax);
            Assert.AreEqual(new Vector2(0.4f, 0.6f), target.pivot);
            Assert.AreEqual(new Vector3(25.0f, 35.0f, 7.0f), target.anchoredPosition3D);
            Assert.AreEqual(new Vector2(70.0f, 90.0f), target.sizeDelta);
            Assert.AreEqual(Quaternion.Euler(0.0f, 0.0f, 12.0f), target.localRotation);
            Assert.AreEqual(new Vector3(1.1f, 0.9f, 1.0f), target.localScale);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 수동 Begin이 이미 점유 중인 동일 Target을 거부하고,
        /// <br/> 실패한 요청의 Layer Usage만 반환해 기존 Drag 상태를 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualController_동일Target중복Begin_기존점유유지()
        {
            var parameters = new DragVisualParams(target, "Drag");
            var handle = controller.Begin(parameters);

            Assert.Throws<InvalidOperationException>(() => controller.Begin(parameters));
            Assert.AreSame(layerRoot, target.parent);
            Assert.IsTrue(presentation.Session.LayerRegistry.HasConsumers);

            handle.Dispose();

            Assert.AreSame(originalParent, target.parent);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 부모 변경 callback의 중첩 Begin을 거부하고,
        /// <br/> 바깥 Handle이 같은 Target과 Layer Usage를 계속 소유하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualController_부모변경재진입_바깥Handle점유유지()
        {
            var relay = target.gameObject.AddComponent<TransformParentChangedRelay>();
            var parameters = new DragVisualParams(target, "Drag");
            Exception nestedException = null;
            relay.Changed = () => nestedException = Assert.Throws<InvalidOperationException>
            (
                () => controller.Begin(parameters)
            );

            var handle = controller.Begin(parameters);
            relay.Changed = null;

            Assert.IsNotNull(nestedException);
            Assert.AreSame(layerRoot, target.parent);
            Assert.IsTrue(presentation.Session.LayerRegistry.HasConsumers);

            handle.Dispose();

            Assert.AreSame(originalParent, target.parent);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 부모 변경 callback에서 Controller가 종료되면 바깥 Begin을 중단하고,
        /// <br/> 원래 계층과 Layer Usage를 한 번 정리하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualController_부모변경중Dispose_바깥Begin중단과Pose복원()
        {
            var relay = target.gameObject.AddComponent<TransformParentChangedRelay>();
            var parameters = new DragVisualParams(target, "Drag");
            relay.Changed = () => controller.Dispose();

            Assert.Throws<ObjectDisposedException>(() => controller.Begin(parameters));
            relay.Changed = null;

            Assert.AreSame(originalParent, target.parent);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
            Assert.DoesNotThrow(controller.Dispose);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 외부에서 Drag 대상을 파괴해도 Handle이 Layer Usage를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_DragVisualHandle_대상외부파괴_LayerUsage반환()
        {
            var handle = controller.Begin(new DragVisualParams(target, "Drag"));

            UnityEngine.Object.DestroyImmediate(target.gameObject);

            Assert.DoesNotThrow(handle.Dispose);
            Assert.IsTrue(handle.IsDisposed);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
        }

    #endregion

    #region B-1: Draggable Begin과 End

        // ----------------------------------------------------------------------
        /// <summary>
        /// 동일 Draggable의 중복 Binding을 거부해 단일 Visual 소유권을 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualController_동일Draggable또는Target중복Bind_기존Binding유지()
        {
            var parameters = new DragVisualParams(target, "Drag");
            binding = controller.Bind(draggable, parameters);

            Assert.Throws<InvalidOperationException>
            (
                () => controller.Bind(draggable, parameters)
            );

            var otherObject = new GameObject
            (
                "Other Draggable",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(DraggableUI)
            );
            ownedObjects.Add(otherObject);
            var otherDraggable = otherObject.GetComponent<DraggableUI>();
            InitializeDraggable(otherDraggable);

            Assert.Throws<InvalidOperationException>
            (
                () => controller.Bind(otherDraggable, parameters)
            );

            var eventData = CreateEventData(new Vector2(100.0f, 120.0f));
            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.pointerDownHandler
            );
            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.beginDragHandler
            );
            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.endDragHandler
            );

            Assert.AreSame(originalParent, target.parent);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 계층 승격 직후 같은 Pointer 위치의 첫 Drag가 현재 화면 위치를 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualBinding_부모변경후첫Drag_Pointer위치유지()
        {
            binding = controller.Bind
            (
                draggable,
                new DragVisualParams(target, "Drag")
            );
            var eventData = CreateEventData(new Vector2(100.0f, 120.0f));

            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.pointerDownHandler
            );
            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.beginDragHandler
            );
            var positionAfterBegin = target.position;

            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.dragHandler
            );

            Assert.AreSame(layerRoot, target.parent);
            Assert.IsTrue(draggable.IsDragging);
            Assert.IsTrue(presentation.Session.LayerRegistry.HasConsumers);
            Assert.Less
            (
                Vector3.Distance(positionAfterBegin, target.position),
                0.001f
            );

            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.endDragHandler
            );

            Assert.IsFalse(draggable.IsDragging);
            Assert.AreSame(originalParent, target.parent);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
            Assert.IsTrue(target.GetComponent<CanvasGroup>().blocksRaycasts);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Drag Root로 옮기는 중 부모 변경 callback이 Drag을 종료해도,
        /// <br/> 뒤늦게 반환된 Visual Handle과 Layer Usage를 즉시 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualBinding_시작중Drag종료_뒤늦은Handle과Usage반환()
        {
            binding = controller.Bind
            (
                draggable,
                new DragVisualParams(target, "Drag")
            );
            var relay = target.gameObject.AddComponent<TransformParentChangedRelay>();
            relay.Changed = () =>
            {
                relay.Changed = null;
                draggable.ForceDragEnd();
            };
            var eventData = CreateEventData(new Vector2(100.0f, 120.0f));

            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.pointerDownHandler
            );
            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.beginDragHandler
            );

            Assert.IsFalse(draggable.IsDragging);
            Assert.AreSame(originalParent, target.parent);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
            Assert.IsTrue(target.GetComponent<CanvasGroup>().blocksRaycasts);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 먼저 등록된 공개 종료 구독자가 실패해도 Drag Visual과 Layer Usage를 정리하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualBinding_종료구독자실패_Visual과LayerUsage정리()
        {
            var endCount = 0;
            draggable.OnDragEnd += (_, _) =>
            {
                endCount++;
                throw new InvalidOperationException
                (
                    "injected drag end subscriber failure"
                );
            };
            binding = controller.Bind
            (
                draggable,
                new DragVisualParams(target, "Drag")
            );
            var eventData = CreateEventData(new Vector2(100.0f, 120.0f));
            LogAssert.Expect
            (
                LogType.Exception,
                new Regex("injected drag end subscriber failure")
            );

            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.pointerDownHandler
            );
            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.beginDragHandler
            );

            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.endDragHandler
            );

            Assert.AreEqual(1, endCount);
            Assert.IsFalse(draggable.IsDragging);
            Assert.AreSame(originalParent, target.parent);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
            Assert.IsTrue(target.GetComponent<CanvasGroup>().blocksRaycasts);
        }

    #endregion

    #region B-2: 연결 선종료

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 활성 Drag 중 Binding을 닫으면 Cancel, Raycast와 Visual 수명이 함께 종료된다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualBinding_활성Drag중Dispose_Cancel과Visual종료()
        {
            binding = controller.Bind
            (
                draggable,
                new DragVisualParams(target, "Drag")
            );
            var eventData = CreateEventData(new Vector2(100.0f, 120.0f));

            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.pointerDownHandler
            );
            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.beginDragHandler
            );

            binding.Dispose();
            binding = null;

            Assert.IsFalse(draggable.IsDragging);
            Assert.AreSame(originalParent, target.parent);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
            Assert.IsTrue(target.GetComponent<CanvasGroup>().blocksRaycasts);
        }

    #endregion

    #region R-1: Controller 종료

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Runtime 안전 경계인 Controller 종료가 활성 Binding과 Layer Usage를 함께 정리한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DragVisualController_활성Binding중Dispose_Drag과LayerUsage종료()
        {
            binding = controller.Bind
            (
                draggable,
                new DragVisualParams(target, "Drag")
            );
            var eventData = CreateEventData(new Vector2(100.0f, 120.0f));

            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.pointerDownHandler
            );
            ExecuteEvents.Execute
            (
                target.gameObject,
                eventData,
                ExecuteEvents.beginDragHandler
            );

            controller.Dispose();
            controller = null;
            binding = null;

            Assert.IsFalse(draggable.IsDragging);
            Assert.AreSame(originalParent, target.parent);
            Assert.IsFalse(presentation.Session.LayerRegistry.HasConsumers);
            Assert.IsTrue(target.GetComponent<CanvasGroup>().blocksRaycasts);
        }

    #endregion

    }
}
