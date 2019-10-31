using System;
using System.Collections.Generic;

namespace BitcoinNet.Protocol.Behaviors
{
	public interface INodeBehavior
	{
		Node AttachedNode { get; }

		void Attach(Node node);
		void Detach();
		INodeBehavior Clone();
	}

	public abstract class NodeBehavior : INodeBehavior
	{
		private readonly object _cs = new object();
		private readonly List<IDisposable> _disposables = new List<IDisposable>();

		public Node AttachedNode { get; private set; }

		public void Attach(Node node)
		{
			if (node == null)
			{
				throw new ArgumentNullException(nameof(node));
			}

			if (AttachedNode != null)
			{
				throw new InvalidOperationException("Behavior already attached to a node");
			}

			lock (_cs)
			{
				AttachedNode = node;
				if (Disconnected(node))
				{
					return;
				}

				AttachCore();
			}
		}

		public void Detach()
		{
			lock (_cs)
			{
				if (AttachedNode == null)
				{
					return;
				}

				DetachCore();
				foreach (var dispo in _disposables)
				{
					dispo.Dispose();
				}

				_disposables.Clear();
				AttachedNode = null;
			}
		}

		INodeBehavior INodeBehavior.Clone()
		{
			return (INodeBehavior) Clone();
		}

		protected void RegisterDisposable(IDisposable disposable)
		{
			_disposables.Add(disposable);
		}

		protected void AssertNotAttached()
		{
			if (AttachedNode != null)
			{
				throw new InvalidOperationException("Can't modify the behavior while it is attached");
			}
		}

		private static bool Disconnected(Node node)
		{
			return node.State == NodeState.Disconnecting || node.State == NodeState.Failed ||
			       node.State == NodeState.Offline;
		}

		protected abstract void AttachCore();

		protected abstract void DetachCore();

		public abstract object Clone();
	}
}