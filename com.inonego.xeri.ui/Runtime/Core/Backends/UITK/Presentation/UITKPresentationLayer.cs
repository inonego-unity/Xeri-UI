/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationLayer.cs
수정일 : 2026-10-21

# 설명
UITK Presentation Layer의 현재 Root, 논리 활성 상태와 런타임 콘텐츠 재연결을 관리한다.
PanelRenderer Native Output과 sibling ordering은 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// VisualElement를 View 배치 Root로 사용하는 UITK Presentation Layer.
    /// </summary>
    // ======================================================================
    public sealed class UITKPresentationLayer : IPresentationLayerDriver<VisualElement>
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 resolve된 배치 Root이며 unload 중에는 null이다.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Root { get; private set; }

        private readonly List<VisualElement> attachments = new();
        private StyleEnum<DisplayStyle> originalDisplay;
        private bool hasActiveState;
        private bool active = true;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Root의 교체와 unload를 구독한 소유자에게 알린다.
        /// </summary>
        // ------------------------------------------------------------
        internal event Action<UITKPresentationLayer> RootChanged;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 VisualElement를 Layer Root로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal UITKPresentationLayer(VisualElement root)
        {
            Root = root;
            if (root != null)
            {
                originalDisplay = root.style.display;
            }
        }

    #endregion

    #region 콘텐츠 부착과 루트 재연결

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Xeri Layer의 콘텐츠를 연결하고 루트 교체 시 다시 부착한다.
        /// <br/> 반환한 Lease를 해제하면 재연결 등록과 계층을 함께 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public static Lease Attach(IPresentationLayerDriver<VisualElement> layer, VisualElement content)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (content.parent != null || ReferenceEquals(content, layer.Root))
            {
                throw new InvalidOperationException("런타임 콘텐츠는 다른 계층에 연결되지 않은 별도 루트여야 합니다.");
            }

            if (layer is not UITKPresentationLayer owned)
            {
                layer.Root.Add(content);
                return new Lease(content.RemoveFromHierarchy);
            }

            if (owned.attachments.Contains(content))
            {
                throw new InvalidOperationException("이미 Layer에 연결된 런타임 콘텐츠입니다.");
            }

            owned.attachments.Add(content);
            try
            {
                owned.Root?.Add(content);
            }
            catch (Exception exception)
            {
                owned.attachments.Remove(content);
                try
                {
                    content.RemoveFromHierarchy();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException("UITK 콘텐츠 연결과 분리가 모두 실패했습니다.", exception, cleanupException);
                }

                throw;
            }

            return new Lease
            (
                () =>
                {
                    owned.attachments.Remove(content);
                    content.RemoveFromHierarchy();
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// authored 요소를 반환하고 소유한 콘텐츠만 새 Root로 다시 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void RebindRoot(VisualElement root)
        {
            if (ReferenceEquals(Root, root)) return;
            var previous = Root;
            if (previous != null && hasActiveState)
            {
                previous.style.display = originalDisplay;
            }

            Root = root;
            if (root != null)
            {
                originalDisplay = root.style.display;
                if (hasActiveState)
                {
                    root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            var errors = new List<Exception>();
            foreach (var content in attachments.ToArray())
            {
                if (!attachments.Contains(content)) continue;
                try
                {
                    content.RemoveFromHierarchy();
                    Root?.Add(content);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            // 콘텐츠 콜백이 실패해도 참조·포커스 구독자는 현재 Root를 관찰해야 한다.
            var callbacks = RootChanged?.GetInvocationList();
            if (callbacks != null)
            {
                foreach (Action<UITKPresentationLayer> callback in callbacks)
                {
                    try
                    {
                        callback(this);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("UITK Layer 루트 재연결 중 오류가 발생했습니다.", errors);
            }
        }

    #endregion

    #region 프레젠테이션 레이어 드라이버 구현

        // ------------------------------------------------------------
        /// <summary>
        /// UITK Layer Root가 존재하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Validate(out string error)
        {
            if (Root == null)
            {
                error = "UITK Layer Root VisualElement가 없습니다.";
                return false;
            }

            error = "";
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Layer Root의 display 상태로 논리 활성 상태를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void SetActive(bool active)
        {
            this.active = active;
            hasActiveState = true;
            if (Root != null)
            {
                Root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

    #endregion

    }
}
