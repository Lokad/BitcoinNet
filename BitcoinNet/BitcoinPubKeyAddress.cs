using System;
using System.Linq;
using BCashAddr;
using BitcoinNet.DataEncoders;

namespace BitcoinNet
{
	/// <summary>
	///     Abstracts an object able to verify messages from which it is possible to extract public key.
	/// </summary>
	public interface IPubkeyHashUsable
	{
		bool VerifyMessage(string message, string signature);

		bool VerifyMessage(byte[] message, byte[] signature);
	}

	/// <summary>
	///     Base58 representation of a pubkey hash and base class for the representation of a script hash
	/// </summary>
	public class BitcoinPubKeyAddress : BitcoinAddress, IBase58Data, IPubkeyHashUsable
	{
		private readonly BchAddr.BchAddrData _addr;

		public BitcoinPubKeyAddress(string str, Network expectedNetwork = null)
			: base(Validate(str, ref expectedNetwork), expectedNetwork)
		{
			_addr = ParseAddress(str, expectedNetwork);
			Hash = new KeyId(_addr.Hash);
		}

		public BitcoinPubKeyAddress(KeyId keyId, Network expectedNetwork = null)
			: this(EncodeAddress(keyId, expectedNetwork), expectedNetwork)
		{
		}

		/// <summary>
		/// Creates a new public key instance.
		/// Since, CashAddr allows lower-case or upper-case addresses,
		/// str argument is expected to store user supplied value instead of addr.ToString().
		/// </summary>
		/// <param name="str"></param>
		/// <param name="addr"></param>
		internal BitcoinPubKeyAddress(string str, BchAddr.BchAddrData addr)
			: base(str, addr.Network)
		{
			_addr = addr;
			Hash = new KeyId(_addr.Hash);
		}

		public CashFormat Format => _addr.Format;

		public KeyId Hash { get; }

		public Base58Type Type => Base58Type.PUBKEY_ADDRESS;

		public bool VerifyMessage(string message, string signature)
		{
			var key = PubKey.RecoverFromMessage(message, signature);
			return key.Hash == Hash;
		}

		public bool VerifyMessage(byte[] message, byte[] signature)
		{
			var key = PubKey.RecoverFromMessage(message, signature);
			return key.Hash == Hash;
		}
		
		private static string EncodeAddress(KeyId keyId, Network expectedNetwork)
		{
			var addr = BchAddr.BchAddrData.Create(CashFormat.Cashaddr, expectedNetwork, BchAddr.CashType.P2PKH,
				keyId.ToBytes());
			return addr.ToString();
		}

		private static byte[] ParseAddressHash(CashFormat format, string str, Network expectedNetwork)
		{
			switch (format)
			{
				case CashFormat.Legacy:
					var data = Encoders.Base58Check.DecodeData(str);
					var versionBytes = expectedNetwork.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, false);
					if (versionBytes != null && data.StartWith(versionBytes))
					{
						if (data.Length == versionBytes.Length + 20)
						{
							return data.Skip(versionBytes.Length).ToArray();
						}
					}

					break;

				case CashFormat.Cashaddr:
					var cashaddr = CashAddr.Decode(str);
					if (cashaddr.Prefix == expectedNetwork.Prefix)
					{
						return cashaddr.Hash;
					}

					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			throw new FormatException();
		}

		private static BchAddr.BchAddrData ParseAddress(string str, Network expectedNetwork)
		{
			var format = BchAddr.DetectFormat(str);
			var hash = ParseAddressHash(format, str, expectedNetwork);
			return BchAddr.BchAddrData.Create(format, expectedNetwork, BchAddr.CashType.P2PKH, hash);
		}

		private static string ValidateLegacyAddress(string str, ref Network expectedNetwork)
		{
			var data = Encoders.Base58Check.DecodeData(str);
			var networks = expectedNetwork == null ? Network.GetNetworks() : new[] { expectedNetwork };
			foreach (var network in networks)
			{
				var versionBytes = network.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, false);
				if (versionBytes != null && data.StartWith(versionBytes))
				{
					if (data.Length == versionBytes.Length + 20)
					{
						expectedNetwork = network;
						return str;
					}
				}
			}

			throw new FormatException("Invalid public key address.");
		}

		private static string ValidateCashAddr(string str, ref Network expectedNetwork)
		{
			var data = CashAddr.Decode(str);
			if (data.Type == BchAddr.CashType.P2PKH)
			{
				var networks = expectedNetwork == null ? Network.GetNetworks() : new[] {expectedNetwork};
				foreach (var network in networks)
				{
					if (data.Prefix == network.Prefix)
					{
						expectedNetwork = network;
						return str;
					}
				}
			}

			throw new FormatException("Invalid public key address.");
		}

		private static string Validate(string str, ref Network expectedNetwork)
		{
			var format = BchAddr.DetectFormat(str);
			switch (format)
			{
				case CashFormat.Legacy:
					return ValidateLegacyAddress(str, ref expectedNetwork);

				case CashFormat.Cashaddr:
					return ValidateCashAddr(str, ref expectedNetwork);

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected override Script GeneratePaymentScript()
		{
			return new KeyId(_addr.Hash).ScriptPubKey;
		}

		public BitcoinPubKeyAddress ToLegacy()
		{
			return _addr.Network.CreateBitcoinAddress(CashFormat.Legacy, Hash);
		}

		public BitcoinPubKeyAddress ToCashAddr()
		{
			return _addr.Network.CreateBitcoinAddress(CashFormat.Cashaddr, Hash);
		}

		public static bool TryParse(string str, Network expectedNetwork, out BitcoinPubKeyAddress result)
		{
			try
			{
				result = new BitcoinPubKeyAddress(str, expectedNetwork);
			}
			catch
			{
				result = null;
			}

			return result != null;
		}
	}
}