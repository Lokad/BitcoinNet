using System.Collections.Generic;
using System.Linq;

namespace BitcoinNet.Protocol
{
	/// <summary>
	///     Block headers received after a getheaders messages
	/// </summary>
	[Payload("headers")]
	public class HeadersPayload : Payload
	{
		public HeadersPayload()
		{
		}

		public HeadersPayload(params BlockHeader[] headers)
		{
			Headers.AddRange(headers);
		}

		public List<BlockHeader> Headers { get; } = new List<BlockHeader>();

		public override void ReadWriteCore(BitcoinStream stream)
		{
			if (stream.Serializing)
			{
				var headersOff = Headers.Select(h => new BlockHeaderWithTxCount(h)).ToList();
				stream.ReadWrite(ref headersOff);
			}
			else
			{
				Headers.Clear();
				var headersOff = new List<BlockHeaderWithTxCount>();
				stream.ReadWrite(ref headersOff);
				Headers.AddRange(headersOff.Select(h => h._header));
			}
		}

		private class BlockHeaderWithTxCount : IBitcoinSerializable
		{
			internal BlockHeader _header;

			public BlockHeaderWithTxCount()
			{
			}

			public BlockHeaderWithTxCount(BlockHeader header)
			{
				_header = header;
			}

			// IBitcoinSerializable Members

			public void ReadWrite(BitcoinStream stream)
			{
				stream.ReadWrite(ref _header);
				var txCount = new VarInt(0);
				stream.ReadWrite(ref txCount);
			}
		}
	}
}