/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : PresentationTarget.cs
수정일 : 2026-10-07

# 설명
UI를 현재 Session의 local Layer 또는 Runtime PresentationHost destination에 표시할 대상을 정의한다.
Screen identity, local topology와 app-wide destination identity를 분리한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.UI
{
    public enum PresentationTargetScope
    {
        Local = 0,
        Host = 1,
    }

    // ======================================================================
    /// <summary>
    /// local Layer 또는 app-wide Host destination을 가리키는 표시 대상.
    /// </summary>
    // ======================================================================
    public readonly struct PresentationTarget : IEquatable<PresentationTarget>
    {

    #region 필드

        public PresentationTargetScope Scope { get; }
        public string ID { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ID);

    #endregion

    #region 생성

        private PresentationTarget(PresentationTargetScope scope, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Presentation Target ID가 비어 있습니다.", nameof(id));
            }

            Scope = scope;
            ID = id;
        }

        public static PresentationTarget Local(string layerID)
        {
            return new PresentationTarget(PresentationTargetScope.Local, layerID);
        }

        public static PresentationTarget Host(string destinationID)
        {
            return new PresentationTarget(PresentationTargetScope.Host, destinationID);
        }

    #endregion

    #region 비교

        public bool Equals(PresentationTarget other)
        {
            return Scope == other.Scope &&
                string.Equals(ID, other.ID, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Scope, ID);
        }

        public static bool operator ==(PresentationTarget left, PresentationTarget right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PresentationTarget left, PresentationTarget right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return IsValid ? $"{Scope}:{ID}" : "<invalid>";
        }

    #endregion

    }

    public static class PresentationDestinationID
    {
        public const string Application = "Application";
        public const string Overlay = "Overlay";
        public const string Modal = "Modal";
        public const string System = "System";
    }
}
