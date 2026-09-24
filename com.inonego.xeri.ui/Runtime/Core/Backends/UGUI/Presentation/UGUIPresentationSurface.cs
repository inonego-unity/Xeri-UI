/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UGUIPresentationSurface.cs
수정일 : 2026-10-03

# 설명
Embedded UGUI Session이 부모 Canvas를 공유하면서 Plan Layer roots를 containment한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.UI
{
    internal sealed class UGUIPresentationSurface : PresentationSurface
    {

    #region 필드

        public RectTransform Root { get; private set; }

    #endregion

    #region 생성자

        public UGUIPresentationSurface
        (
            RectTransform parent,
            IReadOnlyList<PresentationPlanLayer> layers
        )
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (layers == null)
            {
                throw new ArgumentNullException(nameof(layers));
            }

            Root = CreateRect("Presentation Surface", parent);

            try
            {
                for (var index = 0; index < layers.Count; index++)
                {
                    AddLayer(layers[index]);
                }
            }
            catch (Exception exception)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException
                    (
                        "UGUI Presentation Surface 생성과 롤백이 모두 실패했습니다.",
                        exception,
                        cleanupException
                    );
                }

                throw;
            }
        }

    #endregion

    #region 레이어 구성

        private void AddLayer(PresentationPlanLayer plan)
        {
            var root = CreateRect($"Layer - {plan.LayerID}", Root);
            var gameObject = root.gameObject;
            var driver = new UGUIPresentationLayer(root);
            AddLayer(plan, driver, () => DestroyObject(gameObject));
        }

        protected override void ApplyLayerOrder(IReadOnlyList<IPresentationLayerDriver> orderedDrivers)
        {
            for (var index = 0; index < orderedDrivers.Count; index++)
            {
                if (orderedDrivers[index] is not IPresentationLayerDriver<RectTransform> layer)
                {
                    throw new InvalidOperationException("UGUI Surface에 다른 backend Layer가 등록되었습니다.");
                }

                layer.Root.SetSiblingIndex(index);
            }
        }

    #endregion

    #region 루트 수명

        protected override void ReleaseSurfaceRoot()
        {
            var root = Root;
            Root = null;

            if (root != null)
            {
                DestroyObject(root.gameObject);
            }
        }

        internal static RectTransform CreateRect(string name, RectTransform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        internal static void DestroyObject(GameObject gameObject)
        {
            if (gameObject == null) return;

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

    #endregion

    }
}
