using System;
using System.Collections.Generic;
using System.IO;

namespace BitcoinNet.Scripting
{
	public class ScriptReader
	{
		private readonly Stream _Inner;
		public Stream Inner
		{
			get
			{
				return _Inner;
			}
		}
		public ScriptReader(Stream stream)
		{
			if(stream == null)
				throw new ArgumentNullException(nameof(stream));
			_Inner = stream;
		}
		public ScriptReader(byte[] data)
			: this(new MemoryStream(data))
		{

		}


		public Op Read()
		{
			var b = Inner.ReadByte();
			if(b == -1)
				return null;
			var opcode = (OpcodeType)b;
			if(Op.IsPushCode(opcode))
			{
				Op op = new Op();
				op.Code = opcode;
				op.PushData = op.ReadData(Inner);
				return op;
			}
			return new Op()
			{
				Code = opcode
			};
		}

		public bool HasError
		{
			get;
			private set;
		}

		public IEnumerable<Op> ToEnumerable()
		{
			Op code;
			while((code = Read()) != null)
			{
				yield return code;
			}
		}
	}
}
