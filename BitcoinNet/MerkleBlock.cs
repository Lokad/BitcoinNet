using System.Collections.Generic;
using System.Linq;

namespace BitcoinNet
{
	public class MerkleBlock : IBitcoinSerializable
	{
		private BlockHeader _header;
		private PartialMerkleTree _partialMerkleTree;

		public MerkleBlock()
		{
		}

		// Create from a CBlock, filtering transactions according to filter
		// Note that this will call IsRelevantAndUpdate on the filter for each transaction,
		// thus the filter will likely be modified.
		public MerkleBlock(Block block, BloomFilter filter)
		{
			_header = block.Header;

			var vMatch = new List<bool>();
			var vHashes = new List<uint256>();

			for (uint i = 0; i < block.Transactions.Count; i++)
			{
				var hash = block.Transactions[(int) i].GetHash();
				vMatch.Add(filter.IsRelevantAndUpdate(block.Transactions[(int) i]));
				vHashes.Add(hash);
			}

			_partialMerkleTree = new PartialMerkleTree(vHashes.ToArray(), vMatch.ToArray());
		}

		public MerkleBlock(Block block, uint256[] txIds)
		{
			_header = block.Header;

			var vMatch = new List<bool>();
			var vHashes = new List<uint256>();
			for (var i = 0; i < block.Transactions.Count; i++)
			{
				var hash = block.Transactions[i].GetHash();
				vHashes.Add(hash);
				vMatch.Add(txIds.Contains(hash));
			}

			_partialMerkleTree = new PartialMerkleTree(vHashes.ToArray(), vMatch.ToArray());
		}

		public BlockHeader Header
		{
			get => _header;
			set => _header = value;
		}

		public PartialMerkleTree PartialMerkleTree
		{
			get => _partialMerkleTree;
			set => _partialMerkleTree = value;
		}

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _header);
			stream.ReadWrite(ref _partialMerkleTree);
		}
	}
}