using System.Collections;

namespace BitcoinNet.Protocol.Payloads
{
	[Payload("utxos")]
	public class UTxOutputPayload : Payload
	{
		private UTxOutputs _uTxOutputs;

		public override void ReadWriteCore(BitcoinStream stream)
		{
			_uTxOutputs = new UTxOutputs();
			stream.ReadWrite(ref _uTxOutputs);
		}
	}

	public class UTxOutputs : IBitcoinSerializable
	{
		private VarString _bitmap;
		private int _chainHeight;
		private uint256 _chainTipHash;
		private UTxOut[] _outputs;

		public int ChainHeight
		{
			get => _chainHeight;
			internal set => _chainHeight = value;
		}

		public uint256 ChainTipHash
		{
			get => _chainTipHash;
			internal set => _chainTipHash = value;
		}

		public BitArray Bitmap
		{
			get => new BitArray(_bitmap.ToBytes());
			internal set
			{
				var bits = value;
				var buffer = new BitReader(bits).ToWriter().ToBytes();
				_bitmap = new VarString(buffer);
			}
		}

		public UTxOut[] Outputs
		{
			get => _outputs;
			internal set => _outputs = value;
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _chainHeight);
			stream.ReadWrite(ref _chainTipHash);
			stream.ReadWrite(ref _bitmap);
			stream.ReadWrite(ref _outputs);
		}
	}

	public class UTxOut : IBitcoinSerializable
	{
		private uint _height;
		private TxOut _txOut;
		private uint _version;

		public uint Version
		{
			get => _version;
			internal set => _version = value;
		}

		public uint Height
		{
			get => _height;
			internal set => _height = value;
		}

		public TxOut Output
		{
			get => _txOut;
			internal set => _txOut = value;
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _version);
			stream.ReadWrite(ref _height);
			stream.ReadWrite(ref _txOut);
		}
	}
}