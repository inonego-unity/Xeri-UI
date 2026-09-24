/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationAlpha.cs
수정일 : 2026-10-06

# 설명
Presentation의 local Alpha 값과 Modifier 소유권을 backend 적용과 함께 관리한다.
Alpha와 Visibility가 공유하는 내부 값 소유권 구현을 포함한다.
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
    /// Base Alpha와 Modifier 결과를 backend에 반영한다.
    /// </summary>
    // ============================================================
    public sealed class PresentationAlpha : IMValue<float>
    {

    #region 상태

        public bool IsValid => target != null && target.IsValid;
        public float Base
        {
            get => state.Base;
            set => Set(value);
        }

        public float Modified => state.Modified;
        public IReadOnlyXOrdered<int, string, IModifier<float>> Modifiers => state.Modifiers;

        private readonly IPresentationAlphaTarget target;
        private readonly PresentationValue<float> state;

        // ------------------------------------------------------------
        /// <summary>
        /// Base Alpha 변경을 이전 값과 현재 값으로 알린다.
        /// </summary>
        // ------------------------------------------------------------
        public event ValueChangeEventHandler<float> OnBaseChange
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
        public event ValueChangeEventHandler<float> OnModifiedChange
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

        public PresentationAlpha()
        {
            state = new PresentationValue<float>(this, 1.0f, null, null);
        }

        public PresentationAlpha(IPresentationAlphaTarget target)
        {
            this.target = RequireTarget(target);
            state = new PresentationValue<float>(this, Mathf.Clamp01(target.Alpha), ReadOutput, ApplyLocal);
            state.Refresh(false);
        }

        public PresentationAlpha(IPresentationAlphaTarget target, float baseAlpha)
        {
            this.target = RequireTarget(target);
            state = new PresentationValue<float>(this, Mathf.Clamp01(baseAlpha), ReadOutput, ApplyLocal);
            state.Refresh(false);
        }

    #endregion

    #region 값과 수정자

        // ------------------------------------------------------------
        /// <summary>
        /// Base를 설정하고 Modifier를 재평가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Set(float value, bool invokeEvent = true)
        {
            state.Set(Mathf.Clamp01(value), invokeEvent);
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
        public void AddModifier(string key, IModifier<float> modifier, int order = 0, bool invokeEvent = true)
        {
            state.AddModifier(key, modifier, order, invokeEvent);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Modifier의 Key를 등록 키로 사용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddModifier<TModifier>(TModifier modifier, int order = 0, bool invokeEvent = true)
        where TModifier : IModifier<float>, IKeyable<string>
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
        public void AddModifier(IModifier<float> modifier, int order = 0, bool invokeEvent = true)
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
        public void InvokeOnBaseChange(float previousValue)
        {
            state.InvokeOnBaseChange(previousValue);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 상태 변경 없이 이전 값과 현재 Modified로 이벤트를 보낸다.
        /// </summary>
        // ------------------------------------------------------------
        public void InvokeOnModifiedChange(float previousValue)
        {
            state.InvokeOnModifiedChange(previousValue);
        }

    #endregion

    #region 백엔드 적용

        internal void ApplyComposite(float value)
        {
            if (target == null) return;

            state.ApplyComposite(Mathf.Clamp01(value));
        }

        private float ReadOutput() => RequireTarget(target).Alpha;

        private void ApplyLocal(float value)
        {
            RequireTarget(target).SetAlpha(value);
        }

        private static IPresentationAlphaTarget RequireTarget(IPresentationAlphaTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!target.IsValid)
            {
                throw new InvalidOperationException("Presentation Alpha Target이 유효하지 않습니다.");
            }

            return target;
        }

    #endregion

    #region 값 변환

        public static implicit operator float(PresentationAlpha value) =>
            value != null ? value.Modified : default;

        public override bool Equals(object obj)
        {
            if (obj is IReadOnlyMValue<float> other)
            {
                return EqualityComparer<float>.Default.Equals(Modified, other.Modified);
            }

            return obj is float value && EqualityComparer<float>.Default.Equals(Modified, value);
        }

        public override int GetHashCode() => Modified.GetHashCode();

        public override string ToString() => $"{Modified}({Base})";

    #endregion

    }

    internal sealed class PresentationValue<T>
    {

    #region 상태와 구독

        private sealed class Subscription
        {
            internal IModifier<T> Modifier;
            internal PresentationValue<T> Owner;

            internal void HandleChange()
            {
                var owner = Owner;
                owner?.Set(owner.Base);
            }
        }

        internal T Base { get; private set; }
        internal T Modified { get; private set; }
        internal IReadOnlyXOrdered<int, string, IModifier<T>> Modifiers => modifiers;
        private readonly XOrdered<int, string, IModifier<T>> modifiers = new();

        internal event ValueChangeEventHandler<T> OnBaseChange;
        internal event ValueChangeEventHandler<T> OnModifiedChange;

        private readonly List<Subscription> subscriptions = new();
        private readonly object sender;
        private readonly Func<T> read;
        private readonly Action<T> apply;
        private bool isUpdating;
        private bool applyPending;
        private int revision;

        internal PresentationValue(object sender, T value, Func<T> read, Action<T> apply)
        {
            this.sender = sender;
            this.read = read;
            this.apply = apply;
            Base = value;
            Modified = value;
        }

    #endregion

    #region 값 갱신

        internal void Set(T value, bool invokeEvent = true)
        {
            Update(value, invokeEvent, false);
        }

        internal void Refresh(bool invokeEvent = true)
        {
            Update(Base, invokeEvent, true);
        }

        internal void ApplyComposite(T value)
        {
            EnterUpdate();

            try
            {
                apply?.Invoke(value);
                // 성공한 합성 출력은 이전 로컬 적용의 실패 대기를 대체한다.
                applyPending = false;
            }
            finally
            {
                isUpdating = false;
            }
        }

        private void Update(T value, bool invokeEvent, bool forceApply)
        {
            EnterUpdate();
            var previousBase = Base;
            var previousModified = Modified;
            List<Exception> errors = null;

            try
            {
                // 순수 평가가 성공한 뒤에만 논리 값을 commit한다.
                var next = Evaluate(value);
                if (!Equal(previousBase, value) || !Equal(previousModified, next))
                {
                    revision++;
                }

                Base = value;
                Modified = next;
                Apply(forceApply || !Equal(previousModified, next));
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }
            finally
            {
                isUpdating = false;
            }

            NotifyChanges(previousBase, previousModified, invokeEvent, ref errors);
            ThrowErrors(errors);
        }

        private T Evaluate(T value)
        {
            foreach (var entry in modifiers)
            {
                value = entry.Value.Modify(value);
            }

            return value;
        }

        private void Apply(bool changed)
        {
            if (apply == null) return;
            if (!changed && !applyPending) return;

            // Backend가 일부 적용한 뒤 실패해도 다음 명시 요청에서 재적용한다.
            applyPending = true;
            apply(Modified);
            applyPending = false;
        }

        private void EnterUpdate()
        {
            if (isUpdating)
            {
                throw new InvalidOperationException("Presentation 값 적용 중 재진입할 수 없습니다.");
            }

            isUpdating = true;
        }

        private static bool Equal(T first, T second) =>
            EqualityComparer<T>.Default.Equals(first, second);

    #endregion

    #region 수정자 소유권

        internal void AddModifier(string key, IModifier<T> modifier, int order, bool invokeEvent)
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            EnterUpdate();
            var previous = Modified;
            var previousApplyPending = applyPending;
            var previousOutput = default(T);
            List<Exception> errors = null;
            var added = false;
            var applied = false;
            Subscription subscription = null;

            try
            {
                modifiers.Add(order, key, modifier);
                added = true;

                if (FindSubscription(modifier) == null)
                {
                    subscription = new Subscription
                    {
                        Modifier = modifier,
                    };
                    // Accessor가 실제 등록 후 throw해도 이 등록만 해제한다.
                    modifier.OnChange += subscription.HandleChange;
                    subscription.Owner = this;
                    subscriptions.Add(subscription);
                }

                Modified = Evaluate(Base);
                if (apply != null && (!Equal(previous, Modified) || applyPending))
                {
                    // Composite 결과는 local Modified와 다르므로 실제 출력부터 보존한다.
                    previousOutput = read();
                    applied = true;
                    Apply(true);
                }

                revision++;
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);

                if (added)
                {
                    modifiers.Remove(key);
                }

                if (subscription != null)
                {
                    subscriptions.Remove(subscription);
                    Detach(subscription, ref errors);
                }

                Modified = previous;

                if (applied)
                {
                    try
                    {
                        applyPending = true;
                        apply(previousOutput);
                        applyPending = previousApplyPending;
                    }
                    catch (Exception cleanupException)
                    {
                        (errors ??= new List<Exception>()).Add(cleanupException);
                    }
                }
            }
            finally
            {
                isUpdating = false;
            }

            if (errors == null || errors.Count == 0)
            {
                NotifyChanges(Base, previous, invokeEvent, ref errors);
            }

            ThrowErrors(errors);
        }

        internal bool RemoveModifier(string key, bool invokeEvent)
        {
            EnterUpdate();
            var previous = Modified;
            List<Exception> errors = null;
            var removed = false;

            try
            {
                if (modifiers.TryGetEntry(key, out var entry))
                {
                    removed = modifiers.Remove(key);
                    revision++;
                    var subscription = FindSubscription(entry.Value);
                    var stillUsed = false;

                    foreach (var remaining in modifiers)
                    {
                        stillUsed |= ReferenceEquals(remaining.Value, entry.Value);
                    }

                    if (!stillUsed && subscription != null)
                    {
                        subscriptions.Remove(subscription);
                        Detach(subscription, ref errors);
                    }
                }

                Modified = Evaluate(Base);
                Apply(!Equal(previous, Modified));
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }
            finally
            {
                isUpdating = false;
            }

            NotifyChanges(Base, previous, invokeEvent, ref errors);
            ThrowErrors(errors);
            return removed;
        }

        internal void ClearModifiers(bool invokeEvent)
        {
            EnterUpdate();
            var previous = Modified;
            List<Exception> errors = null;

            try
            {
                // 해제 accessor가 실패해도 모든 논리 소유권은 먼저 종료한다.
                if (modifiers.Count > 0)
                {
                    revision++;
                }

                modifiers.Clear();
                Modified = Base;

                foreach (var subscription in subscriptions)
                {
                    subscription.Owner = null;
                }

                foreach (var subscription in subscriptions)
                {
                    Detach(subscription, ref errors);
                }

                subscriptions.Clear();
                Apply(!Equal(previous, Modified));
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }
            finally
            {
                isUpdating = false;
            }

            NotifyChanges(Base, previous, invokeEvent, ref errors);
            ThrowErrors(errors);
        }

        private Subscription FindSubscription(IModifier<T> modifier)
        {
            foreach (var subscription in subscriptions)
            {
                if (ReferenceEquals(subscription.Modifier, modifier)) return subscription;
            }

            return null;
        }

        private static void Detach(Subscription subscription, ref List<Exception> errors)
        {
            subscription.Owner = null;

            try
            {
                subscription.Modifier.OnChange -= subscription.HandleChange;
            }
            catch (Exception exception)
            {
                (errors ??= new List<Exception>()).Add(exception);
            }
        }

    #endregion

    #region 완료 알림

        internal void InvokeOnBaseChange(T previous)
        {
            List<Exception> errors = null;
            Notify(OnBaseChange, previous, Base, revision, ref errors);
            ThrowErrors(errors);
        }

        internal void InvokeOnModifiedChange(T previous)
        {
            List<Exception> errors = null;
            Notify(OnModifiedChange, previous, Modified, revision, ref errors);
            ThrowErrors(errors);
        }

        private void NotifyChanges(T previousBase, T previousModified, bool invokeEvent, ref List<Exception> errors)
        {
            if (!invokeEvent) return;

            var currentRevision = revision;
            var currentModified = Modified;

            if (!Equal(previousBase, Base))
            {
                Notify(OnBaseChange, previousBase, Base, currentRevision, ref errors);
            }

            if (!Equal(previousModified, currentModified))
            {
                Notify(OnModifiedChange, previousModified, currentModified, currentRevision, ref errors);
            }
        }

        private void Notify
        (
            ValueChangeEventHandler<T> handlers,
            T previous,
            T current,
            int expectedRevision,
            ref List<Exception> errors
        )
        {
            if (handlers == null) return;

            var args = new ValueChangeEventArgs<T>(previous, current);

            foreach (ValueChangeEventHandler<T> handler in handlers.GetInvocationList())
            {
                if (revision != expectedRevision) break;

                try
                {
                    handler(sender, args);
                }
                catch (Exception exception)
                {
                    (errors ??= new List<Exception>()).Add(exception);
                }
            }
        }

        private static void ThrowErrors(List<Exception> errors)
        {
            if (errors == null || errors.Count == 0) return;

            if (errors.Count == 1)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
            }

            throw new AggregateException("Presentation 값 갱신과 후속 처리가 실패했습니다.", errors);
        }

    #endregion

    }
}
