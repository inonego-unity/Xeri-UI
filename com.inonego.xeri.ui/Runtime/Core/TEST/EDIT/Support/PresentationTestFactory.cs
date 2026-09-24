/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationTestFactory.cs
수정일 : 2026-09-24

# 설명
EditMode Core 테스트에서 PresentationLayout, immutable PresentationPlan과 실제 PresentationSession을 간결하게 조립한다.
serialized Layout 주입에 필요한 reflection은 이 fixture 내부로 격리한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

namespace inonego.Xeri.UI.TEST.Core
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.UI;

    // ======================================================================
    /// <summary>
    /// 새 PresentationPlan/Session 계약을 사용하는 EditMode 테스트 fixture.
    /// </summary>
    // ======================================================================
    internal sealed class PresentationTestScope : IDisposable
    {

    #region 필드

        public PresentationSession Session { get; }
        public PresentationPlan Plan { get; }
        public GameObject Root { get; }

    #endregion

    #region 생성자

        private PresentationTestScope
        (
            PresentationPlan plan,
            GameObject root,
            PresentationSession session
        )
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

    #endregion

    #region 생성

        // ----------------------------------------------------------------------
        /// <summary>
        /// 테스트용 PresentationLayout을 생성하고 serialized 정의 목록을 주입한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static PresentationLayout CreateLayout
        (
            IReadOnlyList<PresentationLayerDefinition> layers,
            IReadOnlyList<PresentationPlacementDefinition> placements
        )
        {
            var layout = ScriptableObject.CreateInstance<PresentationLayout>();
            SetSerializedField
            (
                layout,
                "layers",
                new List<PresentationLayerDefinition>(layers)
            );
            SetSerializedField
            (
                layout,
                "placements",
                new List<PresentationPlacementDefinition>(placements)
            );
            return layout;
        }

        public static PresentationTestScope CreateUGUI
        (
            string layerID,
            params (string PresentationID, int LocalOrder)[] placements
        )
        {
            if (string.IsNullOrWhiteSpace(layerID))
            {
                throw new ArgumentException("테스트 Layer ID가 비어 있습니다.", nameof(layerID));
            }

            var layer = new PresentationPlanLayer
            (
                layerID,
                layerID,
                0,
                PresentationBackend.UGUI
            );
            var planPlacements = new List<PresentationPlanPlacement>();

            for (var index = 0; index < placements.Length; index++)
            {
                var placement = placements[index];
                planPlacements.Add
                (
                    new PresentationPlanPlacement
                    (
                        placement.PresentationID,
                        layer,
                        placement.LocalOrder
                    )
                );
            }

            var plan = new PresentationPlan
            (
                new List<PresentationPlanLayer>
                {
                    layer,
                },
                planPlacements
            );
            var root = new GameObject("Presentation Test Root");
            var session = PresentationSession.CreateTopLevel
            (
                plan,
                root.transform,
                null,
                null
            );
            return new PresentationTestScope(plan, root, session);
        }

        public static PresentationPlanLayer CreateLayer
        (
            string layerID,
            int layerOrder = 0,
            PresentationBackend backend = PresentationBackend.UGUI
        )
        {
            return new PresentationPlanLayer
            (
                layerID,
                layerID,
                layerOrder,
                backend
            );
        }

    #endregion

    #region 내부 처리

        // ----------------------------------------------------------------------
        /// <summary>
        /// PresentationLayout의 private serialized field에 테스트 정의를 주입한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void SetSerializedField(object target, string name, object value)
        {
            var field = target.GetType().GetField
            (
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (field == null)
            {
                throw new InvalidOperationException
                (
                    $"Serialized field '{name}'을 찾을 수 없습니다."
                );
            }

            field.SetValue(target, value);
        }

    #endregion

    #region 수명 해제

        public void Dispose()
        {
            Exception failure = null;

            try
            {
                Session?.Dispose();
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }

            if (failure != null)
            {
                throw failure;
            }
        }

    #endregion

    }
}
