using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BitcoinNet
{
	public class ThreadSafeCollection<T> : IEnumerable<T>
	{
		private readonly ConcurrentDictionary<T, T> _behaviors = new ConcurrentDictionary<T, T>();

		// IEnumerable<T> Members

		public IEnumerator<T> GetEnumerator()
		{
			return _behaviors.Select(k => k.Key).GetEnumerator();
		}

		// IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>
		///     Add an item to the collection
		/// </summary>
		/// <param name="item"></param>
		/// <returns>When disposed, the item is removed</returns>
		public IDisposable Add(T item)
		{
			if (item == null)
			{
				throw new ArgumentNullException(nameof(item));
			}

			OnAdding(item);
			_behaviors.TryAdd(item, item);
			return new ActionDisposable(() => { }, () => Remove(item));
		}

		protected virtual void OnAdding(T obj)
		{
		}

		protected virtual void OnRemoved(T obj)
		{
		}

		public bool Remove(T item)
		{
			var removed = _behaviors.TryRemove(item, out var old);
			if (removed)
			{
				OnRemoved(old);
			}

			return removed;
		}


		public void Clear()
		{
			foreach (var behavior in this)
			{
				Remove(behavior);
			}
		}

		public T FindOrCreate<U>() where U : T, new()
		{
			return FindOrCreate(() => new U());
		}

		public U FindOrCreate<U>(Func<U> create) where U : T
		{
			var result = this.OfType<U>().FirstOrDefault();
			if (result == null)
			{
				result = create();
				Add(result);
			}

			return result;
		}

		public U Find<U>() where U : T
		{
			return this.OfType<U>().FirstOrDefault();
		}

		public void Remove<U>() where U : T
		{
			foreach (var b in this.OfType<U>())
			{
				_behaviors.TryRemove(b, out _);
			}
		}
	}
}