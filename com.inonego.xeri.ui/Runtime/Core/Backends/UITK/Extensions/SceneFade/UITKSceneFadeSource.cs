/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKSceneFadeSource.cs
수정일 : 2026-09-17
# 설명
직렬화한 VisualTreeAsset Scene Fade View를 UITK Layer에 Clone하고 반환 소유권을 관리한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// VisualTreeAsset 기반 Scene Fade View Source.
    /// </summary>
    // ============================================================
    public sealed class UITKSceneFadeSource : UISceneFadeSource
    {

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// Source가 생성한 Fade Driver와 Clone Root.
        /// </summary>
        // ============================================================
        private sealed class OwnedView
        {

        #region 필드

            // ------------------------------------------------------------
            /// <summary>
            /// 생성한 Fade Driver.
            /// </summary>
            // ------------------------------------------------------------
            public UITKSceneFadeDriver Driver { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Layer에 추가한 VisualTreeAsset Clone Root.
            /// </summary>
            // ------------------------------------------------------------
            public TemplateContainer Container { get; }

        #endregion

        #region 생성자

            // ------------------------------------------------------------
            /// <summary>
            /// Fade Driver와 제거할 Clone Root를 묶는다.
            /// </summary>
            // ------------------------------------------------------------
            public OwnedView
            (
                UITKSceneFadeDriver driver,
                TemplateContainer container
            ) : base()
            {
                Driver = driver ?? throw new ArgumentNullException(nameof(driver));
                Container = container ?? throw new ArgumentNullException(nameof(container));
            }

        #endregion

        }

    #endregion

    #region 필드

        [SerializeField]
        private VisualTreeAsset viewAsset = null;

        [SerializeField]
        private string rootName = "SceneFade";

        private readonly List<OwnedView> ownedViews = new List<OwnedView>();
        private bool isInitialized = false;
        private bool isDisposed = false;

    #endregion

    #region UISceneFadeSource

        // ------------------------------------------------------------
        /// <summary>
        /// Scene Fade UXML Asset과 Driver Root 이름을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Initialize()
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("UITK Scene Fade Source가 이미 초기화됐습니다.");
            }

            if (isDisposed)
            {
                throw new InvalidOperationException("해제된 UITK Scene Fade Source는 초기화할 수 없습니다.");
            }

            if (!enabled)
            {
                throw new InvalidOperationException("UITK Scene Fade Source가 비활성 상태입니다.");
            }

            if (viewAsset == null)
            {
                throw new InvalidOperationException("UITK Scene Fade UXML Asset이 설정되지 않았습니다.");
            }

            if (string.IsNullOrWhiteSpace(rootName))
            {
                throw new InvalidOperationException("UITK Scene Fade Root 이름이 비어 있습니다.");
            }

            isInitialized = true;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// UITK Layer에 Scene Fade Visual Tree를 Clone하고 Driver를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public override ISceneFadeDriver Acquire(IPresentationLayerDriver layer)
        {
            if (!isInitialized || isDisposed)
            {
                throw new InvalidOperationException("UITK Scene Fade Source가 사용 가능한 상태가 아닙니다.");
            }

            if (!(layer is IPresentationLayerDriver<VisualElement> layerPanel))
            {
                throw new InvalidOperationException("UITK Scene Fade Source에는 VisualElement Layer가 필요합니다.");
            }

            if (layerPanel.Root == null)
            {
                throw new InvalidOperationException("UITK Scene Fade Layer Root가 없습니다.");
            }

            var container = viewAsset.CloneTree();
            var root = container.Q<VisualElement>(rootName);

            if (root == null)
            {
                throw new InvalidOperationException
                (
                    $"Scene Fade UXML에 Root '{rootName}'이 없습니다."
                );
            }

            layerPanel.Root.Add(container);

            // Panel Attach callback이 Source를 종료했으면 새 View를 종료된 소유 목록에 공개하지 않는다.
            if (isDisposed)
            {
                container.RemoveFromHierarchy();
                throw new ObjectDisposedException(nameof(UITKSceneFadeSource));
            }

            var driver = new UITKSceneFadeDriver(root, container);
            ownedViews.Add(new OwnedView(driver, container));
            return driver;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Source가 생성한 Scene Fade Clone을 Visual Tree에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Release(ISceneFadeDriver view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (isDisposed) return;

            var index = FindOwnedViewIndex(view);

            if (index < 0)
            {
                throw new InvalidOperationException("이 Source가 소유하지 않은 Scene Fade View입니다.");
            }

            var container = ownedViews[index].Container;
            ownedViews.RemoveAt(index);
            container.RemoveFromHierarchy();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 참조가 같은 Scene Fade Driver의 소유 위치를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private int FindOwnedViewIndex(ISceneFadeDriver view)
        {
            for (var i = 0; i < ownedViews.Count; i++)
            {
                if (ReferenceEquals(ownedViews[i].Driver, view))
                {
                    return i;
                }
            }

            return -1;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 아직 Source가 소유하는 Scene Fade Clone을 모두 한 번씩 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Dispose()
        {
            if (isDisposed) return;

            isInitialized = false;
            isDisposed = true;
            var errors = new List<Exception>();

            for (var i = ownedViews.Count - 1; i >= 0; i--)
            {
                var container = ownedViews[i].Container;
                ownedViews.RemoveAt(i);

                try
                {
                    container.RemoveFromHierarchy();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("UITK Scene Fade Source 해제가 실패했습니다.", errors);
            }
        }

    #endregion

    }
}
