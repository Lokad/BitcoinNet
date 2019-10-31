using System;
using System.Collections.Generic;
using System.Linq;
using BitcoinNet;
using BitcoinNet.DataEncoders;

namespace BitcoinNet
{
	public class BitcoinCash : INetworkSet
	{
		private BitcoinCash()
		{
		}

		public static BitcoinCash Instance { get; } = new BitcoinCash();

		public Network Mainnet => Network.Main;

		public Network Testnet => Network.TestNet;

		public Network Regtest => Network.RegTest;

		public string CryptoCode => "BCH";

		public Network GetNetwork(NetworkType networkType)
		{
			switch (networkType)
			{
				case NetworkType.Mainnet:
					return Mainnet;
				case NetworkType.Testnet:
					return Testnet;
				case NetworkType.Regtest:
					return Regtest;
			}

			throw new NotSupportedException(networkType.ToString());
		}
	}
}

public enum CashFormat
{
	Legacy,
	Cashaddr
}

namespace BCashAddr
{
	// https://github.com/bitcoincashjs/bchaddrjs
	internal static class BchAddr
	{
		public enum CashType
		{
			P2PKH,
			P2SH
		}

		/// <summary>
		///     Detects a given address format without validating it.
		/// </summary>
		/// <param name="str">The address to be detected</param>
		/// <param name="format">Detected format</param>
		/// <returns>True when detection is successful, false otherwise.</returns>
		public static bool TryDetectFormat(string str, out CashFormat format)
		{
			if (!string.IsNullOrEmpty(str))
			{
				// According to Bitcoin Cash documentation, the prefix separator always exists. So, we use it as our primary guess factor.
				// Reference: https://github.com/bitcoincashorg/bitcoincash.org/blob/master/spec/cashaddr.md

				var separatorIndex = str.IndexOf(':');
				if (separatorIndex >= 0)
				{
					if (!Base32.ContainsInvalidCharacter(str, separatorIndex + 1))
					{
						format = CashFormat.Cashaddr;
						return true;
					}
				}
				else
				{
					if (!Base58Encoder.ContainsInvalidCharacter(str))
					{
						format = CashFormat.Legacy;
						return true;
					}
				}
			}

			format = default;
			return false;
		}

		/// <summary>
		///     Detects a given address format without validating it.
		/// </summary>
		/// <param name="str">The address to be detected</param>
		/// <returns>Detected format</returns>
		public static CashFormat DetectFormat(string str)
		{
			if (string.IsNullOrEmpty(str))
			{
				throw new ArgumentException(nameof(str));
			}

			if (TryDetectFormat(str, out var result))
			{
				return result;
			}

			throw new FormatException();
		}

		/// <summary>
		///     Encodes the given decoded address into cashaddr format
		/// </summary>
		/// <param name="decoded"></param>
		/// <returns></returns>
		public static string EncodeAsCashaddr(BchAddrData decoded)
		{
			return CashAddr.Encode(decoded.Prefix, decoded.Type, decoded.Hash);
		}

		/// <summary>
		///     Encodes the given decoded address into cashaddr format without a prefix
		/// </summary>
		/// <param name="decoded"></param>
		/// <returns></returns>
		public static string EncodeAsCashaddrNoPrefix(BchAddrData decoded)
		{
			var address = EncodeAsCashaddr(decoded);
			if (address.IndexOf(":", StringComparison.InvariantCulture) != -1)
			{
				return address.Split(':')[1];
			}

			throw new Validation.ValidationError("Invalid BchAddrData");
		}

		/// <summary>
		///     Detects what is the given address' format
		/// </summary>
		/// <param name="address"></param>
		/// <returns></returns>
		public static BchAddrData DecodeAddress(string address, string prefix, Network network)
		{
			try
			{
				return DecodeCashAddress(address, prefix, network);
			}
			catch
			{
			}

			throw new Validation.ValidationError($"Invalid address {address}");
		}

		/// <summary>
		///     Attempts to decode the given address assuming it is a cashaddr address
		/// </summary>
		/// <param name="address">A valid Bitcoin Cash address in any format</param>
		/// <returns></returns>
		private static BchAddrData DecodeCashAddress(string address, string prefix, Network network)
		{
			//if(address.IndexOf(":") != -1)
			//{
			//	return DecodeCashAddressWithPrefix(address);
			//}
			//else
			//{
			try
			{
				var result = DecodeCashAddressWithPrefix(address, network);
				return result;
			}
			catch
			{
			}

			//}
			throw new Validation.ValidationError($"Invalid address {address}");
		}

