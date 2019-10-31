using System.Collections.Generic;

namespace BitcoinNet.Protocol
{
	/// <summary>
	///     Ask for transaction, block or merkle block
	/// </summary>
	[Payload("getdata")]
	public class GetDataPayload : Payload
	{
		private List<InventoryVector> _inventory = new List<InventoryVector>();

		public GetDataPayload()
		{
		}

		public GetDataPayload(params InventoryVector[] vectors)
		{
			_inventory.AddRange(vectors);
		}

		public List<InventoryVector> Inventory
		{
			set => _inventory = value;
			get => _inventory;
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _inventory);
		}
	}
}