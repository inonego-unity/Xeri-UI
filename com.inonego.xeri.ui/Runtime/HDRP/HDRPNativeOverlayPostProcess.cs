/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : HDRPNativeOverlayPostProcess.cs
수정일 : 2026-10-21

# 설명
HDRP가 CustomPP를 예약하는 시점에 Native UI 소유권을 획득하고 실제 카메라 버퍼에 합성한다.
정책·카메라 범위·스텐실 수명은 이 진입점이 소유한다. UI별 텍스처와 재질은 만들지 않는다.

# 제약사항
단일 전체 화면 Game 카메라, Linear SDR, FP16 Post Process 버퍼만 지원한다.
Before/After 등록은 Unity가 서로 다른 VolumeComponent 타입을 요구하므로 얇은 파생 진입점을 둔다.
========================================================================= BLOCK_HEADER_END */

using System;

using Unity;
using Unity.Collections;

using UnityEngine;
using UnityEngine.Experimental;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace inonego.Xeri.UI.HDRP
{
    // ============================================================
    /// <summary>
    /// Native UI를 HDRP 주요 후처리 뒤의 FP16 버퍼에 합성한다.
    /// </summary>
    // ============================================================
    [Serializable]
    public class HDRPNativeOverlayPostProcess : CustomPostProcessVolumeComponent, IPostProcessComponent
    {

    #region 설정과 상태

        private static Camera candidate;
        private static ScriptableRenderContext context;
        private static HDRPNativeOverlayPostProcess owner;
        private static Camera observedCamera;
        private static string frameFallbackReason;
        private static readonly int stencilId = Shader.PropertyToID("_XeriHDRPNativeUIStencil");

        private RendererList toolkit;
        private RendererList remaining;

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 마지막 HDRP Game 카메라가 Native로 남은 이유를 제공한다.
        /// <br/> null은 미지원 사유가 없다는 뜻이며 GPU 성공을 보증하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static string NativeFallbackReason { get; private set; }

        // ------------------------------------------------------------
        /// <summary>
        /// 파생 Unity 진입점이 선택하는 주요 후처리 참여 시점.
        /// </summary>
        // ------------------------------------------------------------
        protected virtual bool Affected => false;

        // ------------------------------------------------------------
        /// <summary>
        /// Unity가 타입별로 등록하는 삽입 지점.
        /// </summary>
        // ------------------------------------------------------------
        public override CustomPostProcessInjectionPoint injectionPoint => Affected
            ? CustomPostProcessInjectionPoint.BeforePostProcess
            : CustomPostProcessInjectionPoint.AfterPostProcess;

        // ------------------------------------------------------------
        /// <summary>
        /// Scene View는 원래 Native 출력을 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        public override bool visibleInSceneView => false;

    #endregion

    #region 카메라 수명과 소유권

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Restore();
            candidate = null;
            observedCamera = null;
            frameFallbackReason = null;
            NativeFallbackReason = null;
            Subscribe();
        }

        private static void Subscribe()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Editor와 Player에서 같은 카메라 범위 복원을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Setup()
        {
            Subscribe();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 볼륨 재생성·해제에서도 자신이 소유한 상태만 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Cleanup()
        {
            if (ReferenceEquals(owner, this))
            {
                Restore();
            }
        }

        private static void BeginCamera(ScriptableRenderContext renderContext, Camera camera)
        {
            Restore();
            context = renderContext;
            candidate = null;
            observedCamera = null;
            frameFallbackReason = null;
            if (GraphicsSettings.currentRenderPipeline is not HDRenderPipelineAsset || camera.cameraType != CameraType.Game) return;
            observedCamera = camera;
            frameFallbackReason = GetCameraFallbackReason(camera);
            if (frameFallbackReason == null)
            {
                candidate = camera;
            }

            if (!UIPresentationOutputPolicy.TryGetComposition(out _, out _))
            {
                NativeFallbackReason = null;
            }
            else if (frameFallbackReason != null)
            {
                ReportNativeFallback(frameFallbackReason);
            }
        }

        private static void EndCamera(ScriptableRenderContext renderContext, Camera camera)
        {
            if (!ReferenceEquals(camera, observedCamera)) return;
            if (ReferenceEquals(owner, null) && UIPresentationOutputPolicy.TryGetComposition(out _, out _))
            {
                ReportNativeFallback(frameFallbackReason ?? "선택된 Custom Post Process가 예약되지 않았습니다. 후처리·Volume·등록 상태를 확인하세요.");
            }

            Restore();
            candidate = null;
            observedCamera = null;
        }

        private static string GetCameraFallbackReason(Camera camera)
        {
            if (Camera.allCamerasCount != 1) return "단일 Game 카메라만 지원합니다.";
            if (camera.targetTexture != null || camera.rect != new Rect(0, 0, 1, 1)) return "전체 화면 기본 출력에서만 지원합니다.";
            if (camera.stereoEnabled) return "XR 출력은 지원하지 않습니다.";
            if (camera.allowDynamicResolution) return "동적 해상도에서는 Native 출력을 유지합니다.";
            if (HDROutputSettings.main.active) return "HDR 디스플레이 출력은 지원하지 않습니다.";
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return "Linear 색 공간만 지원합니다.";
            return null;
        }

        private static void ReportNativeFallback(string reason)
        {
            frameFallbackReason = reason;
            if (NativeFallbackReason == reason) return;
            NativeFallbackReason = reason;
            Debug.LogWarning($"Xeri UI: Native 출력 유지 — {reason}");
        }

        private static void Restore()
        {
            if (ReferenceEquals(owner, null)) return;
            NativeUIOverlayOwnership.Release(owner, candidate);
            owner = null;
        }

    #endregion

    #region 정책과 합성 예약

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 실제 CustomPP 예약 중에만 Native 출력을 인계받는다.
        /// <br/> PP/CustomPP 생략 시 Native가 그대로 출력된다.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsActive()
        {
            if
            (
                !active || candidate == null ||
                !UIPresentationOutputPolicy.TryGetComposition(out _, out var affected) || Affected != affected
            )
            {
                return false;
            }

            if
            (
                GraphicsSettings.currentRenderPipeline is not HDRenderPipelineAsset pipeline ||
                pipeline.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat !=
                    PostProcessBufferFormat.R16G16B16A16
            )
            {
                ReportNativeFallback("HDRP 후처리 버퍼를 R16G16B16A16으로 설정해야 합니다.");
                return false;
            }

            if (!SystemInfo.IsFormatSupported(GraphicsFormat.D32_SFloat_S8_UInt, GraphicsFormatUsage.Render))
            {
                ReportNativeFallback("Native UI 클리핑에 필요한 깊이·스텐실 포맷을 지원하지 않습니다.");
                return false;
            }

            if (ReferenceEquals(owner, this)) return true;
            if (!ReferenceEquals(owner, null)) return false;
            toolkit = context.CreateUIOverlayRendererList(candidate, UISubset.UIToolkit_UGUI);
            remaining = context.CreateUIOverlayRendererList(candidate, UISubset.LowLevel);
            if (!toolkit.isValid || !remaining.isValid || !NativeUIOverlayOwnership.TryAcquire(this, candidate))
            {
                ReportNativeFallback("Native UI 렌더 목록 또는 Overlay 소유권을 획득하지 못했습니다.");
                return false;
            }

            owner = this;
            NativeFallbackReason = null;
            frameFallbackReason = null;
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 화면 전체 합성은 타일 단위 처리를 사용하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsTileCompatible() => false;

    #endregion

    #region 네이티브 UI 합성

        // ------------------------------------------------------------
        /// <summary>
        /// 원래 UI 목록과 정렬을 유지해 카메라의 FP16 destination에 그린다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Render(CommandBuffer command, HDCamera camera, RTHandle source, RTHandle destination)
        {
            HDUtils.BlitCameraTexture(command, source, destination);
            if (!ReferenceEquals(owner, this) || !ReferenceEquals(camera.camera, candidate)) return;

            var descriptor = new RenderTextureDescriptor(destination.rt.width, destination.rt.height)
            {
                graphicsFormat = GraphicsFormat.None,
                depthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt,
                msaaSamples = 1
            };
            command.GetTemporaryRT(stencilId, descriptor);

            // UITK mask는 z-fail 스텐실 상태를 사용한다. 색상 타깃만 바꾸는 Unsafe draw로 대체하지 않는다.
            var attachments = new NativeArray<AttachmentDescriptor>(2, Allocator.Temp);
            var subpasses = new NativeArray<SubPassDescriptor>(1, Allocator.Temp);
            try
            {
                attachments[0] = new AttachmentDescriptor(destination.rt.graphicsFormat)
                {
                    loadStoreTarget = destination,
                    loadAction = RenderBufferLoadAction.Load,
                    storeAction = RenderBufferStoreAction.Store
                };
                attachments[1] = new AttachmentDescriptor(descriptor.depthStencilFormat)
                {
                    loadStoreTarget = new RenderTargetIdentifier(stencilId),
                    loadAction = RenderBufferLoadAction.Clear,
                    storeAction = RenderBufferStoreAction.DontCare,
                    clearDepth = 1,
                    clearStencil = 0
                };
                var colors = new AttachmentIndexArray(1);
                colors[0] = 0;
                subpasses[0] = new SubPassDescriptor
                {
                    colorOutputs = colors
                };
                command.BeginRenderPass
                (
                    destination.rt.width, destination.rt.height, 1, attachments, 1, subpasses
                );
                command.SetViewport(new Rect(0, 0, camera.actualWidth, camera.actualHeight));
                command.DrawRendererList(toolkit);
                command.EndRenderPass();
            }
            finally
            {
                attachments.Dispose();
                subpasses.Dispose();
            }

            command.SetRenderTarget(destination);
            command.DrawRendererList(remaining);
            command.ReleaseTemporaryRT(stencilId);
        }

    #endregion

    }
}