		/// <summary>
		///     Attempts to decode the given address assuming it is a cashaddr address with explicit prefix
		/// </summary>
		/// <param name="address">A valid Bitcoin Cash address in any format</param>
		/// <returns></returns>
		private static BchAddrData DecodeCashAddressWithPrefix(string address, Network network)
		{
			var decoded = CashAddr.Decode(address);
			return BchAddrData.Create(CashFormat.Cashaddr, network, decoded.Type, decoded.Hash);
		}

		public class BchAddrData
		{
			public CashFormat Format { get; set; }

			public Network Network { get; set; }

			public CashType Type { get; set; }

			public byte[] Hash { get; set; }

			public string Prefix { get; internal set; }

			public string GetHash()
			{
				if (Hash == null)
				{
					return null;
				}

				return Encoders.Hex.EncodeData(Hash);
			}

			public static BchAddrData Create(CashFormat format, Network network, CashType type, byte[] hash)
			{
				return new BchAddrData
				{
					Format = format,
					Network = network,
					Type = type,
					Hash = hash,
					Prefix = network?.Prefix
				};
			}

			public override string ToString()
			{
				switch (Format)
				{
					case CashFormat.Legacy:
						var base58Type = Type == CashType.P2PKH ? Base58Type.PUBKEY_ADDRESS : Base58Type.SCRIPT_ADDRESS;
						var data = Network.GetVersionBytes(base58Type, false).Concat(Hash).ToArray();
						return Encoders.Base58Check.EncodeData(data);

					case CashFormat.Cashaddr:
						return EncodeAsCashaddr(this);

					default:
						return GetHash();
				}
			}
		}
	}

	// https://github.com/bitcoincashjs/cashaddrjs
	internal static class CashAddr
	{
		/// <summary>
		///     Encodes a hash from a given type into a Bitcoin Cash address with the given prefix
		/// </summary>
		/// <param name="prefix">Network prefix. E.g.: 'bitcoincash'</param>
		/// <param name="type">Type of address to generate.</param>
		/// <param name="hash">Hash to encode represented as an array of 8-bit integers</param>
		/// <returns></returns>
		public static string Encode(string prefix, BchAddr.CashType type, byte[] hash)
		{
			var prefixData = Concat(PrefixToByte5Array(prefix), new byte[1]);
			var versionByte = GetTypeBits(type) + GetHashSizeBits(hash);
			var payloadData = ToByte5Array(Concat(new byte[1] {(byte) versionByte}, hash));
			var checksumData = Concat(Concat(prefixData, payloadData), new byte[8]);
			var payload = Concat(payloadData, ChecksumToByte5Array(Polymod(checksumData)));
			return prefix + ':' + Base32.Encode(payload);
		}

		/// <summary>
		///     Decodes the given address into its constituting prefix, type and hash
		/// </summary>
		/// <param name="address">Address to decode. E.g.: 'bitcoincash:qpm2qsznhks23z7629mms6s4cwef74vcwvy22gdx6a'</param>
		/// <returns>DecodeData</returns>
		public static CashAddrData Decode(string address)
		{
			var pieces = address.ToLower().Split(':');
			Validation.Validate(pieces.Length == 2, $"Missing prefix: {address}");
			var prefix = pieces[0];
			var payload = Base32.Decode(pieces[1]);
			Validation.Validate(ValidChecksum(prefix, payload), $"Invalid checksum: {address}");
			var data = payload.Take(payload.Length - 8).ToArray();
			var payloadData = FromByte5Array(data);
			var versionByte = payloadData[0];
			var hash = payloadData.Skip(1).ToArray();
			Validation.Validate(GetHashSize(versionByte) == hash.Length * 8, $"Invalid hash size: {address}");
			var type = GetType(versionByte);
			return new CashAddrData
			{
				Prefix = prefix,
				Type = type,
				Hash = hash
			};
		}

		/// <summary>
		///     Retrieves the address type from its bit representation within the version byte
		/// </summary>
		/// <param name="versionByte"></param>
		/// <returns></returns>
		public static BchAddr.CashType GetType(byte versionByte)
		{
			switch (versionByte & 120)
			{
				case 0:
					return BchAddr.CashType.P2PKH;
				case 8:
					return BchAddr.CashType.P2SH;
				default:
					throw new Validation.ValidationError($"Invalid address type in version byte: {versionByte}");
			}
		}

		/// <summary>
		///     Verify that the payload has not been corrupted by checking that the checksum is valid
		/// </summary>
		/// <param name="prefix"></param>
		/// <param name="payload"></param>
		/// <returns></returns>
		public static bool ValidChecksum(string prefix, byte[] payload)
		{
			var prefixData = Concat(PrefixToByte5Array(prefix), new byte[1]);
			var checksumData = Concat(prefixData, payload);
			return Polymod(checksumData).Equals(0);
		}


