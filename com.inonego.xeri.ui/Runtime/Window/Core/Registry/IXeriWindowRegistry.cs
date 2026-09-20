/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IXeriWindowRegistry.cs
수정일 : 2026-09-20

# 설명
Xeri 커스텀 윈도우 등록, 조회, 포커스, 상태 명령을 제공하는 registry 계약.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI.Window
{
    // ============================================================
    /// <summary>
    /// Xeri 커스텀 윈도우 registry 계약.
    /// </summary>
    // ============================================================
    public interface IXeriWindowRegistry
    {

    #region 프로퍼티

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 활성 윈도우 handle.
        /// </summary>
        // ------------------------------------------------------------
        XeriWindowHandle ActiveHandle { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 윈도우 record 목록.
        /// </summary>
        // ------------------------------------------------------------
        IReadOnlyList<XeriWindowRecord> Records { get; }

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// Window 목록 구성이 변경될 때 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        event EventHandler OnCollectionChange;

        // ------------------------------------------------------------
        /// <summary>
        /// Window가 등록될 때 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        event EventHandler<XeriWindowEventArgs> OnRegister;

        // ------------------------------------------------------------
        /// <summary>
        /// Window 등록이 해제될 때 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        event EventHandler<XeriWindowEventArgs> OnUnregister;

        // ------------------------------------------------------------
        /// <summary>
        /// Active window가 변경될 때 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        event EventHandler<XeriWindowEventArgs> OnActiveChange;

        // ------------------------------------------------------------
        /// <summary>
        /// Window layer 표시 순서가 변경될 때 호출된다.
        /// </summary>
        // ------------------------------------------------------------
        event EventHandler OnOrderChange;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 controller를 등록하고 handle을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        XeriWindowHandle Register(string id, XeriWindowController controller);

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 controller와 record를 등록하고 handle을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        XeriWindowHandle Register(string id, XeriWindowController controller, XeriWindowRecord record);

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우 등록을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        bool Unregister(XeriWindowHandle handle);

        // ------------------------------------------------------------
        /// <summary>
        /// Handle이 현재 registry에서 유효한지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        bool Contains(XeriWindowHandle handle);

        // ------------------------------------------------------------
        /// <summary>
        /// Handle에 대응하는 record를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        bool TryGetRecord(XeriWindowHandle handle, out XeriWindowRecord record);

        // ------------------------------------------------------------
        /// <summary>
        /// Handle에 대응하는 controller를 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        bool TryGetController(XeriWindowHandle handle, out XeriWindowController controller);

        // ------------------------------------------------------------
        /// <summary>
        /// ID에 대응하는 handle을 조회한다.
        /// </summary>
        // ------------------------------------------------------------
        bool TryGetHandle(string id, out XeriWindowHandle handle);

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 활성 순서의 앞으로 가져온다.
        /// </summary>
        // ------------------------------------------------------------
        void Focus(XeriWindowHandle handle);

        // ------------------------------------------------------------
        /// <summary>
        /// Window를 같은 layer의 가장 앞으로 이동시킨다.
        /// </summary>
        // ------------------------------------------------------------
        void BringToFront(XeriWindowHandle handle);

        // ------------------------------------------------------------
        /// <summary>
        /// Window를 같은 layer의 가장 뒤로 이동시킨다.
        /// </summary>
        // ------------------------------------------------------------
        void SendToBack(XeriWindowHandle handle);

        // ------------------------------------------------------------
        /// <summary>
        /// Window의 화면 정렬 layer를 변경한다.
        /// </summary>
        // ------------------------------------------------------------
        void SetStackLayer(XeriWindowHandle handle, XeriWindowStackLayer stackLayer);

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 normal 상태로 되돌린다.
        /// </summary>
        // ------------------------------------------------------------
        void ShowNormal(XeriWindowHandle handle);

        // ------------------------------------------------------------
        /// <summary>
        /// 최소화 이전 표시 상태로 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        void Restore(XeriWindowHandle handle);

        // ------------------------------------------------------------
        /// <summary>
        /// 윈도우를 닫는다.
        /// </summary>
        // ------------------------------------------------------------
        void Close(XeriWindowHandle handle);

    #endregion

    }
}
