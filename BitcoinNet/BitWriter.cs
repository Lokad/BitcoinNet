using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BitcoinNet
{
	public class BitReader
	{
		private readonly BitArray _array;

		public BitReader(byte[] data, int bitCount)
		{
			var writer = new BitWriter();
			writer.Write(data, bitCount);
			_array = writer.ToBitArray();
		}

		public BitReader(BitArray array)
		{
			_array = new BitArray(array.Length);
			for (var i = 0; i < array.Length; i++)
			{
				_array.Set(i, array.Get(i));
			}
		}

		public int Position { get; set; }

		public int Count => _array.Length;

		public bool Read()
		{
			var v = _array.Get(Position);
			Position++;
			return v;
		}

		public uint ReadUInt(int bitCount)
		{
			uint value = 0;
			for (var i = 0; i < bitCount; i++)
			{
				var v = Read() ? 1U : 0U;
				value += v << i;
			}

			return value;
		}

		public BitArray ToBitArray()
		{
			var result = new BitArray(_array.Length);
			for (var i = 0; i < _array.Length; i++)
			{
				result.Set(i, _array.Get(i));
			}

			return result;
		}

		public BitWriter ToWriter()
		{
			var writer = new BitWriter();
			writer.Write(_array);
			return writer;
		}

		public void Consume(int count)
		{
			Position += count;
		}

		public bool Same(BitReader b)
		{
			while (Position != Count && b.Position != b.Count)
			{
				var valueA = Read();
				var valueB = b.Read();
				if (valueA != valueB)
				{
					return false;
				}
			}

			return true;
		}

		public override string ToString()
		{
			var builder = new StringBuilder(_array.Length);
			for (var i = 0; i < Count; i++)
			{
				if (i != 0 && i % 8 == 0)
				{
					builder.Append(' ');
				}

				builder.Append(_array.Get(i) ? "1" : "0");
			}

			return builder.ToString();
		}
	}

	public class BitWriter
	{
		private readonly List<bool> _values = new List<bool>();

		public int Count => _values.Count;

		public int Position { get; set; }

		public void Write(bool value)
		{
			_values.Insert(Position, value);
			Position++;
		}

		public void Write(byte[] bytes)
		{
			Write(bytes, bytes.Length * 8);
		}

		public void Write(byte[] bytes, int bitCount)
		{
			bytes = SwapEndianBytes(bytes);
			var array = new BitArray(bytes);
			_values.InsertRange(Position, array.OfType<bool>().Take(bitCount));
			Position += bitCount;
		}

		public byte[] ToBytes()
		{
			var array = ToBitArray();
			var bytes = ToByteArray(array);
			bytes = SwapEndianBytes(bytes);
			return bytes;
		}

		//BitArray.CopyTo do not exist in portable lib
		private static byte[] ToByteArray(BitArray bits)
		{
			var arrayLength = bits.Length / 8;
			if (bits.Length % 8 != 0)
			{
				arrayLength++;
			}

			var array = new byte[arrayLength];

			for (var i = 0; i < bits.Length; i++)
			{
				var b = i / 8;
				var offset = i % 8;
				array[b] |= bits.Get(i) ? (byte) (1 << offset) : (byte) 0;
			}

			return array;
		}

		public BitArray ToBitArray()
		{
			return new BitArray(_values.ToArray());
		}

		private static byte[] SwapEndianBytes(byte[] bytes)
		{
			var output = new byte[bytes.Length];
			for (var i = 0; i < output.Length; i++)
			{
				byte newByte = 0;
				for (var ib = 0; ib < 8; ib++)
				{
					newByte += (byte) (((bytes[i] >> ib) & 1) << (7 - ib));
				}

				output[i] = newByte;
			}

			return output;
		}

		public void Write(uint value, int bitCount)
		{
			for (var i = 0; i < bitCount; i++)
			{
				Write((value & 1) == 1);
				value = value >> 1;
			}
		}

		internal void Write(BitReader reader, int bitCount)
		{
			for (var i = 0; i < bitCount; i++)
			{
				Write(reader.Read());
			}
		}

		public void Write(BitArray bitArray)
		{
			Write(bitArray, bitArray.Length);
		}

		public void Write(BitArray bitArray, int bitCount)
		{
			for (var i = 0; i < bitCount; i++)
			{
				Write(bitArray.Get(i));
			}
		}

		public void Write(BitReader reader)
		{
			Write(reader, reader.Count - reader.Position);
		}

		public BitReader ToReader()
		{
			return new BitReader(ToBitArray());
		}

		public override string ToString()
		{
			var builder = new StringBuilder(_values.Count);
			for (var i = 0; i < Count; i++)
			{
				if (i != 0 && i % 8 == 0)
				{
					builder.Append(' ');
				}

				builder.Append(_values[i] ? "1" : "0");
			}

			return builder.ToString();
		}
	}
}