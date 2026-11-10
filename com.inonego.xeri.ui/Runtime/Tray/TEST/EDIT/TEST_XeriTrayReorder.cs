/* BLOCK_HEADER_BEGIN =======================================================================
파일명: TEST_XeriTrayReorder.cs
수정일 : 2026-11-10

# 설명
공통 Tray reorder 계산과 panel 이벤트 전달을 검증한다.

# 테스트 구성
 A–B: Animator와 Reorder 계산
 C–E: Panel 이벤트와 Animator
 F–H: Visual·Pointer 수명·Bounds
========================================================================= BLOCK_HEADER_END */

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
using inonego.Xeri.UI.Tray;

namespace inonego.Xeri.UI.TEST.Tray
{
    // ============================================================
    /// <summary>
    /// 공통 Tray reorder 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_XeriTrayReorder
    {

    #region A: 테스트 Animator

        private sealed class ThrowingPreviewAnimator : IXeriTrayReorderAnimator
        {
            public int PreviewCount { get; private set; }

            public void Preview(IXeriTrayReorderTarget target, XeriTrayReorderSession session)
            {
                PreviewCount++;
                target.GetEntryButtons()[1].style.translate = new Translate(18f, 0f, 0f);
                throw new System.InvalidOperationException("preview failed");
            }

            public void Commit(IXeriTrayReorderTarget target, XeriTrayReorderSession session)
            {
                Clear(target);
            }

            public void Cancel(IXeriTrayReorderTarget target, XeriTrayReorderSession session)
            {
                Clear(target);
            }

            public void Clear(IXeriTrayReorderTarget target)
            {
                XeriTrayReorderVisual.ClearOffsets(target);
            }
        }

        private sealed class TestReorderAnimator : IXeriTrayReorderAnimator
        {
            public int ClearCount { get; private set; } = 0;

            public void Preview
            (
                IXeriTrayReorderTarget target,
                XeriTrayReorderSession session
            )
            {
                // NONE
            }

            public void Commit
            (
                IXeriTrayReorderTarget target,
                XeriTrayReorderSession session
            )
            {
                // NONE
            }

            public void Cancel
            (
                IXeriTrayReorderTarget target,
                XeriTrayReorderSession session
            )
            {
                // NONE
            }

            public void Clear(IXeriTrayReorderTarget target)
            {
                ClearCount++;
            }
        }

    #endregion

    #region B: Reorder 계산

        // ------------------------------------------------------------
        /// <summary>
        /// Horizontal reorder의 target index를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderCalculator_Horizontal_TargetIndex_계산()
        {
            var calculator = new XeriTrayReorderCalculator();
            var bounds = CreateHorizontalBounds();

            var firstTarget = calculator.CalculateTargetIndex
            (
                bounds,
                0,
                new Vector2(46f, 20f),
                XeriTrayReorderAxis.Horizontal
            );
            var lastTarget = calculator.CalculateTargetIndex
            (
                bounds,
                0,
                new Vector2(140f, 20f),
                XeriTrayReorderAxis.Horizontal
            );

            Assert.AreEqual(1, firstTarget);
            Assert.AreEqual(2, lastTarget);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Horizontal reorder는 pointer가 boundary 앞에 있으면 기존 index를 유지한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderCalculator_Horizontal_Boundary_전_유지()
        {
            var calculator = new XeriTrayReorderCalculator();
            var target = calculator.CalculateTargetIndex
            (
                CreateHorizontalBounds(),
                0,
                new Vector2(44f, 20f),
                XeriTrayReorderAxis.Horizontal
            );

            Assert.AreEqual(0, target);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Horizontal reorder는 이전 boundary를 지나면 앞쪽 target index를 계산한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderCalculator_Horizontal_Backward_TargetIndex_계산()
        {
            var calculator = new XeriTrayReorderCalculator();
            var firstTarget = calculator.CalculateTargetIndex
            (
                CreateHorizontalBounds(),
                2,
                new Vector2(94f, 20f),
                XeriTrayReorderAxis.Horizontal
            );
            var lastTarget = calculator.CalculateTargetIndex
            (
                CreateHorizontalBounds(),
                2,
                new Vector2(10f, 20f),
                XeriTrayReorderAxis.Horizontal
            );

            Assert.AreEqual(1, firstTarget);
            Assert.AreEqual(0, lastTarget);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Vertical reorder의 target index를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderCalculator_Vertical_TargetIndex_계산()
        {
            var calculator = new XeriTrayReorderCalculator();
            var target = calculator.CalculateTargetIndex
            (
                CreateVerticalBounds(),
                0,
                new Vector2(20f, 96f),
                XeriTrayReorderAxis.Vertical
            );

            Assert.AreEqual(2, target);
        }

    #endregion

    #region C: Panel 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Panel은 reorder 요청을 OnEntryReorder 이벤트로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_InvokeEntryReorder_OnEntryReorder_발생()
        {
            var panel = new XeriTrayPanel();
            var entries = new[]
            {
                new XeriTrayEntry("a", "A"),
                new XeriTrayEntry("b", "B"),
            };
            var options = XeriTrayOptions.Default();
            options.Reorderable = true;

            XeriTrayReorderEventArgs eventArgs = null;
            panel.OnEntryReorder += (_, e) => eventArgs = e;
            panel.Reload(entries, options);

            panel.InvokeEntryReorder(new XeriTrayReorderRequest(entries[0], 0, 1));

            Assert.IsNotNull(eventArgs);
            Assert.AreSame(entries[0], eventArgs.Entry);
            Assert.AreEqual(0, eventArgs.SourceIndex);
            Assert.AreEqual(1, eventArgs.TargetIndex);
        }

    #endregion

    #region D: 사용자 정의 Animator

        // ----------------------------------------------------------------------
        /// <summary>
        /// Custom reorder animator는 Panel Reload 뒤에도 override를 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_CustomReorderAnimator_Reload후_유지()
        {
            var panel = new XeriTrayPanel();
            var animator = new TestReorderAnimator();
            var options = XeriTrayOptions.Default();
            options.AnimateReorder = false;

            panel.SetReorderAnimator(animator);
            panel.Reload
            (
                new[]
                {
                    new XeriTrayEntry("a", "A"),
                    new XeriTrayEntry("b", "B"),
                },
                options
            );

            Assert.AreSame(animator, panel.ReorderAnimator);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// null override는 custom animator를 해제하고 options 기반 기본 animator로 복귀한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayPanel_SetReorderAnimator_Null_Default복귀()
        {
            var panel = new XeriTrayPanel();
            var animator = new TestReorderAnimator();
            var options = XeriTrayOptions.Default();
            options.AnimateReorder = false;
            panel.Reload(null, options);
            panel.SetReorderAnimator(animator);

            panel.SetReorderAnimator(null);

            Assert.AreNotSame(animator, panel.ReorderAnimator);
            Assert.IsInstanceOf<XeriTrayNoReorderAnimator>(panel.ReorderAnimator);
            Assert.Greater(animator.ClearCount, 0);
        }

    #endregion

    #region E: Animator 정리

        // ------------------------------------------------------------
        /// <summary>
        /// Preview 활성·비활성 Animator는 모두 button offset을 원복한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderAnimator_DefaultClear_ButtonOffsets_복원()
        {
            var panel = new XeriTrayPanel();
            panel.Reload
            (
                new[]
                {
                    new XeriTrayEntry("a", "A"),
                    new XeriTrayEntry("b", "B"),
                },
                XeriTrayOptions.Default()
            );
            var button = panel.GetEntryButtons()[0];
            IXeriTrayReorderAnimator[] animators =
            {
                new XeriTrayReorderAnimator(),
                new XeriTrayNoReorderAnimator(),
            };

            foreach (var animator in animators)
            {
                button.style.translate = new Translate(12f, -8f, 0f);

                animator.Clear(panel);

                Assert.AreEqual(0f, button.style.translate.value.x.value);
                Assert.AreEqual(0f, button.style.translate.value.y.value);
            }
        }

    #endregion

    #region F: Drag 시각 상태

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Reorder visual은 drag 중 원본 button을 숨기고 Clear 시 원래 표시 상태로 되돌린다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderVisual_Move_Clear_Button_Visible_복구()
        {
            var panel = new XeriTrayPanel();
            var entries = new[]
            {
                new XeriTrayEntry("a", "A"),
                new XeriTrayEntry("b", "B"),
            };
            var options = XeriTrayOptions.Default();
            options.Reorderable = true;
            panel.Reload(entries, options);

            var button = panel.GetEntryButtons()[0];
            var session = new XeriTrayReorderSession(button.Entry, 0, Vector2.zero);
            var visual = new XeriTrayReorderVisual();

            visual.Move(button, session, new Vector2(8f, 0f), panel);

            Assert.IsFalse(button.visible);

            visual.Clear(button);

            Assert.IsTrue(button.visible);
        }

    #endregion

    #region G: Pointer 입력 수명

        // ----------------------------------------------------------------------
        /// <summary>
        /// 외부에서 포인터 capture를 빼앗으면 drag proxy와 입력 세션을 종료한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderManipulator_Capture상실_원본복원_재획득금지()
        {
            var panel = new XeriTrayPanel();
            var options = XeriTrayOptions.Default();
            options.Reorderable = true;
            panel.Reload
            (
                new[]
                {
                    new XeriTrayEntry("a", "A"),
                    new XeriTrayEntry("b", "B"),
                },
                options
            );
            var host = EditorWindow.CreateInstance<EditorWindow>();
            host.Show();
            host.rootVisualElement.Add(panel);
            var button = panel.GetEntryButtons()[0];

            try
            {
                var downEvent = new Event
                {
                    type = EventType.MouseDown,
                    button = 0,
                    mousePosition = Vector2.zero,
                };

                using (var down = PointerDownEvent.GetPooled(downEvent))
                {
                    down.target = button;
                    button.SendEvent(down);
                }

                var firstMoveEvent = new Event
                {
                    type = EventType.MouseMove,
                    mousePosition = new Vector2(15f, 0f),
                };

                using (var move = PointerMoveEvent.GetPooled(firstMoveEvent))
                {
                    move.target = button;
                    button.SendEvent(move);
                }

                Assert.IsTrue(panel.EntryContainer.HasPointerCapture(PointerId.mousePointerId));
                Assert.IsFalse(button.visible);

                // 캡처 중에는 Tray 밖으로 나가도 진행 중인 drag 소유권을 유지한다.
                using (var move = PointerMoveEvent.GetPooled(firstMoveEvent))
                {
                    using (var leave = PointerLeaveEvent.GetPooled(move))
                    {
                        leave.target = panel.EntryContainer;
                        panel.EntryContainer.SendEvent(leave);
                    }
                }

                Assert.IsTrue(panel.EntryContainer.HasPointerCapture(PointerId.mousePointerId));
                Assert.IsFalse(button.visible);

                panel.EntryContainer.ReleasePointer(PointerId.mousePointerId);

                var secondMoveEvent = new Event
                {
                    type = EventType.MouseMove,
                    mousePosition = new Vector2(30f, 0f),
                };

                using (var move = PointerMoveEvent.GetPooled(secondMoveEvent))
                {
                    move.target = button;
                    button.SendEvent(move);
                }

                Assert.IsFalse(panel.EntryContainer.HasPointerCapture(PointerId.mousePointerId));
                Assert.IsTrue(button.visible);
                Assert.AreEqual(2, panel.EntryContainer.childCount);
            }
            finally
            {
                host.Close();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Drag 임계값 전 일반 클릭은 기존 Button 표시 offset을 변경하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderManipulator_일반클릭_표시상태유지()
        {
            var panel = new XeriTrayPanel();
            var options = XeriTrayOptions.Default();
            options.Reorderable = true;
            panel.Reload
            (
                new[]
                {
                    new XeriTrayEntry("a", "A"),
                    new XeriTrayEntry("b", "B"),
                },
                options
            );

            var window = EditorWindow.CreateInstance<EditorWindow>();
            window.Show();
            window.rootVisualElement.Add(panel);
            var button = panel.GetEntryButtons()[0];

            try
            {
                button.style.translate = new Translate(12f, -8f, 0f);

                var downEvent = new Event
                {
                    type = EventType.MouseDown,
                    button = 0,
                    mousePosition = Vector2.zero,
                };

                using (var down = PointerDownEvent.GetPooled(downEvent))
                {
                    down.target = button;
                    button.SendEvent(down);
                }

                var upEvent = new Event
                {
                    type = EventType.MouseUp,
                    button = 0,
                    mousePosition = Vector2.zero,
                };

                using (var up = PointerUpEvent.GetPooled(upEvent))
                {
                    up.target = button;
                    button.SendEvent(up);
                }

                Assert.AreEqual(12f, button.style.translate.value.x.value);
                Assert.AreEqual(-8f, button.style.translate.value.y.value);
                Assert.IsTrue(button.visible);
                Assert.AreEqual(2, panel.EntryContainer.childCount);
                Assert.IsFalse(panel.EntryContainer.HasPointerCapture(PointerId.mousePointerId));
            }
            finally
            {
                window.Close();
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Preview 오류가 발생해도 capture, proxy, 원본 표시를 모두 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderManipulator_Preview실패_드래그수명복원()
        {
            var panel = new XeriTrayPanel();
            var options = XeriTrayOptions.Default();
            options.Reorderable = true;
            panel.Reload
            (
                new[]
                {
                    new XeriTrayEntry("a", "A"),
                    new XeriTrayEntry("b", "B"),
                },
                options
            );
            var animator = new ThrowingPreviewAnimator();
            panel.SetReorderAnimator(animator);

            var window = EditorWindow.CreateInstance<EditorWindow>();
            window.Show();
            window.rootVisualElement.Add(panel);
            var button = panel.GetEntryButtons()[0];

            try
            {
                var downEvent = new Event
                {
                    type = EventType.MouseDown,
                    button = 0,
                    mousePosition = Vector2.zero,
                };
                using (var down = PointerDownEvent.GetPooled(downEvent))
                {
                    down.target = button;
                    button.SendEvent(down);
                }

                var moveEvent = new Event
                {
                    type = EventType.MouseMove,
                    mousePosition = new Vector2(1000f, 0f),
                };
                var field = typeof(XeriTrayPanel).GetField
                (
                    "reorderManipulator",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic
                );
                Assert.IsNotNull(field);
                var manipulator = field.GetValue(panel);
                var moveMethod = manipulator.GetType().GetMethod
                (
                    "OnPointerMove",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic
                );
                Assert.IsNotNull(moveMethod);

                using (var move = PointerMoveEvent.GetPooled(moveEvent))
                {
                    var failure = Assert.Throws<System.Reflection.TargetInvocationException>
                    (
                        () => moveMethod.Invoke
                        (
                            manipulator,
                            new object[]
                            {
                                move,
                            }
                        )
                    );
                    Assert.IsInstanceOf<System.InvalidOperationException>(failure.InnerException);
                }

                Assert.AreEqual(1, animator.PreviewCount);
                Assert.IsFalse(panel.EntryContainer.HasPointerCapture(PointerId.mousePointerId));
                Assert.IsTrue(button.visible);
                Assert.AreEqual(2, panel.EntryContainer.childCount);
                Assert.AreEqual(0f, panel.GetEntryButtons()[1].style.translate.value.x.value);
            }
            finally
            {
                window.Close();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Drag 시작 전 포인터가 Tray를 떠나면 입력 후보를 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderManipulator_Drag전이탈_입력후보종료()
        {
            var panel = new XeriTrayPanel();
            var options = XeriTrayOptions.Default();
            options.Reorderable = true;
            panel.Reload
            (
                new[]
                {
                    new XeriTrayEntry("a", "A"),
                    new XeriTrayEntry("b", "B"),
                },
                options
            );

            var window = EditorWindow.CreateInstance<EditorWindow>();
            window.Show();
            window.rootVisualElement.Add(panel);

            try
            {
                var button = panel.GetEntryButtons()[0];
                var field = typeof(XeriTrayReorderManipulator).GetField
                (
                    "session",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic
                );
                Assert.IsNotNull(field);

                var manipulatorField = typeof(XeriTrayPanel).GetField
                (
                    "reorderManipulator",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic
                );
                Assert.IsNotNull(manipulatorField);
                var manipulator = manipulatorField.GetValue(panel);

                var downEvent = new Event
                {
                    type = EventType.MouseDown,
                    button = 0,
                    mousePosition = Vector2.zero,
                };

                using (var down = PointerDownEvent.GetPooled(downEvent))
                {
                    down.target = button;
                    button.SendEvent(down);
                }

                Assert.IsNotNull(field.GetValue(manipulator));

                var moveEvent = new Event
                {
                    type = EventType.MouseMove,
                    mousePosition = new Vector2(2f, 0f),
                };

                using (var source = PointerMoveEvent.GetPooled(moveEvent))
                {
                    using (var leave = PointerLeaveEvent.GetPooled(source))
                    {
                        leave.target = panel.EntryContainer;
                        panel.EntryContainer.SendEvent(leave);
                    }
                }

                Assert.IsNull(field.GetValue(manipulator));
                Assert.IsTrue(button.visible);
                Assert.AreEqual(2, panel.EntryContainer.childCount);
            }
            finally
            {
                window.Close();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 닫기 버튼은 Reorder 입력 후보가 되지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_XeriTrayReorderManipulator_닫기버튼_드래그후보제외()
        {
            var panel = new XeriTrayPanel();
            var options = XeriTrayOptions.Default();
            options.Reorderable = true;
            panel.Reload
            (
                new[]
                {
                    new XeriTrayEntry("a", "A"),
                    new XeriTrayEntry("b", "B"),
                },
                options
            );

            var window = EditorWindow.CreateInstance<EditorWindow>();
            window.Show();
            window.rootVisualElement.Add(panel);

            try
            {
                var button = panel.GetEntryButtons()[0];
                var closeButton = button.Q<Button>("entry-close-button");
                Assert.IsNotNull(closeButton);

                var downEvent = new Event
                {
                    type = EventType.MouseDown,
                    button = 0,
                    mousePosition = Vector2.zero,
                };

                using (var down = PointerDownEvent.GetPooled(downEvent))
                {
                    down.target = closeButton;
                    closeButton.SendEvent(down);
                }

                var moveEvent = new Event
                {
                    type = EventType.MouseMove,
                    mousePosition = new Vector2(30f, 0f),
                };

                using (var move = PointerMoveEvent.GetPooled(moveEvent))
                {
                    move.target = closeButton;
                    closeButton.SendEvent(move);
                }

                Assert.IsFalse(panel.EntryContainer.HasPointerCapture(PointerId.mousePointerId));
                Assert.IsTrue(button.visible);
                Assert.AreEqual(2, panel.EntryContainer.childCount);
            }
            finally
            {
                window.Close();
            }
        }

    #endregion

    #region H: Bounds 테스트 데이터

        // ------------------------------------------------------------
        /// <summary>
        /// Horizontal reorder 계산용 entry bounds를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Rect[] CreateHorizontalBounds()
        {
            return new[]
            {
                new Rect(0f, 0f, 40f, 40f),
                new Rect(50f, 0f, 40f, 40f),
                new Rect(100f, 0f, 40f, 40f),
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Vertical reorder 계산용 entry bounds를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Rect[] CreateVerticalBounds()
        {
            return new[]
            {
                new Rect(0f, 0f, 40f, 40f),
                new Rect(0f, 50f, 40f, 40f),
                new Rect(0f, 100f, 40f, 40f),
            };
        }

    #endregion

    }
}
