using System;
using System.Linq;
using BitcoinNet.BouncyCastle.Math;
using BitcoinNet.Crypto;
using BitcoinNet.DataEncoders;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	/// <summary>
	///     A private Hierarchical Deterministic key
	/// </summary>
	public class ExtKey : IBitcoinSerializable, IDestination, ISecret
	{
		private const int FingerprintLength = 4;
		private const int ChainCodeLength = 32;

		private static readonly byte[] HashKey = Encoders.ASCII.DecodeData("Bitcoin seed");

		private Key _key;
		private uint _nChild;
		private byte _nDepth;
		private byte[] _vchChainCode = new byte[ChainCodeLength];
		private byte[] _vchFingerprint = new byte[FingerprintLength];

		/// <summary>
		///     Constructor. Reconstructs an extended key from the Base58 representations of
		///     the public key and corresponding private key.
		/// </summary>
		public ExtKey(BitcoinExtPubKey extPubKey, BitcoinSecret key)
			: this(extPubKey.ExtPubKey, key.PrivateKey)
		{
		}

		/// <summary>
		///     Constructor. Creates an extended key from the public key and corresponding private key.
		/// </summary>
		/// <remarks>
		///     <para>
		///         The ExtPubKey has the relevant values for child number, depth, chain code, and fingerprint.
		///     </para>
		/// </remarks>
		public ExtKey(ExtPubKey extPubKey, Key privateKey)
		{
			if (extPubKey == null)
			{
				throw new ArgumentNullException(nameof(extPubKey));
			}

			if (privateKey == null)
			{
				throw new ArgumentNullException(nameof(privateKey));
			}

			_nChild = extPubKey._nChild;
			_nDepth = extPubKey._nDepth;
			_vchChainCode = extPubKey._vchChainCode;
			_vchFingerprint = extPubKey._vchFingerprint;
			_key = privateKey;
		}

		/// <summary>
		///     Constructor. Creates an extended key from the private key, and specified values for
		///     chain code, depth, fingerprint, and child number.
		/// </summary>
		public ExtKey(Key key, byte[] chainCode, byte depth, byte[] fingerprint, uint child)
		{
			if (key == null)
			{
				throw new ArgumentNullException(nameof(key));
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

			_key = key;
			_nDepth = depth;
			_nChild = child;
			Buffer.BlockCopy(fingerprint, 0, _vchFingerprint, 0, FingerprintLength);
			Buffer.BlockCopy(chainCode, 0, _vchChainCode, 0, ChainCodeLength);
		}

		/// <summary>
		///     Constructor. Creates an extended key from the private key, with the specified value
		///     for chain code. Depth, fingerprint, and child number, will have their default values.
		/// </summary>
		public ExtKey(Key masterKey, byte[] chainCode)
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

			_key = masterKey;
			Buffer.BlockCopy(chainCode, 0, _vchChainCode, 0, ChainCodeLength);
		}

		/// <summary>
		///     Constructor. Creates a new extended key with a random 64 byte seed.
		/// </summary>
		public ExtKey()
		{
			var seed = RandomUtils.GetBytes(64);
			SetMaster(seed);
		}

		/// <summary>
		///     Constructor. Creates a new extended key from the specified seed bytes, from the given hex string.
		/// </summary>
		public ExtKey(string seedHex)
		{
			SetMaster(Encoders.Hex.DecodeData(seedHex));
		}

		/// <summary>
		///     Constructor. Creates a new extended key from the specified seed bytes.
		/// </summary>
		public ExtKey(byte[] seed)
		{
			SetMaster(seed.ToArray());
		}

		/// <summary>
		///     Gets the depth of this extended key from the root key.
		/// </summary>
		public byte Depth => _nDepth;

		/// <summary>
		///     Gets the child number of this key (in reference to the parent).
		/// </summary>
		public uint Child => _nChild;

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

		/// <summary>
		///     Gets whether or not this extended key is a hardened child.
		/// </summary>
		public bool IsHardened => (_nChild & 0x80000000u) != 0;

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			using (stream.BigEndianScope())
			{
				stream.ReadWrite(ref _nDepth);
				stream.ReadWrite(ref _vchFingerprint);
				stream.ReadWrite(ref _nChild);
				stream.ReadWrite(ref _vchChainCode);
				byte b = 0;
				stream.ReadWrite(ref b);
				stream.ReadWrite(ref _key);
			}
		}

		// IDestination Members

		/// <summary>
		///     Gets the script of the hash of the public key corresponding to the private key.
		/// </summary>
		public Script ScriptPubKey => PrivateKey.PubKey.Hash.ScriptPubKey;

		/// <summary>
		///     Get the private key of this extended key.
		/// </summary>
		public Key PrivateKey => _key;

		/// <summary>
		///     Parses the Base58 data (checking the network if specified), checks it represents the
		///     correct type of item, and then returns the corresponding ExtKey.
		/// </summary>
		public static ExtKey Parse(string wif, Network expectedNetwork = null)
		{
			return Network.Parse<BitcoinExtKey>(wif, expectedNetwork).ExtKey;
		}

		private void SetMaster(byte[] seed)
		{
			var hashMac = Hashes.HMACSHA512(HashKey, seed);
			_key = new Key(hashMac.SafeSubArray(0, 32));

			Buffer.BlockCopy(hashMac, 32, _vchChainCode, 0, ChainCodeLength);
		}

		/// <summary>
		///     Create the public key from this key.
		/// </summary>
		public ExtPubKey Neuter()
		{
			var ret = new ExtPubKey
			{
				_nDepth = _nDepth,
				_vchFingerprint = _vchFingerprint.ToArray(),
				_nChild = _nChild,
				_pubKey = _key.PubKey,
				_vchChainCode = _vchChainCode.ToArray()
			};
			return ret;
		}

		public bool IsChildOf(ExtKey parentKey)
		{
			if (Depth != parentKey.Depth + 1)
			{
				return false;
			}

			return parentKey.CalculateChildFingerprint().SequenceEqual(Fingerprint);
		}

		public bool IsParentOf(ExtKey childKey)
		{
			return childKey.IsChildOf(this);
		}

		private byte[] CalculateChildFingerprint()
		{
			return _key.PubKey.Hash.ToBytes().SafeSubArray(0, FingerprintLength);
		}

		/// <summary>
		///     Derives a new extended key in the hierarchy as the given child number.
		/// </summary>
		public ExtKey Derive(uint index)
		{
			var result = new ExtKey
			{
				_nDepth = (byte) (_nDepth + 1),
				_vchFingerprint = CalculateChildFingerprint(),
				_nChild = index
			};
			result._key = _key.Derivate(_vchChainCode, index, out result._vchChainCode);
			return result;
		}

		/// <summary>
		///     Derives a new extended key in the hierarchy as the given child number,
		///     setting the high bit if hardened is specified.
		/// </summary>
		public ExtKey Derive(int index, bool hardened)
		{
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(index), "the index can't be negative");
			}

			var realIndex = (uint) index;
			realIndex = hardened ? realIndex | 0x80000000u : realIndex;
			return Derive(realIndex);
		}

		/// <summary>
		///     Derives a new extended key in the hierarchy at the given path below the current key,
		///     by deriving the specified child at each step.
		/// </summary>
		public ExtKey Derive(KeyPath derivation)
		{
			var result = this;
			return derivation.Indexes.Aggregate(result, (current, index) => current.Derive(index));
		}

		/// <summary>
		///     Converts the extended key to the base58 representation, within the specified network.
		/// </summary>
		public BitcoinExtKey GetWif(Network network)
		{
			return new BitcoinExtKey(this, network);
		}

		/// <summary>
		///     Converts the extended key to the base58 representation, as a string, within the specified network.
		/// </summary>
		public string ToString(Network network)
		{
			return new BitcoinExtKey(this, network).ToString();
		}

		/// <summary>
		///     Recreates the private key of the parent from the private key of the child
		///     combinated with the public key of the parent (hardened children cannot be
		///     used to recreate the parent).
		/// </summary>
		public ExtKey GetParentExtKey(ExtPubKey parent)
		{
			if (parent == null)
			{
				throw new ArgumentNullException(nameof(parent));
			}

			if (Depth == 0)
			{
				throw new InvalidOperationException("This ExtKey is the root key of the HD tree");
			}

			if (IsHardened)
			{
				throw new InvalidOperationException("This private key is hardened, so you can't get its parent");
			}

			var expectedFingerPrint = parent.CalculateChildFingerprint();
			if (parent.Depth != Depth - 1 || !expectedFingerPrint.SequenceEqual(_vchFingerprint))
			{
				throw new ArgumentException("The parent ExtPubKey is not the immediate parent of this ExtKey",
					nameof(parent));
			}

			byte[] l = null;
			var ll = new byte[32];
			var lr = new byte[32];

			var pubKey = parent.PubKey.ToBytes();
			l = Hashes.BIP32Hash(parent._vchChainCode, _nChild, pubKey[0], pubKey.SafeSubArray(1));
			Array.Copy(l, ll, 32);
			Array.Copy(l, 32, lr, 0, 32);
			var ccChild = lr;

			var parse256LL = new BigInteger(1, ll);
			var N = ECKey.Curve.N;

			if (!ccChild.SequenceEqual(_vchChainCode))
			{
				throw new InvalidOperationException(
					"The derived chain code of the parent is not equal to this child chain code");
			}

			var keyBytes = PrivateKey.ToBytes();
			var key = new BigInteger(1, keyBytes);

			var kPar = key.Add(parse256LL.Negate()).Mod(N);
			var keyParentBytes = kPar.ToByteArrayUnsigned();
			if (keyParentBytes.Length < 32)
			{
				keyParentBytes = new byte[32 - keyParentBytes.Length].Concat(keyParentBytes).ToArray();
			}

			var parentExtKey = new ExtKey
			{
				_vchChainCode = parent._vchChainCode,
				_nDepth = parent.Depth,
				_vchFingerprint = parent.Fingerprint,
				_nChild = parent._nChild,
				_key = new Key(keyParentBytes)
			};
			return parentExtKey;
		}
	}
}