using System;
using System.Collections.Generic;

namespace BitcoinNet.Protocol
{
	[Payload("getblocktxn")]
	public class GetBlockTxnPayload : Payload
	{
		private uint256 _blockId = uint256.Zero;

		public uint256 BlockId
		{
			get => _blockId;
			set => _blockId = value;
		}

		public List<int> Indices { get; } = new List<int>();

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _blockId);
			var indexesSize = (ulong) Indices.Count;
			stream.ReadWriteAsVarInt(ref indexesSize);
			if (!stream.Serializing)
			{
				ulong i = 0;
				ulong indicesCount = 0;
				while ((ulong) Indices.Count < indexesSize)
				{
					indicesCount = Math.Min(1000UL + indicesCount, indexesSize);
					for (; i < indicesCount; i++)
					{
						ulong index = 0;
						stream.ReadWriteAsVarInt(ref index);
						if (index > int.MaxValue)
						{
							throw new FormatException("indexes overflowed 31-bits");
						}

						Indices.Add((int) index);
					}
				}

				var offset = 0;
				for (var ii = 0; ii < Indices.Count; ii++)
				{
					if ((ulong) Indices[ii] + (ulong) offset > int.MaxValue)
					{
						throw new FormatException("indexes overflowed 31-bits");
					}

					Indices[ii] = Indices[ii] + offset;
					offset = Indices[ii] + 1;
				}
			}
			else
			{
				for (var i = 0; i < Indices.Count; i++)
				{
					var index = Indices[i] - (i == 0 ? 0 : Indices[i - 1] + 1);
					stream.ReadWrite(ref index);
				}
			}
		}
	}
}