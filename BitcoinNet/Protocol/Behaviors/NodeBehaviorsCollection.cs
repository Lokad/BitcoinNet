namespace BitcoinNet.Protocol.Behaviors
{
	public class NodeBehaviorsCollection : ThreadSafeCollection<INodeBehavior>
	{
		private readonly Node _node;
		private bool _delayAttach;

		public NodeBehaviorsCollection(Node node)
		{
			_node = node;
		}

		private bool CanAttach => _node != null && !DelayAttach && _node.State != NodeState.Offline &&
		                          _node.State != NodeState.Failed && _node.State != NodeState.Disconnecting;

		internal bool DelayAttach
		{
			get => _delayAttach;
			set
			{
				_delayAttach = value;
				if (CanAttach)
				{
					foreach (var b in this)
					{
						b.Attach(_node);
					}
				}
			}
		}

		protected override void OnAdding(INodeBehavior obj)
		{
			if (CanAttach)
			{
				obj.Attach(_node);
			}
		}

		protected override void OnRemoved(INodeBehavior obj)
		{
			if (obj.AttachedNode != null)
			{
				obj.Detach();
			}
		}
	}
}