namespace BitcoinNet.Protocol
{
	[Payload("filteradd")]
	public class FilterAddPayload : Payload
	{
		private byte[] _data;

		public FilterAddPayload()
		{
		}

		public FilterAddPayload(byte[] data)
		{
			_data = data;
		}

		public byte[] Data
		{
			get => _data;
			set => _data = value;
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWriteAsVarString(ref _data);
		}
	}
}