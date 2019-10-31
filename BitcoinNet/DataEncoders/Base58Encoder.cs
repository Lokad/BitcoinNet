using System;
using System.Linq;
using System.Text;
using BitcoinNet.BouncyCastle.Math;
using BitcoinNet.Crypto;

namespace BitcoinNet.DataEncoders
{
	public class Base58CheckEncoder : Base58Encoder
	{
		private static readonly Base58Encoder InternalEncoder = new Base58Encoder();

		public override string EncodeData(byte[] data, int offset, int count)
		{
			var toEncode = new byte[count + 4];
			Buffer.BlockCopy(data, offset, toEncode, 0, count);

			var hash = Hashes.Hash256(data, offset, count).ToBytes();
			Buffer.BlockCopy(hash, 0, toEncode, count, 4);

			return InternalEncoder.EncodeData(toEncode, 0, toEncode.Length);
		}

		public override byte[] DecodeData(string encoded)
		{
			var vchRet = InternalEncoder.DecodeData(encoded);
			if (vchRet.Length < 4)
			{
				Array.Clear(vchRet, 0, vchRet.Length);
				throw new FormatException("Invalid checked base 58 string");
			}

			var calculatedHash = Hashes.Hash256(vchRet, 0, vchRet.Length - 4).ToBytes().SafeSubArray(0, 4);
			var expectedHash = vchRet.SafeSubArray(vchRet.Length - 4, 4);

			if (!Utils.ArrayEqual(calculatedHash, expectedHash))
			{
				Array.Clear(vchRet, 0, vchRet.Length);
				throw new FormatException("Invalid hash of the base 58 string");
			}

			vchRet = vchRet.SafeSubArray(0, vchRet.Length - 4);
			return vchRet;
		}
	}

	public class Base58Encoder : DataEncoder
	{
		private const string PszBase58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
		private static readonly BigInteger Bn58 = BigInteger.ValueOf(58);

		private static readonly char[] PszBase58Chars =
			"123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz".ToCharArray();

		public static bool ContainsInvalidCharacter(string s, int offset, int count)
		{
			if (s != null)
			{
				for (var i = 0; i < count; ++i)
				{
					var c = s[offset + i];
					if (!PszBase58Chars.Contains(c))
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

		public override string EncodeData(byte[] data, int offset, int count)
		{
			var bn0 = BigInteger.Zero;

			// Convert big endian data to little endian
			// Extra zero at the end make sure bignum will interpret as a positive number
			var vchTmp = data.SafeSubArray(offset, count);

			// Convert little endian data to bignum
			var bn = new BigInteger(1, vchTmp);

			// Convert bignum to std::string
			var builder = new StringBuilder();
			// Expected size increase from base58 conversion is approximately 137%
			// use 138% to be safe

			while (bn.CompareTo(bn0) > 0)
			{
				var r = bn.DivideAndRemainder(Bn58);
				var dv = r[0];
				var rem = r[1];
				bn = dv;
				var c = rem.IntValue;
				builder.Append(PszBase58[c]);
			}

			// Leading zeroes encoded as base58 zeros
			for (var i = offset; i < offset + count && data[i] == 0; i++)
			{
				builder.Append(PszBase58[0]);
			}

			// Convert little endian std::string to big endian
			var chars = builder.ToString().ToCharArray();
			Array.Reverse(chars);
			var str = new string(chars); //keep that way to be portable
			return str;
		}

		public override byte[] DecodeData(string encoded)
		{
			if (encoded == null)
			{
				throw new ArgumentNullException(nameof(encoded));
			}

			var result = new byte[0];
			if (encoded.Length == 0)
			{
				return result;
			}

			var bn = BigInteger.Zero;
			var i = 0;
			while (IsSpace(encoded[i]))
			{
				i++;
				if (i >= encoded.Length)
				{
					return result;
				}
			}

			for (var y = i; y < encoded.Length; y++)
			{
				var p1 = PszBase58.IndexOf(encoded[y]);
				if (p1 == -1)
				{
					while (IsSpace(encoded[y]))
					{
						y++;
						if (y >= encoded.Length)
						{
							break;
						}
					}

					if (y != encoded.Length)
					{
						throw new FormatException("Invalid base 58 string");
					}

					break;
				}

				var bnChar = BigInteger.ValueOf(p1);
				bn = bn.Multiply(Bn58);
				bn = bn.Add(bnChar);
			}

			// Get bignum as little endian data
			var vchTmp = bn.ToByteArrayUnsigned();
			Array.Reverse(vchTmp);
			if (vchTmp.All(b => b == 0))
			{
				vchTmp = new byte[0];
			}

			// Trim off sign byte if present
			if (vchTmp.Length >= 2 && vchTmp[vchTmp.Length - 1] == 0 && vchTmp[vchTmp.Length - 2] >= 0x80)
			{
				vchTmp = vchTmp.SafeSubArray(0, vchTmp.Length - 1);
			}

			// Restore leading zeros
			var nLeadingZeros = 0;
			for (var y = i; y < encoded.Length && encoded[y] == PszBase58[0]; y++)
			{
				nLeadingZeros++;
			}


			result = new byte[nLeadingZeros + vchTmp.Length];
			Array.Copy(vchTmp.Reverse().ToArray(), 0, result, nLeadingZeros, vchTmp.Length);
			return result;
		}
	}
}