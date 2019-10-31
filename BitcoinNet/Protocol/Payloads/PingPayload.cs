namespace BitcoinNet.Protocol
{
	[Payload("ping")]
	public class PingPayload : Payload
	{
		private ulong _nonce;

		public PingPayload()
		{
			_nonce = RandomUtils.GetUInt64();
		}

		public ulong Nonce
		{
			get => _nonce;
			set => _nonce = value;
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _nonce);
		}

		public PongPayload CreatePong()
		{
			return new PongPayload
			{
				Nonce = Nonce
			};
		}

		public override string ToString()
		{
			return base.ToString() + " : " + Nonce;
		}
	}
}