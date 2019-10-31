using System;
using System.Collections.Concurrent;
using System.Reflection;
using BitcoinNet.Protocol;

namespace BitcoinNet
{
	public class ConsensusFactory
	{
		private readonly TypeInfo _blockHeaderType = typeof(BlockHeader).GetTypeInfo();
		private readonly TypeInfo _blockType = typeof(Block).GetTypeInfo();

		private readonly ConcurrentDictionary<Type, bool> _isAssignableFromBlock =
			new ConcurrentDictionary<Type, bool>();

		private readonly ConcurrentDictionary<Type, bool> _isAssignableFromBlockHeader =
			new ConcurrentDictionary<Type, bool>();

		private readonly ConcurrentDictionary<Type, bool> _isAssignableFromTransaction =
			new ConcurrentDictionary<Type, bool>();

		private readonly TypeInfo _transactionType = typeof(Transaction).GetTypeInfo();

		protected bool IsBlockHeader(Type type)
		{
			return IsAssignable(type, _blockHeaderType, _isAssignableFromBlockHeader);
		}

		protected bool IsBlock(Type type)
		{
			return IsAssignable(type, _blockType, _isAssignableFromBlock);
		}

		protected bool IsTransaction(Type type)
		{
			return IsAssignable(type, _transactionType, _isAssignableFromTransaction);
		}

		private bool IsAssignable(Type type, TypeInfo baseType, ConcurrentDictionary<Type, bool> cache)
		{
			if (!cache.TryGetValue(type, out var isAssignable))
			{
				isAssignable = baseType.IsAssignableFrom(type.GetTypeInfo());
				cache.TryAdd(type, isAssignable);
			}

			return isAssignable;
		}

		public virtual bool TryCreateNew(Type type, out IBitcoinSerializable result)
		{
			result = null;
			if (IsBlock(type))
			{
				result = CreateBlock();
				return true;
			}

			if (IsBlockHeader(type))
			{
				result = CreateBlockHeader();
				return true;
			}

			if (IsTransaction(type))
			{
				result = CreateTransaction();
				return true;
			}

			return false;
		}

		public bool TryCreateNew<T>(out T result) where T : IBitcoinSerializable
		{
			result = default;
			var success = TryCreateNew(typeof(T), out var r);
			if (success)
			{
				result = (T) r;
			}

			return success;
		}

		public virtual ProtocolCapabilities GetProtocolCapabilities(uint protocolVersion)
		{
			return new ProtocolCapabilities
			{
				PeerTooOld = protocolVersion < 209U,
				SupportTimeAddress = protocolVersion >= 31402U,
				SupportGetBlock = protocolVersion < 32000U || protocolVersion > 32400U,
				SupportPingPong = protocolVersion > 60000U,
				SupportMempoolQuery = protocolVersion >= 60002U,
				SupportReject = protocolVersion >= 70002U,
				SupportNodeBloom = protocolVersion >= 70011U,
				SupportSendHeaders = protocolVersion >= 70012U,
				SupportWitness = false,
				SupportCompactBlocks = protocolVersion >= 70014U,
				SupportCheckSum = protocolVersion >= 60002,
				SupportUserAgent = protocolVersion >= 60002
			};
		}

		public virtual Block CreateBlock()
		{
#pragma warning disable CS0618 // Type or member is obsolete
			return new Block(CreateBlockHeader());
#pragma warning restore CS0618 // Type or member is obsolete
		}

		public virtual BlockHeader CreateBlockHeader()
		{
#pragma warning disable CS0618 // Type or member is obsolete
			return new BlockHeader();
#pragma warning restore CS0618 // Type or member is obsolete
		}

		public virtual Transaction CreateTransaction()
		{
			return new ForkIdTransaction(0x00, false, this);
		}

		protected virtual TransactionBuilder CreateTransactionBuilderCore()
		{
#pragma warning disable CS0618 // Type or member is obsolete
			return new TransactionBuilder();
#pragma warning restore CS0618 // Type or member is obsolete
		}

		public TransactionBuilder CreateTransactionBuilder()
		{
#pragma warning disable CS0618 // Type or member is obsolete
			var builder = CreateTransactionBuilderCore();
			builder.SetConsensusFactory(this);
			return builder;
#pragma warning restore CS0618 // Type or member is obsolete
		}

		public TransactionBuilder CreateTransactionBuilder(int seed)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			var builder = CreateTransactionBuilderCore();
			builder.SetConsensusFactory(this);
			builder.ShuffleRandom = new Random(seed);
			return builder;
#pragma warning restore CS0618 // Type or member is obsolete
		}
	}
}