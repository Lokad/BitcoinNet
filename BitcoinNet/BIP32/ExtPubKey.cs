using System;
using System.Linq;
using BitcoinNet.Crypto;
using BitcoinNet.DataEncoders;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	/// <summary>
	///     A public HD key
	/// </summary>
	public class ExtPubKey : IBitcoinSerializable, IDestination
	{
		private const int FingerprintLength = 4;
		private const int ChainCodeLength = 32;

		private static readonly byte[] ValidPubKey =
			Encoders.Hex.DecodeData("0374ef3990e387b5a2992797f14c031a64efd80e5cb843d7c1d4a0274a9bc75e55");

		internal uint _nChild;
		internal byte _nDepth;

		internal PubKey _pubKey = new PubKey(ValidPubKey);
		internal byte[] _vchChainCode = new byte[ChainCodeLength];
		internal byte[] _vchFingerprint = new byte[FingerprintLength];

		internal ExtPubKey()
		{
		}

		public ExtPubKey(byte[] bytes)
		{
			if (bytes == null)
			{
				throw new ArgumentNullException(nameof(bytes));
			}

			this.ReadWrite(bytes);
		}

		public ExtPubKey(PubKey pubKey, byte[] chainCode, byte depth, byte[] fingerprint, uint child)
		{
			if (pubKey == null)
			{
				throw new ArgumentNullException(nameof(pubKey));
			}

			if (chainCode == null)
			{
				throw new ArgumentNullException(nameof(chainCode));
			}

			if (fingerprint == null)
			{
				throw new ArgumentNullException(nameof(fingerprint));
			}

			if (fingerprint.Length != FingerprintLength)
			{
				throw new ArgumentException($"The fingerprint must be {FingerprintLength} bytes.",
					nameof(fingerprint));
			}

			if (chainCode.Length != ChainCodeLength)
			{
				throw new ArgumentException($"The chain code must be {ChainCodeLength} bytes.",
					nameof(chainCode));
			}

			_pubKey = pubKey;
			_nDepth = depth;
			_nChild = child;
			Buffer.BlockCopy(fingerprint, 0, _vchFingerprint, 0, FingerprintLength);
			Buffer.BlockCopy(chainCode, 0, _vchChainCode, 0, ChainCodeLength);
		}

		public ExtPubKey(PubKey masterKey, byte[] chainCode)
		{
			if (masterKey == null)
			{
				throw new ArgumentNullException(nameof(masterKey));
			}

			if (chainCode == null)
			{
				throw new ArgumentNullException(nameof(chainCode));
			}

			if (chainCode.Length != ChainCodeLength)
			{
				throw new ArgumentException($"The chain code must be {ChainCodeLength} bytes.",
					nameof(chainCode));
			}

			_pubKey = masterKey;
			Buffer.BlockCopy(chainCode, 0, _vchChainCode, 0, ChainCodeLength);
		}

		public byte Depth => _nDepth;

		public uint Child => _nChild;

		public bool IsHardened => (_nChild & 0x80000000u) != 0;

		public PubKey PubKey => _pubKey;

		public byte[] ChainCode
		{
			get
			{
				var chainCodeCopy = new byte[ChainCodeLength];
				Buffer.BlockCopy(_vchChainCode, 0, chainCodeCopy, 0, ChainCodeLength);

				return chainCodeCopy;
			}
		}

		public byte[] Fingerprint => _vchFingerprint;


		private uint256 Hash => Hashes.Hash256(this.ToBytes());

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			using (stream.BigEndianScope())
			{
				stream.ReadWrite(ref _nDepth);
				stream.ReadWrite(ref _vchFingerprint);
				stream.ReadWrite(ref _nChild);
				stream.ReadWrite(ref _vchChainCode);
				stream.ReadWrite(ref _pubKey);
			}
		}

		// IDestination Members

		/// <summary>
		///     The P2PKH payment script
		/// </summary>
		public Script ScriptPubKey => PubKey.Hash.ScriptPubKey;

		public static ExtPubKey Parse(string wif, Network expectedNetwork = null)
		{
			return Network.Parse<BitcoinExtPubKey>(wif, expectedNetwork).ExtPubKey;
		}

		public bool IsChildOf(ExtPubKey parentKey)
		{
			if (Depth != parentKey.Depth + 1)
			{
				return false;
			}

			return parentKey.CalculateChildFingerprint().SequenceEqual(Fingerprint);
		}

		public bool IsParentOf(ExtPubKey childKey)
		{
			return childKey.IsChildOf(this);
		}

		public byte[] CalculateChildFingerprint()
		{
			return _pubKey.Hash.ToBytes().SafeSubArray(0, FingerprintLength);
		}

		public ExtPubKey Derive(uint index)
		{
			var result = new ExtPubKey
			{
				_nDepth = (byte) (_nDepth + 1),
				_vchFingerprint = CalculateChildFingerprint(),
				_nChild = index
			};
			result._pubKey = _pubKey.Derivate(_vchChainCode, index, out result._vchChainCode);
			return result;
		}

		public ExtPubKey Derive(KeyPath derivation)
		{
			var result = this;
			return derivation.Indexes.Aggregate(result, (current, index) => current.Derive(index));
		}

		public ExtPubKey Derive(int index, bool hardened)
		{
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(index), "the index can't be negative");
			}

			var realIndex = (uint) index;
			realIndex = hardened ? realIndex | 0x80000000u : realIndex;
			return Derive(realIndex);
		}

		public BitcoinExtPubKey GetWif(Network network)
		{
			return new BitcoinExtPubKey(this, network);
		}

		public override bool Equals(object obj)
		{
			var item = obj as ExtPubKey;
			if (item == null)
			{
				return false;
			}

			return Hash.Equals(item.Hash);
		}

		public static bool operator ==(ExtPubKey a, ExtPubKey b)
		{
			if (ReferenceEquals(a, b))
			{
				return true;
			}

			if ((object) a == null || (object) b == null)
			{
				return false;
			}

			return a.Hash == b.Hash;
		}

		public static bool operator !=(ExtPubKey a, ExtPubKey b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return Hash.GetHashCode();
		}

		public string ToString(Network network)
		{
			return new BitcoinExtPubKey(this, network).ToString();
		}
	}
}