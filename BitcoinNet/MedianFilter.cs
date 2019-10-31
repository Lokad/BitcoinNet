using System;
using System.Collections.Generic;
using System.Linq;

namespace BitcoinNet
{
	public class MedianFilterInt32
	{
		private readonly uint _nSize;
		private readonly Queue<int> _vValues;
		private Queue<int> _vSorted;

		public MedianFilterInt32(uint size, int initialValue)
		{
			_nSize = size;
			_vValues = new Queue<int>((int) size);
			_vValues.Enqueue(initialValue);
			_vSorted = new Queue<int>(_vValues);
		}

		public int Median
		{
			get
			{
				var size = _vSorted.Count;
				if (size <= 0)
				{
					throw new InvalidOperationException("size <= 0");
				}

				var sortedList = _vSorted.ToList();
				if (size % 2 == 1)
				{
					return sortedList[size / 2];
				}

				return (sortedList[size / 2 - 1] + sortedList[size / 2]) / 2;
			}
		}

		public void Input(int value)
		{
			if (_vValues.Count == _nSize)
			{
				_vValues.Dequeue();
			}

			_vValues.Enqueue(value);
			_vSorted = new Queue<int>(_vValues.OrderBy(o => o));
		}
	}

	public class MedianFilterInt64
	{
		private readonly uint _nSize;
		private readonly Queue<long> _vValues;
		private Queue<long> _vSorted;

		public MedianFilterInt64(uint size, long initialValue)
		{
			_nSize = size;
			_vValues = new Queue<long>((int) size);
			_vValues.Enqueue(initialValue);
			_vSorted = new Queue<long>(_vValues);
		}

		public long Median
		{
			get
			{
				var size = _vSorted.Count;
				if (size <= 0)
				{
					throw new InvalidOperationException("size <= 0");
				}

				var sortedList = _vSorted.ToList();
				if (size % 2 == 1)
				{
					return sortedList[size / 2];
				}

				return (sortedList[size / 2 - 1] + sortedList[size / 2]) / 2;
			}
		}

		public void Input(long value)
		{
			if (_vValues.Count == _nSize)
			{
				_vValues.Dequeue();
			}

			_vValues.Enqueue(value);
			_vSorted = new Queue<long>(_vValues.OrderBy(o => o));
		}
	}
}