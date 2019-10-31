using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BitcoinNet.Protocol.Behaviors
{
	/// <summary>
	///     The Chain Behavior is responsible for keeping a ConcurrentChain up to date with the peer, it also responds to
	///     getheaders messages.
	/// </summary>
	public class ChainBehavior : NodeBehavior
	{
		private ConcurrentChain _chain;

		private ChainedBlock
			_pendingTip; //Might be different than Chain.Tip, in the rare event of large fork > 2000 blocks

		private Timer _refresh;
		private int _synchingCount;

		public ChainBehavior(ConcurrentChain chain)
		{
			if (chain == null)
			{
				throw new ArgumentNullException(nameof(chain));
			}

			SharedState = new State();
			_chain = chain;
			AutoSync = true;
			CanSync = true;
			CanRespondToGetHeaders = true;
		}

		/// <summary>
		///     If true, the Chain maintained by the behavior with have its ChainedBlock with no Header (default: false)
		/// </summary>
		public bool StripHeader { get; set; }

		/// <summary>
		///     If true, skip PoW checks (default: false)
		/// </summary>
		public bool SkipPoWCheck { get; set; }

		public State SharedState { get; private set; }

		/// <summary>
		///     Keep the chain in Sync (Default : true)
		/// </summary>
		public bool CanSync { get; set; }

		/// <summary>
		///     Respond to getheaders messages (Default : true)
		/// </summary>
		public bool CanRespondToGetHeaders { get; set; }

		public ConcurrentChain Chain
		{
			get => _chain;
			set
			{
				AssertNotAttached();
				_chain = value;
			}
		}

		/// <summary>
		///     Using for test, this might not be reliable
		/// </summary>
		internal bool Synching => _synchingCount != 0;

		/// <summary>
		///     Sync the chain as headers come from the network (Default : true)
		/// </summary>
		public bool AutoSync { get; set; }

		public bool InvalidHeaderReceived { get; private set; }

		public ChainedBlock PendingTip
		{
			get
			{
				var tip = _pendingTip;
				if (tip == null)
				{
					return null;
				}

				//Prevent memory leak by returning a block from the chain instead of real pending tip of possible
				return Chain.GetBlock(tip.HashBlock) ?? tip;
			}
		}

		protected override void AttachCore()
		{
			_refresh = new Timer(o =>
			{
				if (AutoSync)
				{
					TrySync();
				}
			}, null, 0, (int) TimeSpan.FromMinutes(10).TotalMilliseconds);
			RegisterDisposable(_refresh);
			if (AttachedNode.State == NodeState.Connected)
			{
				var highPoW = SharedState.HighestValidatedPoW;
				AttachedNode.MyVersion.StartHeight = highPoW == null ? Chain.Height : highPoW.Height;
			}

			AttachedNode.StateChanged += AttachedNode_StateChanged;
			RegisterDisposable(AttachedNode.Filters.Add(Intercept));
		}

		private void Intercept(IncomingMessage message, Action act)
		{
			var inv = message.Message.Payload as InvPayload;
			if (inv != null)
			{
				if (inv.Inventory.Any(i => (i.Type & InventoryType.MSG_BLOCK) != 0 && !Chain.Contains(i.Hash)))
				{
					_refresh.Dispose(); //No need of periodical refresh, the peer is notifying us
					if (AutoSync)
					{
						TrySync();
					}
				}
			}

			var getheaders = message.Message.Payload as GetHeadersPayload;
			if (getheaders != null && CanRespondToGetHeaders && !StripHeader)
			{
				var headers = new HeadersPayload();
				var highestPow = SharedState.HighestValidatedPoW;
				highestPow = highestPow == null ? null : Chain.GetBlock(highestPow.HashBlock);
				var fork = Chain.FindFork(getheaders.BlockLocators);
				if (fork != null)
				{
					if (highestPow != null && fork.Height > highestPow.Height)
					{
						fork = null; //fork not yet validated
					}

					if (fork != null)
					{
						foreach (var header in Chain.EnumerateToTip(fork).Skip(1))
						{
							if (highestPow != null && header.Height > highestPow.Height)
							{
								break;
							}

							headers.Headers.Add(header.Header);
							if (header.HashBlock == getheaders.HashStop || headers.Headers.Count == 2000)
							{
								break;
							}
						}
					}
				}

				AttachedNode.SendMessageAsync(headers);
			}

			var newHeaders = message.Message.Payload as HeadersPayload;
			var pendingTipBefore = GetPendingTipOrChainTip();
			if (newHeaders != null && CanSync)
			{
				var tip = GetPendingTipOrChainTip();
				foreach (var header in newHeaders.Headers)
				{
					var prev = tip.FindAncestorOrSelf(header.HashPrevBlock);
					if (prev == null)
					{
						break;
					}

					tip = new ChainedBlock(header, header.GetHash(), prev);
					var validated = Chain.GetBlock(tip.HashBlock) != null || SkipPoWCheck ||
					                tip.Validate(AttachedNode.Network);
					validated &= !SharedState.IsMarkedInvalid(tip.HashBlock);
					if (!validated)
					{
						InvalidHeaderReceived = true;
						break;
					}

					_pendingTip = tip;
				}

				var isHigherBlock = false;
				if (SkipPoWCheck)
				{
					isHigherBlock = _pendingTip.Height > Chain.Tip.Height;
				}
				else
				{
					isHigherBlock = _pendingTip.GetChainWork(true) > Chain.Tip.GetChainWork(true);
				}

				if (isHigherBlock)
				{
					Chain.SetTip(_pendingTip);
					if (StripHeader)
					{
						_pendingTip.StripHeader();
					}
				}

				var chainedPendingTip = Chain.GetBlock(_pendingTip.HashBlock);
				if (chainedPendingTip != null)
				{
					_pendingTip =
						chainedPendingTip; //This allows garbage collection to collect the duplicated pendingtip and ancestors
				}

				if (newHeaders.Headers.Count != 0 && pendingTipBefore.HashBlock != GetPendingTipOrChainTip().HashBlock)
				{
					TrySync();
				}

				Interlocked.Decrement(ref _synchingCount);
			}

			act();
		}

		/// <summary>
		///     Check if any past blocks announced by this peer is in the invalid blocks list, and set InvalidHeaderReceived flag
		///     accordingly
		/// </summary>
		/// <returns>True if no invalid block is received</returns>
		public bool CheckAnnouncedBlocks()
		{
			var tip = _pendingTip;
			if (tip != null && !InvalidHeaderReceived)
			{
				try
				{
					SharedState.InvalidBlocksLock.EnterReadLock();
					if (SharedState.InvalidBlocks.Count != 0)
					{
						foreach (var header in tip.EnumerateToGenesis())
						{
							if (InvalidHeaderReceived)
							{
								break;
							}

							InvalidHeaderReceived |= SharedState.InvalidBlocks.Contains(header.HashBlock);
						}
					}
				}
				finally
				{
					SharedState.InvalidBlocksLock.ExitReadLock();
				}
			}

			return !InvalidHeaderReceived;
		}

		private void AttachedNode_StateChanged(Node node, NodeState oldState)
		{
			TrySync();
		}

		/// <summary>
		///     Asynchronously try to sync the chain
		/// </summary>
		public void TrySync()
		{
			var node = AttachedNode;
			if (node != null)
			{
				if (node.State == NodeState.HandShaked && CanSync && !InvalidHeaderReceived)
				{
					Interlocked.Increment(ref _synchingCount);
					node.SendMessageAsync(new GetHeadersPayload
					{
						BlockLocators = GetPendingTipOrChainTip().GetLocator()
					});
				}
			}
		}

		private ChainedBlock GetPendingTipOrChainTip()
		{
			_pendingTip = _pendingTip ?? Chain.Tip;
			return _pendingTip;
		}

		protected override void DetachCore()
		{
			AttachedNode.StateChanged -= AttachedNode_StateChanged;
		}

		// ICloneable Members

		public override object Clone()
		{
			var clone = new ChainBehavior(Chain)
			{
				CanSync = CanSync,
				CanRespondToGetHeaders = CanRespondToGetHeaders,
				AutoSync = AutoSync,
				SkipPoWCheck = SkipPoWCheck,
				StripHeader = StripHeader,
				SharedState = SharedState
			};
			return clone;
		}

		public class State
		{
			internal readonly HashSet<uint256> InvalidBlocks = new HashSet<uint256>();

			internal readonly ReaderWriterLockSlim InvalidBlocksLock =
				new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

			/// <summary>
			///     ChainBehaviors sharing this state will not broadcast headers which are above HighestValidatedPoW
			/// </summary>
			public ChainedBlock HighestValidatedPoW { get; set; }

			public bool IsMarkedInvalid(uint256 hashBlock)
			{
				try
				{
					InvalidBlocksLock.EnterReadLock();
					return InvalidBlocks.Contains(hashBlock);
				}
				finally
				{
					InvalidBlocksLock.ExitReadLock();
				}
			}

			public void MarkBlockInvalid(uint256 blockHash)
			{
				try
				{
					InvalidBlocksLock.EnterWriteLock();
					InvalidBlocks.Add(blockHash);
				}
				finally
				{
					InvalidBlocksLock.ExitWriteLock();
				}
			}
		}
	}
}