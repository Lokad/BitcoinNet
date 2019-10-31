using System;
using System.Linq;
using System.Text;
using BitcoinNet.BouncyCastle.Math;
using BitcoinNet.Crypto;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	public class Key : IBitcoinSerializable, IDestination
	{
		private const int KeySize = 32;

		private static readonly uint256 N =
			uint256.Parse("fffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364141");

		internal ECKey _ecKey;
		private PubKey _pubKey;
		private byte[] _vch = new byte[0];

		public Key()
			: this(true)
		{
		}

		public Key(bool fCompressedIn)
		{
			var data = new byte[KeySize];
			do
			{
				RandomUtils.GetBytes(data);
			} while (!Check(data));

			SetBytes(data, data.Length, fCompressedIn);
		}

		public Key(byte[] data, int count = -1, bool fCompressedIn = true)
		{
			if (count == -1)
			{
				count = data.Length;
			}

			if (count != KeySize)
			{
				throw new ArgumentException(paramName: nameof(data),
					message: $"The size of an EC key should be {KeySize}");
			}

			if (Check(data))
			{
				SetBytes(data, count, fCompressedIn);
			}
			else
			{
				throw new ArgumentException(paramName: nameof(data), message: "Invalid EC key");
			}
		}

		public bool IsCompressed { get; internal set; }

		public PubKey PubKey
		{
			get
			{
				if (_pubKey == null)
				{
					var key = new ECKey(_vch, true);
					_pubKey = key.GetPubKey(IsCompressed);
				}

				return _pubKey;
			}
		}

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _vch);
			if (!stream.Serializing)
			{
				_ecKey = new ECKey(_vch, true);
			}
		}

		// IDestination Members

		public Script ScriptPubKey => PubKey.Hash.ScriptPubKey;

		public static Key Parse(string wif, Network network = null)
		{
			return Network.Parse<BitcoinSecret>(wif, network).PrivateKey;
		}

		private void SetBytes(byte[] data, int count, bool fCompressedIn)
		{
			_vch = data.SafeSubArray(0, count);
			IsCompressed = fCompressedIn;
			_ecKey = new ECKey(_vch, true);
		}

		private static bool Check(byte[] vch)
		{
			var candidateKey = new uint256(vch.SafeSubArray(0, KeySize), false);
			return candidateKey > 0 && candidateKey < N;
		}

		public ECDSASignature Sign(uint256 hash)
		{
			return _ecKey.Sign(hash);
		}

		public string SignMessage(string message)
		{
			return SignMessage(Encoding.UTF8.GetBytes(message));
		}

		public string SignMessage(byte[] messageBytes)
		{
			var data = Utils.FormatMessageForSigning(messageBytes);
			var hash = Hashes.Hash256(data);
			return Convert.ToBase64String(SignCompact(hash));
		}

		public byte[] SignCompact(uint256 hash)
		{
			var sig = _ecKey.Sign(hash);
			// Now we have to work backwards to figure out the recId needed to recover the signature.
			var recId = -1;
			for (var i = 0; i < 4; i++)
			{
				var k = ECKey.RecoverFromSignature(i, sig, hash, IsCompressed);
				if (k != null && k.GetPubKey(IsCompressed).ToHex() == PubKey.ToHex())
				{
					recId = i;
					break;
				}
			}

			if (recId == -1)
			{
				throw new InvalidOperationException("Could not construct a recoverable key. This should never happen.");
			}

			var headerByte = recId + 27 + (IsCompressed ? 4 : 0);

			var sigData = new byte[65]; // 1 header + 32 bytes for R + 32 bytes for S

			sigData[0] = (byte) headerByte;

			Array.Copy(Utils.BigIntegerToBytes(sig.R, 32), 0, sigData, 1, 32);
			Array.Copy(Utils.BigIntegerToBytes(sig.S, 32), 0, sigData, 33, 32);
			return sigData;
		}

		public Key Derivate(byte[] cc, uint nChild, out byte[] ccChild)
		{
			byte[] l = null;
			if (nChild >> 31 == 0)
			{
				var pubKey = PubKey.ToBytes();
				l = Hashes.BIP32Hash(cc, nChild, pubKey[0], pubKey.SafeSubArray(1));
			}
			else
			{
				l = Hashes.BIP32Hash(cc, nChild, 0, this.ToBytes());
			}

			var ll = l.SafeSubArray(0, 32);
			var lr = l.SafeSubArray(32, 32);

			ccChild = lr;

			var parse256LL = new BigInteger(1, ll);
			var kPar = new BigInteger(1, _vch);
			var N = ECKey.Curve.N;

			if (parse256LL.CompareTo(N) >= 0)
			{
				throw new InvalidOperationException(
					"You won a prize ! this should happen very rarely. Take a screenshot, and roll the dice again.");
			}

			var key = parse256LL.Add(kPar).Mod(N);
			if (key == BigInteger.Zero)
			{
				throw new InvalidOperationException(
					"You won the big prize ! this has probability lower than 1 in 2^127. Take a screenshot, and roll the dice again.");
			}

			var keyBytes = key.ToByteArrayUnsigned();
			if (keyBytes.Length < 32)
			{
				keyBytes = new byte[32 - keyBytes.Length].Concat(keyBytes).ToArray();
			}

			return new Key(keyBytes);
		}

		public BitcoinSecret GetBitcoinSecret(Network network)
		{
			return new BitcoinSecret(this, network);
		}

		/// <summary>
		///     Same than GetBitcoinSecret
		/// </summary>
		/// <param name="network"></param>
		/// <returns></returns>
		public BitcoinSecret GetWif(Network network)
		{
			return new BitcoinSecret(this, network);
		}

		public string ToString(Network network)
		{
			return new BitcoinSecret(this, network).ToString();
		}

		public TransactionSignature Sign(uint256 hash, SigHash sigHash)
		{
			return new TransactionSignature(Sign(hash), sigHash);
		}

		public override bool Equals(object obj)
		{
			var item = obj as Key;
			if (item == null)
			{
				return false;
			}

			return PubKey.Equals(item.PubKey);
		}

		public static bool operator ==(Key a, Key b)
		{
			if (ReferenceEquals(a, b))
			{
				return true;
			}

			if ((object) a == null || (object) b == null)
			{
				return false;
			}

			return a.PubKey == b.PubKey;
		}

		public static bool operator !=(Key a, Key b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return PubKey.GetHashCode();
		}
	}
}