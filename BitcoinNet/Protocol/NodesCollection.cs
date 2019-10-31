using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace BitcoinNet.Protocol
{
	public class NodeEventArgs : EventArgs
	{
		public NodeEventArgs(Node node, bool added)
		{
			Added = added;
			Node = node;
		}

		public bool Added { get; }

		public Node Node { get; }
	}

	public interface IReadOnlyNodesCollection : IEnumerable<Node>
	{
		event EventHandler<NodeEventArgs> Added;
		event EventHandler<NodeEventArgs> Removed;

		Node FindByEndpoint(IPEndPoint endpoint);
		Node FindByIp(IPAddress ip);
		Node FindLocal();
	}

	public class NodesCollection : IEnumerable<Node>, IReadOnlyNodesCollection
	{
		private readonly Bridge _bridge;
		private readonly ConcurrentDictionary<Node, Node> _nodes = new ConcurrentDictionary<Node, Node>();

		public NodesCollection()
		{
			_bridge = new Bridge(MessageProducer);
		}

		public MessageProducer<IncomingMessage> MessageProducer { get; } = new MessageProducer<IncomingMessage>();

		public int Count => _nodes.Count;

		// IEnumerable<Node> Members

		public IEnumerator<Node> GetEnumerator()
		{
			return _nodes.Select(n => n.Key).AsEnumerable().GetEnumerator();
		}

		// IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public event EventHandler<NodeEventArgs> Added;
		public event EventHandler<NodeEventArgs> Removed;

		public Node FindLocal()
		{
			return FindByIp(IPAddress.Loopback);
		}

		public Node FindByIp(IPAddress ip)
		{
			ip = ip.EnsureIPv6();
			return _nodes.Where(n => Match(ip, null, n.Key)).Select(s => s.Key).FirstOrDefault();
		}

		public Node FindByEndpoint(IPEndPoint endpoint)
		{
			var ip = endpoint.Address.EnsureIPv6();
			var port = endpoint.Port;
			return _nodes.Select(n => n.Key).FirstOrDefault(n => Match(ip, port, n));
		}

		public bool Add(Node node)
		{
			if (node == null)
			{
				throw new ArgumentNullException(nameof(node));
			}

			if (_nodes.TryAdd(node, node))
			{
				node.MessageProducer.AddMessageListener(_bridge);
				OnNodeAdded(node);
				return true;
			}

			return false;
		}

		public bool Remove(Node node)
		{
			if (_nodes.TryRemove(node, out var old))
			{
				node.MessageProducer.RemoveMessageListener(_bridge);
				OnNodeRemoved(old);
				return true;
			}

			return false;
		}

		private void OnNodeAdded(Node node)
		{
			var added = Added;
			if (added != null)
			{
				added(this, new NodeEventArgs(node, true));
			}
		}

		private void OnNodeRemoved(Node node)
		{
			var removed = Removed;
			if (removed != null)
			{
				removed(this, new NodeEventArgs(node, false));
			}
		}

		private static bool Match(IPAddress ip, int? port, Node n)
		{
			if (port.HasValue)
			{
				return n.State > NodeState.Disconnecting && n.RemoteSocketAddress.Equals(ip) &&
				       n.RemoteSocketPort == port.Value ||
				       n.PeerVersion.AddressFrom.Address.Equals(ip) && n.PeerVersion.AddressFrom.Port == port.Value;
			}

			return n.State > NodeState.Disconnecting && n.RemoteSocketAddress.Equals(ip) ||
			       n.PeerVersion.AddressFrom.Address.Equals(ip);
		}

		public void DisconnectAll(CancellationToken cancellation = default)
		{
			foreach (var node in _nodes)
			{
				node.Key.DisconnectAsync();
			}
		}

		public void Clear()
		{
			_nodes.Clear();
		}

		private class Bridge : IMessageListener<IncomingMessage>
		{
			private readonly MessageProducer<IncomingMessage> _prod;

			public Bridge(MessageProducer<IncomingMessage> prod)
			{
				_prod = prod;
			}

			// IMessageListener<IncomingMessage> Members

			public void PushMessage(IncomingMessage message)
			{
				_prod.PushMessage(message);
			}
		}
	}
}