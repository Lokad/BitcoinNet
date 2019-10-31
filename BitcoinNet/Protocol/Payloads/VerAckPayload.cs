namespace BitcoinNet.Protocol
{
	[Payload("verack")]
	public class VerAckPayload : Payload, IBitcoinSerializable
	{
		// IBitcoinSerializable Members

		public override void ReadWriteCore(BitcoinStream stream)
		{
		}
	}
}