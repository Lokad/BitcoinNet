namespace BitcoinNet.Protocol
{
	[Payload("sendcmpct")]
	public class SendCmpctPayload : Payload
	{
		private byte _preferHeaderAndIDs;
		private ulong _version = 1;

		public SendCmpctPayload()
		{
		}

		public SendCmpctPayload(bool preferHeaderAndIDs)
		{
			PreferHeaderAndIDs = preferHeaderAndIDs;
		}

		public bool PreferHeaderAndIDs
		{
			get => _preferHeaderAndIDs == 1;
			set => _preferHeaderAndIDs = value ? (byte) 1 : (byte) 0;
		}

		public ulong Version
		{
			get => _version;
			set => _version = value;
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _preferHeaderAndIDs);
			stream.ReadWrite(ref _version);
		}
	}
}