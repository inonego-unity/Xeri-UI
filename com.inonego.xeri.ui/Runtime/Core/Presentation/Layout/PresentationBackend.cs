/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationBackend.cs
수정일 : 2026-09-26

# 설명
현재 Presentation Core가 실제로 materialize하는 UI backend를 정의한다.
기존 Layout asset의 backend 값을 안전하게 읽기 위한 private serialization payload 외에는
미지원 render-space/depth 계약을 노출하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.UI
{
    public enum PresentationBackend
    {
        UGUI = 0,
        UITK = 1,
    }

    [Serializable]
    internal struct PresentationBackendSerialization
    {
        internal PresentationBackend Backend => backend;

        [SerializeField]
        private PresentationBackend backend;

        internal PresentationBackendSerialization(PresentationBackend backend)
        {
            this.backend = backend;
        }
    }
}
