using System;
using System.Globalization;
using System.Linq;

namespace BitcoinNet
{
	/// <summary>
	///     Represent a path in the hierarchy of HD keys (BIP32)
	/// </summary>
	public class KeyPath
	{
		private readonly uint[] _indexes;
		private string _path;

		public KeyPath()
		{
			_indexes = new uint[0];
		}

		public KeyPath(string path)
		{
			_indexes =
				path
					.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
					.Where(p => p != "m")
					.Select(ParseCore)
					.ToArray();
		}

		public KeyPath(params uint[] indexes)
		{
			_indexes = indexes;
		}

		public uint this[int index] => _indexes[index];

		public uint[] Indexes => _indexes.ToArray();

		public KeyPath Parent
		{
			get
			{
				if (_indexes.Length == 0)
				{
					return null;
				}

				return new KeyPath(_indexes.Take(_indexes.Length - 1).ToArray());
			}
		}

		public bool IsHardened
		{
			get
			{
				if (_indexes.Length == 0)
				{
					throw new InvalidOperationException("No indices found in this KeyPath");
				}

				return (_indexes[_indexes.Length - 1] & 0x80000000u) != 0;
			}
		}

		/// <summary>
		///     Parse a KeyPath
		/// </summary>
		/// <param name="path">The KeyPath formatted like 10/0/2'/3</param>
		/// <returns></returns>
		public static KeyPath Parse(string path)
		{
			var parts = path
				.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
				.Where(p => p != "m")
				.Select(ParseCore)
				.ToArray();
			return new KeyPath(parts);
		}

		private static uint ParseCore(string i)
		{
			var hardened = i.EndsWith("'");
			var nonHardened = hardened ? i.Substring(0, i.Length - 1) : i;
			var index = uint.Parse(nonHardened);
			return hardened ? index | 0x80000000u : index;
		}

		public KeyPath Derive(int index, bool hardened)
		{
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(index), "the index can't be negative");
			}

			var realIndex = (uint) index;
			realIndex = hardened ? realIndex | 0x80000000u : realIndex;
			return Derive(new KeyPath(realIndex));
		}

		public KeyPath Derive(uint index)
		{
			return Derive(new KeyPath(index));
		}

		public KeyPath Derive(KeyPath derivation)
		{
			return new KeyPath(
				_indexes
					.Concat(derivation._indexes)
					.ToArray());
		}

		public KeyPath Increment()
		{
			if (_indexes.Length == 0)
			{
				return null;
			}

			var indices = _indexes.ToArray();
			indices[indices.Length - 1]++;
			return new KeyPath(indices);
		}

		public override bool Equals(object obj)
		{
			var item = obj as KeyPath;
			if (item == null)
			{
				return false;
			}

			return ToString().Equals(item.ToString());
		}

		public static bool operator ==(KeyPath a, KeyPath b)
		{
			if (ReferenceEquals(a, b))
			{
				return true;
			}

			if ((object) a == null || (object) b == null)
			{
				return false;
			}

			return a.ToString() == b.ToString();
		}

		public static bool operator !=(KeyPath a, KeyPath b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return ToString().GetHashCode();
		}

		public override string ToString()
		{
			return _path ?? (_path = string.Join("/", _indexes.Select(ToString).ToArray()));
		}

		private static string ToString(uint i)
		{
			var hardened = (i & 0x80000000u) != 0;
			var nonHardened = i & ~0x80000000u;
			return hardened ? nonHardened + "'" : nonHardened.ToString(CultureInfo.InvariantCulture);
		}
	}
}