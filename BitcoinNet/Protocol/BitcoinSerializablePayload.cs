namespace BitcoinNet.Protocol
{
	public class BitcoinSerializablePayload<T> : Payload where T : IBitcoinSerializable, new()
	{
		private T _object = new T();

		public BitcoinSerializablePayload()
		{
		}

		public BitcoinSerializablePayload(T obj)
		{
			_object = obj;
		}

		public T Object
		{
			get => _object;
			set => _object = value;
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			if (!stream.Serializing)
			{
				_object = default;
			}

			stream.ReadWrite(ref _object);
		}
	}
}