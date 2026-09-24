/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriWindowContainer.cs
수정일 : 2026-09-22

# 설명
Xeri Window Panel의 UITK hierarchy 부착·제거와 표시 순서만 담당하는 낮은 수준의 host.
Window Controller, Registry 등록, View Source, 입력 binding 수명은 상위 Workspace/Session이 소유한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// 이미 조립된 XeriWindowPanel을 표시하는 UITK host.
    /// </summary>
    // ============================================================
    internal sealed class XeriWindowContainer : VisualElement, IDisposable
    {

    #region 필드

        private const string CONTAINER_UXML_PATH = "XeriUI/Window/XeriWindowContainer";
        private const string CONTAINER_USS_PATH  = "XeriUI/Window/XeriWindowContainer";

        // ------------------------------------------------------------
        /// <summary>
        /// Window panel이 배치되는 layer.
        /// </summary>
        // ------------------------------------------------------------
        internal VisualElement WindowLayer => windowLayer;

        private readonly VisualElement windowLayer = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 표시 순서를 읽는 Window Registry.
        /// </summary>
        // ------------------------------------------------------------
        internal IXeriWindowRegistry Registry => registry;

        private readonly IXeriWindowRegistry registry = null;
        private readonly Dictionary<XeriWindowHandle, XeriWindowPanel> panels = new();
        private bool isRegistryBound = false;
        private bool isDisposed = false;

    #endregion

    #region 생성자

        // ----------------------------------------------------------------------
        /// <summary>
        /// 지정 Registry의 표시 순서를 반영하는 Window Container를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal XeriWindowContainer(IXeriWindowRegistry registry) : base()
        {
            name = "xeri-window-container";
            AddToClassList("xeri-window-container");
            ApplyDefaultLayout();
            LoadStyleSheet();

            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            windowLayer = CreateWindowLayer();

            ApplyWindowLayerLayout();
            hierarchy.Add(windowLayer);

            try
            {
                BindRegistry();
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };
                UnbindRegistry(errors);

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    "Window Container Registry 연결과 롤백이 실패했습니다.",
                    errors
                );
            }
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Window record와 option을 표시하는 Panel을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        internal XeriWindowPanel CreatePanel
        (
            XeriWindowRecord record,
            XeriWindowOptions? options
        )
        {
            ThrowIfDisposed();

            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            var panel = new XeriWindowPanel();
            panel.ApplyOptions(options ?? XeriWindowOptions.Default());
            panel.ApplyTheme(record.ThemeID);
            panel.ApplyTitle(record.Title);
            panel.ApplyTitleIcon(record.Icon);

            return panel;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Registry에 이미 등록된 Window Handle과 Panel을 표시 hierarchy에 연결한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void AttachWindow
        (
            XeriWindowHandle handle,
            XeriWindowPanel panel
        )
        {
            ThrowIfDisposed();

            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (panel == null)
            {
                throw new ArgumentNullException(nameof(panel));
            }

            if (!registry.Contains(handle))
            {
                throw new InvalidOperationException
                (
                    $"Registry에 등록되지 않은 Window Handle은 Container에 연결할 수 없습니다. ID: {handle.ID}"
                );
            }

            if (panels.TryGetValue(handle, out var current))
            {
                if (ReferenceEquals(current, panel)) return;

                throw new InvalidOperationException
                (
                    $"Window Handle '{handle.ID}'에 다른 Panel이 이미 연결되어 있습니다."
                );
            }

            panels.Add(handle, panel);

            try
            {
                windowLayer.Add(panel);
                ApplyWindowOrder();
            }
            catch
            {
                panels.Remove(handle);
                panel.RemoveFromHierarchy();
                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Window Panel을 표시 hierarchy에서만 분리한다.
        /// </summary>
        // ------------------------------------------------------------
        internal bool DetachWindow(XeriWindowHandle handle)
        {
            if (handle == null) return false;
            if (!panels.TryGetValue(handle, out var panel)) return false;

            panels.Remove(handle);
            panel?.RemoveFromHierarchy();

            return true;
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Container USS를 Resources에서 로드한다.
        /// </summary>
        // ------------------------------------------------------------
        private void LoadStyleSheet()
        {
            var styleSheet = Resources.Load<StyleSheet>(CONTAINER_USS_PATH);

            if (styleSheet == null)
            {
                throw new InvalidOperationException
                (
                    $"XeriWindowContainer USS를 로드할 수 없습니다. Path: {CONTAINER_USS_PATH}"
                );
            }

            styleSheets.Add(styleSheet);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Container가 부모 영역을 채우도록 기본 layout을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyDefaultLayout()
        {
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.right = 0f;
            style.bottom = 0f;
            style.flexGrow = 1f;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Window layer가 Container 영역 안에서 배치되도록 기본 layout을 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplyWindowLayerLayout()
        {
            windowLayer.style.position = Position.Absolute;
            windowLayer.style.left = 0f;
            windowLayer.style.top = 0f;
            windowLayer.style.right = 0f;
            windowLayer.style.bottom = 0f;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Container UXML에서 Window layer를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static VisualElement CreateWindowLayer()
        {
            var template = Resources.Load<VisualTreeAsset>(CONTAINER_UXML_PATH);

            if (template == null)
            {
                throw new InvalidOperationException
                (
                    $"XeriWindowContainer UXML을 로드할 수 없습니다. Path: {CONTAINER_UXML_PATH}"
                );
            }

            var tree = template.CloneTree();
            var windowLayer = tree.Q<VisualElement>("window-layer");

            if (windowLayer == null)
            {
                throw new InvalidOperationException
                (
                    "XeriWindowContainer UXML에 window-layer가 없습니다."
                );
            }

            windowLayer.RemoveFromHierarchy();

            return windowLayer;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 변경을 Container 표시 순서 갱신으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private void BindRegistry()
        {
            isRegistryBound = true;
            registry.OnOrderChange += OnRegistryOrderChange;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 표시 순서 구독을 한 번 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        private void UnbindRegistry(List<Exception> errors)
        {
            if (!isRegistryBound) return;

            isRegistryBound = false;

            try
            {
                registry.OnOrderChange -= OnRegistryOrderChange;
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종료된 Container 사용을 거부한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(XeriWindowContainer));
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 순서를 Window layer의 실제 Panel 순서에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ApplyWindowOrder()
        {
            var ordered = registry.Records;
            var orderedPanels = new List<XeriWindowPanel>();

            foreach (var record in ordered)
            {
                if (!registry.TryGetHandle(record.ID, out var handle)) continue;
                if (!panels.TryGetValue(handle, out var panel)) continue;

                orderedPanels.Add(panel);
            }

            foreach (var panel in orderedPanels)
            {
                panel.BringToFront();
            }
        }

    #endregion

    #region 이벤트 핸들러

        // ------------------------------------------------------------
        /// <summary>
        /// Registry order 변경을 Window layer z-order에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnRegistryOrderChange(object sender, EventArgs e)
        {
            if (isDisposed) return;

            ApplyWindowOrder();
        }

    #endregion

    #region IDisposable

        // ------------------------------------------------------------
        /// <summary>
        /// Registry 구독과 Panel hierarchy 연결만 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            var errors = new List<Exception>();
            UnbindRegistry(errors);

            foreach (var panel in panels.Values)
            {
                try
                {
                    panel?.RemoveFromHierarchy();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            panels.Clear();

            if (errors.Count > 0)
            {
                throw new AggregateException
                (
                    "Window Container 해제가 실패했습니다.",
                    errors
                );
            }
        }

    #endregion

    }
}
