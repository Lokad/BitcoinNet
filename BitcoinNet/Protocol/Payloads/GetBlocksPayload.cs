namespace BitcoinNet.Protocol
{
	/// <summary>
	///     Ask for the block hashes (inv) that happened since BlockLocators
	/// </summary>
	[Payload("getblocks")]
	public class GetBlocksPayload : Payload
	{
		private BlockLocator _blockLocators;
		private uint256 _hashStop = uint256.Zero;
		private uint _version = Network.Main.MaxP2PVersion;

		public GetBlocksPayload(BlockLocator locator)
		{
			BlockLocators = locator;
		}

		public GetBlocksPayload()
		{
		}

		public uint Version
		{
			get => _version;
			set => _version = value;
		}

		public BlockLocator BlockLocators
		{
			get => _blockLocators;
			set => _blockLocators = value;
		}

		public uint256 HashStop
		{
			get => _hashStop;
			set => _hashStop = value;
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _version);
			stream.ReadWrite(ref _blockLocators);
			stream.ReadWrite(ref _hashStop);
		}
	}
}