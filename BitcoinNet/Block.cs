using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BitcoinNet.BouncyCastle.Math;
using BitcoinNet.Crypto;
using BitcoinNet.DataEncoders;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	/// <summary>
	///     Nodes collect new transactions into a block, hash them into a hash tree,
	///     and scan through nonce values to make the block's hash satisfy proof-of-work
	///     requirements.  When they solve the proof-of-work, they broadcast the block
	///     to everyone and the block is added to the block chain.  The first transaction
	///     in the block is a special one that creates a new coin owned by the creator
	///     of the block.
	/// </summary>
	public class BlockHeader : IBitcoinSerializable
	{
		public const int Size = 80;

		// header
		private const int CurrentVersion = 3;
		private static readonly BigInteger Pow256 = BigInteger.ValueOf(2).Pow(256);

		private uint256[] _hashes;
		protected uint256 _hashMerkleRoot;
		protected uint256 _hashPrevBlock;
		protected uint _nBits;
		protected uint _nNonce;
		protected uint _nTime;
		protected int _nVersion;

		[Obsolete("You should instantiate BlockHeader from ConsensusFactory.CreateBlockHeader")]
		public BlockHeader()
		{
			SetNull();
		}

		public BlockHeader(string hex, Network network)
			: this(hex, network?.Consensus?.ConsensusFactory ?? throw new ArgumentNullException(nameof(network)))
		{
		}

		public BlockHeader(string hex, Consensus consensus)
			: this(hex, consensus?.ConsensusFactory ?? throw new ArgumentNullException(nameof(consensus)))
		{
		}

		public BlockHeader(string hex, ConsensusFactory consensusFactory)
		{
			if (hex == null)
			{
				throw new ArgumentNullException(nameof(hex));
			}

			if (consensusFactory == null)
			{
				throw new ArgumentNullException(nameof(consensusFactory));
			}

			var bs = new BitcoinStream(Encoders.Hex.DecodeData(hex))
			{
				ConsensusFactory = consensusFactory
			};
			ReadWrite(bs);
		}

		public BlockHeader(byte[] data, Network network)
			: this(data, network?.Consensus?.ConsensusFactory ?? throw new ArgumentNullException(nameof(network)))
		{
		}

		public BlockHeader(byte[] data, Consensus consensus)
			: this(data, consensus?.ConsensusFactory ?? throw new ArgumentNullException(nameof(consensus)))
		{
		}

		public BlockHeader(byte[] data, ConsensusFactory consensusFactory)
		{
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			if (consensusFactory == null)
			{
				throw new ArgumentNullException(nameof(consensusFactory));
			}

			var bs = new BitcoinStream(data)
			{
				ConsensusFactory = consensusFactory
			};
			ReadWrite(bs);
		}

		public uint256 HashPrevBlock
		{
			get => _hashPrevBlock;
			set => _hashPrevBlock = value;
		}

		public Target Bits
		{
			get => _nBits;
			set => _nBits = value;
		}

		public int Version
		{
			get => _nVersion;
			set => _nVersion = value;
		}

		public uint Nonce
		{
			get => _nNonce;
			set => _nNonce = value;
		}

		public uint256 HashMerkleRoot
		{
			get => _hashMerkleRoot;
			set => _hashMerkleRoot = value;
		}

		public bool IsNull => _nBits == 0;

		public DateTimeOffset BlockTime
		{
			get => Utils.UnixTimeToDateTime(_nTime);
			set => _nTime = Utils.DateTimeToUnixTime(value);
		}

		// IBitcoinSerializable Members

		public virtual void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _nVersion);
			stream.ReadWrite(ref _hashPrevBlock);
			stream.ReadWrite(ref _hashMerkleRoot);
			stream.ReadWrite(ref _nTime);
			stream.ReadWrite(ref _nBits);
			stream.ReadWrite(ref _nNonce);
		}

		public static BlockHeader Parse(string hex, Network network)
		{
			if (network == null)
			{
				throw new ArgumentNullException(nameof(network));
			}

			return Parse(hex, network.Consensus.ConsensusFactory);
		}

		public static BlockHeader Parse(string hex, Consensus consensus)
		{
			if (consensus == null)
			{
				throw new ArgumentNullException(nameof(consensus));
			}

			return Parse(hex, consensus.ConsensusFactory);
		}

		public static BlockHeader Parse(string hex, ConsensusFactory consensusFactory)
		{
			if (consensusFactory == null)
			{
				throw new ArgumentNullException(nameof(consensusFactory));
			}

			return new BlockHeader(Encoders.Hex.DecodeData(hex), consensusFactory);
		}

		protected internal virtual void SetNull()
		{
			_nVersion = CurrentVersion;
			_hashPrevBlock = 0;
			_hashMerkleRoot = 0;
			_nTime = 0;
			_nBits = 0;
			_nNonce = 0;
		}

		public virtual uint256 GetPoWHash()
		{
			return GetHash();
		}

		public uint256 GetHash()
		{
			uint256 h = null;
			var hashes = _hashes;
			if (hashes != null)
			{
				h = hashes[0];
			}

			if (h != null)
			{
				return h;
			}

			using (var hs = CreateHashStream())
			{
				var stream = new BitcoinStream(hs, true);
				stream.SerializationTypeScope(SerializationType.Hash);
				ReadWrite(stream);
				h = hs.GetHash();
			}

			hashes = _hashes;
			if (hashes != null)
			{
				hashes[0] = h;
			}

			return h;
		}

		protected virtual HashStreamBase CreateHashStream()
		{
			return new HashStream();
		}

		/// <summary>
		///     Precompute the block header hash so that later calls to GetHash() will returns the precomputed hash
		/// </summary>
		/// <param name="invalidateExisting">If true, the previous precomputed hash is thrown away, else it is reused</param>
		/// <param name="lazily">
		///     If true, the hash will be calculated and cached at the first call to GetHash(), else it will be
		///     immediately
		/// </param>
		public void PrecomputeHash(bool invalidateExisting, bool lazily)
		{
			_hashes = invalidateExisting ? new uint256[1] : _hashes ?? new uint256[1];
			if (!lazily && _hashes[0] == null)
			{
				_hashes[0] = GetHash();
			}
		}

		public bool CheckProofOfWork()
		{
			var bits = Bits.ToBigInteger();
			if (bits.CompareTo(BigInteger.Zero) <= 0 || bits.CompareTo(Pow256) >= 0)
			{
				return false;
			}

			// Check proof of work matches claimed amount
			return GetPoWHash() <= Bits.ToUInt256();
		}

		public override string ToString()
		{
			return GetHash().ToString();
		}

		/// <summary>
		///     Set time to consensus acceptable value
		/// </summary>
		/// <param name="network">Network</param>
		/// <param name="prev">previous block</param>
		public void UpdateTime(Network network, ChainedBlock prev)
		{
			UpdateTime(DateTimeOffset.UtcNow, network, prev);
		}

		/// <summary>
		///     Set time to consensus acceptable value
		/// </summary>
		/// <param name="consensus">Consensus</param>
		/// <param name="prev">previous block</param>
		public void UpdateTime(Consensus consensus, ChainedBlock prev)
		{
			UpdateTime(DateTimeOffset.UtcNow, consensus, prev);
		}

		/// <summary>
		///     Set time to consensus acceptable value
		/// </summary>
		/// <param name="now">The expected date</param>
		/// <param name="consensus">Consensus</param>
		/// <param name="prev">previous block</param>
		public void UpdateTime(DateTimeOffset now, Consensus consensus, ChainedBlock prev)
		{
			var nOldTime = BlockTime;
			var mtp = prev.GetMedianTimePast() + TimeSpan.FromSeconds(1);
			var nNewTime = mtp > now ? mtp : now;

			if (nOldTime < nNewTime)
			{
				BlockTime = nNewTime;
			}

			// Updating time can change work required on testnet:
			if (consensus.PowAllowMinDifficultyBlocks)
			{
				Bits = GetWorkRequired(consensus, prev);
			}
		}

		/// <summary>
		///     Set time to consensus acceptable value
		/// </summary>
		/// <param name="now">The expected date</param>
		/// <param name="network">Network</param>
		/// <param name="prev">previous block</param>
		public void UpdateTime(DateTimeOffset now, Network network, ChainedBlock prev)
		{
			UpdateTime(now, network.Consensus, prev);
		}

		public Target GetWorkRequired(Network network, ChainedBlock prev)
		{
			return GetWorkRequired(network.Consensus, prev);
		}

		public Target GetWorkRequired(Consensus consensus, ChainedBlock prev)
		{
			return new ChainedBlock(this, null, prev).GetWorkRequired(consensus);
		}
	}

	public class Block : IBitcoinSerializable
	{
		//FIXME: it needs to be changed when Gavin Andresen increase the max block size. 
		public const uint MaxBlockSize = 1000 * 1000;
		private BlockHeader _header;

		// network and disk
		private List<Transaction> vtx = new List<Transaction>();

		[Obsolete("Should use Block.CreateBlock(Network)")]
		public Block() : this(Consensus.Main.ConsensusFactory.CreateBlockHeader())
		{
		}

		[Obsolete("Should use ConsensusFactories")]
		public Block(BlockHeader blockHeader)
		{
			if (blockHeader == null)
			{
				throw new ArgumentNullException(nameof(blockHeader));
			}

			SetNull();
			_header = blockHeader;
		}

		public List<Transaction> Transactions
		{
			get => vtx;
			set => vtx = value;
		}

		public bool HeaderOnly => vtx == null || vtx.Count == 0;

		public BlockHeader Header => _header;

		public void ReadWrite(BitcoinStream stream)
		{
			using (stream.ConsensusFactoryScope(GetConsensusFactory()))
			{
				stream.ReadWrite(ref _header);
				stream.ReadWrite(ref vtx);
			}
		}

		public MerkleNode GetMerkleRoot()
		{
			return MerkleNode.GetRoot(Transactions.Select(t => t.GetHash()));
		}

		public static Block CreateBlock(Network network)
		{
			return CreateBlock(network.Consensus.ConsensusFactory);
		}

		public static Block CreateBlock(ConsensusFactory consensusFactory)
		{
			return consensusFactory.CreateBlock();
		}

		public static Block CreateBlock(BlockHeader header, Network network)
		{
			return CreateBlock(header, network.Consensus.ConsensusFactory);
		}

		public static Block CreateBlock(BlockHeader header, ConsensusFactory consensusFactory)
		{
			var ms = new MemoryStream(100);
			var bs = new BitcoinStream(ms, true);
			bs.ConsensusFactory = consensusFactory;
			bs.ReadWrite(header);

			var block = consensusFactory.CreateBlock();
			ms.Position = 0;
			bs = new BitcoinStream(ms, false);
			block.Header.ReadWrite(bs);
			return block;
		}

		/// <summary>
		///     Get the coinbase height as specified by the first tx input of this block (BIP 34)
		/// </summary>
		/// <returns>Null if block has been created before BIP34 got enforced, else, the height</returns>
		public int? GetCoinbaseHeight()
		{
			if (Header.Version < 2 || Transactions.Count == 0 || Transactions[0].Inputs.Count == 0)
			{
				return null;
			}

			return Transactions[0].Inputs[0].ScriptSig.ToOps().FirstOrDefault()?.GetInt();
		}

		private void SetNull()
		{
			if (_header != null)
			{
				_header.SetNull();
			}

			vtx.Clear();
		}

		public uint256 GetHash()
		{
			//Block's hash is his header's hash
			return _header.GetHash();
		}

		public void ReadWrite(byte[] array, int startIndex)
		{
			var ms = new MemoryStream(array);
			ms.Position += startIndex;
			var bitStream = new BitcoinStream(ms, false);
			ReadWrite(bitStream);
		}

		public Transaction AddTransaction(Transaction tx)
		{
			Transactions.Add(tx);
			return tx;
		}

		/// <summary>
		///     Create a block with the specified option only. (useful for stripping data from a block)
		/// </summary>
		/// <param name="options">Options to keep</param>
		/// <returns>A new block with only the options wanted</returns>
		public Block WithOptions(TransactionOptions options)
		{
			if (Transactions.Count == 0)
			{
				return this;
			}

			if (options == TransactionOptions.None)
			{
				return this;
			}

			var instance = GetConsensusFactory().CreateBlock();
			var ms = new MemoryStream();
			var bms = new BitcoinStream(ms, true) {TransactionOptions = options};
			ReadWrite(bms);
			ms.Position = 0;
			bms = new BitcoinStream(ms, false) {TransactionOptions = options};
			instance.ReadWrite(bms);
			return instance;
		}

		public virtual ConsensusFactory GetConsensusFactory()
		{
			return Consensus.Main.ConsensusFactory;
		}

		public void UpdateMerkleRoot()
		{
			Header.HashMerkleRoot = GetMerkleRoot().Hash;
		}

		/// <summary>
		///     Check proof of work and merkle root
		/// </summary>
		/// <returns></returns>
		public bool Check()
		{
			return CheckMerkleRoot() && Header.CheckProofOfWork();
		}

		public bool CheckProofOfWork()
		{
			return Header.CheckProofOfWork();
		}

		public bool CheckMerkleRoot()
		{
			return Header.HashMerkleRoot == GetMerkleRoot().Hash;
		}

		public Block CreateNextBlockWithCoinbase(BitcoinAddress address, int height)
		{
			return CreateNextBlockWithCoinbase(address, height, DateTimeOffset.UtcNow);
		}

		public Block CreateNextBlockWithCoinbase(BitcoinAddress address, int height, DateTimeOffset now)
		{
			if (address == null)
			{
				throw new ArgumentNullException(nameof(address));
			}

			var block = GetConsensusFactory().CreateBlock();
			block.Header.Nonce = RandomUtils.GetUInt32();
			block.Header.HashPrevBlock = GetHash();
			block.Header.BlockTime = now;
			var tx = block.AddTransaction(GetConsensusFactory().CreateTransaction());
			tx.Inputs.Add(scriptSig: new Script(Op.GetPushOp(RandomUtils.GetBytes(30))));
			tx.Outputs.Add(new TxOut(address.Network.GetReward(height), address)
			{
				Value = address.Network.GetReward(height)
			});
			return block;
		}

		public int GetWeight()
		{
			return this.GetSerializedSize(TransactionOptions.None) * 3 + this.GetSerializedSize(TransactionOptions.All);
		}

		public Block CreateNextBlockWithCoinbase(PubKey pubkey, Money value, DateTimeOffset now,
			ConsensusFactory consensusFactory)
		{
			var block = consensusFactory.CreateBlock();
			block.Header.Nonce = RandomUtils.GetUInt32();
			block.Header.HashPrevBlock = GetHash();
			block.Header.BlockTime = now;
			var tx = block.AddTransaction(consensusFactory.CreateTransaction());
			tx.Inputs.Add(scriptSig: new Script(Op.GetPushOp(RandomUtils.GetBytes(30))));
			tx.Outputs.Add(new TxOut
			{
				Value = value,
				ScriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(pubkey)
			});
			return block;
		}

		public Block CreateNextBlockWithCoinbase(PubKey pubkey, Money value, ConsensusFactory consensusFactory)
		{
			return CreateNextBlockWithCoinbase(pubkey, value, DateTimeOffset.UtcNow, consensusFactory);
		}

		public static Block Parse(string hex, Network network)
		{
			if (network == null)
			{
				throw new ArgumentNullException(nameof(network));
			}

			return Parse(hex, network.Consensus.ConsensusFactory);
		}

		public static Block Parse(string hex, Consensus consensus)
		{
			if (consensus == null)
			{
				throw new ArgumentNullException(nameof(consensus));
			}

			return Parse(hex, consensus.ConsensusFactory);
		}

		public static Block Parse(string hex, ConsensusFactory consensusFactory)
		{
			if (hex == null)
			{
				throw new ArgumentNullException(nameof(hex));
			}

			if (consensusFactory == null)
			{
				throw new ArgumentNullException(nameof(consensusFactory));
			}

			var block = consensusFactory.CreateBlock();
			block.ReadWrite(Encoders.Hex.DecodeData(hex), consensusFactory);
			return block;
		}

		public static Block Load(byte[] hex, Network network)
		{
			if (hex == null)
			{
				throw new ArgumentNullException(nameof(hex));
			}

			if (network == null)
			{
				throw new ArgumentNullException(nameof(network));
			}

			return Load(hex, network.Consensus.ConsensusFactory);
		}

		public static Block Load(byte[] hex, Consensus consensus)
		{
			if (hex == null)
			{
				throw new ArgumentNullException(nameof(hex));
			}

			if (consensus == null)
			{
				throw new ArgumentNullException(nameof(consensus));
			}

			return Load(hex, consensus.ConsensusFactory);
		}

		public static Block Load(byte[] hex, ConsensusFactory consensusFactory)
		{
			if (hex == null)
			{
				throw new ArgumentNullException(nameof(hex));
			}

			if (consensusFactory == null)
			{
				throw new ArgumentNullException(nameof(consensusFactory));
			}

			var block = consensusFactory.CreateBlock();
			block.ReadWrite(hex, consensusFactory);
			return block;
		}

		public MerkleBlock Filter(params uint256[] txIds)
		{
			return new MerkleBlock(this, txIds);
		}

		public MerkleBlock Filter(BloomFilter filter)
		{
			return new MerkleBlock(this, filter);
		}
	}
}