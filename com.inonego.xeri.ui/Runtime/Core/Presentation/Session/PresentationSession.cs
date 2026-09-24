/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationSession.cs
수정일 : 2026-10-07

# 설명
Immutable PresentationPlan을 실제 Layer Output, Layer roots와 placement roots로 materialize한다.
Session-local Registry와 dynamic Child PresentationSession tree의 runtime lifetime을 소유한다.
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
    /// 하나의 immutable PresentationPlan을 활성화한 runtime state.
    /// </summary>
    // ============================================================
    public sealed class PresentationSession : IDisposable
    {

    #region 내부 데이터

        private sealed class RuntimeLayer
        {
            public PresentationPlanLayer Plan = null;
            public IPresentationLayerDriver Driver = null;
            public PresentationLayerHandle Handle = null;
            public IDisposable FocusBinding = null;
            public Action ReleaseMaterialization = null;
        }

        private sealed class RuntimePlacement
        {
            public PresentationPlanPlacement Plan = null;
            public IPresentationLayerDriver Driver = null;
            public Action ReleaseRoot = null;
        }

    #endregion

    #region 필드

        internal PresentationPlan Plan { get; }
        internal PresentationLayerRegistry LayerRegistry { get; }
        internal PresentationSession Parent => parent;

        private PresentationSession parent = null;

        public bool IsDisposed { get; private set; }
        public int ChildCount => children.Count;

        private readonly Dictionary<string, RuntimePlacement> placements =
            new(StringComparer.Ordinal);
        private readonly List<RuntimeLayer> layers = new();
        private readonly List<PresentationSession> children = new();
        private readonly Func<IPresentationLayerDriver, IDisposable> bindFocus = null;
        private bool ownerControlsLifetime = false;
        private PresentationSurface embeddedSurface = null;

    #endregion

    #region 생성자

        private PresentationSession
        (
            PresentationPlan plan,
            Func<IPresentationLayerDriver, IDisposable> bindFocus,
            bool ownerControlsLifetime = false
        )
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.bindFocus = bindFocus;
            this.ownerControlsLifetime = ownerControlsLifetime;
            LayerRegistry = new PresentationLayerRegistry();
        }

    #endregion

    #region 생성

        internal static PresentationSession CreateTopLevel
        (
            PresentationPlan plan,
            Transform parent,
            PanelSettings panelSettingsTemplate,
            UGUIPresentationOutput uguiOutputTemplate,
            Func<IPresentationLayerDriver, IDisposable> bindFocus = null,
            IReadOnlyList<PresentationLayerHost> layerHosts = null,
            bool ownerControlsLifetime = false
        )
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var session = new PresentationSession
            (
                plan,
                bindFocus,
                ownerControlsLifetime
            );

            try
            {
                var layerHostMap = session.CreateLayerHostMap(layerHosts);
                var placementHostMap = session.CreatePlacementHostMap(layerHostMap);
                session.MaterializeTopLevel
                (
                    parent,
                    panelSettingsTemplate,
                    uguiOutputTemplate,
                    layerHostMap
                );
                session.MaterializePlacements(placementHostMap);
                return session;
            }
            catch (Exception exception)
            {
                throw session.RollbackCreation(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/>소유 hierarchy 아래 Child Session을 생성한다.
        /// <br/>Parent가 Child 수명을 소유하며 해제 시 함께 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationSession CreateChild
        (
            PresentationLayout layout,
            VisualElement parentRoot
        )
        {
            ThrowIfDisposed();

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (parentRoot == null)
            {
                throw new ArgumentNullException(nameof(parentRoot));
            }

            if (!OwnsChildRoot(parentRoot))
            {
                throw new InvalidOperationException
                (
                    "Child PresentationSession은 현재 Session이 직접 소유하는 UITK hierarchy에만 연결할 수 있습니다."
                );
            }

            var plan = PresentationLayoutResolver.Resolve(layout);
            ValidateEmbeddedBackend(plan, PresentationBackend.UITK);
            var child = new PresentationSession(plan, bindFocus)
            {
                parent = this,
            };

            try
            {
                child.embeddedSurface = new UITKPresentationSurface
                (
                    parentRoot,
                    plan.Layers
                );
                child.CaptureEmbeddedLayers();
                child.MaterializePlacements(null);
                ValidateChildCreationOwnership(parentRoot);
                children.Add(child);
                return child;
            }
            catch (Exception exception)
            {
                throw child.RollbackCreation(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/>소유 hierarchy 아래 Child Session을 생성한다.
        /// <br/>Parent가 Child 수명을 소유하며 해제 시 함께 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationSession CreateChild
        (
            PresentationLayout layout,
            RectTransform parentRoot
        )
        {
            ThrowIfDisposed();

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (parentRoot == null)
            {
                throw new ArgumentNullException(nameof(parentRoot));
            }

            if (!OwnsChildRoot(parentRoot))
            {
                throw new InvalidOperationException
                (
                    "Child PresentationSession은 현재 Session이 직접 소유하는 UGUI hierarchy에만 연결할 수 있습니다."
                );
            }

            var plan = PresentationLayoutResolver.Resolve(layout);
            ValidateEmbeddedBackend(plan, PresentationBackend.UGUI);
            var child = new PresentationSession(plan, bindFocus)
            {
                parent = this,
            };

            try
            {
                child.embeddedSurface = new UGUIPresentationSurface
                (
                    parentRoot,
                    plan.Layers
                );
                child.CaptureEmbeddedLayers();
                child.MaterializePlacements(null);
                ValidateChildCreationOwnership(parentRoot);
                children.Add(child);
                return child;
            }
            catch (Exception exception)
            {
                throw child.RollbackCreation(exception);
            }
        }

        private static void ValidateEmbeddedBackend
        (
            PresentationPlan plan,
            PresentationBackend expectedBackend
        )
        {
            for (var index = 0; index < plan.Layers.Count; index++)
            {
                if (plan.Layers[index].Backend == expectedBackend) continue;

                throw new InvalidOperationException
                (
                    $"Embedded Presentation Layer '{plan.Layers[index].LayerID}'의 Backend가 parent backend와 일치하지 않습니다."
                );
            }
        }

        private bool OwnsChildRoot(VisualElement candidate)
        {
            if (!IsInsideDirectLayer(candidate)) return false;

            for (var index = 0; index < children.Count; index++)
            {
                if (children[index].ContainsElement(candidate)) return false;
            }

            return true;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Child materialization callback 뒤에도 Parent와 UITK child root
        /// <br/> 소유권이 유지되는지 확인한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ValidateChildCreationOwnership(VisualElement parentRoot)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PresentationSession));
            }

            if (!OwnsChildRoot(parentRoot))
            {
                throw new InvalidOperationException
                (
                    "Child PresentationSession 생성 중 UITK child root 소유권이 변경됐습니다."
                );
            }
        }

        private bool ContainsElement(VisualElement candidate)
        {
            if (candidate == null) return false;

            for (var index = 0; index < layers.Count; index++)
            {
                if (layers[index].Driver is not IPresentationLayerDriver<VisualElement> layer)
                {
                    continue;
                }

                for (var current = candidate; current != null; current = current.parent)
                {
                    if (ReferenceEquals(current, layer.Root)) return true;
                }
            }

            return false;
        }

        private bool IsInsideDirectLayer(VisualElement candidate)
        {
            return ContainsElement(candidate);
        }

        private bool OwnsChildRoot(RectTransform candidate)
        {
            if (!IsInsideDirectLayer(candidate)) return false;

            for (var index = 0; index < children.Count; index++)
            {
                if (children[index].ContainsTransform(candidate)) return false;
            }

            return true;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Child materialization callback 뒤에도 Parent와 UGUI child root
        /// <br/> 소유권이 유지되는지 확인한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ValidateChildCreationOwnership(RectTransform parentRoot)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PresentationSession));
            }

            if (!OwnsChildRoot(parentRoot))
            {
                throw new InvalidOperationException
                (
                    "Child PresentationSession 생성 중 UGUI child root 소유권이 변경됐습니다."
                );
            }
        }

        private bool ContainsTransform(RectTransform candidate)
        {
            if (candidate == null) return false;

            for (var index = 0; index < layers.Count; index++)
            {
                if (layers[index].Driver is not IPresentationLayerDriver<RectTransform> layer)
                {
                    continue;
                }

                if (candidate == layer.Root || candidate.IsChildOf(layer.Root)) return true;
            }

            return false;
        }

        private bool IsInsideDirectLayer(RectTransform candidate)
        {
            return ContainsTransform(candidate);
        }

    #endregion

    #region 상위 레이어 구성

        private void MaterializeTopLevel
        (
            Transform parent,
            PanelSettings panelSettingsTemplate,
            UGUIPresentationOutput uguiOutputTemplate,
            IReadOnlyDictionary<string, PresentationLayerHost> layerHosts
        )
        {
            for (var index = 0; index < Plan.Layers.Count; index++)
            {
                var plan = Plan.Layers[index];
                IPresentationLayerDriver driver;
                Action releaseMaterialization;

                if
                (
                    layerHosts != null &&
                    layerHosts.TryGetValue(plan.LayerID, out var host)
                )
                {
                    driver = host.Acquire(plan, out releaseMaterialization);
                }
                else
                {
                    driver = PresentationLayerMaterializer.CreateTopLevel
                    (
                        plan,
                        parent,
                        panelSettingsTemplate,
                        uguiOutputTemplate,
                        out releaseMaterialization
                    );
                }

                AddRuntimeLayer(plan, driver, releaseMaterialization);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> explicit Scene Layer Host를 Plan Layer와 대조한다.
        /// <br/> Unity mutation 전에 검증된 lookup map으로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private Dictionary<string, PresentationLayerHost> CreateLayerHostMap
        (
            IReadOnlyList<PresentationLayerHost> layerHosts
        )
        {
            var hosts = new Dictionary<string, PresentationLayerHost>
            (
                StringComparer.Ordinal
            );

            if (layerHosts == null) return hosts;

            for (var index = 0; index < layerHosts.Count; index++)
            {
                var host = layerHosts[index];

                if (host == null)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host {index} 참조가 비어 있습니다."
                    );
                }

                if
                (
                    string.IsNullOrWhiteSpace(host.LayerID) ||
                    !Plan.TryGetLayer(host.LayerID, out var planLayer)
                )
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host '{host.name}'의 Layer ID " +
                        $"'{host.LayerID}'가 현재 Plan에 없습니다."
                    );
                }

                if (host.Backend != planLayer.Backend)
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host '{host.LayerID}' Backend({host.Backend})가 " +
                        $"Plan Backend({planLayer.Backend})와 일치하지 않습니다."
                    );
                }

                if (!hosts.TryAdd(host.LayerID, host))
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation Layer Host '{host.LayerID}'가 중복됐습니다."
                    );
                }
            }

            return hosts;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Layer Host가 제공한 Placement Host를 Plan Placement와 대조한다.
        /// <br/> 검증된 PresentationID lookup map으로 만든다.
        /// </summary>
        // ----------------------------------------------------------------------
        private Dictionary<string, PresentationPlacementHost> CreatePlacementHostMap
        (
            IReadOnlyDictionary<string, PresentationLayerHost> layerHosts
        )
        {
            var hosts = new Dictionary<string, PresentationPlacementHost>
            (
                StringComparer.Ordinal
            );

            if (layerHosts == null) return hosts;

            foreach (var pair in layerHosts)
            {
                var layerHost = pair.Value;
                var placementHosts = layerHost.PlacementHosts;

                for (var index = 0; index < placementHosts.Count; index++)
                {
                    var host = placementHosts[index];

                    if (host == null)
                    {
                        throw new InvalidOperationException
                        (
                            $"Layer Host '{layerHost.LayerID}' Placement Host {index} 참조가 비어 있습니다."
                        );
                    }

                    if
                    (
                        string.IsNullOrWhiteSpace(host.PresentationID) ||
                        !Plan.TryGetPlacement(host.PresentationID, out var placement)
                    )
                    {
                        throw new InvalidOperationException
                        (
                            $"Presentation Placement Host '{host.name}'의 Presentation ID " +
                            $"'{host.PresentationID}'가 현재 Plan에 없습니다."
                        );
                    }

                    if
                    (
                        !string.Equals
                        (
                            placement.Layer.LayerID,
                            layerHost.LayerID,
                            StringComparison.Ordinal
                        )
                    )
                    {
                        throw new InvalidOperationException
                        (
                            $"Presentation Placement Host '{host.PresentationID}'는 " +
                            $"Plan Layer '{placement.Layer.LayerID}'가 아니라 " +
                            $"Layer Host '{layerHost.LayerID}'에 연결되어 있습니다."
                        );
                    }

                    if (host.Backend != placement.Layer.Backend)
                    {
                        throw new InvalidOperationException
                        (
                            $"Presentation Placement Host '{host.PresentationID}' " +
                            $"Backend({host.Backend})가 Plan Backend({placement.Layer.Backend})와 " +
                            "일치하지 않습니다."
                        );
                    }

                    if (!hosts.TryAdd(host.PresentationID, host))
                    {
                        throw new InvalidOperationException
                        (
                            $"Presentation Placement Host '{host.PresentationID}'가 중복됐습니다."
                        );
                    }
                }
            }

            return hosts;
        }

        private void AddRuntimeLayer
        (
            PresentationPlanLayer plan,
            IPresentationLayerDriver driver,
            Action releaseMaterialization
        )
        {
            var runtime = new RuntimeLayer
            {
                Plan = plan,
                Driver = driver,
                ReleaseMaterialization = releaseMaterialization,
            };

            try
            {
                runtime.Handle = LayerRegistry.Register(plan.LayerID, driver);
                runtime.FocusBinding = bindFocus?.Invoke(driver);
                layers.Add(runtime);
            }
            catch (Exception exception)
            {
                var errors = new List<Exception>
                {
                    exception,
                };
                TryDispose(runtime.FocusBinding, errors);
                TryDispose(runtime.Handle, errors);
                TryAction(releaseMaterialization, errors);

                if (errors.Count == 1)
                {
                    throw;
                }

                throw new AggregateException
                (
                    $"Presentation Layer '{plan.LayerID}' 조립과 롤백이 실패했습니다.",
                    errors
                );
            }
        }

        private void CaptureEmbeddedLayers()
        {
            for (var index = 0; index < Plan.Layers.Count; index++)
            {
                var plan = Plan.Layers[index];

                if (!embeddedSurface.TryGetLayer(plan.LayerID, out var driver))
                {
                    throw new InvalidOperationException
                    (
                        $"Embedded Layer '{plan.LayerID}'가 materialize되지 않았습니다."
                    );
                }

                AddRuntimeLayer(plan, driver, releaseMaterialization: null);
            }
        }

    #endregion

    #region 레이어 획득

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Session의 Layer 사용 수명을 획득한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationLayerLease AcquireLayer(string layerID)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(layerID))
            {
                throw new ArgumentException("Presentation Layer ID가 비어 있습니다.", nameof(layerID));
            }

            if (!LayerRegistry.TryAcquireUsage(layerID, out var layer, out var usage))
            {
                throw new InvalidOperationException
                (
                    $"Presentation Layer '{layerID}'을 획득할 수 없습니다."
                );
            }

            return new PresentationLayerLease(layer, usage);
        }

    #endregion

    #region 배치 구성

        private void MaterializePlacements
        (
            IReadOnlyDictionary<string, PresentationPlacementHost> placementHosts
        )
        {
            for (var index = 0; index < Plan.Placements.Count; index++)
            {
                var plan = Plan.Placements[index];

                if (!LayerRegistry.TryGet(plan.Layer.LayerID, out var layerDriver))
                {
                    throw new InvalidOperationException
                    (
                        $"Presentation '{plan.PresentationID}'의 Layer가 materialize되지 않았습니다."
                    );
                }

                RuntimePlacement runtime;

                if
                (
                    placementHosts != null &&
                    placementHosts.TryGetValue(plan.PresentationID, out var host)
                )
                {
                    var driver = host.Acquire
                    (
                        plan,
                        layerDriver,
                        out var releaseRoot
                    );
                    runtime = new RuntimePlacement
                    {
                        Plan = plan,
                        Driver = driver,
                        ReleaseRoot = releaseRoot,
                    };
                }
                else
                {
                    runtime = CreateGeneratedPlacement(plan, layerDriver);
                }

                placements.Add(plan.PresentationID, runtime);
            }
        }

        private static RuntimePlacement CreateGeneratedPlacement
        (
            PresentationPlanPlacement plan,
            IPresentationLayerDriver layerDriver
        )
        {
            if (layerDriver is IPresentationLayerDriver<RectTransform> ugui)
            {
                var root = UGUIPresentationSurface.CreateRect
                (
                    $"Presentation - {plan.PresentationID}",
                    ugui.Root
                );
                root.SetAsLastSibling();
                return new RuntimePlacement
                {
                    Plan = plan,
                    Driver = new UGUIPresentationLayer(root),
                    ReleaseRoot = () => UGUIPresentationSurface.DestroyObject(root.gameObject),
                };
            }

            if (layerDriver is IPresentationLayerDriver<VisualElement> uitk)
            {
                var root = UITKPresentationSurface.CreateRoot
                (
                    $"xeri-presentation-{plan.PresentationID}"
                );
                root.style.zIndex = plan.LocalOrder;
                uitk.Root.Add(root);
                return new RuntimePlacement
                {
                    Plan = plan,
                    Driver = new UITKPresentationLayer(root),
                    ReleaseRoot = root.RemoveFromHierarchy,
                };
            }

            throw new InvalidOperationException
            (
                $"Presentation '{plan.PresentationID}'의 backend placement를 materialize할 수 없습니다."
            );
        }

        internal bool TryAcquirePlacement
        (
            string presentationID,
            out IPresentationLayerDriver driver,
            out Lease usage
        )
        {
            ThrowIfDisposed();

            if
            (
                string.IsNullOrWhiteSpace(presentationID) ||
                !placements.TryGetValue(presentationID, out var runtime)
            )
            {
                driver = null;
                usage = null;
                return false;
            }

            if
            (
                !LayerRegistry.TryAcquireUsage
                (
                    runtime.Plan.Layer.LayerID,
                    out _,
                    out usage
                )
            )
            {
                driver = null;
                return false;
            }

            driver = runtime.Driver;
            return true;
        }

        public bool ContainsPresentation(string presentationID)
        {
            return
                !IsDisposed &&
                !string.IsNullOrWhiteSpace(presentationID) &&
                placements.ContainsKey(presentationID);
        }

    #endregion

    #region 해제

        private Exception RollbackCreation(Exception failure)
        {
            try
            {
                DisposeCore();
                return failure;
            }
            catch (Exception cleanupException)
            {
                return new AggregateException
                (
                    "Presentation Session 생성과 롤백이 실패했습니다.",
                    failure,
                    cleanupException
                );
            }
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(PresentationSession));
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 활성 소비자가 없을 때 Child와 표시 자원을 역순으로 해제한다.
        /// <br/> Parent에서 분리하고 독립 정리 실패를 모아 보고한다.
        /// <br/> owner-controlled Root Session은 직접 해제할 수 없다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            if (IsDisposed) return;

            if (ownerControlsLifetime)
            {
                throw new InvalidOperationException
                (
                    "이 Presentation Session의 수명은 소유자가 관리합니다."
                );
            }

            DisposeCore();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이후 Session 수명을 조립 owner만 종료하도록 고정한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void SetOwnerControlledLifetime()
        {
            ThrowIfDisposed();
            ownerControlsLifetime = true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 외부 Dispose가 제한된 Session을 소유자 종료 경로에서 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void DisposeFromOwner()
        {
            DisposeCore();
        }

        private void DisposeCore()
        {
            if (IsDisposed) return;

            ValidateCanDispose();
            IsDisposed = true;
            parent?.children.Remove(this);
            parent = null;
            var errors = new List<Exception>();

            for (var index = children.Count - 1; index >= 0; index--)
            {
                TryDisposeChild(children[index], errors);
            }

            children.Clear();

            for (var index = Plan.Placements.Count - 1; index >= 0; index--)
            {
                var presentationID = Plan.Placements[index].PresentationID;

                if (placements.TryGetValue(presentationID, out var placement))
                {
                    TryAction(placement.ReleaseRoot, errors);
                }
            }

            placements.Clear();

            for (var index = layers.Count - 1; index >= 0; index--)
            {
                var layer = layers[index];
                TryDispose(layer.FocusBinding, errors);
                TryDispose(layer.Handle, errors);
            }

            TryDispose(embeddedSurface, errors);
            embeddedSurface = null;

            if (!LayerRegistry.IsDisposed)
            {
                TryDispose(LayerRegistry, errors);
            }

            for (var index = layers.Count - 1; index >= 0; index--)
            {
                TryAction(layers[index].ReleaseMaterialization, errors);
            }

            layers.Clear();

            if (errors.Count > 0)
            {
                throw new AggregateException("Presentation Session 해제가 실패했습니다.", errors);
            }
        }

        internal void ValidateCanDispose()
        {
            for (var index = 0; index < children.Count; index++)
            {
                children[index].ValidateCanDispose();
            }

            if (LayerRegistry.HasConsumers)
            {
                throw new InvalidOperationException
                (
                    "Presentation Session에 활성 Presentation 소비자가 남아 있습니다."
                );
            }
        }

        private static void TryDisposeChild
        (
            PresentationSession child,
            ICollection<Exception> errors
        )
        {
            if (child == null) return;

            try
            {
                child.DisposeFromOwner();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        private static void TryDispose(IDisposable target, ICollection<Exception> errors)
        {
            if (target == null) return;

            try
            {
                target.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        private static void TryAction(Action action, ICollection<Exception> errors)
        {
            if (action == null) return;

            try
            {
                action();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

    #endregion

    }
}
