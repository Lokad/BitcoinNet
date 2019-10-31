using System;
using BitcoinNet.Crypto;

namespace BitcoinNet
{
	[Flags]
	public enum BloomFlags : byte
	{
		UPDATE_NONE = 0,
		UPDATE_ALL = 1,

		// Only adds outpoints to the filter if the output is a pay-to-pubkey/pay-to-multisig script
		UPDATE_P2PUBKEY_ONLY = 2,
		UPDATE_MASK = 3
	}

	/// <summary>
	///     Used by SPV client, represent the set of interesting addresses tracked by SPV client with plausible deniability
	/// </summary>
	public class BloomFilter : IBitcoinSerializable
	{
		// 20,000 items with fp rate < 0.1% or 10,000 items and <0.0001%
		private const uint MaxBloomFilterSize = 36000; // bytes
		private const uint MaxHashFuncs = 50;
		private const decimal Ln2Squared = 0.4804530139182014246671025263266649717305529515945455M;
		private const decimal LN2 = 0.6931471805599453094172321214581765680755001343602552M;
		private readonly bool _isFull = false;
		private bool _isEmpty;
		private byte _nFlags;
		private uint _nHashFuncs;
		private uint _nTweak;
		private byte[] _vData;

		public BloomFilter()
		{
		}

		public BloomFilter(int nElements, double nFPRate, BloomFlags nFlagsIn = BloomFlags.UPDATE_ALL)
			: this(nElements, nFPRate, RandomUtils.GetUInt32(), nFlagsIn)
		{
		}

		public BloomFilter(int nElements, double nFPRate, uint nTweakIn, BloomFlags nFlagsIn = BloomFlags.UPDATE_ALL)
		{
			// The ideal size for a bloom filter with a given number of elements and false positive rate is:
			// - nElements * log(fp rate) / ln(2)^2
			// We ignore filter parameters which will create a bloom filter larger than the protocol limits
			_vData = new byte[Math.Min((uint) (-1 / Ln2Squared * nElements * (decimal) Math.Log(nFPRate)),
				                  MaxBloomFilterSize) / 8];
			//vData(min((unsigned int)(-1  / LN2SQUARED * nElements * log(nFPRate)), MAX_BLOOM_FILTER_SIZE * 8) / 8),
			// The ideal number of hash functions is filter size * ln(2) / number of elements
			// Again, we ignore filter parameters which will create a bloom filter with more hash functions than the protocol limits
			// See http://en.wikipedia.org/wiki/Bloom_filter for an explanation of these formulas

			_nHashFuncs = Math.Min((uint) (_vData.Length * 8 / nElements * LN2), MaxHashFuncs);
			_nTweak = nTweakIn;
			_nFlags = (byte) nFlagsIn;
		}

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWriteAsVarString(ref _vData);
			stream.ReadWrite(ref _nHashFuncs);
			stream.ReadWrite(ref _nTweak);
			stream.ReadWrite(ref _nFlags);
		}

		private uint Hash(uint nHashNum, byte[] vDataToHash)
		{
			// 0xFBA4C795 chosen as it guarantees a reasonable bit difference between nHashNum values.
			return (uint) (Hashes.MurmurHash3(nHashNum * 0xFBA4C795 + _nTweak, vDataToHash) % (_vData.Length * 8));
		}

		public void Insert(byte[] vKey)
		{
			if (_isFull)
			{
				return;
			}

			for (uint i = 0; i < _nHashFuncs; i++)
			{
				var nIndex = Hash(i, vKey);
				// Sets bit nIndex of vData
				_vData[nIndex >> 3] |= (byte) (1 << (7 & (int) nIndex));
			}

			_isEmpty = false;
		}

		public bool Contains(byte[] vKey)
		{
			if (_isFull)
			{
				return true;
			}

			if (_isEmpty)
			{
				return false;
			}

			for (uint i = 0; i < _nHashFuncs; i++)
			{
				var nIndex = Hash(i, vKey);
				// Checks bit nIndex of vData
				if ((_vData[nIndex >> 3] & (byte) (1 << (7 & (int) nIndex))) == 0)
				{
					return false;
				}
			}

			return true;
		}

		public bool Contains(OutPoint outPoint)
		{
			if (outPoint == null)
			{
				throw new ArgumentNullException(nameof(outPoint));
			}

			return Contains(outPoint.ToBytes());
		}

		public bool Contains(uint256 hash)
		{
			if (hash == null)
			{
				throw new ArgumentNullException(nameof(hash));
			}

			return Contains(hash.ToBytes());
		}

		public void Insert(OutPoint outPoint)
		{
			if (outPoint == null)
			{
				throw new ArgumentNullException(nameof(outPoint));
			}

			Insert(outPoint.ToBytes());
		}

		public void Insert(uint256 value)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			Insert(value.ToBytes());
		}

		public bool IsWithinSizeConstraints()
		{
			return _vData.Length <= MaxBloomFilterSize && _nHashFuncs <= MaxHashFuncs;
		}

		public bool IsRelevantAndUpdate(Transaction tx)
		{
			if (tx == null)
			{
				throw new ArgumentNullException(nameof(tx));
			}

			var hash = tx.GetHash();
			var fFound = false;
			// Match if the filter contains the hash of tx
			//  for finding tx when they appear in a block
			if (_isFull)
			{
				return true;
			}

			if (_isEmpty)
			{
				return false;
			}

			if (Contains(hash))
			{
				fFound = true;
			}

			for (uint i = 0; i < tx.Outputs.Count; i++)
			{
				var txout = tx.Outputs[(int) i];
				// Match if the filter contains any arbitrary script data element in any scriptPubKey in tx
				// If this matches, also add the specific output that was matched.
				// This means clients don't have to update the filter themselves when a new relevant tx 
				// is discovered in order to find spending transactions, which avoids round-tripping and race conditions.
				foreach (var op in txout.ScriptPubKey.ToOps())
				{
					if (op.PushData != null && op.PushData.Length != 0 && Contains(op.PushData))
					{
						fFound = true;
						if ((_nFlags & (byte) BloomFlags.UPDATE_MASK) == (byte) BloomFlags.UPDATE_ALL)
						{
							Insert(new OutPoint(hash, i));
						}
						else if ((_nFlags & (byte) BloomFlags.UPDATE_MASK) == (byte) BloomFlags.UPDATE_P2PUBKEY_ONLY)
						{
							var template = StandardScripts.GetTemplateFromScriptPubKey(txout.ScriptPubKey);
							if (template != null &&
							    (template.Type == TxOutType.TX_PUBKEY || template.Type == TxOutType.TX_MULTISIG))
							{
								Insert(new OutPoint(hash, i));
							}
						}

						break;
					}
				}
			}

			if (fFound)
			{
				return true;
			}

			foreach (var txin in tx.Inputs)
			{
				// Match if the filter contains an outpoint tx spends
				if (Contains(txin.PrevOut))
				{
					return true;
				}

				// Match if the filter contains any arbitrary script data element in any scriptSig in tx
				foreach (var op in txin.ScriptSig.ToOps())
				{
					if (op.PushData != null && op.PushData.Length != 0 && Contains(op.PushData))
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}