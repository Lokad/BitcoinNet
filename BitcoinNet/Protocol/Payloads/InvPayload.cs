using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BitcoinNet.Protocol
{
	/// <summary>
	///     Announce the hash of a transaction or block
	/// </summary>
	[Payload("inv")]
	public class InvPayload : Payload, IBitcoinSerializable, IEnumerable<InventoryVector>
	{
		// IBitcoinSerializable Members

		public const int MaxInvSz = 50000;
		private List<InventoryVector> _inventory = new List<InventoryVector>();

		public InvPayload()
		{
		}

		public InvPayload(params Transaction[] transactions)
			: this(transactions.Select(tx => new InventoryVector(InventoryType.MSG_TX, tx.GetHash())).ToArray())
		{
		}

		public InvPayload(params Block[] blocks)
			: this(blocks.Select(b => new InventoryVector(InventoryType.MSG_BLOCK, b.GetHash())).ToArray())
		{
		}

		public InvPayload(InventoryType type, params uint256[] hashes)
			: this(hashes.Select(h => new InventoryVector(type, h)).ToArray())
		{
		}

		public InvPayload(params InventoryVector[] invs)
		{
			_inventory.AddRange(invs);
		}

		public List<InventoryVector> Inventory => _inventory;

		// IEnumerable<uint256> Members

		public IEnumerator<InventoryVector> GetEnumerator()
		{
			return Inventory.GetEnumerator();
		}

		// IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			var old = stream.MaxArraySize;
			stream.MaxArraySize = MaxInvSz;
			stream.ReadWrite(ref _inventory);
			stream.MaxArraySize = old;
		}

		public override string ToString()
		{
			return "Count: " + Inventory.Count;
		}
	}
}