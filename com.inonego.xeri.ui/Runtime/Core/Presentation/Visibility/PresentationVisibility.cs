/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationVisibility.cs
수정일 : 2026-10-06

# 설명
Presentation의 local Visibility 값과 Modifier 소유권을 backend 적용과 함께 관리한다.
순수 평가, 등록 rollback, terminal 해제와 명시적 Refresh 재시도를 구분한다.
Composite 누적 값은 local State에 저장하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// Base Visibility와 Modifier 결과를 backend에 반영한다.
    /// </summary>
    // ============================================================
    public sealed class PresentationVisibility : IMValue<bool>
    {

    #region 상태

        public bool IsValid => target != null && target.IsValid;
        public bool Base
        {
            get => state.Base;
            set => Set(value);
        }

        public bool Modified => state.Modified;
        public IReadOnlyXOrdered<int, string, IModifier<bool>> Modifiers => state.Modifiers;

        private readonly IPresentationVisibilityTarget target;
        private readonly PresentationValue<bool> state;

        // ------------------------------------------------------------
        /// <summary>
        /// Base Visibility 변경을 이전 값과 현재 값으로 알린다.
        /// </summary>
        // ------------------------------------------------------------
        public event ValueChangeEventHandler<bool> OnBaseChange
        {
            add
            {
                state.OnBaseChange += value;
            }
            remove
            {
                state.OnBaseChange -= value;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Modifier 평가 결과의 변경을 이전 값과 현재 값으로 알린다.
        /// </summary>
        // ------------------------------------------------------------
        public event ValueChangeEventHandler<bool> OnModifiedChange
        {
            add
            {
                state.OnModifiedChange += value;
            }
            remove
            {
                state.OnModifiedChange -= value;
            }
        }

    #endregion

    #region 생성자

        public PresentationVisibility()
        {
            state = new PresentationValue<bool>(this, true, null, null);
        }

        public PresentationVisibility(IPresentationVisibilityTarget target)
        {
            this.target = RequireTarget(target);
            state = new PresentationValue<bool>(this, target.IsVisible, ReadOutput, ApplyLocal);
            state.Refresh(false);
        }

    #endregion

    #region 값과 수정자

        // ------------------------------------------------------------
        /// <summary>
        /// Base를 설정하고 Modifier를 재평가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Set(bool value, bool invokeEvent = true)
        {
            state.Set(value, invokeEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Modifier를 재평가하고 backend 적용을 재시도한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Refresh(bool invokeEvent = true)
        {
            state.Refresh(invokeEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/>Modifier를 등록해 값을 재평가한다.
        /// <br/>실패 시 신규 등록을 되돌리고 이전 출력 복원을 시도한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddModifier(string key, IModifier<bool> modifier, int order = 0, bool invokeEvent = true)
        {
            state.AddModifier(key, modifier, order, invokeEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Modifier의 Key를 등록 키로 사용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddModifier<TModifier>(TModifier modifier, int order = 0, bool invokeEvent = true)
        where TModifier : IModifier<bool>, IKeyable<string>
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            AddModifier(modifier.Key, modifier, order, invokeEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/>Modifier의 Key를 등록 키로 사용한다.
        /// <br/>키를 제공하지 않으면 등록 전에 예외를 던진다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddModifier(IModifier<bool> modifier, int order = 0, bool invokeEvent = true)
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            if (modifier is not IKeyable<string> keyable)
            {
                throw new ArgumentException("Modifier가 stable string key를 제공해야 합니다.", nameof(modifier));
            }

            AddModifier(keyable.Key, modifier, order, invokeEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/>Modifier를 제거하고 값을 재평가한다.
        /// <br/>마지막 등록이면 구독을 해제하며 적용 실패도 보고한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool RemoveModifier(string key, bool invokeEvent = true) =>
            state.RemoveModifier(key, invokeEvent);

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/>모든 Modifier와 구독을 해제하고 Modified를 Base로 되돌린다.
        /// <br/>해제나 적용 오류가 있어도 소유권을 끝내고 오류를 보고한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void ClearModifiers(bool invokeEvent = true)
        {
            state.ClearModifiers(invokeEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 변경 없이 이전 값과 현재 Base로 이벤트를 보낸다.
        /// </summary>
        // ------------------------------------------------------------
        public void InvokeOnBaseChange(bool previousValue)
        {
            state.InvokeOnBaseChange(previousValue);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 변경 없이 이전 값과 현재 Modified로 이벤트를 보낸다.
        /// </summary>
        // ------------------------------------------------------------
        public void InvokeOnModifiedChange(bool previousValue)
        {
            state.InvokeOnModifiedChange(previousValue);
        }

    #endregion

    #region 백엔드 적용

        internal void ApplyComposite(bool value)
        {
            if (target == null) return;

            state.ApplyComposite(value);
        }

        private bool ReadOutput() => RequireTarget(target).IsVisible;

        private void ApplyLocal(bool value)
        {
            RequireTarget(target).SetVisible(value);
        }

        private static IPresentationVisibilityTarget RequireTarget(IPresentationVisibilityTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!target.IsValid)
            {
                throw new InvalidOperationException("Presentation Visibility Target이 유효하지 않습니다.");
            }

            return target;
        }

    #endregion

    #region 값 변환

        public static implicit operator bool(PresentationVisibility value) =>
            value != null ? value.Modified : default;

        public override bool Equals(object obj)
        {
            if (obj is IReadOnlyMValue<bool> other)
            {
                return EqualityComparer<bool>.Default.Equals(Modified, other.Modified);
            }

            return obj is bool value && EqualityComparer<bool>.Default.Equals(Modified, value);
        }

        public override int GetHashCode() => Modified.GetHashCode();

        public override string ToString() => $"{Modified}({Base})";

    #endregion

    }
}
