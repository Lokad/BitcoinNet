using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BitcoinNet.Protocol.Behaviors
{
	public delegate void TransactionBroadcastedDelegate(Transaction transaction);

	public delegate void TransactionRejectedDelegate(Transaction transaction, RejectPayload reject);

	public class TransactionBroadcast
	{
		public BroadcastState State { get; internal set; }

		public Transaction Transaction { get; internal set; }

		internal ulong PingValue { get; set; }

		public DateTime AnnouncedTime { get; internal set; }
	}

	public enum BroadcastState
	{
		NotSent,
		Announced,
		Broadcasted,
		Rejected,
		Accepted
	}

	public class BroadcastHub
	{
		internal readonly ConcurrentDictionary<uint256, Transaction> BroadcastedTransaction =
			new ConcurrentDictionary<uint256, Transaction>();

		internal readonly ConcurrentDictionary<Node, Node> Nodes = new ConcurrentDictionary<Node, Node>();

		public IEnumerable<Transaction> BroadcastingTransactions => BroadcastedTransaction.Values;

		/// <summary>
		///     If true, the user need to call BroadcastTransactions to ask to the nodes to broadcast it
		/// </summary>
		public bool ManualBroadcast { get; set; } = false;

		public static BroadcastHub GetBroadcastHub(Node node)
		{
			return GetBroadcastHub(node.Behaviors);
		}

		public static BroadcastHub GetBroadcastHub(NodeConnectionParameters parameters)
		{
			return GetBroadcastHub(parameters.TemplateBehaviors);
		}

		public static BroadcastHub GetBroadcastHub(NodeBehaviorsCollection behaviors)
		{
			return behaviors.OfType<BroadcastHubBehavior>().Select(c => c.BroadcastHub).FirstOrDefault();
		}

		public event TransactionBroadcastedDelegate TransactionBroadcasted;
		public event TransactionRejectedDelegate TransactionRejected;

		internal void OnBroadcastTransaction(Transaction transaction)
		{
			var nodes = Nodes
				.Select(n => n.Key.Behaviors.Find<BroadcastHubBehavior>())
				.Where(n => n != null)
				.ToArray();
			foreach (var node in nodes)
			{
				node.BroadcastTransactionCore(transaction);
			}
		}

		internal void OnTransactionRejected(Transaction tx, RejectPayload reject)
		{
			var evt = TransactionRejected;
			if (evt != null)
			{
				evt(tx, reject);
			}
		}

		internal void OnTransactionBroadcasted(Transaction tx)
		{
			var evt = TransactionBroadcasted;
			if (evt != null)
			{
				evt(tx);
			}
		}

		/// <summary>
		///     Broadcast a transaction on the hub
		/// </summary>
		/// <param name="transaction">The transaction to broadcast</param>
		/// <returns>The cause of the rejection or null</returns>
		public Task<RejectPayload> BroadcastTransactionAsync(Transaction transaction)
		{
			if (transaction == null)
			{
				throw new ArgumentNullException(nameof(transaction));
			}

			var completion = new TaskCompletionSource<RejectPayload>();
			var hash = transaction.GetHash();
			if (BroadcastedTransaction.TryAdd(hash, transaction))
			{
				TransactionBroadcastedDelegate broadcasted = null;
				TransactionRejectedDelegate rejected = null;
				broadcasted = t =>
				{
					if (t.GetHash() == hash)
					{
						completion.SetResult(null);
						TransactionRejected -= rejected;
						TransactionBroadcasted -= broadcasted;
					}
				};
				TransactionBroadcasted += broadcasted;
				rejected = (t, r) =>
				{
					if (r.Hash == hash)
					{
						completion.SetResult(r);
						TransactionRejected -= rejected;
						TransactionBroadcasted -= broadcasted;
					}
				};
				TransactionRejected += rejected;
				OnBroadcastTransaction(transaction);
			}

			return completion.Task;
		}

		/// <summary>
		///     Ask the nodes in the hub to broadcast transactions in the Hub manually
		/// </summary>
		public void BroadcastTransactions()
		{
			if (!ManualBroadcast)
			{
				throw new InvalidOperationException("ManualBroadcast should be true to call this method");
			}

			var nodes = Nodes
				.Select(n => n.Key.Behaviors.Find<BroadcastHubBehavior>())
				.Where(n => n != null)
				.ToArray();
			foreach (var node in nodes)
			{
				node.AnnounceAll(true);
			}
		}

		public BroadcastHubBehavior CreateBehavior()
		{
			return new BroadcastHubBehavior(this);
		}
	}

	public class BroadcastHubBehavior : NodeBehavior
	{
		private readonly ConcurrentDictionary<uint256, TransactionBroadcast> _hashToTransaction =
			new ConcurrentDictionary<uint256, TransactionBroadcast>();

		private readonly ConcurrentDictionary<ulong, TransactionBroadcast> _pingToTransaction =
			new ConcurrentDictionary<ulong, TransactionBroadcast>();

		private Timer _flush;

		public BroadcastHubBehavior()
		{
			BroadcastHub = new BroadcastHub();
		}

		public BroadcastHubBehavior(BroadcastHub hub)
		{
			BroadcastHub = hub ?? new BroadcastHub();
			foreach (var tx in BroadcastHub.BroadcastedTransaction)
			{
				_hashToTransaction.TryAdd(tx.Key, new TransactionBroadcast
				{
					State = BroadcastState.NotSent,
					Transaction = tx.Value
				});
			}
		}

		public BroadcastHub BroadcastHub { get; }

		public IEnumerable<TransactionBroadcast> Broadcasts => _hashToTransaction.Values;

		private TransactionBroadcast GetTransaction(uint256 hash, bool remove)
		{
			TransactionBroadcast result;

			if (remove)
			{
				if (_hashToTransaction.TryRemove(hash, out result))
				{
					TransactionBroadcast unused;
					_pingToTransaction.TryRemove(result.PingValue, out unused);
				}
			}
			else
			{
				_hashToTransaction.TryGetValue(hash, out result);
			}

			return result;
		}

		private TransactionBroadcast GetTransaction(ulong pingValue, bool remove)
		{
			TransactionBroadcast result;

			if (remove)
			{
				if (_pingToTransaction.TryRemove(pingValue, out result))
				{
					TransactionBroadcast unused;
					_hashToTransaction.TryRemove(result.Transaction.GetHash(), out unused);
				}
			}
			else
			{
				_pingToTransaction.TryGetValue(pingValue, out result);
			}

			return result;
		}

		private void AttachedNode_StateChanged(Node node, NodeState oldState)
		{
			if (node.State == NodeState.HandShaked)
			{
				BroadcastHub.Nodes.TryAdd(node, node);
				AnnounceAll();
			}
		}

		internal void AnnounceAll(bool force = false)
		{
			foreach (var broadcasted in _hashToTransaction)
			{
				if (broadcasted.Value.State == BroadcastState.NotSent ||
				    DateTime.UtcNow - broadcasted.Value.AnnouncedTime < TimeSpan.FromMinutes(5.0))
				{
					Announce(broadcasted.Value, broadcasted.Key, force);
				}
			}
		}

		internal void BroadcastTransactionCore(Transaction transaction)
		{
			if (transaction == null)
			{
				throw new ArgumentNullException(nameof(transaction));
			}

			var tx = new TransactionBroadcast();
			tx.Transaction = transaction;
			tx.State = BroadcastState.NotSent;
			var hash = transaction.GetHash();
			if (_hashToTransaction.TryAdd(hash, tx))
			{
				Announce(tx, hash);
			}
		}

		internal void Announce(TransactionBroadcast tx, uint256 hash, bool force = false)
		{
			if (!force && BroadcastHub.ManualBroadcast)
			{
				return;
			}

			var node = AttachedNode;
			if (node != null && node.State == NodeState.HandShaked)
			{
				tx.State = BroadcastState.Announced;
				tx.AnnouncedTime = DateTime.UtcNow;
				node.SendMessageAsync(new InvPayload(InventoryType.MSG_TX, hash)).ConfigureAwait(false);
			}
		}

		protected override void AttachCore()
		{
			AttachedNode.StateChanged += AttachedNode_StateChanged;
			AttachedNode.MessageReceived += AttachedNode_MessageReceived;
			_flush = new Timer(o => { AnnounceAll(); }, null, 0, (int) TimeSpan.FromMinutes(10).TotalMilliseconds);
		}

		protected override void DetachCore()
		{
			AttachedNode.StateChanged -= AttachedNode_StateChanged;
			AttachedNode.MessageReceived -= AttachedNode_MessageReceived;

			Node unused;
			BroadcastHub.Nodes.TryRemove(AttachedNode, out unused);
			_flush.Dispose();
		}

		private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
		{
			var invPayload = message.Message.Payload as InvPayload;
			if (invPayload != null)
			{
				foreach (var hash in invPayload.Where(i => i.Type == InventoryType.MSG_TX).Select(i => i.Hash))
				{
					var tx = GetTransaction(hash, true);
					if (tx != null)
					{
						tx.State = BroadcastState.Accepted;
					}

					Transaction unused;
					if (BroadcastHub.BroadcastedTransaction.TryRemove(hash, out unused))
					{
						BroadcastHub.OnTransactionBroadcasted(tx.Transaction);
					}
				}
			}

			var reject = message.Message.Payload as RejectPayload;
			if (reject != null && reject.Message == "tx")
			{
				var tx = GetTransaction(reject.Hash, true);
				if (tx != null)
				{
					tx.State = BroadcastState.Rejected;
				}

				Transaction tx2;
				if (BroadcastHub.BroadcastedTransaction.TryRemove(reject.Hash, out tx2))
				{
					BroadcastHub.OnTransactionRejected(tx2, reject);
				}
			}

			var getData = message.Message.Payload as GetDataPayload;
			if (getData != null)
			{
				foreach (var inventory in getData.Inventory.Where(i => i.Type == InventoryType.MSG_TX))
				{
					var tx = GetTransaction(inventory.Hash, false);
					if (tx != null)
					{
						tx.State = BroadcastState.Broadcasted;
						var ping = new PingPayload();
						tx.PingValue = ping.Nonce;
						_pingToTransaction.TryAdd(tx.PingValue, tx);
						node.SendMessageAsync(new TxPayload(tx.Transaction));
						node.SendMessageAsync(ping);
					}
				}
			}

			var pong = message.Message.Payload as PongPayload;
			if (pong != null)
			{
				var tx = GetTransaction(pong.Nonce, true);
				if (tx != null)
				{
					tx.State = BroadcastState.Accepted;
					Transaction unused;
					if (BroadcastHub.BroadcastedTransaction.TryRemove(tx.Transaction.GetHash(), out unused))
					{
						BroadcastHub.OnTransactionBroadcasted(tx.Transaction);
					}
				}
			}
		}

		public override object Clone()
		{
			return new BroadcastHubBehavior(BroadcastHub);
		}
	}
}