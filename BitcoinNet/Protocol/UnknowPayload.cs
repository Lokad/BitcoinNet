namespace BitcoinNet.Protocol
{
	public class UnknowPayload : Payload
	{
		internal string _command;
		private byte[] _data = new byte[0];

		public UnknowPayload()
		{
		}

		public UnknowPayload(string command)
		{
			_command = command;
		}

		public override string Command => _command;

		public byte[] Data
		{
			get => _data;
			set => _data = value;
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _data);
		}
	}
}