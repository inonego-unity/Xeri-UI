/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ExceptionBuffer.cs
수정일 : 2026-11-03

# 설명
오류가 발생할 때만 Exception 보관 목록을 생성하고, 외부에는 읽기 전용 결과를 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.UI
{
    // ============================================================
    /// <summary>
    /// 정상 경로에 할당을 만들지 않는 일회성 Exception 수집 버퍼.
    /// </summary>
    // ============================================================
    internal struct ExceptionBuffer
    {
        private List<Exception> items;

        public int Count => items?.Count ?? 0;

        public Exception this[int index] => items[index];

        // ------------------------------------------------------------
        /// <summary>
        /// 최초 오류를 보유한 상태로 버퍼를 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        public ExceptionBuffer(Exception first)
        {
            items = new List<Exception>
            {
                first,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 첫 오류가 들어올 때만 저장소를 만든다.
        /// </summary>
        // ------------------------------------------------------------
        public void Add(Exception exception)
        {
            (items ??= new List<Exception>()).Add(exception);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 최초 실패를 복구 실패보다 앞에 배치한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Prepend(Exception exception)
        {
            (items ??= new List<Exception>()).Insert(0, exception);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 빈 결과를 받으면 저장소를 만들지 않고 오류만 합친다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddRange(IReadOnlyList<Exception> exceptions)
        {
            if (exceptions == null)
            {
                throw new ArgumentNullException(nameof(exceptions));
            }

            var count = exceptions.Count;

            for (var index = 0; index < count; index++)
            {
                Add(exceptions[index]);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 다른 버퍼를 합칠 때 값 형식의 인터페이스 박싱을 피한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddRange(ExceptionBuffer other)
        {
            // 버퍼 복사본이 동일한 저장소를 공유해도 최초 항목까지만 병합한다.
            var count = other.Count;

            for (var index = 0; index < count; index++)
            {
                Add(other[index]);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 오류가 없으면 공유 빈 배열, 있으면 읽기 전용 목록을 제공한다.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyList<Exception> AsReadOnlyList()
        {
            if (items == null) return Array.Empty<Exception>();

            return items.AsReadOnly();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 내부 오류 목록에서 값 형식 박싱 없이 집계 예외를 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        public AggregateException CreateAggregateException(string message)
        {
            IEnumerable<Exception> exceptions = items;
            return new AggregateException(message, exceptions ?? Array.Empty<Exception>());
        }
    }
}
