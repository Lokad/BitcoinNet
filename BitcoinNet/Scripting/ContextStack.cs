using System;
using System.Collections;
using System.Collections.Generic;

namespace BitcoinNet.Scripting
{
	/// <summary>
	/// ContextStack is used internally by the bitcoin script evaluator. This class contains
	/// operations not typically available in a "pure" Stack class, as example:
	/// Insert, Swap, Erase and Top (Peek w/index)
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ContextStack<T> : IEnumerable<T>
	{
		private T[] _array;
		private int _position;

		/// <summary>
		/// Initializes a new instance of the <see cref="ContextStack{T}"/> class.
		/// </summary>
		public ContextStack()
		{
			_position = -1;
			_array = new T[16];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ContextStack{T}"/> 
		/// base on another stack. This is for copy/clone. 
		/// </summary>
		/// <param name="stack">The stack.</param>
		public ContextStack(ContextStack<T> stack)
		{
			_position = stack._position;
			_array = new T[stack._array.Length];
			stack._array.CopyTo(_array, 0);
		}

		/// <summary>
		/// Gets the number of items in the stack.
		/// </summary>
		public int Count
		{
			get
			{
				return _position + 1;
			}
		}

		/// <summary>
		/// Pushes the specified item on the stack.
		/// </summary>
		/// <param name="item">The item to by pushed.</param>
		public void Push(T item)
		{
			EnsureSize();
			_array[++_position] = item;
		}

		/// <summary>
		/// Pops this element in top of the stack.
		/// </summary>
		/// <returns>The element in top of the stack</returns>
		public T Pop()
		{
			return _array[_position--];
		}

		/// <summary>
		/// Pops as many items as specified.
		/// </summary>
		/// <param name="n">The number of items to be poped</param>
		/// <exception cref="System.ArgumentOutOfRangeException">Cannot remove more elements</exception>
		public void Clear(int n)
		{
			if(n > Count)
				throw new ArgumentOutOfRangeException("n", "Cannot remove more elements");
			_position -= n;
		}

		/// <summary>
		/// Returns the i-th element from the top of the stack.
		/// </summary>
		/// <param name="i">The i-th index.</param>
		/// <returns>the i-th element from the top of the stack</returns>
		/// <exception cref="System.IndexOutOfRangeException">topIndex</exception>
		public T Top(int i)
		{
			if(i > 0 || -i > Count)
				throw new IndexOutOfRangeException("topIndex");
			return _array[Count + i];
		}

		/// <summary>
		/// Swaps the specified i and j elements in the stack.
		/// </summary>
		/// <param name="i">The i-th index.</param>
		/// <param name="j">The j-th index.</param>
		/// <exception cref="System.IndexOutOfRangeException">
		/// i or  j
		/// </exception>
		public void Swap(int i, int j)
		{
			if(i > 0 || -i > Count)
				throw new IndexOutOfRangeException("i");
			if(i > 0 || -j > Count)
				throw new IndexOutOfRangeException("j");

			var t = _array[Count + i];
			_array[Count + i] = _array[Count + j];
			_array[Count + j] = t;
		}

		/// <summary>
		/// Inserts an item in the specified position.
		/// </summary>
		/// <param name="position">The position.</param>
		/// <param name="value">The value.</param>
		public void Insert(int position, T value)
		{
			EnsureSize();

			position = Count + position;
			for(int i = _position; i >= position + 1; i--)
			{
				_array[i + 1] = _array[i];
			}
			_array[position + 1] = value;
			_position++;
		}

		/// <summary>
		/// Removes the i-th item.
		/// </summary>
		/// <param name="from">The item position</param>
		public void Remove(int from)
		{
			Remove(from, from + 1);
		}

		/// <summary>
		/// Removes items from the i-th position to the j-th position.
		/// </summary>
		/// <param name="from">The item position</param>
		/// <param name="to">The item position</param>
		public void Remove(int from, int to)
		{
			int toRemove = to - from;
			for(int i = Count + from; i < Count + from + toRemove; i++)
			{
				for(int y = Count + from; y < Count; y++)
					_array[y] = _array[y + 1];
			}
			_position -= toRemove;
		}

		private void EnsureSize()
		{
			if(_position < _array.Length - 1)
				return;
			Array.Resize(ref _array, 2 * _array.Length);
		}

		/// <summary>
		/// Returns a copy of the internal array.
		/// </summary>
		/// <returns>A copy of the internal array</returns>
		public T[] AsInternalArray()
		{
			var array = new T[Count];
			Array.Copy(_array, 0, array, 0, Count);
			return array;
		}

		public IEnumerator<T> GetEnumerator()
		{
			return new Enumerator(this);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return new Enumerator(this);
		}

		// Reverse order enumerator (for Stacks)

		/// <summary>
		/// Implements a reverse enumerator for the ContextStack
		/// </summary>
		public struct Enumerator : IEnumerator<T>
		{
			private ContextStack<T> _stack;
			private int _index;

			public Enumerator(ContextStack<T> stack)
			{
				_stack = stack;
				_index = stack._position + 1;
			}

			public T Current
			{
				get
				{
					if(_index == -1)
					{
						throw new InvalidOperationException("Enumeration has ended");
					}
					return _stack._array[_index];
				}
			}

			object IEnumerator.Current
			{
				get
				{
					return Current;
				}
			}

			public bool MoveNext()
			{
				return --_index >= 0;
			}

			public void Reset()
			{
				_index = _stack._position + 1;
			}

			public void Dispose()
			{
			}
		}

		internal void Clear()
		{
			Clear(Count);
		}
	}
}