		/// <summary>
		///     Returns the concatenation a and b
		/// </summary>
		public static byte[] Concat(byte[] a, byte[] b)
		{
			return Enumerable.Concat(a, b).ToArray();
		}

		/// <summary>
		///     Returns an array representation of the given checksum to be encoded within the address' payload
		/// </summary>
		/// <param name="checksum"></param>
		/// <returns></returns>
		public static byte[] ChecksumToByte5Array(long checksum)
		{
			var result = new byte[8];
			for (var i = 0; i < 8; ++i)
			{
				result[7 - i] = (byte) (checksum & 31);
				checksum = checksum >> 5;
			}

			return result;
		}

		/// <summary>
		///     Computes a checksum from the given input data as specified for the CashAddr
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static long Polymod(byte[] data)
		{
			var generator = new[] {0x98f2bc8e61, 0x79b76d99e2, 0xf33e5fb3c4, 0xae2eabe2a8, 0x1e4f43e470};
			long checksum = 1;
			for (var i = 0; i < data.Length; ++i)
			{
				var value = data[i];
				var topBits = checksum >> 35;
				checksum = ((checksum & 0x07ffffffff) << 5) ^ value;
				for (var j = 0; j < generator.Length; ++j)
				{
					if (((topBits >> j) & 1).Equals(1))
					{
						checksum = checksum ^ generator[j];
					}
				}
			}

			return checksum ^ 1;
		}

		/// <summary>
		///     Derives an array from the given prefix to be used in the computation of the address checksum
		/// </summary>
		/// <param name="prefix">Network prefix. E.g.: 'bitcoincash'</param>
		/// <returns></returns>
		public static byte[] PrefixToByte5Array(string prefix)
		{
			var result = new byte[prefix.Length];
			var i = 0;
			foreach (var c in prefix)
			{
				result[i++] = (byte) (c & 31);
			}

			return result;
		}

		/// <summary>
		///     Returns true if, and only if, the given string contains either uppercase or lowercase letters, but not both
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static bool HasSingleCase(string str)
		{
			return str == str.ToLower() || str == str.ToUpper();
		}

		/// <summary>
		///     Returns the bit representation of the length in bits of the given hash within the version byte
		/// </summary>
		/// <param name="hash">Hash to encode represented as an array of 8-bit integers</param>
		/// <returns></returns>
		public static byte GetHashSizeBits(byte[] hash)
		{
			switch (hash.Length * 8)
			{
				case 160:
					return 0;
				case 192:
					return 1;
				case 224:
					return 2;
				case 256:
					return 3;
				case 320:
					return 4;
				case 384:
					return 5;
				case 448:
					return 6;
				case 512:
					return 7;
				default:
					throw new Validation.ValidationError($"Invalid hash size: {hash.Length}");
			}
		}

		/// <summary>
		///     Retrieves the the length in bits of the encoded hash from its bit representation within the version byte
		/// </summary>
		/// <param name="versionByte"></param>
		/// <returns></returns>
		public static int GetHashSize(byte versionByte)
		{
			switch (versionByte & 7)
			{
				case 0:
					return 160;
				case 1:
					return 192;
				case 2:
					return 224;
				case 3:
					return 256;
				case 4:
					return 320;
				case 5:
					return 384;
				case 6:
					return 448;
				case 7:
					return 512;
				default:
					throw new Validation.ValidationError($"Invalid versionByte: {versionByte}");
			}
		}

		/// <summary>
		///     Returns the bit representation of the given type within the version byte
		/// </summary>
		/// <param name="type">Address type. Either 'P2PKH' or 'P2SH'</param>
		/// <returns></returns>
		public static byte GetTypeBits(BchAddr.CashType type)
		{
			switch (type)
			{
				case BchAddr.CashType.P2PKH:
					return 0;
				case BchAddr.CashType.P2SH:
					return 8;
				default:
					throw new Validation.ValidationError($"Invalid type: {type}");
			}
		}

		/// <summary>
		///     Converts an array of 8-bit integers into an array of 5-bit integers, right-padding with zeroes if necessary
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static byte[] ToByte5Array(byte[] data)
		{
			return ConvertBits.Convert(data, 8, 5);
		}

		/// <summary>
		///     Converts an array of 5-bit integers back into an array of 8-bit integers
		///     removing extra zeroes left from padding if necessary.
		///     Throws a ValidationError if input is not a zero-padded array of 8-bit integers
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static byte[] FromByte5Array(byte[] data)
		{
			return ConvertBits.Convert(data, 5, 8, true);
		}

		public class CashAddrData
		{
			public string Prefix { get; set; }

			public BchAddr.CashType Type { get; set; }

			public byte[] Hash { get; set; }
		}
	}

