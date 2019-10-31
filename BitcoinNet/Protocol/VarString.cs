using System;
using System.Linq;

namespace BitcoinNet.Protocol
{
	public class VarString : IBitcoinSerializable
	{
		private byte[] _bytes = new byte[0];

		public VarString()
		{
		}

		public VarString(byte[] bytes)
		{
			if (bytes == null)
			{
				throw new ArgumentNullException(nameof(bytes));
			}

			_bytes = bytes;
		}

		public int Length => _bytes.Length;

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			var len = new VarInt((ulong) _bytes.Length);
			stream.ReadWrite(ref len);
			if (!stream.Serializing)
			{
				if (len.ToLong() > (uint) stream.MaxArraySize)
				{
					throw new ArgumentOutOfRangeException(nameof(stream), "Array size not big");
				}

				_bytes = new byte[len.ToLong()];
			}

			stream.ReadWrite(ref _bytes);
		}

		public byte[] GetString()
		{
			return GetString(false);
		}

		public byte[] GetString(bool @unsafe)
		{
			if (@unsafe)
			{
				return _bytes;
			}

			return _bytes.ToArray();
		}

		internal static void StaticWrite(BitcoinStream bs, byte[] bytes)
		{
			VarInt.StaticWrite(bs, (ulong) bytes.Length);
			bs.ReadWrite(ref bytes);
		}

		internal static void StaticRead(BitcoinStream bs, ref byte[] bytes)
		{
			var len = VarInt.StaticRead(bs);
			bytes = new byte[len];
			bs.ReadWrite(ref bytes);
		}
	}
}