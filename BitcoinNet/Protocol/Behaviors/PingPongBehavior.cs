using System;
using System.Threading;

namespace BitcoinNet.Protocol.Behaviors
{
	[Flags]
	public enum PingPongMode
	{
		SendPing = 1,
		RespondPong = 2,
		Both = 3
	}

	/// <summary>
	///     The PingPongBehavior is responsible for firing ping message every PingInterval and responding with pong message,
	///     and close the connection if the Ping has not been completed after TimeoutInterval.
	/// </summary>
	public class PingPongBehavior : NodeBehavior
	{
		private readonly object _cs = new object();
		private volatile PingPayload _currentPing;
		private DateTimeOffset _dateSent;
		private PingPongMode _mode;
		private TimeSpan _pingInterval;
		private Timer _pingTimeoutTimer;
		private TimeSpan _timeoutInterval;

		public PingPongBehavior()
		{
			Mode = PingPongMode.Both;
			TimeoutInterval =
				TimeSpan.FromMinutes(
					20.0); //Long time, if in middle of download of a large bunch of blocks, it can takes time
			PingInterval = TimeSpan.FromMinutes(2.0);
		}

		/// <summary>
		///     Whether the behavior send Ping and respond with Pong (Default : Both)
		/// </summary>
		public PingPongMode Mode
		{
			get => _mode;
			set
			{
				AssertNotAttached();
				_mode = value;
			}
		}

		/// <summary>
		///     Interval after which an unresponded Ping will result in a disconnection. (Default : 20 minutes)
		/// </summary>
		public TimeSpan TimeoutInterval
		{
			get => _timeoutInterval;
			set
			{
				AssertNotAttached();
				_timeoutInterval = value;
			}
		}

		/// <summary>
		///     Interval after which a Ping message is fired after the last received Pong (Default : 2 minutes)
		/// </summary>
		public TimeSpan PingInterval
		{
			get => _pingInterval;
			set
			{
				AssertNotAttached();
				_pingInterval = value;
			}
		}

		public TimeSpan Latency { get; private set; }

		protected override void AttachCore()
		{
			if (AttachedNode.PeerVersion != null && !PingVersion()
			) //If not handshaked, stil attach (the callback will also check version)
			{
				return;
			}

			AttachedNode.MessageReceived += AttachedNode_MessageReceived;
			AttachedNode.StateChanged += AttachedNode_StateChanged;
			RegisterDisposable(new Timer(Ping, null, 0, (int) PingInterval.TotalMilliseconds));
		}

		private bool PingVersion()
		{
			var node = AttachedNode;
			return node != null && node.ProtocolCapabilities.SupportPingPong;
		}

		private void AttachedNode_StateChanged(Node node, NodeState oldState)
		{
			if (node.State == NodeState.HandShaked)
			{
				Ping(null);
			}
		}

		private void Ping(object unused)
		{
			if (Monitor.TryEnter(_cs))
			{
				try
				{
					var node = AttachedNode;
					if (node == null)
					{
						return;
					}

					if (!PingVersion())
					{
						return;
					}

					if (node.State != NodeState.HandShaked)
					{
						return;
					}

					if (_currentPing != null)
					{
						return;
					}

					_currentPing = new PingPayload();
					_dateSent = DateTimeOffset.UtcNow;
					node.SendMessageAsync(_currentPing);
					_pingTimeoutTimer = new Timer(PingTimeout, _currentPing, (int) TimeoutInterval.TotalMilliseconds,
						Timeout.Infinite);
				}
				finally
				{
					Monitor.Exit(_cs);
				}
			}
		}

		/// <summary>
		///     Send a ping asynchronously
		/// </summary>
		public void Probe()
		{
			Ping(null);
		}

		private void PingTimeout(object ping)
		{
			var node = AttachedNode;
			if (node != null && (PingPayload) ping == _currentPing)
			{
				node.DisconnectAsync("Pong timeout for " + ((PingPayload) ping).Nonce);
			}
		}

		private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
		{
			if (!PingVersion())
			{
				return;
			}

			var ping = message.Message.Payload as PingPayload;
			if (ping != null && Mode.HasFlag(PingPongMode.RespondPong))
			{
				node.SendMessageAsync(new PongPayload
				{
					Nonce = ping.Nonce
				});
			}

			var pong = message.Message.Payload as PongPayload;
			if (pong != null &&
			    Mode.HasFlag(PingPongMode.SendPing) &&
			    _currentPing != null &&
			    _currentPing.Nonce == pong.Nonce)
			{
				Latency = DateTimeOffset.UtcNow - _dateSent;
				ClearCurrentPing();
			}
		}

		private void ClearCurrentPing()
		{
			lock (_cs)
			{
				_currentPing = null;
				_dateSent = default;
				var timeout = _pingTimeoutTimer;
				if (timeout != null)
				{
					timeout.Dispose();
					_pingTimeoutTimer = null;
				}
			}
		}

		protected override void DetachCore()
		{
			AttachedNode.MessageReceived -= AttachedNode_MessageReceived;
			AttachedNode.StateChanged -= AttachedNode_StateChanged;
			ClearCurrentPing();
		}

		// ICloneable Members

		public override object Clone()
		{
			return new PingPongBehavior
			{
				Mode = Mode,
				PingInterval = PingInterval,
				TimeoutInterval = TimeoutInterval
			};
		}
	}
}