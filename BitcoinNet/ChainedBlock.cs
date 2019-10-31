using System;
using System.Collections.Generic;
using System.Linq;
using BitcoinNet.BouncyCastle.Math;

namespace BitcoinNet
{
	/// <summary>
	///     A BlockHeader chained with all its ancestors
	/// </summary>
	public class ChainedBlock
	{
		private const int NMedianTimeSpan = 11;
		private static readonly BigInteger Pow256 = BigInteger.ValueOf(2).Pow(256);
		private BigInteger _chainWork;
		private BlockHeader _header;

		// pointer to the hash of the block, if any. memory is owned by this CBlockIndex
		private uint256 _pHashBlock;

		// pointer to the index of the predecessor of this block

		public ChainedBlock(BlockHeader header, uint256 headerHash, ChainedBlock previous)
		{
			if (header == null && headerHash == null)
			{
				throw new ArgumentException("At least, either header or headerHash should be different from null");
			}

			if (previous != null)
			{
				Height = previous.Height + 1;
			}

			Previous = previous;
			//this.nDataPos = pos;
			_header = header;
			_pHashBlock = headerHash ?? header.GetHash();

			if (header != null)
			{
				if (previous == null)
				{
					if (header.HashPrevBlock != uint256.Zero)
					{
						throw new ArgumentException("Only the genesis block can have no previous block");
					}
				}
				else
				{
					if (previous.HashBlock != header.HashPrevBlock)
					{
						throw new ArgumentException("The previous block has not the expected hash");
					}
				}
			}
		}

		public ChainedBlock(BlockHeader header, int height)
		{
			if (header == null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			Height = height;
			//this.nDataPos = pos;
			_header = header;
			_pHashBlock = header.GetHash();
		}

		public uint256 HashBlock
		{
			get
			{
				var h = _pHashBlock;
				if (h == null)
				{
					AssertHasHeader();
					h = Header.GetHash();
					_pHashBlock = h;
				}

				return h;
			}
		}

		public ChainedBlock Previous { get; }

		/// <summary>
		///     Height of the entry in the chain. The genesis block has height 0.
		/// </summary>
		public int Height { get; }

		/// <summary>
		///     Returns true if this ChainedBlock has the underlying header
		/// </summary>
		public bool HasHeader => _header != null;

		/// <summary>
		///     Get the underlying block header, throws if the Header is not present.
		/// </summary>
		public BlockHeader Header
		{
			get
			{
				AssertHasHeader();
				return _header;
			}
		}

		/// <summary>
		///     Free up some memory (cached HashBlock and ChainWork) at the price of efficiency
		/// </summary>
		public void StripCachedData()
		{
			_pHashBlock = null;
			_chainWork = null;
		}

		/// <summary>
		///     Strip the Header to free up memory
		/// </summary>
		public void StripHeader()
		{
			_header = null;
		}

		/// <summary>
		///     Get the BlockHeader
		/// </summary>
		/// <param name="header">The block header</param>
		/// <returns>True if this ChainedBlock has block header</returns>
		public bool TryGetHeader(out BlockHeader header)
		{
			header = _header;
			return header != null;
		}

		/// <summary>
		///     Get the value of the chain work
		/// </summary>
		/// <param name="cacheResult">
		///     If true, called GetChainWork on this block and future block will be faster, but this trade
		///     for space
		/// </param>
		/// <returns>The chain work value</returns>
		public uint256 GetChainWork(bool cacheResult)
		{
			return Target.ToUInt256(GetChainWorkValue(cacheResult));
		}

		private BigInteger GetChainWorkValue(bool cacheResult)
		{
			var chainWork = _chainWork;
			if (chainWork == null)
			{
				chainWork = CalculateChainWork();
				if (cacheResult)
				{
					_chainWork = chainWork;
				}
			}

			return chainWork;
		}

		private BigInteger CalculateChainWork()
		{
			var aggregate = BigInteger.Zero;
			var previous = new Stack<ChainedBlock>();
			foreach (var header in EnumerateToGenesis().Skip(1))
			{
				var value = header._chainWork;
				if (value == null)
				{
					previous.Push(header);
				}
				else
				{
					aggregate = value;
					break;
				}
			}

			while (previous.Count != 0)
			{
				aggregate = aggregate.Add(previous.Pop().GetBlockProof());
			}

			return aggregate.Add(GetBlockProof());
		}

		private BigInteger GetBlockProof()
		{
			AssertHasHeader();
			var bnTarget = Header.Bits.ToBigInteger();
			if (bnTarget.CompareTo(BigInteger.Zero) <= 0 || bnTarget.CompareTo(Pow256) >= 0)
			{
				return BigInteger.Zero;
			}

			// We need to compute 2**256 / (bnTarget+1), but we can't represent 2**256
			// as it's too large for a arith_uint256. However, as 2**256 is at least as large
			// as bnTarget+1, it is equal to ((2**256 - bnTarget - 1) / (bnTarget+1)) + 1,
			// or ~bnTarget / (nTarget+1) + 1.
			return Pow256.Subtract(bnTarget).Subtract(BigInteger.One).Divide(bnTarget.Add(BigInteger.One))
				.Add(BigInteger.One);
		}

		public BlockLocator GetLocator()
		{
			var nStep = 1;
			var vHave = new List<uint256>();

			var pindex = this;
			while (pindex != null)
			{
				vHave.Add(pindex.HashBlock);
				// Stop when we have added the genesis block.
				if (pindex.Height == 0)
				{
					break;
				}

				// Exponentially larger steps back, plus the genesis block.
				var nHeight = Math.Max(pindex.Height - nStep, 0);
				while (pindex.Height > nHeight)
				{
					pindex = pindex.Previous;
				}

				if (vHave.Count > 10)
				{
					nStep *= 2;
				}
			}

			var locators = new BlockLocator {Blocks = vHave};
			return locators;
		}

		public override bool Equals(object obj)
		{
			var item = obj as ChainedBlock;
			if (item == null)
			{
				return false;
			}

			return HashBlock.Equals(item.HashBlock);
		}

		public static bool operator ==(ChainedBlock a, ChainedBlock b)
		{
			if (ReferenceEquals(a, b))
			{
				return true;
			}

			if ((object) a == null || (object) b == null)
			{
				return false;
			}

			return a.HashBlock == b.HashBlock;
		}

		public static bool operator !=(ChainedBlock a, ChainedBlock b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return HashBlock.GetHashCode();
		}

		public IEnumerable<ChainedBlock> EnumerateToGenesis()
		{
			var current = this;
			while (current != null)
			{
				yield return current;
				current = current.Previous;
			}
		}

		public override string ToString()
		{
			return Height + " - " + HashBlock;
		}

		public ChainedBlock FindAncestorOrSelf(int height)
		{
			if (height > Height)
			{
				throw new InvalidOperationException("Can only find blocks below or equals to current height");
			}

			if (height < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(height));
			}

			var currentBlock = this;
			while (height != currentBlock.Height)
			{
				currentBlock = currentBlock.Previous;
			}

			return currentBlock;
		}

