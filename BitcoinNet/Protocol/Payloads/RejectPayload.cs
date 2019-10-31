using BitcoinNet.DataEncoders;

namespace BitcoinNet.Protocol
{
	public enum RejectCode : byte
	{
		MALFORMED = 0x01,
		INVALID = 0x10,
		OBSOLETE = 0x11,
		DUPLICATE = 0x12,
		NONSTANDARD = 0x40,
		DUST = 0x41,
		INSUFFICIENTFEE = 0x42,
		CHECKPOINT = 0x43
	}

	public enum RejectCodeType
	{
		Common,
		Version,
		Transaction,
		Block
	}

	/// <summary>
	///     A transaction or block are rejected being transmitted through tx or block messages
	/// </summary>
	[Payload("reject")]
	public class RejectPayload : Payload
	{
		private byte _code;
		private uint256 _hash;
		private VarString _message = new VarString();
		private VarString _reason = new VarString();

		/// <summary>
		///     "tx" or "block"
		/// </summary>
		public string Message
		{
			get => Encoders.ASCII.EncodeData(_message.GetString(true));
			set => _message = new VarString(Encoders.ASCII.DecodeData(value));
		}

		public RejectCode Code
		{
			get => (RejectCode) _code;
			set => _code = (byte) value;
		}

		public RejectCodeType CodeType
		{
			get
			{
				switch (Code)
				{
					case RejectCode.MALFORMED:
						return RejectCodeType.Common;
					case RejectCode.OBSOLETE:
						if (Message == "block")
						{
							return RejectCodeType.Block;
						}
						else
						{
							return RejectCodeType.Version;
						}
					case RejectCode.DUPLICATE:
						if (Message == "tx")
						{
							return RejectCodeType.Transaction;
						}
						else
						{
							return RejectCodeType.Version;
						}
					case RejectCode.NONSTANDARD:
					case RejectCode.DUST:
					case RejectCode.INSUFFICIENTFEE:
						return RejectCodeType.Transaction;
					case RejectCode.CHECKPOINT:
						return RejectCodeType.Block;
					case RejectCode.INVALID:
						if (Message == "tx")
						{
							return RejectCodeType.Transaction;
						}
						else
						{
							return RejectCodeType.Block;
						}
					default:
						return RejectCodeType.Common;
				}
			}
		}

		/// <summary>
		///     Details of the error
		/// </summary>
		public string Reason
		{
			get => Encoders.ASCII.EncodeData(_reason.GetString(true));
			set => _reason = new VarString(Encoders.ASCII.DecodeData(value));
		}

		/// <summary>
		///     The hash being rejected
		/// </summary>
		public uint256 Hash
		{
			get => _hash;
			set => _hash = value;
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _message);
			stream.ReadWrite(ref _code);
			stream.ReadWrite(ref _reason);
			if (Message == "tx" || Message == "block")
			{
				stream.ReadWrite(ref _hash);
			}
		}
	}
}