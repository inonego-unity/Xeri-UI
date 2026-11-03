/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationLayer.cs
수정일 : 2026-11-04

# 설명
UITK Presentation Layer의 현재 Root, 논리 활성 상태와 런타임 콘텐츠 재연결을 관리한다.
PanelRenderer Native Output과 sibling ordering은 소유하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.CompilerServices;

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

        private static readonly ConditionalWeakTable<VisualElement, IPresentationLayerDriver<VisualElement>> contentOwners = new();
        private readonly List<VisualElement> ownedContents = new();
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

    #region 콘텐츠 Lease 부착과 해제

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

            if (contentOwners.TryGetValue(content, out _))
            {
                throw new InvalidOperationException("UITK 콘텐츠의 소유 Lease가 이미 존재합니다.");
            }

            if (content.parent != null || ReferenceEquals(content, layer.Root))
            {
                throw new InvalidOperationException("런타임 콘텐츠는 다른 계층에 연결되지 않은 별도 루트여야 합니다.");
            }

            var registeredOwner = contentOwners.GetValue(content, _ => layer);
            if (!ReferenceEquals(registeredOwner, layer))
            {
                throw new InvalidOperationException("이미 다른 Layer가 콘텐츠를 소유합니다.");
            }

            if (layer is not UITKPresentationLayer owned)
            {
                try
                {
                    layer.Root.Add(content);
                }
                catch
                {
                    UnbindContent(layer, content, null);
                    throw;
                }

                return new Lease(() => UnbindContent(layer, content, null));
            }

            if (owned.ownedContents.Contains(content))
            {
                throw new InvalidOperationException("이미 Layer에 연결된 런타임 콘텐츠입니다.");
            }

            owned.ownedContents.Add(content);
            try
            {
                owned.BindContent(content);
            }
            catch (Exception exception)
            {
                owned.ownedContents.Remove(content);
                try
                {
                    UnbindContent(layer, content, null);
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
                    UnbindContent(layer, content, owned);
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Lease 반환 시 콘텐츠 소유권을 해제하고 트리에서 분리한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void UnbindContent
        (
            IPresentationLayerDriver<VisualElement> layer,
            VisualElement content,
            UITKPresentationLayer owned
        )
        {
            if (!contentOwners.TryGetValue(content, out var actualOwner)) return;
            if (!ReferenceEquals(actualOwner, layer)) return;
            owned?.ownedContents.Remove(content);
            try
            {
                content.RemoveFromHierarchy();
            }
            finally
            {
                contentOwners.Remove(content);
            }
        }

    #endregion

    #region Root 교체와 콘텐츠 재연결

        // ------------------------------------------------------------
        /// <summary>
        /// Root 교체 시 현재 소유 콘텐츠의 배치 위치를 갱신한다.
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

            var errors = new ExceptionBuffer();
            foreach (var content in ownedContents.ToArray())
            {
                if (!ownedContents.Contains(content)) continue;

                try
                {
                    BindContent(content);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            // 중첩 교체가 일어났다면 최신 호출에서 이미 RootChanged를 전달한다.
            var callbacks = ReferenceEquals(Root, root)
                ? RootChanged?.GetInvocationList()
                : null;
            if (callbacks != null)
            {
                foreach (Action<UITKPresentationLayer> callback in callbacks)
                {
                    if (!ReferenceEquals(Root, root)) break;

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
                throw errors.CreateAggregateException("UITK Layer 루트 재연결 중 오류가 발생했습니다.");
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 소유 콘텐츠를 현재 Root의 ContentContainer에 연결한다.
        /// <br/> 동기 부착·분리 콜백에서 Root나 Lease가 바뀌면 최신 소유 상태를 따른다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void BindContent(VisualElement content)
        {
            while (ownedContents.Contains(content))
            {
                var destination = Root;
                var container = destination?.contentContainer;
                if (!ownedContents.Contains(content)) return;
                if (!ReferenceEquals(Root, destination)) continue;
                if (ReferenceEquals(content.parent, container)) return;

                content.RemoveFromHierarchy();
                if (!ownedContents.Contains(content)) return;
                if (!ReferenceEquals(Root, destination)) continue;
                if (destination == null) return;

                destination.Add(content);

                if (!ownedContents.Contains(content))
                {
                    ReconcileContentOwner(content);
                    return;
                }

                if (!ReferenceEquals(Root, destination)) continue;

                // Add callback도 Root와 소유권을 바꿀 수 있으므로 실제 부착 위치를 재확인한다.
                var attachedContainer = destination.contentContainer;
                if (!ownedContents.Contains(content))
                {
                    ReconcileContentOwner(content);
                    return;
                }

                if (!ReferenceEquals(Root, destination)) continue;
                if (!ReferenceEquals(content.parent, attachedContainer))
                {
                    throw new InvalidOperationException
                    (
                        "UITK Layer가 소유한 콘텐츠를 현재 Root의 ContentContainer에 부착하지 못했습니다."
                    );
                }

                return;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이전 부착 콜백이 끝났을 때 실제 Lease 소유자의 트리만 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ReconcileContentOwner(VisualElement content)
        {
            if (!contentOwners.TryGetValue(content, out var owner))
            {
                content.RemoveFromHierarchy();
                return;
            }

            if (owner is UITKPresentationLayer layer)
            {
                if (layer.ownedContents.Contains(content))
                {
                    layer.BindContent(content);
                }
                else
                {
                    content.RemoveFromHierarchy();
                }

                return;
            }

            var root = owner.Root;
            if (root == null)
            {
                content.RemoveFromHierarchy();
            }
            else if (!ReferenceEquals(content.parent, root.contentContainer))
            {
                root.Add(content);
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