		public ChainedBlock FindAncestorOrSelf(uint256 blockHash)
		{
			var currentBlock = this;
			while (currentBlock != null && currentBlock.HashBlock != blockHash)
			{
				currentBlock = currentBlock.Previous;
			}

			return currentBlock;
		}

		public Target GetWorkRequired(Network network)
		{
			return GetWorkRequired(network.Consensus);
		}

		public Target GetNextWorkRequired(Network network)
		{
			return GetNextWorkRequired(network.Consensus);
		}

		public Target GetNextWorkRequired(Consensus consensus)
		{
			var dummy = consensus.ConsensusFactory.CreateBlockHeader();
			dummy.HashPrevBlock = HashBlock;
			dummy.BlockTime = DateTimeOffset.UtcNow;
			return GetNextWorkRequired(dummy, consensus);
		}

		public Target GetNextWorkRequired(BlockHeader block, Network network)
		{
			return GetNextWorkRequired(block, network.Consensus);
		}

		public Target GetNextWorkRequired(BlockHeader block, Consensus consensus)
		{
			return new ChainedBlock(block, block.GetHash(), this).GetWorkRequired(consensus);
		}

		private void AssertHasHeader()
		{
			if (_header == null)
			{
				throw new InvalidOperationException("ChainedBlock.Header must be available");
			}
		}

		public Target GetWorkRequired(Consensus consensus)
		{
			AssertHasHeader();
			// Genesis block
			if (Height == 0)
			{
				return consensus.PowLimit;
			}

			var nProofOfWorkLimit = consensus.PowLimit;
			var pindexLast = Previous;
			var height = Height;

			if (pindexLast == null)
			{
				return nProofOfWorkLimit;
			}

			// Only change once per interval
			if (height % consensus.DifficultyAdjustmentInterval != 0)
			{
				if (consensus.PowAllowMinDifficultyBlocks)
				{
					// Special difficulty rule for testnet:
					// If the new block's timestamp is more than 2* 10 minutes
					// then allow mining of a min-difficulty block.
					if (Header.BlockTime > pindexLast.Header.BlockTime +
					    TimeSpan.FromTicks(consensus.PowTargetSpacing.Ticks * 2))
					{
						return nProofOfWorkLimit;
					}

					// Return the last non-special-min-difficulty-rules-block
					var pindex = pindexLast;
					while (pindex.Previous != null && pindex.Height % consensus.DifficultyAdjustmentInterval != 0 &&
					       pindex.Header.Bits == nProofOfWorkLimit)
					{
						pindex = pindex.Previous;
					}

					return pindex.Header.Bits;
				}

				return pindexLast.Header.Bits;
			}

			long pastHeight = 0;
			if (consensus.LitecoinWorkCalculation)
			{
				var blockstogoback = consensus.DifficultyAdjustmentInterval - 1;
				if (pindexLast.Height + 1 != consensus.DifficultyAdjustmentInterval)
				{
					blockstogoback = consensus.DifficultyAdjustmentInterval;
				}

				pastHeight = pindexLast.Height - blockstogoback;
			}
			else
			{
				// Go back by what we want to be 14 days worth of blocks
				pastHeight = pindexLast.Height - (consensus.DifficultyAdjustmentInterval - 1);
			}

			var pindexFirst = EnumerateToGenesis().FirstOrDefault(o => o.Height == pastHeight);
			assert(pindexFirst);
			if (consensus.PowNoRetargeting)
			{
				return pindexLast.Header.Bits;
			}

			// Limit adjustment step
			var nActualTimespan = pindexLast.Header.BlockTime - pindexFirst.Header.BlockTime;
			if (nActualTimespan < TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks / 4))
			{
				nActualTimespan = TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks / 4);
			}

