/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UITKPresentationOutput.cs
수정일 : 2026-11-11

# 설명
Native PanelRenderer의 생성·대여, Root 재연결 및 원래 설정 복원을 소유한다.
Root에 추가한 기본 스타일도 같은 Output 수명 안에서 연결하고 반환한다.
렌더 파이프라인, 출력 정밀도 및 스타일 보간은 이 클래스가 선택하지 않는다.
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
    /// Native UITK 출력과 빌린 설정·Root의 원상 복원을 소유한다.
    /// </summary>
    // ============================================================
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class UITKPresentationOutput : MonoBehaviour
    {

    #region 설정과 상태

        private enum State { Inactive, Acquired, Releasing }

        private const string BASELINE_PATH = "Xeri/UI/Core/UIRuntimeBaseline";
        private const string ROOT_CLASS = "xeri-ui";

        // ------------------------------------------------------------
        /// <summary>
        /// 생성형과 대여형 출력이 사용하는 원래 Native 렌더러.
        /// </summary>
        // ------------------------------------------------------------
        public PanelRenderer PanelRenderer => panelRenderer != null ? panelRenderer : GetComponent<PanelRenderer>();
        private PanelRenderer panelRenderer;

        // ------------------------------------------------------------
        /// <summary>
        /// Native 재연결에 따라 갱신되며 출력 반환 시 해제되는 루트.
        /// </summary>
        // ------------------------------------------------------------
        public VisualElement Root => root;
        private VisualElement root;

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Output이 생성한 복사본. 대여형은 원래 설정을 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        public PanelSettings RuntimePanelSettings { get; private set; }

        private VisualElement layerRoot;
        private StyleSheet runtimeBaseline;
        private PanelSettings borrowedSettings;
        private float borrowedSortingOrder;
        private State state = State.Inactive;
        private bool IsInitialized => state == State.Acquired;
        private bool reloadRegistered;
        private bool ownsRootClass;
        private bool ownsRootStyleSheet;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Native 루트와 콘텐츠 재생성이 반영된 현재 요소를 알린다.
        /// </summary>
        // ------------------------------------------------------------
        internal event Action<VisualElement> RootUpdated;

    #endregion

    #region 출력 수명

        // ------------------------------------------------------------
        /// <summary>
        /// Template의 복사본을 만들어 생성형 출력에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Initialize(PanelSettings template, int layerOrder)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            InitializeCore(template, layerOrder, false);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 설정을 교체하지 않고 정렬 순서만 빌려 사용한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void InitializeBorrowed(int layerOrder)
        {
            InitializeCore(null, layerOrder, true);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 생성·대여 설정을 연결하고 실패 시 취득한 native 자원을 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
        private void InitializeCore(PanelSettings template, int layerOrder, bool borrowed)
        {
            if (state != State.Inactive)
            {
                throw new InvalidOperationException("UITK Presentation Output의 수명이 아직 종료되지 않았습니다.");
            }

            panelRenderer = GetComponent<PanelRenderer>();
            if (panelRenderer == null)
            {
                throw new MissingComponentException("UITK Presentation Output PanelRenderer가 없습니다.");
            }

            if (borrowed && panelRenderer.panelSettings == null)
            {
                throw new InvalidOperationException("Scene-authored UITK Output에는 PanelSettings가 필요합니다.");
            }

            // Native 연결이 실패해도 획득한 자원은 같은 해제 경로로 반환한다.
            state = State.Acquired;
            try
            {
                if (borrowed)
                {
                    borrowedSettings = panelRenderer.panelSettings;
                    borrowedSortingOrder = borrowedSettings.sortingOrder;
                    borrowedSettings.sortingOrder = layerOrder;
                }
                else
                {
                    RuntimePanelSettings = Instantiate(template);
                    RuntimePanelSettings.name = $"{template.name} - Layer {layerOrder}";
                    RuntimePanelSettings.targetTexture = null;
                    RuntimePanelSettings.sortingOrder = layerOrder;
                    panelRenderer.visualTreeAsset = null;
                    panelRenderer.panelSettings = RuntimePanelSettings;
                }

                RegisterRootCallback();

                if (!IsInitialized)
                {
                    throw new InvalidOperationException
                    (
                        "UITK Output 초기화 도중 Native 콜백에서 수명이 종료됐습니다."
                    );
                }
            }
            catch (Exception initializationError)
            {
                try
                {
                    ReleaseOutputState();
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException(initializationError, cleanupError);
                }

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 콜백·Layer·기본 스타일을 반환하고 대여한 설정을 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void ReleaseOutputState()
        {
            if (state == State.Releasing) return;
            if (!IsInitialized && !reloadRegistered && root == null && layerRoot == null) return;

            var errors = new ExceptionBuffer();
            var generated = RuntimePanelSettings;
            var borrowed = borrowedSettings;
            var order = borrowedSortingOrder;
            var restoreBorrowed = !ReferenceEquals(borrowed, null);
            var detachedLayer = layerRoot;
            var unregister = reloadRegistered;

            // 파괴 콜백이 다시 들어와도 같은 자원을 중복 반환하지 않는다.
            state = State.Releasing;
            reloadRegistered = false;
            layerRoot = null;
            RuntimePanelSettings = null;
            borrowedSettings = null;
            borrowedSortingOrder = 0f;

            if (unregister && panelRenderer != null)
            {
                Cleanup(() => panelRenderer.UnregisterUIReloadCallback(HandleUIReload), ref errors);
            }

            Cleanup(RestoreRootStyle, ref errors);
            Cleanup(() => NotifyRootUpdated(null), ref errors);
            Cleanup(() => detachedLayer?.RemoveFromHierarchy(), ref errors);
            if (restoreBorrowed)
            {
                if (borrowed != null)
                {
                    Cleanup(() => borrowed.sortingOrder = order, ref errors);
                }
            }
            else if (panelRenderer != null)
            {
                Cleanup(() => panelRenderer.panelSettings = null, ref errors);
                Cleanup(() => panelRenderer.visualTreeAsset = null, ref errors);
            }

            Cleanup(() => DestroyOwnedObject(generated), ref errors);
            state = State.Inactive;
            if (errors.Count > 0)
            {
                throw errors.CreateAggregateException("UITK Presentation Output 해제가 실패했습니다.");
            }
        }

        private static void Cleanup(Action action, ref ExceptionBuffer errors)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        private static void DestroyOwnedObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }

        private void OnDestroy()
        {
            ReleaseOutputState();
        }

    #endregion

    #region 루트 연결과 스타일 복원

        // ------------------------------------------------------------
        /// <summary>
        /// Native 화면 출력에 필요한 설정과 Theme를 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Validate(out string error)
        {
            var renderer = PanelRenderer;
            if (renderer == null || renderer.panelSettings == null)
            {
                error = "UITK Presentation Output PanelRenderer/PanelSettings가 없습니다.";
            }
            else if (renderer.panelSettings.themeStyleSheet == null)
            {
                error = "UITK Presentation Output ThemeStyleSheet이 없습니다.";
            }
            else if (renderer.panelSettings.targetTexture != null)
            {
                error = "UITK ScreenOverlay Output은 Target Texture를 사용할 수 없습니다.";
            }
            else
            {
                error = "";
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Native 재연결 콜백으로 현재 Root를 동기적으로 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        internal VisualElement RequireRoot()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException
                (
                    "UITK Presentation Output이 초기화되지 않았습니다."
                );
            }

            if (!Validate(out var error))
            {
                throw new InvalidOperationException(error);
            }

            panelRenderer = PanelRenderer;
            RegisterRootCallback();
            return root ?? throw new MissingReferenceException("UITK Presentation Output Root를 찾을 수 없습니다.");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Root가 바뀌어도 유지할 Xeri Layer를 하나만 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void AttachLayerRoot(VisualElement value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!IsInitialized)
            {
                throw new InvalidOperationException
                (
                    "종료되었거나 초기화되지 않은 UITK Output에는 Layer를 연결할 수 없습니다."
                );
            }

            if (layerRoot != null && !ReferenceEquals(layerRoot, value))
            {
                throw new InvalidOperationException("UITK Presentation Output에는 Layer Root를 하나만 연결할 수 있습니다.");
            }

            layerRoot = value;
            AttachLayerToRoot();

            if (!IsInitialized || !ReferenceEquals(layerRoot, value))
            {
                throw new InvalidOperationException
                (
                    "UITK Output의 Layer 연결 도중 출력 수명이 종료됐습니다."
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 구독을 중복하지 않고 현재 Native 루트를 동기적으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RegisterRootCallback()
        {
            if (reloadRegistered)
            {
                panelRenderer.UnregisterUIReloadCallback(HandleUIReload);
            }

            reloadRegistered = true;
            panelRenderer.RegisterUIReloadCallback(HandleUIReload);

            // 등록 콜백 안에서 Output이 반환되면 미완료 Native 구독도 다시 회수한다.
            if (!IsInitialized)
            {
                panelRenderer.UnregisterUIReloadCallback(HandleUIReload);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Native Root 교체에 맞춰 소유 스타일과 Layer 연결을 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        private void HandleUIReload(PanelRenderer renderer, VisualElement value, int version)
        {
            if (!IsInitialized || !ReferenceEquals(renderer, panelRenderer)) return;
            if (!ReferenceEquals(root, value))
            {
                RestoreRootStyle();
                root = value;
            }

            if (root == null)
            {
                NotifyRootUpdated(null);
                return;
            }

            runtimeBaseline ??= Resources.Load<StyleSheet>(BASELINE_PATH);
            if (runtimeBaseline == null)
            {
                throw new MissingReferenceException($"UI Runtime Baseline '{BASELINE_PATH}'을 찾을 수 없습니다.");
            }

            // Authoring 상태는 보존하고 이번 Output이 추가한 항목만 소유한다.
            if (!root.ClassListContains(ROOT_CLASS))
            {
                root.AddToClassList(ROOT_CLASS);
                ownsRootClass = true;
            }

            if (!root.styleSheets.Contains(runtimeBaseline))
            {
                root.styleSheets.Add(runtimeBaseline);
                ownsRootStyleSheet = true;
            }

            AttachLayerToRoot();

            if (!IsInitialized || !ReferenceEquals(root, value)) return;

            NotifyRootUpdated(value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Native Root 변경을 모든 구독자에 전달하고 오류를 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private void NotifyRootUpdated(VisualElement value)
        {
            var handlers = RootUpdated;
            if (handlers == null) return;

            var errors = new ExceptionBuffer();

            foreach (Action<VisualElement> handler in handlers.GetInvocationList())
            {
                // 이전 알림 중 Release·재연결되면 오래된 Root를 뒤 구독자에게 전달하지 않는다.
                if (!ReferenceEquals(root, value)) break;

                try
                {
                    handler(value);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            if (errors.Count > 1)
            {
                throw errors.CreateAggregateException
                (
                    "UITK Presentation Output Root 변경 알림이 실패했습니다."
                );
            }
        }

        private void AttachLayerToRoot()
        {
            while
            (
                IsInitialized &&
                root != null &&
                layerRoot != null &&
                !ReferenceEquals(layerRoot.parent, root)
            )
            {
                var destination = root;
                var content = layerRoot;
                content.RemoveFromHierarchy();

                // 분리 callback이 Output을 반환하거나 Root를 교체했을 수 있다.
                if
                (
                    !IsInitialized ||
                    !ReferenceEquals(root, destination) ||
                    !ReferenceEquals(layerRoot, content)
                )
                {
                    continue;
                }

                destination.Add(content);

                // 부착 callback에서 이미 반환된 콘텐츠를 남기지 않는다.
                if
                (
                    !IsInitialized ||
                    !ReferenceEquals(root, destination) ||
                    !ReferenceEquals(layerRoot, content)
                )
                {
                    if (destination.Contains(content))
                    {
                        content.RemoveFromHierarchy();
                    }

                    continue;
                }

                if (!ReferenceEquals(content.parent, destination))
                {
                    throw new InvalidOperationException
                    (
                        "UITK Output이 현재 Root에 Layer 콘텐츠를 부착하지 못했습니다."
                    );
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Output이 추가한 스타일만 현재 Root에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        private void RestoreRootStyle()
        {
            var previous = root;
            root = null;
            if (previous != null)
            {
                if (ownsRootStyleSheet && runtimeBaseline != null)
                {
                    previous.styleSheets.Remove(runtimeBaseline);
                }

                if (ownsRootClass)
                {
                    previous.RemoveFromClassList(ROOT_CLASS);
                }
            }

            ownsRootClass = false;
            ownsRootStyleSheet = false;
        }

    #endregion

    }
}