	internal static class ConvertBits
	{
		/// <summary>
		///     Converts an array of integers made up of 'from' bits into an
		///     array of integers made up of 'to' bits.The output array is
		///     zero-padded if necessary, unless strict mode is true.
		/// </summary>
		/// <param name="data">data Array of integers made up of 'from' bits</param>
		/// <param name="from">from Length in bits of elements in the input array</param>
		/// <param name="to">to Length in bits of elements in the output array</param>
		/// <param name="strictMode">strictMode Require the conversion to be completed without padding</param>
		/// <returns></returns>
		public static byte[] Convert(byte[] data, int from, int to, bool strictMode = false)
		{
			Validation.Validate(from > 0, "Invald 'from' parameter");
			Validation.Validate(to > 0, "Invald 'to' parameter");
			Validation.Validate(data.Length > 0, "Invald data");
			var d = data.Length * from / (double) to;
			var length = strictMode ? (int) Math.Floor(d) : (int) Math.Ceiling(d);
			var mask = (1 << to) - 1;
			var result = new byte[length];
			var index = 0;
			var accumulator = 0;
			var bits = 0;
			for (var i = 0; i < data.Length; ++i)
			{
				var value = data[i];
				Validation.Validate(0 <= value && value >> from == 0, $"Invalid value: {value}");
				accumulator = (accumulator << from) | value;
				bits += from;
				while (bits >= to)
				{
					bits -= to;
					result[index] = (byte) ((accumulator >> bits) & mask);
					++index;
				}
			}

			if (!strictMode)
			{
				if (bits > 0)
				{
					result[index] = (byte) ((accumulator << (to - bits)) & mask);
					++index;
				}
			}
			else
			{
				Validation.Validate(
					bits < from && ((accumulator << (to - bits)) & mask) == 0,
					$"Input cannot be converted to {to} bits without padding, but strict mode was used"
				);
			}

			return result;
		}
	}

	internal static class Base32
	{
		private static readonly char[] Digits;
		private static readonly Dictionary<char, int> CharMap = new Dictionary<char, int>();

		static Base32()
		{
			Digits = "qpzry9x8gf2tvdw0s3jn54khce6mua7l".ToCharArray();
			for (var i = 0; i < Digits.Length; i++)
			{
				CharMap[Digits[i]] = i;
			}
		}

		/// <summary>
		///     Decodes the given base32-encoded string into an array of 5-bit integers
		/// </summary>
		/// <param name="encoded"></param>
		/// <returns></returns>
		public static byte[] Decode(string encoded)
		{
			if (encoded.Length == 0)
			{
				throw new CashaddrBase32EncoderException("Invalid encoded string");
			}

			var result = new byte[encoded.Length];
			var next = 0;
			foreach (var c in encoded)
			{
				if (!CharMap.ContainsKey(c))
				{
					throw new CashaddrBase32EncoderException($"Invalid character: {c}");
				}

				result[next++] = (byte) CharMap[c];
			}

			return result;
		}

		/// <summary>
		///     Encodes the given array of 5-bit integers as a base32-encoded string
		/// </summary>
		/// <param name="data">data Array of integers between 0 and 31 inclusive</param>
		/// <returns></returns>
		public static string Encode(byte[] data)
		{
			if (data.Length == 0)
			{
				throw new CashaddrBase32EncoderException("Invalid data");
			}

			var base32 = string.Empty;
			for (var i = 0; i < data.Length; ++i)
			{
				var value = data[i];
				if (0 <= value && value < 32)
				{
					base32 += Digits[value];
				}
				else
				{
					throw new CashaddrBase32EncoderException($"Invalid value: {value}");
				}
			}

			return base32;
		}

		public static bool ContainsInvalidCharacter(string s, int offset, int count)
		{
			if (s != null)
			{
				for (var i = 0; i < count; ++i)
				{
					var c = s[offset + i];
					if (!Digits.Contains(c))
					{
						return true;
					}
				}
			}

			return false;
		}

		public static bool ContainsInvalidCharacter(string s, int offset)
		{
			if (s == null)
			{
				return false;
			}

			return ContainsInvalidCharacter(s, offset, s.Length - offset);
		}

		public static bool ContainsInvalidCharacter(string s)
		{
			if (s == null)
			{
				return false;
			}

			return ContainsInvalidCharacter(s, 0, s.Length);
		}

		private class CashaddrBase32EncoderException : Exception
		{
			public CashaddrBase32EncoderException(string message) : base(message)
			{
			}
		}
	}

	internal static class Validation
	{
		public static void Validate(bool condition, string message)
		{
			if (!condition)
			{
				throw new ValidationError(message);
			}
		}

		internal class ValidationError : Exception
		{
			public ValidationError(string message) : base(message)
			{
			}
		}
	}
}