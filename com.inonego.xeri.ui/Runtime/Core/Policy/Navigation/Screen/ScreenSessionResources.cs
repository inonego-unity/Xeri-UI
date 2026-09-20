/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScreenSessionResources.cs
수정일 : 2026-09-17
# 설명
한 Screen Session이 획득한 Source, Layer, 입력과 하위 표시 자원의 소유권을 묶는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ======================================================================
    /// <summary>
    /// <br/> Screen 준비부터 종료까지 함께 이동하는 자원을 소유한다.
    /// <br/> 외부 획득 중 종료되면 늦게 반환된 자원을 공개하지 않고 즉시 반환한다.
    /// </summary>
    // ======================================================================
    internal sealed class ScreenSessionResources
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Source가 조립하고 아직 반환하지 않은 Screen backend 묶음.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenInstance Instance => instance;

        private ScreenInstance instance = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 Driver가 조립하고 아직 반환하지 않은 입력 Session.
        /// </summary>
        // ------------------------------------------------------------
        public ScreenInputSession InputSession => inputSession;

        private ScreenInputSession inputSession = null;
        private readonly IScreenSource source = null;
        private readonly List<IDisposable> childHandles = new List<IDisposable>();

        private Lease layerUsage = null;
        private bool isReleasing = false;

    #endregion

    #region 생성자

        // ----------------------------------------------------------------------
        /// <summary>
        /// 준비 Session이 이미 획득한 Source와 Layer Usage 소유권을 받는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public ScreenSessionResources
        (
            IScreenSource source,
            Lease layerUsage
        ) : base()
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.layerUsage = layerUsage;
        }

    #endregion

    #region 메서드

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Source에서 ScreenInstance를 획득하고 이 소유자에 연결한다.
        /// <br/> 획득 callback 중 종료됐으면 늦게 반환된 Instance를 Source에 한 번 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public bool TryAcquireSource(ScreenViewScope scope)
        {
            var acquired = source.Acquire(scope);

            // 획득 중 종료된 Session에 외부 자원을 다시 연결하지 않는다.
            if (isReleasing)
            {
                if (acquired != null)
                {
                    source.Release(acquired);
                }

                return false;
            }

            instance = acquired;
            return true;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Screen 입력 정책을 획득하고 이 소유자에 연결한다.
        /// <br/> 획득 callback 중 종료됐으면 늦게 반환된 Session을 한 번 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public bool TryAcquireInput
        (
            IScreenInputDriver driver,
            ScreenOptions options
        )
        {
            var acquired = driver.Acquire(options);

            // 획득 중 종료된 Session이 입력 정책을 다시 점유하지 않게 즉시 반환한다.
            if (isReleasing)
            {
                acquired?.Release(false);
                return false;
            }

            inputSession = acquired;
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 부모 Screen과 함께 종료할 하위 표시 Handle을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public void RegisterChild(IDisposable handle)
        {
            childHandles.Add(handle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 새 자원 연결을 막고 현재 자원의 attempt-once 종료를 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        public void BeginRelease()
        {
            isReleasing = true;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 하위 표시 Handle을 생성 역순으로 소유 목록에서 먼저 제거한 뒤 한 번 해제한다.
        /// <br/> 실패한 Handle은 다시 보관하지 않고 나머지 독립 정리를 계속한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public void ReleaseChildren(List<Exception> errors)
        {
            for (var i = childHandles.Count - 1; i >= 0; i--)
            {
                var handle = childHandles[i];
                childHandles.RemoveAt(i);

                try
                {
                    handle.Dispose();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Source 소유권을 먼저 비운 뒤 Instance를 한 번 반환한다.
        /// <br/> 반환 실패 뒤에도 같은 Instance를 다시 반환하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        public void ReleaseSource(List<Exception> errors)
        {
            var current = instance;
            instance = null;

            if (current == null) return;

            try
            {
                source.Release(current);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Layer Usage 소유권을 먼저 비운 뒤 한 번 반환한다.
        /// <br/> 반환 실패 뒤에도 같은 Usage를 다시 반환하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        public void ReleaseLayer(List<Exception> errors)
        {
            var current = layerUsage;
            layerUsage = null;

            if (current == null) return;

            try
            {
                current.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 입력 복원 정책을 Controller가 적용하도록 소유 중인 Session을 한 번 꺼낸다.
        /// </summary>
        // ----------------------------------------------------------------------
        public ScreenInputSession TakeInputSession()
        {
            var current = inputSession;
            inputSession = null;
            return current;
        }

    #endregion

    }
}
