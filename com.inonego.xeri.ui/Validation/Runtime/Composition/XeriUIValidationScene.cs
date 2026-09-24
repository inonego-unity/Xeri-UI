/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XeriUIValidationScene.cs
수정일 : 2026-10-05

# 설명
Xeri UI 통합 Validation Scene의 명시적 composition root.
scene-authored Desktop Host를 준비한 뒤 전용 Settings로 UIRuntime을 초기화하고 Validation Shell을 조립·역순 해제한다.

# 특이사항, 제약사항
기존 전역 UIRuntime을 재사용하지 않는다. 다른 Runtime이 이미 활성화되어 있으면 결정적 Validation 환경을 위해 시작을 거부한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;

namespace inonego.Xeri.UI.Validation
{
    // ============================================================
    /// <summary>
    /// Validation Desktop의 조립과 Runtime 수명을 소유한다.
    /// </summary>
    // ============================================================
    public sealed class XeriUIValidationScene : MonoBehaviour
    {

    #region 필드와 상태

        [SerializeField]
        private UIRuntime runtime = null;

        [SerializeField]
        private UISettingsAsset settings = null;

        [SerializeField]
        private XeriValidationAssets assets = null;

        [SerializeField]
        private XeriValidationDesktopAuthoring desktop = null;

        private XeriValidationShell shell = null;
        private bool isReleased = false;

    #endregion

    #region 시작과 참조 검증

        // ------------------------------------------------------------
        /// <summary>
        /// 작성된 Desktop을 준비한 뒤 Runtime과 Shell을 조립한다.
        /// </summary>
        // ------------------------------------------------------------
        private IEnumerator Start()
        {
            ValidateReferences();

            if
            (
                UIRuntime.TryCurrent(out var current) &&
                !ReferenceEquals(current, runtime)
            )
            {
                throw new InvalidOperationException
                (
                    "통합 Validation Scene은 기존 UIRuntime을 재사용하지 않습니다. " +
                    "외부 Runtime을 종료한 뒤 Scene을 다시 실행하세요."
                );
            }

            desktop.Prepare();

            // PanelRenderer가 authored UXML과 authoring-id reference를 resolve할 시간을 준다.
            yield return null;
            yield return null;

            runtime.Initialize(settings);

            shell = new XeriValidationShell
            (
                runtime,
                assets,
                desktop
            );
            try
            {
                runtime.Main.RegisterChild(shell);
                shell.Initialize();
            }
            catch
            {
                Release();
                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 시작에 필요한 Scene 참조를 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ValidateReferences()
        {
            if (runtime == null)
            {
                throw new InvalidOperationException("Validation UIRuntime 참조가 없습니다.");
            }

            if (settings == null)
            {
                throw new InvalidOperationException("Validation UISettingsAsset 참조가 없습니다.");
            }

            if (assets == null)
            {
                throw new InvalidOperationException("Validation Asset catalog 참조가 없습니다.");
            }

            if (desktop == null)
            {
                throw new InvalidOperationException("Validation Desktop Authoring 참조가 없습니다.");
            }
        }

    #endregion

    #region 종료

        // ------------------------------------------------------------
        /// <summary>
        /// Scene이 비활성화되면 소유한 UI를 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Release();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 소유한 콘텐츠를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Release()
        {
            if (isReleased)
            {
                return;
            }

            isReleased = true;
            ReleaseShell();

            if
            (
                runtime != null &&
                runtime.IsInitialized &&
                !runtime.IsReleasing &&
                !runtime.IsReleased
            )
            {
                try
                {
                    runtime.Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Shell을 먼저 반환하고 정리 실패를 보고한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ReleaseShell()
        {
            try
            {
                shell?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                shell = null;
            }
        }

    #endregion

    }
}
