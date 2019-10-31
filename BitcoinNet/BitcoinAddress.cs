using System;
using System.Linq;
using BCashAddr;
using BitcoinNet.DataEncoders;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	/// <summary>
	///     Base58 representaiton of a script hash
	/// </summary>
	public class BitcoinScriptAddress : BitcoinAddress, IBase58Data
	{
		private readonly BchAddr.BchAddrData _addr;

		public BitcoinScriptAddress(string str, Network expectedNetwork = null)
			: base(Validate(str, ref expectedNetwork), expectedNetwork)
		{
			_addr = ParseAddress(str, expectedNetwork);
			Hash = new ScriptId(_addr.Hash);
		}

		public BitcoinScriptAddress(ScriptId scriptId, Network expectedNetwork = null)
			: this(EncodeAddress(scriptId, expectedNetwork), expectedNetwork)
		{
		}

		/// <summary>
		///     Creates a new script key instance.
		///     Since, CashAddr allows lower-case or upper-case addresses,
		///     str argument is expected to store user supplied value instead of addr.ToString().
		/// </summary>
		/// <param name="str"></param>
		/// <param name="addr"></param>
		internal BitcoinScriptAddress(string str, BchAddr.BchAddrData addr)
			: base(str, addr.Network)
		{
			_addr = addr;
			Hash = new ScriptId(_addr.Hash);
		}

		public CashFormat Format => _addr.Format;
		public ScriptId Hash { get; }
		public Base58Type Type => Base58Type.SCRIPT_ADDRESS;

		private static string EncodeAddress(ScriptId scriptId, Network expectedNetwork)
		{
			//var data = expectedNetwork.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, false).Concat(scriptId.ToBytes());
			var addr = BchAddr.BchAddrData.Create(CashFormat.Cashaddr, expectedNetwork, BchAddr.CashType.P2SH,
				scriptId.ToBytes());
			return addr.ToString();
		}

		private static byte[] ParseAddressHash(CashFormat format, string str, Network expectedNetwork)
		{
			switch (format)
			{
				case CashFormat.Legacy:
					var data = Encoders.Base58Check.DecodeData(str);
					var versionBytes = expectedNetwork.GetVersionBytes(Base58Type.SCRIPT_ADDRESS, false);
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
			return BchAddr.BchAddrData.Create(format, expectedNetwork, BchAddr.CashType.P2SH, hash);
		}

		private static string ValidateLegacyAddress(string str, ref Network expectedNetwork)
		{
			var data = Encoders.Base58Check.DecodeData(str);
			var networks = expectedNetwork == null ? Network.GetNetworks() : new[] {expectedNetwork};
			foreach (var network in networks)
			{
				var versionBytes = network.GetVersionBytes(Base58Type.SCRIPT_ADDRESS, false);
				if (versionBytes != null && data.StartWith(versionBytes))
				{
					if (data.Length == versionBytes.Length + 20)
					{
						expectedNetwork = network;
						return str;
					}
				}
			}

			throw new FormatException("Invalid script key address.");
		}

		private static string ValidateCashAddr(string str, ref Network expectedNetwork)
		{
			var data = CashAddr.Decode(str);
			if (data.Type == BchAddr.CashType.P2SH)
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

			throw new FormatException("Invalid script key address.");
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
			return PayToScriptHashTemplate.Instance.GenerateScriptPubKey(Hash);
		}

		public BitcoinScriptAddress ToLegacy()
		{
			return _addr.Network.CreateBitcoinScriptAddress(CashFormat.Legacy, Hash);
		}

		public BitcoinScriptAddress ToCashAddr()
		{
			return _addr.Network.CreateBitcoinScriptAddress(CashFormat.Cashaddr, Hash);
		}
	}

	/// <summary>
	///     Base58 representation of a bitcoin address
	/// </summary>
	public abstract class BitcoinAddress : IDestination, IBitcoinString
	{
		public delegate BitcoinAddress AddressFactoryDelegate(string str, Network expectedNetwork);

		private static readonly AddressFactoryDelegate[] AddressFactories =
		{
			Create,
			CreateFromLegacy
		};

		private readonly string _str;

		private Script _scriptPubKey;

		protected internal BitcoinAddress(string str, Network network)
		{
			if (network == null)
			{
				throw new ArgumentNullException(nameof(network));
			}

			if (str == null)
			{
				throw new ArgumentNullException(nameof(str));
			}

			_str = str;
			Network = network;
		}

		public Network Network { get; }

		public Script ScriptPubKey
		{
			get
			{
				if (_scriptPubKey == null)
				{
					_scriptPubKey = GeneratePaymentScript();
				}

				return _scriptPubKey;
			}
		}

		/// <summary>
		///     Detect whether the input base58 is a pubkey hash or a script hash
		/// </summary>
		/// <param name="str">The string to parse</param>
		/// <param name="expectedNetwork">The expected network to which it belongs</param>
		/// <returns>A BitcoinAddress or BitcoinScriptAddress</returns>
		/// <exception cref="System.FormatException">Invalid format</exception>
		public static BitcoinAddress Create(string str, Network expectedNetwork)
		{
			if (str == null)
			{
				throw new ArgumentNullException(nameof(str));
			}

			return Network.Parse<BitcoinAddress>(str, expectedNetwork);
		}

		/// <summary>
		///     Creates a Bitcoin Cash address from legacy Bitcoin address.
		/// </summary>
		/// <param name="str">Legacy Bitcoin address</param>
		/// <param name="expectedNetwork">The expected network to which address belongs</param>
		/// <returns>A BitcoinAddress or BitcoinScriptAddress</returns>
		/// <exception cref="System.FormatException">Invalid format</exception>
		public static BitcoinAddress CreateFromLegacy(string str, Network expectedNetwork)
		{
			if (str == null)
			{
				throw new ArgumentNullException(nameof(str));
			}

			return Network.Parse<BitcoinAddress>(str, expectedNetwork);
		}

		/// <summary>
		///     Creates a Bitcoin Cash address from either Cash Addr or legacy Bitcoin address.
		/// </summary>
		/// <param name="str">Cash Addr or legacy Bitcoin address</param>
		/// <param name="expectedNetwork">The expected network to which address belongs</param>
		/// <returns>A BitcoinAddress or BitcoinScriptAddress</returns>
		/// <exception cref="System.FormatException">Invalid format</exception>
		public static BitcoinAddress CreateFromAny(string str, Network expectedNetwork)
		{
			if (str == null)
			{
				throw new ArgumentNullException(nameof(str));
			}

			foreach (var factory in AddressFactories)
			{
				try
				{
					return factory(str, expectedNetwork);
				}
				catch
				{
					// ignore
				}
			}

			throw new FormatException("Invalid format");
		}

		protected abstract Script GeneratePaymentScript();

		public BitcoinScriptAddress GetScriptAddress()
		{
			var bitcoinScriptAddress = this as BitcoinScriptAddress;
			if (bitcoinScriptAddress != null)
			{
				return bitcoinScriptAddress;
			}

			return new BitcoinScriptAddress(ScriptPubKey.Hash, Network);
		}

		public override string ToString()
		{
			return _str;
		}

		public override bool Equals(object obj)
		{
			var item = obj as BitcoinAddress;
			if (item == null)
			{
				return false;
			}

			return _str.Equals(item._str);
		}

		public static bool operator ==(BitcoinAddress a, BitcoinAddress b)
		{
			if (ReferenceEquals(a, b))
			{
				return true;
			}

			if ((object) a == null || (object) b == null)
			{
				return false;
			}

			return a._str == b._str;
		}

		public static bool operator !=(BitcoinAddress a, BitcoinAddress b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return _str.GetHashCode();
		}
	}
}