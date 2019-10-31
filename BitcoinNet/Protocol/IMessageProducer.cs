using System;
using System.Collections.Generic;

namespace BitcoinNet.Protocol
{
	public class MessageProducer<T>
	{
		private readonly List<IMessageListener<T>> _listeners = new List<IMessageListener<T>>();

		public IDisposable AddMessageListener(IMessageListener<T> listener)
		{
			if (listener == null)
			{
				throw new ArgumentNullException(nameof(listener));
			}

			lock (_listeners)
			{
				return new Scope(() => { _listeners.Add(listener); }, () =>
				{
					lock (_listeners)
					{
						_listeners.Remove(listener);
					}
				});
			}
		}

		public void RemoveMessageListener(IMessageListener<T> listener)
		{
			if (listener == null)
			{
				throw new ArgumentNullException(nameof(listener));
			}

			lock (_listeners)
			{
				_listeners.Add(listener);
			}
		}

		public void PushMessage(T message)
		{
			if (message == null)
			{
				throw new ArgumentNullException(nameof(message));
			}

			lock (_listeners)
			{
				foreach (var listener in _listeners)
				{
					listener.PushMessage(message);
				}
			}
		}

		public void PushMessages(IEnumerable<T> messages)
		{
			if (messages == null)
			{
				throw new ArgumentNullException(nameof(messages));
			}

			lock (_listeners)
			{
				foreach (var message in messages)
				{
					if (message == null)
					{
						throw new ArgumentNullException(nameof(message));
					}

					foreach (var listener in _listeners)
					{
						listener.PushMessage(message);
					}
				}
			}
		}
	}
}