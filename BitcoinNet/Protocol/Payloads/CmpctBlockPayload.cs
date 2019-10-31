using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BitcoinNet.Crypto;

namespace BitcoinNet.Protocol
{
	[Payload("cmpctblock")]
	public class CmpctBlockPayload : Payload
	{
		private BlockHeader _header;
		private ulong _nonce;
		private ulong _shortTxidk0;
		private ulong _shortTxidk1;

		public CmpctBlockPayload()
		{
		}

		public CmpctBlockPayload(Block block)
		{
			_header = block.Header;
			_nonce = RandomUtils.GetUInt64();
			UpdateShortTxIDSelector();
			PrefilledTransactions.Add(new PrefilledTransaction
			{
				Index = 0,
				Transaction = block.Transactions[0]
			});
			foreach (var tx in block.Transactions.Skip(1))
			{
				ShortIds.Add(GetShortID(tx.GetHash()));
			}
		}

		public BlockHeader Header
		{
			get => _header;
			set
			{
				_header = value;
				if (value != null)
				{
					UpdateShortTxIDSelector();
				}
			}
		}

		public ulong Nonce
		{
			get => _nonce;
			set
			{
				_nonce = value;
				UpdateShortTxIDSelector();
			}
		}

		public List<ulong> ShortIds { get; } = new List<ulong>();

		public List<PrefilledTransaction> PrefilledTransactions { get; } = new List<PrefilledTransaction>();

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _header);
			stream.ReadWrite(ref _nonce);

			var shorttxids_size = (uint) ShortIds.Count;
			stream.ReadWriteAsVarInt(ref shorttxids_size);
			if (!stream.Serializing)
			{
				ulong i = 0;
				ulong shottxidsCount = 0;
				while (ShortIds.Count < shorttxids_size)
				{
					shottxidsCount = Math.Min(1000UL + shottxidsCount, shorttxids_size);
					for (; i < shottxidsCount; i++)
					{
						uint lsb = 0;
						ushort msb = 0;
						stream.ReadWrite(ref lsb);
						stream.ReadWrite(ref msb);
						ShortIds.Add(((ulong) msb << 32) | lsb);
					}
				}
			}
			else
			{
				for (var i = 0; i < ShortIds.Count; i++)
				{
					var lsb = (uint) (ShortIds[i] & 0xffffffff);
					var msb = (ushort) ((ShortIds[i] >> 32) & 0xffff);
					stream.ReadWrite(ref lsb);
					stream.ReadWrite(ref msb);
				}
			}

			var txnSize = (ulong) PrefilledTransactions.Count;
			stream.ReadWriteAsVarInt(ref txnSize);

			if (!stream.Serializing)
			{
				ulong i = 0;
				ulong indicesCount = 0;
				while ((ulong) PrefilledTransactions.Count < txnSize)
				{
					indicesCount = Math.Min(1000UL + indicesCount, txnSize);
					for (; i < indicesCount; i++)
					{
						ulong index = 0;
						stream.ReadWriteAsVarInt(ref index);
						if (index > int.MaxValue)
						{
							throw new FormatException("indexes overflowed 32-bits");
						}

						Transaction tx = null;
						stream.ReadWrite(ref tx);
						PrefilledTransactions.Add(new PrefilledTransaction
						{
							Index = (int) index,
							Transaction = tx
						});
					}
				}

				var offset = 0;
				for (var ii = 0; ii < PrefilledTransactions.Count; ii++)
				{
					if ((ulong) PrefilledTransactions[ii].Index + (ulong) offset > int.MaxValue)
					{
						throw new FormatException("indexes overflowed 31-bits");
					}

					PrefilledTransactions[ii].Index = PrefilledTransactions[ii].Index + offset;
					offset = PrefilledTransactions[ii].Index + 1;
				}
			}
			else
			{
				for (var i = 0; i < PrefilledTransactions.Count; i++)
				{
					var index = checked((uint) (PrefilledTransactions[i].Index -
					                            (i == 0 ? 0 : PrefilledTransactions[i - 1].Index + 1)));
					stream.ReadWriteAsVarInt(ref index);
					var tx = PrefilledTransactions[i].Transaction;
					stream.ReadWrite(ref tx);
				}
			}

			if (!stream.Serializing)
			{
				UpdateShortTxIDSelector();
			}
		}

		private void UpdateShortTxIDSelector()
		{
			var ms = new MemoryStream();
			var stream = new BitcoinStream(ms, true);
			stream.ReadWrite(ref _header);
			stream.ReadWrite(ref _nonce);
			var shorttxidhash = new uint256(Hashes.SHA256(ms.ToArrayEfficient()));
			_shortTxidk0 = Hashes.SipHasher.GetULong(shorttxidhash, 0);
			_shortTxidk1 = Hashes.SipHasher.GetULong(shorttxidhash, 1);
		}

		public ulong AddTransactionShortId(Transaction tx)
		{
			return AddTransactionShortId(tx.GetHash());
		}

		public ulong AddTransactionShortId(uint256 txId)
		{
			var id = GetShortID(txId);
			ShortIds.Add(id);
			return id;
		}

		public ulong GetShortID(uint256 txId)
		{
			return Hashes.SipHash(_shortTxidk0, _shortTxidk1, txId) & 0xffffffffffffL;
		}
	}

	public class PrefilledTransaction
	{
		public Transaction Transaction { get; set; }

		public int Index { get; set; }
	}
}