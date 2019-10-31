using System;
using System.Collections.Generic;
using System.IO;

namespace BitcoinNet.Scripting
{
	public class ScriptReader
	{
		public ScriptReader(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			Inner = stream;
		}

		public ScriptReader(byte[] data)
			: this(new MemoryStream(data))
		{
		}

		public Stream Inner { get; }

		public bool HasError { get; private set; }


		public Op Read()
		{
			var b = Inner.ReadByte();
			if (b == -1)
			{
				return null;
			}

			var opcode = (OpcodeType) b;
			if (Op.IsPushCode(opcode))
			{
				var op = new Op {Code = opcode};
				op.PushData = op.ReadData(Inner);
				return op;
			}

			return new Op
			{
				Code = opcode
			};
		}

		public IEnumerable<Op> ToEnumerable()
		{
			Op code;
			while ((code = Read()) != null)
			{
				yield return code;
			}
		}
	}
}