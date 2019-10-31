namespace BitcoinNet.Protocol
{
	[Payload("pong")]
	public class PongPayload : Payload
	{
		private ulong _nonce;

		public ulong Nonce
		{
			get => _nonce;
			set => _nonce = value;
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _nonce);
		}

		public override string ToString()
		{
			return base.ToString() + " : " + Nonce;
		}
	}
}