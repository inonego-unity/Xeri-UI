/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationGroup.cs
수정일 : 2026-09-19

# 설명
여러 IPresentation을 재사용 가능한 Composite Tree로 묶는다.
Apply는 현재 Tree를 한 번 평가하며 parent 누적 Alpha와 Visibility를 root에서 leaf까지 전달한다.

# 특이사항, 제약사항
Group은 reactive binding이나 parent subscription을 만들지 않는다.
Member의 Base, Modified, Modifier는 변경하지 않는다.
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

    #region Member

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

    #region Tree 적용

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Group을 Root로 Alpha 곱셈과 Visibility AND 합성을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Apply()
        {
            ValidateTree();

            var visited = new HashSet<IPresentation>
            (
                ReferenceEqualityComparer<IPresentation>.Instance
            );
            List<Exception> errors = null;

            ApplyTree
            (
                this,
                parentAlpha: 1.0f,
                parentVisibility: true,
                visited,
                ref errors
            );

            if (errors != null)
            {
                throw new AggregateException
                (
                    "Presentation Tree 적용이 실패했습니다.",
                    errors
                );
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 Presentation 경로의 누적값을 계산하고 leaf backend까지 순회 적용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ApplyTree
        (
            IPresentation presentation,
            float parentAlpha,
            bool parentVisibility,
            ISet<IPresentation> visited,
            ref List<Exception> errors
        )
        {
            if (!visited.Add(presentation))
            {
                throw new InvalidOperationException
                (
                    "하나의 Presentation Tree 안에 같은 Presentation이 두 번 포함되어 있습니다."
                );
            }

            var alpha = presentation.Alpha;
            var visibility = presentation.Visibility;

            if (presentation is PresentationGroup group)
            {
                var nextAlpha = parentAlpha * (alpha?.Modified ?? 1.0f);
                var nextVisibility = parentVisibility && (visibility?.Modified ?? true);

                for (var index = 0; index < group.members.Count; index++)
                {
                    ApplyTree
                    (
                        group.members[index],
                        nextAlpha,
                        nextVisibility,
                        visited,
                        ref errors
                    );
                }

                return;
            }

            if (alpha != null)
            {
                try
                {
                    alpha.ApplyInherited(parentAlpha);
                }
                catch (Exception exception)
                {
                    errors ??= new List<Exception>();
                    errors.Add(exception);
                }
            }

            if (visibility != null)
            {
                try
                {
                    visibility.ApplyInherited(parentVisibility);
                }
                catch (Exception exception)
                {
                    errors ??= new List<Exception>();
                    errors.Add(exception);
                }
            }
        }

    #endregion

    #region IValueSetter

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

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Group을 Root로 현재 Composite topology 전체를 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ValidateTree()
        {
            var visited = new HashSet<IPresentation>
            (
                ReferenceEqualityComparer<IPresentation>.Instance
            );

            ValidateTree(this, visited);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 한 Apply Tree 안의 중복 reference와 leaf backend 유효성을 재귀 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ValidateTree
        (
            IPresentation presentation,
            ISet<IPresentation> visited
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
                    throw new InvalidOperationException
                    (
                        "적용할 Presentation Group에 Member가 없습니다."
                    );
                }

                for (var index = 0; index < group.members.Count; index++)
                {
                    ValidateTree(group.members[index], visited);
                }

                return;
            }

            var alpha = presentation.Alpha;
            var visibility = presentation.Visibility;
            var hasTarget = false;

            if (alpha != null)
            {
                hasTarget = true;

                if (!alpha.IsValid)
                {
                    throw new InvalidOperationException
                    (
                        "Presentation Alpha Target이 유효하지 않습니다."
                    );
                }
            }

            if (visibility != null)
            {
                hasTarget = true;

                if (!visibility.IsValid)
                {
                    throw new InvalidOperationException
                    (
                        "Presentation Visibility Target이 유효하지 않습니다."
                    );
                }
            }

            if (!hasTarget)
            {
                throw new InvalidOperationException
                (
                    "Presentation에 적용 가능한 State Target이 없습니다."
                );
            }
        }

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
