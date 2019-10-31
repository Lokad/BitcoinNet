using System;
using System.IO;

namespace BitcoinNet.Protocol
{
	public class CompactVarInt : IBitcoinSerializable
	{
		private readonly int _size;
		private ulong _value;

		public CompactVarInt(int size)
		{
			_size = size;
		}

		public CompactVarInt(ulong value, int size)
		{
			_value = value;
			_size = size;
		}

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			if (stream.Serializing)
			{
				var n = _value;
				Span<byte> tmp = stackalloc byte[(_size * 8 + 6) / 7];

				var len = 0;
				while (true)
				{
					var a = (byte) (n & 0x7F);
					var b = (byte) (len != 0 ? 0x80 : 0x00);
					tmp[len] = (byte) (a | b);
					if (n <= 0x7F)
					{
						break;
					}

					n = (n >> 7) - 1;
					len++;
				}

				do
				{
					var b = tmp[len];
					stream.ReadWrite(ref b);
				} while (len-- != 0);
			}
			else
			{
				ulong n = 0;
				while (true)
				{
					byte chData = 0;
					stream.ReadWrite(ref chData);
					var a = n << 7;
					var b = (byte) (chData & 0x7F);
					n = a | b;
					if ((chData & 0x80) != 0)
					{
						n++;
					}
					else
					{
						break;
					}
				}

				_value = n;
			}
		}

		public ulong ToLong()
		{
			return _value;
		}
	}


	//https://en.bitcoin.it/wiki/Protocol_specification#Variable_length_integer
	public class VarInt : IBitcoinSerializable
	{
		private ulong _value;

		public VarInt()
			: this(0)
		{
		}

		public VarInt(ulong value)
		{
			SetValue(value);
		}

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			if (stream.Serializing)
			{
				StaticWrite(stream, _value);
			}
			else
			{
				_value = StaticRead(stream);
			}
		}

		internal void SetValue(ulong value)
		{
			_value = value;
		}

		public static void StaticWrite(BitcoinStream bs, ulong length)
		{
			if (!bs.Serializing)
			{
				throw new InvalidOperationException("Stream should be serializing");
			}

			var stream = bs.Inner;
			bs.Counter.AddBytesWritten(1);
			if (length < 0xFD)
			{
				stream.WriteByte((byte) length);
			}
			else if (length <= 0xffff)
			{
				var value = (ushort) length;
				stream.WriteByte(0xFD);
				bs.ReadWrite(ref value);
			}
			else if (length <= 0xffff)
			{
				var value = (uint) length;
				stream.WriteByte(0xFE);
				bs.ReadWrite(ref value);
			}
			else if (length <= 0xffffffff)
			{
				var value = length;
				stream.WriteByte(0xFF);
				bs.ReadWrite(ref value);
			}
		}

		public static ulong StaticRead(BitcoinStream bs)
		{
			if (bs.Serializing)
			{
				throw new InvalidOperationException("Stream should not be serializing");
			}

			var prefix = bs.Inner.ReadByte();
			bs.Counter.AddBytesRead(1);
			if (prefix == -1)
			{
				throw new EndOfStreamException("No more byte to read");
			}

			if (prefix < 0xFD)
			{
				return (byte) prefix;
			}

			if (prefix == 0xFD)
			{
				var value = (ushort) 0;
				bs.ReadWrite(ref value);
				return value;
			}

			if (prefix == 0xFE)
			{
				var value = (uint) 0;
				bs.ReadWrite(ref value);
				return value;
			}
			else
			{
				var value = (ulong) 0;
				bs.ReadWrite(ref value);
				return value;
			}
		}

		public ulong ToLong()
		{
			return _value;
		}
	}
}