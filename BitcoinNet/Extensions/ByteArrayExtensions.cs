using System.Linq;

namespace System
{
	public static class ByteArrayExtensions
	{
		public static bool StartWith(this byte[] data, byte[] versionBytes)
		{
			if (data.Length < versionBytes.Length)
			{
				return false;
			}

			for (var i = 0; i < versionBytes.Length; i++)
			{
				if (data[i] != versionBytes[i])
				{
					return false;
				}
			}

			return true;
		}

		public static byte[] SafeSubArray(this byte[] array, int offset, int count)
		{
			if (array == null)
			{
				throw new ArgumentNullException(nameof(array));
			}

			if (offset < 0 || offset > array.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(offset));
			}

			if (count < 0 || offset + count > array.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			if (offset == 0 && array.Length == count)
			{
				return array;
			}

			var data = new byte[count];
			Buffer.BlockCopy(array, offset, data, 0, count);
			return data;
		}

		public static byte[] SafeSubArray(this byte[] array, int offset)
		{
			if (array == null)
			{
				throw new ArgumentNullException(nameof(array));
			}

			if (offset < 0 || offset > array.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(offset));
			}

			var count = array.Length - offset;
			var data = new byte[count];
			Buffer.BlockCopy(array, offset, data, 0, count);
			return data;
		}

		public static byte[] Concat(this byte[] arr, params byte[][] arrays)
		{
			var len = arr.Length + arrays.Sum(a => a.Length);
			var ret = new byte[len];
			Buffer.BlockCopy(arr, 0, ret, 0, arr.Length);
			var pos = arr.Length;
			foreach (var a in arrays)
			{
				Buffer.BlockCopy(a, 0, ret, pos, a.Length);
				pos += a.Length;
			}

			return ret;
		}
	}
}