using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BitcoinNet.Protocol
{
	public class NodeListener : PollMessageListener<IncomingMessage>, IDisposable
	{
		private readonly List<Func<IncomingMessage, bool>> _predicates = new List<Func<IncomingMessage, bool>>();
		private readonly IDisposable _subscription;

		public NodeListener(Node node)
		{
			_subscription = node.MessageProducer.AddMessageListener(this);
			Node = node;
		}

		public Node Node { get; }

		// IDisposable Members

		public void Dispose()
		{
			if (_subscription != null)
			{
				_subscription.Dispose();
			}
		}

		public NodeListener Where(Func<IncomingMessage, bool> predicate)
		{
			_predicates.Add(predicate);
			return this;
		}

		public NodeListener OfType<TPayload>() where TPayload : Payload
		{
			_predicates.Add(i => i.Message.Payload is TPayload);
			return this;
		}

		public TPayload ReceivePayload<TPayload>(CancellationToken cancellationToken = default)
			where TPayload : Payload
		{
			if (!Node.IsConnected)
			{
				throw new InvalidOperationException("The node is not in a connected state");
			}

			var pushedAside = new Queue<IncomingMessage>();
			try
			{
				while (true)
				{
					var message = ReceiveMessage(CancellationTokenSource
						.CreateLinkedTokenSource(cancellationToken, Node._connection.Cancel.Token).Token);
					if (_predicates.All(p => p(message)))
					{
						if (message.Message.Payload is TPayload)
						{
							return (TPayload) message.Message.Payload;
						}
						else
						{
							pushedAside.Enqueue(message);
						}
					}
				}
			}
			catch (OperationCanceledException)
			{
				if (Node._connection.Cancel.IsCancellationRequested)
				{
					throw new InvalidOperationException("The node is not in a connected state");
				}

				throw;
			}
			finally
			{
				while (pushedAside.Count != 0)
				{
					PushMessage(pushedAside.Dequeue());
				}
			}
		}
	}
}