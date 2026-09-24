/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationGroup.cs
수정일 : 2026-10-06

# 설명
여러 IPresentation을 재사용 가능한 Composite Tree로 묶는다.
Apply는 현재 Tree의 대상과 합성 값을 먼저 확정하고 각 backend에 독립적으로 적용한다.

# 특이사항, 제약사항
Group은 reactive binding이나 parent subscription을 만들지 않는다.
Member의 Base, Modified, Modifier는 변경하지 않는다.
적용 중 topology와 값 변경은 다음 Apply에서 반영한다.
같은 Presentation은 여러 Group에 포함될 수 있지만 한 Apply Tree 안에서는 한 번만 등장해야 한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Primitive;

namespace inonego.Xeri.UI
{
    // ================================================================================
    /// <summary>
    /// Presentation Tree topology와 one-shot top-down 합성 적용을 관리하는 Group.
    /// </summary>
    // ================================================================================
    public sealed class PresentationGroup :
        IPresentation,
        IValueSetter<float>
    {

    #region 내부 데이터

        private sealed class ApplyTarget
        {
            public IPresentation Presentation;
            public PresentationAlpha AlphaState;
            public PresentationVisibility VisibilityState;
            public float AlphaOutput;
            public bool VisibilityOutput;
        }

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Tree의 직접 Member 목록.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<IPresentation> Members => readOnlyMembers;

        private readonly ReadOnlyCollection<IPresentation> readOnlyMembers;

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 직접 Member 수.
        /// </summary>
        // ------------------------------------------------------------
        public int Count => members.Count;

        // ------------------------------------------------------------
        /// <summary>
        /// Group 경로에 적용되는 local Alpha State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationAlpha Alpha { get; } = new();

        // ------------------------------------------------------------
        /// <summary>
        /// Group 경로에 적용되는 local Visibility State.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationVisibility Visibility { get; } = new();

        private readonly List<IPresentation> members = new();

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// Member가 없는 identity Presentation Group을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationGroup() : base()
        {
            readOnlyMembers = members.AsReadOnly();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 Presentation들을 직접 Member로 가지는 Group을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public PresentationGroup(IEnumerable<IPresentation> presentations) : this()
        {
            if (presentations == null)
            {
                throw new ArgumentNullException(nameof(presentations));
            }

            foreach (var presentation in presentations)
            {
                Add(presentation);
            }
        }

    #endregion

    #region 멤버

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation을 직접 Tree Member로 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Add(IPresentation presentation)
        {
            if (presentation == null)
            {
                throw new ArgumentNullException(nameof(presentation));
            }

            if (ReferenceEquals(this, presentation))
            {
                throw new InvalidOperationException
                (
                    "Presentation Group은 자기 자신을 Member로 가질 수 없습니다."
                );
            }

            if (IndexOfReference(presentation) >= 0) return false;

            if
            (
                presentation is PresentationGroup group &&
                group.Contains(this)
            )
            {
                throw new InvalidOperationException
                (
                    "Presentation Group에 순환 Tree를 구성할 수 없습니다."
                );
            }

            members.Add(presentation);
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Presentation을 직접 Tree Member에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Remove(IPresentation presentation)
        {
            if (presentation == null) return false;

            var index = IndexOfReference(presentation);
            if (index < 0) return false;

            members.RemoveAt(index);
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 직접 Member를 Tree에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Clear()
        {
            members.Clear();
        }

    #endregion

    #region 트리 적용

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Group을 Root로 Alpha 곱셈과 Visibility AND 합성을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Apply()
        {
            // 외부 backend 호출 전에 topology와 출력 값을 하나의 적용 계획으로 확정한다.
            var targets = BuildApplyPlan();
            List<Exception> errors = null;

            foreach (var target in targets)
            {
                if (target.AlphaState != null)
                {
                    try
                    {
                        target.AlphaState.ApplyComposite(target.AlphaOutput);
                    }
                    catch (Exception exception)
                    {
                        (errors ??= new List<Exception>()).Add(exception);
                    }
                }

                if (target.VisibilityState != null)
                {
                    try
                    {
                        target.VisibilityState.ApplyComposite(target.VisibilityOutput);
                    }
                    catch (Exception exception)
                    {
                        (errors ??= new List<Exception>()).Add(exception);
                    }
                }
            }

            if (errors != null)
            {
                throw new AggregateException("Presentation Tree 적용이 실패했습니다.", errors);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 대상과 합성 출력을 확정하고 모든 Target을 적용 전에 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private List<ApplyTarget> BuildApplyPlan()
        {
            var targets = new List<ApplyTarget>();
            var visited = new HashSet<IPresentation>
            (
                ReferenceEqualityComparer<IPresentation>.Instance
            );
            CollectApplyTargets(this, 1.0f, true, visited, targets);

            // 사용자 구현의 State·Target 조회도 확정된 대상 목록 안에서 처리한다.
            foreach (var target in targets)
            {
                var alpha = target.Presentation.Alpha;
                var visibility = target.Presentation.Visibility;
                target.AlphaState = alpha;
                target.VisibilityState = visibility;

                if (alpha == null && visibility == null)
                {
                    throw new InvalidOperationException
                    (
                        "Presentation에 적용 가능한 State Target이 없습니다."
                    );
                }

                if (alpha != null)
                {
                    if (!alpha.IsValid)
                    {
                        throw new InvalidOperationException("Presentation Alpha Target이 유효하지 않습니다.");
                    }

                    target.AlphaOutput *= alpha.Modified;
                }

                if (visibility != null)
                {
                    if (!visibility.IsValid)
                    {
                        throw new InvalidOperationException("Presentation Visibility Target이 유효하지 않습니다.");
                    }

                    target.VisibilityOutput &= visibility.Modified;
                }
            }

            return targets;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Tree를 검증하며 각 leaf의 부모 합성 값을 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CollectApplyTargets
        (
            IPresentation presentation,
            float parentAlpha,
            bool parentVisibility,
            ISet<IPresentation> visited,
            List<ApplyTarget> targets
        )
        {
            if (!visited.Add(presentation))
            {
                throw new InvalidOperationException
                (
                    "Presentation Composite는 하나의 Apply 기준에서 Tree여야 합니다."
                );
            }

            if (presentation is PresentationGroup group)
            {
                if (group.members.Count == 0)
                {
                    throw new InvalidOperationException("적용할 Presentation Group에 Member가 없습니다.");
                }

                var nextAlpha = parentAlpha * group.Alpha.Modified;
                var nextVisibility = parentVisibility && group.Visibility.Modified;

                for (var index = 0; index < group.members.Count; index++)
                {
                    CollectApplyTargets
                    (
                        group.members[index], nextAlpha, nextVisibility, visited, targets
                    );
                }

                return;
            }

            targets.Add
            (
                new ApplyTarget
                {
                    Presentation = presentation,
                    AlphaOutput = parentAlpha,
                    VisibilityOutput = parentVisibility,
                }
            );
        }

    #endregion

    #region 값 설정 구현

        // ----------------------------------------------------------------------
        /// <summary>
        /// Transition 값을 Group local Alpha에 설정하고 현재 Tree를 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        void IValueSetter<float>.Set(float value)
        {
            Alpha.Set(value);
            Apply();
        }

    #endregion

    #region 검증

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이 Group 하위 topology에 지정한 Presentation reference가 있는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private bool Contains(IPresentation target)
        {
            var visited = new HashSet<IPresentation>
            (
                ReferenceEqualityComparer<IPresentation>.Instance
            );

            return Contains(this, target, visited);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 지정한 Presentation부터 reference 기반 하위 포함 여부를 재귀 탐색한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool Contains
        (
            IPresentation presentation,
            IPresentation target,
            ISet<IPresentation> visited
        )
        {
            if (ReferenceEquals(presentation, target)) return true;
            if (!visited.Add(presentation)) return false;
            if (presentation is not PresentationGroup group) return false;

            for (var index = 0; index < group.members.Count; index++)
            {
                if
                (
                    Contains
                    (
                        group.members[index],
                        target,
                        visited
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 직접 Member 목록에서 동일 reference의 인덱스를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private int IndexOfReference(IPresentation presentation)
        {
            for (var index = 0; index < members.Count; index++)
            {
                if (ReferenceEquals(members[index], presentation)) return index;
            }

            return -1;
        }

    #endregion

    }
}
