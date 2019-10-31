using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace BitcoinNet
{
	/// <summary>
	///     Thread safe class representing a chain of headers from genesis
	/// </summary>
	public class ConcurrentChain : ChainBase
	{
		private readonly Dictionary<int, ChainedBlock> _blocksByHeight = new Dictionary<int, ChainedBlock>();
		private readonly Dictionary<uint256, ChainedBlock> _blocksById = new Dictionary<uint256, ChainedBlock>();
		private readonly ReaderWriterLock _lock = new ReaderWriterLock();
		private volatile ChainedBlock _tip;

		public ConcurrentChain()
		{
		}

		public ConcurrentChain(BlockHeader genesis)
		{
			SetTip(new ChainedBlock(genesis, 0));
		}

		public ConcurrentChain(Network network)
		{
			if (network != null)
			{
				var genesis = network.GetGenesis();
				SetTip(new ChainedBlock(genesis.Header, 0));
			}
		}

		public ConcurrentChain(byte[] bytes, ConsensusFactory consensusFactory) : this(bytes, consensusFactory, null)
		{
		}

		public ConcurrentChain(byte[] bytes, Consensus consensus) : this(bytes, consensus, null)
		{
		}

		public ConcurrentChain(byte[] bytes, Network network) : this(bytes, network, null)
		{
		}

		public ConcurrentChain(byte[] bytes, ConsensusFactory consensusFactory, ChainSerializationFormat format)
		{
			Load(bytes, consensusFactory, format);
		}

		public ConcurrentChain(byte[] bytes, Consensus consensus, ChainSerializationFormat format)
		{
			Load(bytes, consensus, format);
		}

		public ConcurrentChain(byte[] bytes, Network network, ChainSerializationFormat format)
		{
			Load(bytes, network, format);
		}

		public override ChainedBlock Tip => _tip;

		public override int Height => Tip.Height;

		public void Load(byte[] chain, Network network, ChainSerializationFormat format)
		{
			Load(new MemoryStream(chain), network, format);
		}

		public void Load(byte[] chain, Consensus consensus, ChainSerializationFormat format)
		{
			Load(new MemoryStream(chain), consensus, format);
		}

		public void Load(byte[] chain, ConsensusFactory consensusFactory, ChainSerializationFormat format)
		{
			Load(new MemoryStream(chain), consensusFactory, format);
		}

		public void Load(byte[] chain, ConsensusFactory consensusFactory)
		{
			Load(chain, consensusFactory, null);
		}

		public void Load(byte[] chain, Consensus consensus)
		{
			Load(chain, consensus, null);
		}

		public void Load(byte[] chain, Network network)
		{
			Load(chain, network, null);
		}

		public void Load(Stream stream, ConsensusFactory consensusFactory, ChainSerializationFormat format)
		{
			if (consensusFactory == null)
			{
				throw new ArgumentNullException(nameof(consensusFactory));
			}

			Load(new BitcoinStream(stream, false) {ConsensusFactory = consensusFactory}, format);
		}

		public void Load(Stream stream, Network network, ChainSerializationFormat format)
		{
			if (network == null)
			{
				throw new ArgumentNullException(nameof(network));
			}

			Load(stream, network.Consensus.ConsensusFactory, format);
		}

		public void Load(Stream stream, Consensus consensus, ChainSerializationFormat format)
		{
			if (consensus == null)
			{
				throw new ArgumentNullException(nameof(consensus));
			}

			Load(stream, consensus.ConsensusFactory, format);
		}

		public void Load(Stream stream)
		{
			Load(new BitcoinStream(stream, false), null);
		}

		public void Load(BitcoinStream stream)
		{
			Load(stream, null);
		}

		public void Load(BitcoinStream stream, ChainSerializationFormat format)
		{
			format = format ?? new ChainSerializationFormat();
			format.AssertCoherent();
			var genesis = Genesis;
			using (_lock.LockWrite())
			{
				try
				{
					var height = 0;
					while (true)
					{
						uint256.MutableUint256 id = null;
						if (format.SerializePrecomputedBlockHash)
						{
							stream.ReadWrite(ref id);
						}

						BlockHeader header = null;
						if (format.SerializeBlockHeader)
						{
							stream.ReadWrite(ref header);
						}

						if (height == 0)
						{
							_blocksByHeight.Clear();
							_blocksById.Clear();
							_tip = null;
							if (header != null && genesis != null && header.GetHash() != genesis.HashBlock)
							{
								throw new InvalidOperationException("Unexpected genesis block");
							}

							SetTipNoLock(new ChainedBlock(genesis?.Header ?? header, 0));
						}
						else if (!format.SerializeBlockHeader ||
						         _tip.HashBlock == header.HashPrevBlock && !(header.IsNull && header.Nonce == 0))
						{
							SetTipNoLock(new ChainedBlock(header, id?.Value, Tip));
						}
						else
						{
							break;
						}

						height++;
					}
				}
				catch (EndOfStreamException)
				{
				}
			}
		}

		public byte[] ToBytes()
		{
			using (var ms = new MemoryStream())
			{
				WriteTo(ms);
				return ms.ToArray();
			}
		}

		public void WriteTo(Stream stream)
		{
			WriteTo(stream, null);
		}

		public void WriteTo(Stream stream, ChainSerializationFormat format)
		{
			WriteTo(new BitcoinStream(stream, true), format);
		}

		public void WriteTo(BitcoinStream stream)
		{
			WriteTo(stream, null);
		}

		public void WriteTo(BitcoinStream stream, ChainSerializationFormat format)
		{
			format = format ?? new ChainSerializationFormat();
			format.AssertCoherent();
			using (_lock.LockRead())
			{
				for (var i = 0; i < Tip.Height + 1; i++)
				{
					var block = GetBlockNoLock(i);
					if (format.SerializePrecomputedBlockHash)
					{
						stream.ReadWrite(block.HashBlock.AsBitcoinSerializable());
					}

					if (format.SerializeBlockHeader)
					{
						stream.ReadWrite(block.Header);
					}
				}
			}
		}

		public ConcurrentChain Clone()
		{
			var chain = new ConcurrentChain {_tip = _tip};
			using (_lock.LockRead())
			{
				foreach (var kv in _blocksById)
				{
					chain._blocksById.Add(kv.Key, kv.Value);
				}

				foreach (var kv in _blocksByHeight)
				{
					chain._blocksByHeight.Add(kv.Key, kv.Value);
				}
			}

			return chain;
		}

		/// <summary>
		///     Force a new tip for the chain
		/// </summary>
		/// <param name="pindex"></param>
		/// <returns>forking point</returns>
		public override ChainedBlock SetTip(ChainedBlock block)
		{
			using (_lock.LockWrite())
			{
				return SetTipNoLock(block);
			}
		}

		private ChainedBlock SetTipNoLock(ChainedBlock block)
		{
			var height = Tip == null ? -1 : Tip.Height;
			foreach (var orphaned in EnumerateThisToFork(block))
			{
				_blocksById.Remove(orphaned.HashBlock);
				_blocksByHeight.Remove(orphaned.Height);
				height--;
			}

			var fork = GetBlockNoLock(height);
			foreach (var newBlock in block.EnumerateToGenesis()
				.TakeWhile(c => c != fork))
			{
				_blocksById.AddOrReplace(newBlock.HashBlock, newBlock);
				_blocksByHeight.AddOrReplace(newBlock.Height, newBlock);
			}

			_tip = block;
			return fork;
		}

		private IEnumerable<ChainedBlock> EnumerateThisToFork(ChainedBlock block)
		{
			if (_tip == null)
			{
				yield break;
			}

			var tip = _tip;
			while (true)
			{
				if (ReferenceEquals(null, block) || ReferenceEquals(null, tip))
				{
					throw new InvalidOperationException("No fork found between the two chains");
				}

				if (tip.Height > block.Height)
				{
					yield return tip;
					tip = tip.Previous;
				}
				else if (tip.Height < block.Height)
				{
					block = block.Previous;
				}
				else if (tip.Height == block.Height)
				{
					if (tip.HashBlock == block.HashBlock)
					{
						break;
					}

					yield return tip;
					block = block.Previous;
					tip = tip.Previous;
				}
			}
		}

		// IChain Members

		public override ChainedBlock GetBlock(uint256 id)
		{
			using (_lock.LockRead())
			{
				_blocksById.TryGetValue(id, out var result);
				return result;
			}
		}

		private ChainedBlock GetBlockNoLock(int height)
		{
			_blocksByHeight.TryGetValue(height, out var result);
			return result;
		}

		public override ChainedBlock GetBlock(int height)
		{
			using (_lock.LockRead())
			{
				return GetBlockNoLock(height);
			}
		}

		protected override IEnumerable<ChainedBlock> EnumerateFromStart()
		{
			var i = 0;
			while (true)
			{
				ChainedBlock block = null;
				using (_lock.LockRead())
				{
					block = GetBlockNoLock(i);
					if (block == null)
					{
						yield break;
					}
				}

				yield return block;
				i++;
			}
		}

		public override string ToString()
		{
			return Tip == null ? "no tip" : Tip.Height.ToString();
		}

		public class ChainSerializationFormat
		{
			public ChainSerializationFormat()
			{
				SerializePrecomputedBlockHash = true;
				SerializeBlockHeader = true;
			}

			public bool SerializePrecomputedBlockHash { get; set; }

			public bool SerializeBlockHeader { get; set; }

			internal void AssertCoherent()
			{
				if (!SerializePrecomputedBlockHash && !SerializeBlockHeader)
				{
					throw new InvalidOperationException(
						"The ChainSerializationFormat is invalid, SerializePrecomputedBlockHash or SerializeBlockHeader shoudl be true");
				}
			}
		}
	}

	internal class ReaderWriterLock
	{
		private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

		public IDisposable LockRead()
		{
			return new ActionDisposable(() => _lock.EnterReadLock(), () => _lock.ExitReadLock());
		}

		public IDisposable LockWrite()
		{
			return new ActionDisposable(() => _lock.EnterWriteLock(), () => _lock.ExitWriteLock());
		}

		internal bool TryLockWrite(out IDisposable locked)
		{
			locked = null;
			if (_lock.TryEnterWriteLock(0))
			{
				locked = new ActionDisposable(() => { }, () => _lock.ExitWriteLock());
				return true;
			}

			return false;
		}
	}
}