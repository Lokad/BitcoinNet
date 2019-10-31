using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BitcoinNet.Protocol
{
	public delegate void NodeServerNodeEventHandler(NodeServer sender, Node node);

	public delegate void NodeServerMessageEventHandler(NodeServer sender, IncomingMessage message);

	public class NodeServer : IDisposable
	{
		private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
		internal readonly MessageProducer<object> _internalMessageProducer = new MessageProducer<object>();
		private readonly MessageProducer<IncomingMessage> _messageProducer = new MessageProducer<IncomingMessage>();
		private readonly List<IDisposable> _resources = new List<IDisposable>();
		private readonly TraceCorrelation _trace;
		private volatile IPEndPoint _externalEndpoint;
		private IPEndPoint _localEndpoint;
		private ulong _nonce;
		private Socket _socket;

		public NodeServer(Network network, uint? version = null, int internalPort = -1)
		{
			AllowLocalPeers = true;
			InboundNodeConnectionParameters = new NodeConnectionParameters();
			internalPort = internalPort == -1 ? network.DefaultPort : internalPort;
			_localEndpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0").MapToIPv6Ex(), internalPort);
			MaxConnections = 125;
			Network = network;
			_externalEndpoint = new IPEndPoint(_localEndpoint.Address, Network.DefaultPort);
			Version = version ?? network.MaxP2PVersion;
			var listener = new EventLoopMessageListener<IncomingMessage>(ProcessMessage);
			_messageProducer.AddMessageListener(listener);
			OwnResource(listener);
			ConnectedNodes = new NodesCollection();
			ConnectedNodes.Added += _Nodes_NodeAdded;
			ConnectedNodes.Removed += _Nodes_NodeRemoved;
			ConnectedNodes.MessageProducer.AddMessageListener(listener);
			_trace = new TraceCorrelation(NodeServerTrace.Trace, "Node server listening on " + LocalEndpoint);
		}

		public Network Network { get; }

		public uint Version { get; }

		/// <summary>
		///     The parameters that will be cloned and applied for each node connecting to the NodeServer
		/// </summary>
		public NodeConnectionParameters InboundNodeConnectionParameters { get; set; }

		public bool AllowLocalPeers { get; set; }

		public int MaxConnections { get; set; }

		public IPEndPoint LocalEndpoint
		{
			get => _localEndpoint;
			set => _localEndpoint = Utils.EnsureIPv6(value);
		}

		public bool IsListening => _socket != null;

		public MessageProducer<IncomingMessage> AllMessages { get; } = new MessageProducer<IncomingMessage>();

		public IPEndPoint ExternalEndpoint
		{
			get => _externalEndpoint;
			set => _externalEndpoint = Utils.EnsureIPv6(value);
		}

		public NodesCollection ConnectedNodes { get; } = new NodesCollection();

		public ulong Nonce
		{
			get
			{
				if (_nonce == 0)
				{
					_nonce = RandomUtils.GetUInt64();
				}

				return _nonce;
			}
			set => _nonce = value;
		}

		public void Dispose()
		{
			if (!_cancel.IsCancellationRequested)
			{
				_cancel.Cancel();
				_trace.LogInside(() => NodeServerTrace.Information("Stopping node server..."));
				lock (_resources)
				{
					foreach (var resource in _resources)
					{
						resource.Dispose();
					}
				}

				try
				{
					ConnectedNodes.DisconnectAll();
				}
				finally
				{
					if (_socket != null)
					{
						Utils.SafeCloseSocket(_socket);
						_socket = null;
					}
				}
			}
		}


		public event NodeServerNodeEventHandler NodeRemoved;
		public event NodeServerNodeEventHandler NodeAdded;
		public event NodeServerMessageEventHandler MessageReceived;

		private void _Nodes_NodeRemoved(object sender, NodeEventArgs node)
		{
			var removed = NodeRemoved;
			if (removed != null)
			{
				removed(this, node.Node);
			}
		}

		private void _Nodes_NodeAdded(object sender, NodeEventArgs node)
		{
			var added = NodeAdded;
			if (added != null)
			{
				added(this, node.Node);
			}
		}

		public void Listen(int maxIncoming = 8)
		{
			if (_socket != null)
			{
				throw new InvalidOperationException("Already listening");
			}

			using (_trace.Open())
			{
				try
				{
					_socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
					_socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

					_socket.Bind(LocalEndpoint);
					_socket.Listen(maxIncoming);
					NodeServerTrace.Information("Listening...");
					BeginAccept();
				}
				catch (Exception ex)
				{
					NodeServerTrace.Error("Error while opening the Protocol server", ex);
					throw;
				}
			}
		}

		private void BeginAccept()
		{
			if (_cancel.IsCancellationRequested)
			{
				NodeServerTrace.Information("Stop accepting connection...");
				return;
			}

			NodeServerTrace.Information("Accepting connection...");
			var args = new SocketAsyncEventArgs();
			args.Completed += Accept_Completed;
			if (!_socket.AcceptAsync(args))
			{
				EndAccept(args);
			}
		}

		private void Accept_Completed(object sender, SocketAsyncEventArgs e)
		{
			EndAccept(e);
		}

		private void EndAccept(SocketAsyncEventArgs args)
		{
			using (_trace.Open())
			{
				Socket client = null;
				try
				{
					if (args.SocketError != SocketError.Success)
					{
						throw new SocketException((int) args.SocketError);
					}

					client = args.AcceptSocket;
					if (_cancel.IsCancellationRequested)
					{
						return;
					}

					NodeServerTrace.Information("Client connection accepted : " + client.RemoteEndPoint);
					var cancel = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
					cancel.CancelAfter(TimeSpan.FromSeconds(10));

					var stream = new NetworkStream(client, false);
					while (true)
					{
						if (ConnectedNodes.Count >= MaxConnections)
						{
							NodeServerTrace.Information("MaxConnections limit reached");
							Utils.SafeCloseSocket(client);
							break;
						}

						cancel.Token.ThrowIfCancellationRequested();
						var message = Message.ReadNext(stream, Network, Version, cancel.Token, out var counter);
						_messageProducer.PushMessage(new IncomingMessage
						{
							Socket = client,
							Message = message,
							Length = counter.BytesRead,
							Node = null
						});
						if (message.Payload is VersionPayload)
						{
							break;
						}

						NodeServerTrace.Error(
							"The first message of the remote peer did not contained a Version payload", null);
					}
				}
				catch (OperationCanceledException)
				{
					Utils.SafeCloseSocket(client);
					if (!_cancel.Token.IsCancellationRequested)
					{
						NodeServerTrace.Error(
							"The remote connecting failed to send a message within 10 seconds, dropping connection",
							null);
					}
				}
				catch (Exception ex)
				{
					if (_cancel.IsCancellationRequested)
					{
						return;
					}

					if (client == null)
					{
						NodeServerTrace.Error("Error while accepting connection ", ex);
						Thread.Sleep(3000);
					}
					else
					{
						Utils.SafeCloseSocket(client);
						NodeServerTrace.Error("Invalid message received from the remote connecting node", ex);
					}
				}

				BeginAccept();
			}
		}


		internal void ExternalAddressDetected(IPAddress iPAddress)
		{
			if (!ExternalEndpoint.Address.IsRoutable(AllowLocalPeers) && iPAddress.IsRoutable(AllowLocalPeers))
			{
				NodeServerTrace.Information("New externalAddress detected " + iPAddress);
				ExternalEndpoint = new IPEndPoint(iPAddress, ExternalEndpoint.Port);
			}
		}

		private void ProcessMessage(IncomingMessage message)
		{
			AllMessages.PushMessage(message);
			TraceCorrelation trace = null;
			if (message.Node != null)
			{
				trace = message.Node.TraceCorrelation;
			}
			else
			{
				trace = new TraceCorrelation(NodeServerTrace.Trace, "Processing inbound message " + message.Message);
			}

			using (trace.Open(false))
			{
				ProcessMessageCore(message);
			}
		}

		private void ProcessMessageCore(IncomingMessage message)
		{
			if (message.Message.Payload is VersionPayload)
			{
				var version = message.AssertPayload<VersionPayload>();
				var connectedToSelf = version.Nonce == Nonce;
				if (message.Node != null && connectedToSelf)
				{
					NodeServerTrace.ConnectionToSelfDetected();
					message.Node.DisconnectAsync();
					return;
				}

				if (message.Node == null)
				{
					var remoteEndpoint = version.AddressFrom;
					if (!remoteEndpoint.Address.IsRoutable(AllowLocalPeers))
					{
						//Send his own endpoint
						remoteEndpoint = new IPEndPoint(((IPEndPoint) message.Socket.RemoteEndPoint).Address,
							Network.DefaultPort);
					}

					var peer = new NetworkAddress
					{
						Endpoint = remoteEndpoint,
						Time = DateTimeOffset.UtcNow
					};
					var node = new Node(peer, Network, CreateNodeConnectionParameters(), message.Socket, version);

					if (connectedToSelf)
					{
						node.SendMessage(CreateNodeConnectionParameters().CreateVersion(node.Peer.Endpoint, Network));
						NodeServerTrace.ConnectionToSelfDetected();
						node.Disconnect();
						return;
					}

					var cancel = new CancellationTokenSource();
					cancel.CancelAfter(TimeSpan.FromSeconds(10.0));
					try
					{
						ConnectedNodes.Add(node);
						node.StateChanged += node_StateChanged;
						node.RespondToHandShake(cancel.Token);
					}
					catch (OperationCanceledException ex)
					{
						NodeServerTrace.Error(
							"The remote node did not respond fast enough (10 seconds) to the handshake completion, dropping connection",
							ex);
						node.DisconnectAsync();
						throw;
					}
					catch (Exception)
					{
						node.DisconnectAsync();
						throw;
					}
				}
			}

			var messageReceived = MessageReceived;
			if (messageReceived != null)
			{
				messageReceived(this, message);
			}
		}

		private void node_StateChanged(Node node, NodeState oldState)
		{
			if (node.State == NodeState.Disconnecting ||
			    node.State == NodeState.Failed ||
			    node.State == NodeState.Offline)
			{
				ConnectedNodes.Remove(node);
			}
		}

		private IDisposable OwnResource(IDisposable resource)
		{
			if (_cancel.IsCancellationRequested)
			{
				resource.Dispose();
				return Scope.Nothing;
			}

			return new Scope(() =>
			{
				lock (_resources)
				{
					_resources.Add(resource);
				}
			}, () =>
			{
				lock (_resources)
				{
					_resources.Remove(resource);
				}
			});
		}

		internal NodeConnectionParameters CreateNodeConnectionParameters()
		{
			var myExternal = Utils.EnsureIPv6(ExternalEndpoint);
			var param2 = InboundNodeConnectionParameters.Clone();
			param2.Nonce = Nonce;
			param2.Version = Version;
			param2.AddressFrom = myExternal;
			return param2;
		}

		public bool IsConnectedTo(IPEndPoint endpoint)
		{
			return ConnectedNodes.FindByEndpoint(endpoint) != null;
		}

		public Node FindOrConnect(IPEndPoint endpoint)
		{
			while (true)
			{
				var node = ConnectedNodes.FindByEndpoint(endpoint);
				if (node != null)
				{
					return node;
				}

				node = Node.Connect(Network, endpoint, CreateNodeConnectionParameters());
				node.StateChanged += node_StateChanged;
				if (!ConnectedNodes.Add(node))
				{
					node.DisconnectAsync();
				}
				else
				{
					return node;
				}
			}
		}
	}
}