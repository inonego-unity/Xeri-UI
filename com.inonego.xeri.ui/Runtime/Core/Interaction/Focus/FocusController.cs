/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FocusController.cs
수정일 : 2026-09-17
# 설명
Screen별 마지막 Focus와 화면·Driver 기본값·대체 선택을 관리한다.
Context Focus 권한이 있을 때만 실제 Driver 선택을 적용한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Screen Focus 기록과 복원을 소유하는 Controller.
    /// </summary>
    // ============================================================
    public sealed class FocusController
    {

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// Screen별 마지막 선택과 화면·Driver 기본 Focus를 보관한다.
        /// </summary>
        // ============================================================
        private sealed class Record
        {
            public object Last = null;
            public object Default = null;
            public object DriverDefault = null;
        }

    #endregion

    #region 필드

        private readonly IFocusDriver driver = null;
        private readonly Dictionary<ScreenSession, Record> records =
            new Dictionary<ScreenSession, Record>();
        private bool isFocused = true;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Focus backend를 사용하는 Controller를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public FocusController(IFocusDriver driver) : base()
        {
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Screen Focus 기록을 만들고 해당 Screen의 유효한 대상을 선택한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Activate
        (
            ScreenSession session,
            object defaultFocus,
            object driverDefaultFocus
        )
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (!records.TryGetValue(session, out var record))
            {
                record = new Record();
                records.Add(session, record);
            }

            record.Default = defaultFocus;
            record.DriverDefault = driverDefaultFocus;

            if (!isFocused) return;

            var target = Resolve(record);
            driver.Select(target);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 Screen 소유 범위의 유효한 사용자 Focus를 마지막 선택으로 기록한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void RecordCurrentFocus(ScreenSession session, object target)
        {
            if
            (
                !isFocused ||
                session == null ||
                !driver.IsValid(target) ||
                !session.ContainsFocus(target)
            )
            {
                return;
            }

            if (!records.TryGetValue(session, out var record))
            {
                record = new Record();
                records.Add(session, record);
            }

            record.Last = target;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Screen 전환 직전 실제 backend의 현재 Focus를 마지막 선택으로 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal void CaptureCurrent(ScreenSession session)
        {
            RecordCurrentFocus(session, driver.Current);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 제거된 Screen의 Focus 기록을 폐기한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Remove(ScreenSession session)
        {
            if (session == null) return;

            records.Remove(session);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 새로 노출된 Screen의 마지막·화면 기본·Driver 기본·대체 Focus 순서로 복원한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void Restore(ScreenSession session)
        {
            if (!isFocused) return;

            if (session == null)
            {
                driver.Select(driver.FindFallback());
                return;
            }

            if (!records.TryGetValue(session, out var record))
            {
                record = new Record();
                records.Add(session, record);
            }

            driver.Select(Resolve(record));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 Screen Focus 기록을 제거하고 대체 Focus를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Clear()
        {
            records.Clear();

            if (!isFocused) return;

            driver.Select(driver.FindFallback());
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Controller의 기록을 실제 Focus Driver에 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Focus(ScreenSession session)
        {
            if (isFocused) return;

            // 권한을 먼저 확정해 Select callback의 재진입도 현재 Context로 관찰되게 한다.
            isFocused = true;
            Restore(session);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Screen 선택을 기록하고 실제 Focus Driver 적용을 중지한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void Unfocus(ScreenSession session)
        {
            if (!isFocused) return;

            // 다른 Context가 Driver를 사용하기 전에 현재 Screen 소유 Focus를 마지막 기록으로 보존한다.
            CaptureCurrent(session);
            isFocused = false;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 마지막·화면 기본·Driver 기본·대체 순서로 유효한 Focus 대상을 결정한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private object Resolve(Record record)
        {
            if (driver.IsValid(record.Last))
            {
                return record.Last;
            }

            if (driver.IsValid(record.Default))
            {
                return record.Default;
            }

            if (driver.IsValid(record.DriverDefault))
            {
                return record.DriverDefault;
            }

            return driver.FindFallback();
        }

    #endregion

    }
}
