using System;

namespace BitcoinNet.Protocol.Filters
{
	public class ActionFilter : INodeFilter
	{
		private readonly Action<IncomingMessage, Action> _onIncoming;
		private readonly Action<Node, Payload, Action> _onSending;

		public ActionFilter(Action<IncomingMessage, Action> onIncoming = null,
			Action<Node, Payload, Action> onSending = null)
		{
			_onIncoming = onIncoming ?? ((m, n) => n());
			_onSending = onSending ?? ((m, p, n) => n());
		}

		// INodeFilter Members

		public void OnReceivingMessage(IncomingMessage message, Action next)
		{
			_onIncoming(message, next);
		}

		public void OnSendingMessage(Node node, Payload payload, Action next)
		{
			_onSending(node, payload, next);
		}
	}
}