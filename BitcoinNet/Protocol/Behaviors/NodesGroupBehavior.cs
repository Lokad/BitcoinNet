namespace BitcoinNet.Protocol.Behaviors
{
	/// <summary>
	///     Maintain connection to a given set of nodes
	/// </summary>
	internal class NodesGroupBehavior : NodeBehavior
	{
		internal readonly NodesGroup _parent;

		public NodesGroupBehavior(NodesGroup parent)
		{
			_parent = parent;
		}

		private NodesGroupBehavior()
		{
		}

		protected override void AttachCore()
		{
			AttachedNode.StateChanged += AttachedNode_StateChanged;
		}

		protected override void DetachCore()
		{
			AttachedNode.StateChanged -= AttachedNode_StateChanged;
		}

		private void AttachedNode_StateChanged(Node node, NodeState oldState)
		{
			if (node.State == NodeState.HandShaked)
			{
				_parent.ConnectedNodes.Add(node);
			}

			if (node.State == NodeState.Failed || node.State == NodeState.Disconnecting ||
			    node.State == NodeState.Offline)
			{
				if (_parent.ConnectedNodes.Remove(node))
				{
					_parent.StartConnecting();
				}
			}
		}

		// ICloneable Members

		public override object Clone()
		{
			return new NodesGroupBehavior(_parent);
		}
	}
}