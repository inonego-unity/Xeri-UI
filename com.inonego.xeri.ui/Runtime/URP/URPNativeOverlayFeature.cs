/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : URPNativeOverlayFeature.cs
수정일 : 2026-10-28

# 설명
URP의 카메라 attachment에 Unity Native UI 목록을 중복 없이 합성한다.
Feature가 정책 적용, 지원 조건 및 카메라 소유권을 소유하고 중첩 Pass는 Unity 호출만 전달한다.

# 제약사항
단일 화면 카메라, Linear SDR, 원래 해상도와 일치하는 색상·스텐실 attachment만 지원한다.
FP16 요청의 실제 포맷이 다르면 Native 출력을 유지한다. UI별 Texture와 Material은 생성하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.Experimental;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace inonego.Xeri.UI.URP
{
    // ============================================================
    /// <summary>
    /// URP Native UI 합성을 예약하고 카메라 범위의 전역 소유권을 관리한다.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class URPNativeOverlayFeature : ScriptableRendererFeature
    {

    #region 설정과 상태

        private NativeOverlayPass pass;
        private Camera ownedCamera;
        private bool requiresFp16;

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 마지막 Game 카메라 검사에서 Native로 반환한 이유.
        /// <br/> null은 미지원 사유가 없다는 뜻이며 GPU 성공을 보증하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public string NativeFallbackReason { get; private set; }

    #endregion

    #region 생성과 해제

        // ------------------------------------------------------------
        /// <summary>
        /// Unity가 호출할 Pass와 카메라 종료 복원을 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Create()
        {
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RestoreOwnership();
            NativeFallbackReason = null;
            pass = new NativeOverlayPass(this);
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Feature 재생성·해제에서도 자신이 획득한 상태만 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        protected override void Dispose(bool disposing)
        {
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RestoreOwnership();
            NativeFallbackReason = null;
            pass = null;
            base.Dispose(disposing);
        }

    #endregion

    #region 정책 적용과 지원 조건

        // ------------------------------------------------------------
        /// <summary>
        /// Runtime 정책을 해당 카메라의 Unity Pass 시점에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || renderingData.cameraData.cameraType != CameraType.Game) return;

            if (!UIPresentationOutputPolicy.TryGetComposition(out requiresFp16, out var affected))
            {
                NativeFallbackReason = null;
                return;
            }

            pass.renderPassEvent = affected
                ? RenderPassEvent.BeforeRenderingPostProcessing
                : RenderPassEvent.AfterRenderingPostProcessing;
            renderer.EnqueuePass(pass);
        }

        private bool SupportsCamera(UniversalCameraData cameraData, out string reason)
        {
            var camera = cameraData.camera;
            reason = null;
            if (camera == null || cameraData.cameraType != CameraType.Game || cameraData.renderType != CameraRenderType.Base)
            {
                reason = "단일 Game Base 카메라만 지원합니다.";
            }
            else if (Camera.allCamerasCount != 1)
            {
                reason = "활성 카메라가 여러 개이면 Native 출력을 유지합니다.";
            }
            else if (!cameraData.isDefaultViewport || camera.targetTexture != null)
            {
                reason = "전체 viewport의 화면 출력만 지원합니다.";
            }
            else if (cameraData.xrRendering || cameraData.isHDROutputActive || QualitySettings.activeColorSpace != ColorSpace.Linear)
            {
                reason = "Linear SDR 화면만 지원하며 XR과 HDR 디스플레이는 미지원입니다.";
            }
            else if (requiresFp16 && !cameraData.isHdrEnabled)
            {
                reason = "FP16 출력에는 카메라 HDR이 필요합니다.";
            }
            else if
            (
                !Mathf.Approximately(cameraData.renderScale, 1f) ||
                !Mathf.Approximately(ScalableBufferManager.widthScaleFactor, 1f) ||
                !Mathf.Approximately(ScalableBufferManager.heightScaleFactor, 1f)
            )
            {
                reason = "Render Scale과 동적 해상도 배율이 1이어야 합니다.";
            }
            else if
            (
                camera.TryGetComponent<UniversalAdditionalCameraData>(out var additional) &&
                additional.cameraStack != null && additional.cameraStack.Count > 0
            )
            {
                reason = "카메라 스택은 미지원입니다.";
            }

            return reason == null;
        }

        private bool SupportsAttachments(RenderGraph graph, UniversalResourceData resources, out string reason)
        {
            reason = null;
            if
            (
                resources.isActiveTargetBackBuffer ||
                !resources.activeColorTexture.IsValid() || !resources.activeDepthTexture.IsValid()
            )
            {
                reason = "합성할 카메라 색상·깊이 attachment가 없습니다.";
                return false;
            }

            var color = graph.GetTextureDesc(resources.activeColorTexture);
            var depth = graph.GetTextureDesc(resources.activeDepthTexture);
            if (requiresFp16 && color.format != GraphicsFormat.R16G16B16A16_SFloat)
            {
                reason = "실제 카메라 색상 attachment가 FP16이 아닙니다.";
            }
            else if (!GraphicsFormatUtility.IsStencilFormat(depth.format))
            {
                reason = "Native UI 클리핑에 필요한 스텐실 attachment가 없습니다.";
            }
            else if (color.width != depth.width || color.height != depth.height || color.msaaSamples != depth.msaaSamples)
            {
                reason = "색상·스텐실 attachment의 크기와 샘플 수가 일치하지 않습니다.";
            }

            return reason == null;
        }

        private void ReportNativeFallback(string reason)
        {
            if (NativeFallbackReason == reason) return;
            NativeFallbackReason = reason;
            Debug.LogWarning($"Xeri UI: Native 출력 유지 — {reason}", this);
        }

    #endregion

    #region 화면 합성과 복원

        private void RecordOverlay(RenderGraph graph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resources = frameData.Get<UniversalResourceData>();
            if (!SupportsCamera(cameraData, out var reason) || !SupportsAttachments(graph, resources, out reason))
            {
                ReportNativeFallback(reason);
                return;
            }

            // 다른 Feature가 이미 합성 중인 경우를 Native fallback으로 오인하지 않는다.
            NativeFallbackReason = null;
            if (!NativeUIOverlayOwnership.TryAcquire(this, cameraData.camera)) return;

            // 전역 억제와 Pass 등록은 이 Feature의 한 카메라 수명에 귀속된다.
            ownedCamera = cameraData.camera;
            try
            {
                using (var builder = graph.AddRasterRenderPass<OverlayData>("Xeri Native UITK/UGUI Overlay", out var data))
                {
                    data.RendererList = graph.CreateUIOverlayRendererList(ownedCamera, UISubset.UIToolkit_UGUI);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                    builder.UseRendererList(data.RendererList);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc
                    (
                        static (OverlayData data, RasterGraphContext context) =>
                        {
                            // RenderGraph가 함께 연결한 색상·스텐실에 Native 목록을 기록한다.
                            context.cmd.DrawRendererList(data.RendererList);
                        }
                    );
                }

                // Unity가 분리한 나머지 Native 목록도 같은 소유권 안에서 보존한다.
                using (var builder = graph.AddUnsafePass<OverlayData>("Xeri Remaining Native Overlay", out var data))
                {
                    data.Color = resources.activeColorTexture;
                    data.RendererList = graph.CreateUIOverlayRendererList(ownedCamera, UISubset.LowLevel);
                    builder.UseTexture(data.Color, AccessFlags.ReadWrite);
                    builder.UseRendererList(data.RendererList);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc
                    (
                        static (OverlayData data, UnsafeGraphContext context) =>
                        {
                            context.cmd.SetRenderTarget(data.Color);
                            context.cmd.DrawRendererList(data.RendererList);
                        }
                    );
                }
            }
            catch
            {
                RestoreOwnership();
                throw;
            }
        }

        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (ReferenceEquals(camera, ownedCamera))
            {
                RestoreOwnership();
            }
        }

        private void RestoreOwnership()
        {
            if (ReferenceEquals(ownedCamera, null)) return;
            NativeUIOverlayOwnership.Release(this, ownedCamera);
            ownedCamera = null;
        }

        private sealed class OverlayData
        {
            internal TextureHandle Color;
            internal RendererListHandle RendererList;
        }

        private sealed class NativeOverlayPass : ScriptableRenderPass
        {
            private readonly URPNativeOverlayFeature owner;

            internal NativeOverlayPass(URPNativeOverlayFeature owner)
            {
                this.owner = owner;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Unity의 기록 요청을 정책과 소유권을 가진 Feature에 전달한다.
            /// </summary>
            // ------------------------------------------------------------
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                owner.RecordOverlay(graph, frameData);
            }
        }

    #endregion

    }
}
