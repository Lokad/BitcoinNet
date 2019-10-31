using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BitcoinNet.Protocol
{
	public interface IMessageListener<in T>
	{
		void PushMessage(T message);
	}

	public class NullMessageListener<T> : IMessageListener<T>
	{
		// IMessageListener<T> Members

		public void PushMessage(T message)
		{
		}
	}

	public class NewThreadMessageListener<T> : IMessageListener<T>
	{
		private readonly Action<T> _process;

		public NewThreadMessageListener(Action<T> process)
		{
			if (process == null)
			{
				throw new ArgumentNullException(nameof(process));
			}

			_process = process;
		}

		// IMessageListener<T> Members

		public void PushMessage(T message)
		{
			if (message != null)
			{
				Task.Factory.StartNew(() =>
				{
					try
					{
						_process(message);
					}
					catch (Exception ex)
					{
						NodeServerTrace.Error("Unexpected expected during message loop", ex);
					}
				});
			}
		}
	}

	public class EventLoopMessageListener<T> : IMessageListener<T>, IDisposable
	{
		// IDisposable Members

		private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();

		public EventLoopMessageListener(Action<T> processMessage)
		{
			new Thread(() =>
			{
				try
				{
					while (!_cancellationSource.IsCancellationRequested)
					{
						var message = MessageQueue.Take(_cancellationSource.Token);
						if (message != null)
						{
							try
							{
								processMessage(message);
							}
							catch (Exception ex)
							{
								NodeServerTrace.Error("Unexpected expected during message loop", ex);
							}
						}
					}
				}
				catch (OperationCanceledException)
				{
				}
			}).Start();
		}

		public BlockingCollection<T> MessageQueue { get; } = new BlockingCollection<T>(new ConcurrentQueue<T>());

		public void Dispose()
		{
			if (_cancellationSource.IsCancellationRequested)
			{
				return;
			}

			_cancellationSource.Cancel();
		}

		// IMessageListener Members

		public void PushMessage(T message)
		{
			MessageQueue.Add(message);
		}
	}

	public class PollMessageListener<T> : IMessageListener<T>
	{
		public BlockingCollection<T> MessageQueue { get; } = new BlockingCollection<T>(new ConcurrentQueue<T>());

		// IMessageListener Members

		public virtual void PushMessage(T message)
		{
			MessageQueue.Add(message);
		}

		public virtual T ReceiveMessage(CancellationToken cancellationToken = default)
		{
			return MessageQueue.Take(cancellationToken);
		}
	}
}