			if (nActualTimespan > TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks * 4))
			{
				nActualTimespan = TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks * 4);
			}

			// Retarget
			var bnNew = pindexLast.Header.Bits.ToBigInteger();
			bnNew = bnNew.Multiply(BigInteger.ValueOf((long) nActualTimespan.TotalSeconds));
			bnNew = bnNew.Divide(BigInteger.ValueOf((long) consensus.PowTargetTimespan.TotalSeconds));
			var newTarget = new Target(bnNew);
			if (newTarget > nProofOfWorkLimit)
			{
				newTarget = nProofOfWorkLimit;
			}

			return newTarget;
		}

		public DateTimeOffset GetMedianTimePast()
		{
			AssertHasHeader();
			var pmedian = new DateTimeOffset[NMedianTimeSpan];
			var pbegin = NMedianTimeSpan;
			var pend = NMedianTimeSpan;

			var pindex = this;
			for (var i = 0; i < NMedianTimeSpan && pindex != null; i++, pindex = pindex.Previous)
			{
				pmedian[--pbegin] = pindex.Header.BlockTime;
			}

			Array.Sort(pmedian);
			return pmedian[pbegin + (pend - pbegin) / 2];
		}

		private static void assert(object obj)
		{
			if (obj == null)
			{
				throw new NotSupportedException("Can only calculate work of a full chain");
			}
		}

		/// <summary>
		///     Check PoW and that the blocks connect correctly
		/// </summary>
		/// <param name="network">The network being used</param>
		/// <returns>True if PoW is correct</returns>
		public bool Validate(Network network)
		{
			if (network == null)
			{
				throw new ArgumentNullException(nameof(network));
			}

			var genesisCorrect = Height != 0 || HashBlock == network.GetGenesis().GetHash();
			return genesisCorrect && Validate(network.Consensus);
		}

		/// <summary>
		///     Check PoW and that the blocks connect correctly
		/// </summary>
		/// <param name="consensus">The consensus being used</param>
		/// <returns>True if PoW is correct</returns>
		public bool Validate(Consensus consensus)
		{
			AssertHasHeader();
			if (consensus == null)
			{
				throw new ArgumentNullException(nameof(consensus));
			}

			if (Height != 0 && Previous == null)
			{
				return false;
			}

			var heightCorrect = Height == 0 || Height == Previous.Height + 1;
			var hashPrevCorrect = Height == 0 || Header.HashPrevBlock == Previous.HashBlock;
			var hashCorrect = HashBlock == Header.GetHash();
			var workCorrect = CheckProofOfWorkAndTarget(consensus);
			return heightCorrect && hashPrevCorrect && hashCorrect && workCorrect;
		}

		public bool CheckProofOfWorkAndTarget(Network network)
		{
			return CheckProofOfWorkAndTarget(network.Consensus);
		}

		public bool CheckProofOfWorkAndTarget(Consensus consensus)
		{
			AssertHasHeader();
			return Height == 0 || Header.CheckProofOfWork() && Header.Bits == GetWorkRequired(consensus);
		}

		/// <summary>
		///     Find first common block between two chains
		/// </summary>
		/// <param name="block">The tip of the other chain</param>
		/// <returns>First common block or null</returns>
		public ChainedBlock FindFork(ChainedBlock block)
		{
			if (block == null)
			{
				throw new ArgumentNullException(nameof(block));
			}

			var highChain = Height > block.Height ? this : block;
			var lowChain = highChain == this ? block : this;
			while (highChain.Height != lowChain.Height)
			{
				highChain = highChain.Previous;
			}

			while (highChain.HashBlock != lowChain.HashBlock)
			{
				lowChain = lowChain.Previous;
				highChain = highChain.Previous;
				if (lowChain == null || highChain == null)
				{
					return null;
				}
			}

			return highChain;
		}

		public ChainedBlock GetAncestor(int height)
		{
			if (height > Height || height < 0)
			{
				return null;
			}

			var current = this;

			while (true)
			{
				if (current.Height == height)
				{
					return current;
				}

				current = current.Previous;
			}
		}
	}
}