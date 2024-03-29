﻿using System.Collections;

namespace SerialMonitor
{
    public class RingBuffer<T> : IEnumerable<T>,IList
    {
        private readonly T[] data;

        private int start = 0;
        private int end = 0;
        private int count = 0;

        public RingBuffer(int capacity)
        {
            data = new T[capacity];
        }

        public int Capacity
        {
            get
            {
                return data.Length;
            }
        }

        public int Count
        {
            get
            {
                return count;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return Count == 0;
            }
        }
        
        public T Last
        {
            get
            {
                if (IsEmpty)
                    return default;

                return data[(end != 0 ? end : Capacity) - 1];
            }
            set
            {
                if (IsEmpty)
                    return;

                data[(end != 0 ? end : Capacity) - 1] = value;
            }
        }

        public bool IsReadOnly => true;

        public bool IsFixedSize => false;

        public bool IsSynchronized => false;

        public object SyncRoot => null;

        object? IList.this[int index] { get => this[index]; set => this[index]= (T?)value; }

        public T this[int index]
        {
            get
            {
                if (IsEmpty || index >= count)
                    throw new ArgumentOutOfRangeException();
                return data[InternalIndex(index)];
            }
            set
            {
                if (IsEmpty || index >= count)
                    throw new ArgumentOutOfRangeException();
                data[InternalIndex(index)] = value;
            }
        }

        public void Add(T item)
        {
            data[end] = item;
            if (++end == Capacity)
                end = 0;
 
            if (count==Capacity)            
                start = end;            
            else
                ++count;            
        }

        public void Clear()
        {
            start = 0;
            end = 0;
            count = 0;            
        }

        public T[] ToArray()
        {
            T[] newArray = new T[Count];
            var segments = ToArraySegments();
            segments[0].CopyTo(newArray);
            segments[1].CopyTo(newArray, segments[0].Count);            
            return newArray;
        }

        /// <summary>
        /// Get the contents of the buffer as 2 ArraySegments.
        /// Respects the logical contents of the buffer, where
        /// each segment and items in each segment are ordered
        /// according to insertion.
        ///
        /// Fast: does not copy the array elements.
        /// Useful for methods like <c>Send(IList&lt;ArraySegment&lt;Byte&gt;&gt;)</c>.
        /// 
        /// <remarks>Segments may be empty.</remarks>
        /// </summary>
        /// <returns>An IList with 2 segments corresponding to the buffer content.</returns>
        public IList<ArraySegment<T>> ToArraySegments()
        {
            return new[] { ArrayOne(), ArrayTwo() };
        }

        #region IEnumerable<T> implementation
        /// <summary>
        /// Returns an enumerator that iterates through this buffer.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate this collection.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            var segments = ToArraySegments();
            foreach (ArraySegment<T> segment in segments)
            {
                for (int i = 0; i < segment.Count; i++)
                {
                    yield return segment.Array[segment.Offset + i];
                }
            }
        }
        #endregion
        #region IEnumerable implementation
        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)GetEnumerator();
        }
        #endregion

        private int InternalIndex(int index)
        {
            return start + (index < (Capacity - start) ? index : index - Capacity);
        }

        private int OuterIndex(int internalIndex)
        {
            return internalIndex - (internalIndex < start ? (Capacity-start) : start);
        }

        // doing ArrayOne and ArrayTwo methods returning ArraySegment<T> as seen here: 
        // http://www.boost.org/doc/libs/1_37_0/libs/circular_buffer/doc/circular_buffer.html#classboost_1_1circular__buffer_1957cccdcb0c4ef7d80a34a990065818d
        // http://www.boost.org/doc/libs/1_37_0/libs/circular_buffer/doc/circular_buffer.html#classboost_1_1circular__buffer_1f5081a54afbc2dfc1a7fb20329df7d5b
        // should help a lot with the code.

        #region Array items easy access.
        // The array is composed by at most two non-contiguous segments, 
        // the next two methods allow easy access to those.

        private ArraySegment<T> ArrayOne()
        {
            if (IsEmpty)
            {
                return new ArraySegment<T>(Array.Empty<T>());
            }
            else if (start < end)
            {
                return new ArraySegment<T>(data, start, end - start);
            }
            else
            {
                return new ArraySegment<T>(data, start, data.Length - start);
            }
        }

        private ArraySegment<T> ArrayTwo()
        {
            if (IsEmpty)
            {
                return new ArraySegment<T>(Array.Empty<T>());
            }
            else if (start < end)
            {
                return new ArraySegment<T>(data, end, 0);
            }
            else
            {
                return new ArraySegment<T>(data, 0, end);
            }
        }

        public int IndexOf(T item)
        {
            for(int i=0;i<Capacity;i++)
            {
                if (data[i].Equals(item))
                {
                    return OuterIndex(i);
                }
            }

            return -1;
        }

        public bool Contains(object? value)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (data[i].Equals((T)value))
                {
                    return true;
                }
            }

            return false;
        }

        public int IndexOf(object? value)
        {
            if(value==null)
                return -1;
            return IndexOf((T)value);
        }

        public void Insert(int index, object? value)
        {
            throw new NotImplementedException();
        }

        public void Remove(object? value)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public int Add(object? value)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
