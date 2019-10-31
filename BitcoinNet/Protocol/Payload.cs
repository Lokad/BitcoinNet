namespace BitcoinNet.Protocol
{
	public class Payload : IBitcoinSerializable
	{
		public virtual string Command => PayloadAttribute.GetCommandName(GetType());

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			using (stream.SerializationTypeScope(SerializationType.Network))
			{
				ReadWriteCore(stream);
			}
		}

		public virtual void ReadWriteCore(BitcoinStream stream)
		{
		}

		public override string ToString()
		{
			return GetType().Name;
		}
	}
}