using System.Collections.Generic;

namespace BitcoinNet.Protocol
{
	[Payload("blocktxn")]
	public class BlockTxnPayload : Payload
	{
		private uint256 _blockId;
		private List<Transaction> _transactions = new List<Transaction>();

		public uint256 BlockId
		{
			get => _blockId;
			set => _blockId = value;
		}

		public List<Transaction> Transactions => _transactions;

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _blockId);
			stream.ReadWrite(ref _transactions);
		}
	}
}