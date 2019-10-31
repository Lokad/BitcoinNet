using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BitcoinNet.Protocol.Behaviors;

namespace BitcoinNet.Protocol
{
	public class WellKnownGroupSelectors
	{
		private static readonly Random _rand = new Random();
		private static Func<IPEndPoint, byte[]> _groupByRandom;
		private static Func<IPEndPoint, byte[]> _groupByIp;
		private static Func<IPEndPoint, byte[]> _groupByEndpoint;
		private static Func<IPEndPoint, byte[]> _groupByNetwork;

		public static Func<IPEndPoint, byte[]> ByRandom
		{
			get
			{
				return _groupByRandom = _groupByRandom ?? (ip =>
				{
					var group = new byte[20];
					_rand.NextBytes(group);
					return group;
				});
			}
		}

		public static Func<IPEndPoint, byte[]> ByIp
		{
			get { return _groupByIp = _groupByIp ?? (ip => ip.Address.GetAddressBytes()); }
		}

		public static Func<IPEndPoint, byte[]> ByEndpoint
		{
			get
			{
				return _groupByEndpoint = _groupByEndpoint ?? (endpoint =>
				{
					var bytes = endpoint.Address.GetAddressBytes();
					var port = Utils.ToBytes((uint) endpoint.Port, true);
					var result = new byte[bytes.Length + port.Length];
					Array.Copy(bytes, result, bytes.Length);
					Array.Copy(port, 0, result, bytes.Length, port.Length);
					return result;
				});
			}
		}

		public static Func<IPEndPoint, byte[]> ByNetwork
		{
			get { return _groupByNetwork = _groupByNetwork ?? (ip => ip.Address.GetGroup()); }
		}
	}

	public class NodesGroup : IDisposable
	{
		private readonly object _cs;
		private readonly AddressManager _defaultAddressManager = new AddressManager();
		private readonly Network _network;
		private readonly TraceCorrelation _trace = new TraceCorrelation(NodeServerTrace.Trace, "Group connection");
		private volatile bool _connecting;
		private CancellationTokenSource _disconnect;

		public NodesGroup(
			Network network,
			NodeConnectionParameters connectionParameters = null,
			NodeRequirement requirements = null)
		{
			AllowSameGroup = false;
			MaximumNodeConnection = 8;
			_network = network;
			_cs = new object();
			ConnectedNodes = new NodesCollection();
			NodeConnectionParameters = connectionParameters ?? new NodeConnectionParameters();
			NodeConnectionParameters = NodeConnectionParameters.Clone();
			Requirements = requirements ?? new NodeRequirement();
			_disconnect = new CancellationTokenSource();
		}

		public NodeConnectionParameters NodeConnectionParameters { get; set; }

		/// <summary>
		///     The number of node that this behavior will try to maintain online (Default : 8)
		/// </summary>
		public int MaximumNodeConnection { get; set; }

		public NodeRequirement Requirements { get; set; }

		public NodesCollection ConnectedNodes { get; }

		/// <summary>
		///     If false, the search process will do its best to connect to Node in different network group to prevent sybil
		///     attacks. (Default : false)
		///     If CustomGroupSelector is set, AllowSameGroup is ignored.
		/// </summary>
		public bool AllowSameGroup { get; set; }

		/// <summary>
		///     How to calculate a group of an ip, by default using BitcoinNet.IpExtensions.GetGroup.
		///     Overrides AllowSameGroup.
		/// </summary>
		public Func<IPEndPoint, byte[]> CustomGroupSelector { get; set; }

		// IDisposable Members

		/// <summary>
		///     Same as Disconnect
		/// </summary>
		public void Dispose()
		{
			Disconnect();
		}

		/// <summary>
		///     Start connecting asynchronously to remote peers
		/// </summary>
		public void Connect()
		{
			_disconnect = new CancellationTokenSource();
			StartConnecting();
		}

		/// <summary>
		///     Drop connection to all connected nodes
		/// </summary>
		public void Disconnect()
		{
			_disconnect.Cancel();
			ConnectedNodes.DisconnectAll();
		}

		internal void StartConnecting()
		{
			if (_disconnect.IsCancellationRequested)
			{
				return;
			}

			if (ConnectedNodes.Count >= MaximumNodeConnection)
			{
				return;
			}

			if (_connecting)
			{
				return;
			}

			Task.Factory.StartNew(() =>
			{
				if (Monitor.TryEnter(_cs))
				{
					_connecting = true;
					TraceCorrelationScope scope = null;
					try
					{
						while (!_disconnect.IsCancellationRequested && ConnectedNodes.Count < MaximumNodeConnection)
						{
							scope = scope ?? _trace.Open();

							NodeServerTrace.Information("Connected nodes : " + ConnectedNodes.Count + "/" +
							                            MaximumNodeConnection);
							var parameters = NodeConnectionParameters.Clone();
							parameters.TemplateBehaviors.Add(new NodesGroupBehavior(this));
							parameters.ConnectCancellation = _disconnect.Token;
							var addrman = AddressManagerBehavior.GetAddrman(parameters);

							if (addrman == null)
							{
								addrman = _defaultAddressManager;
								AddressManagerBehavior.SetAddrman(parameters, addrman);
							}

							Node node = null;
							try
							{
								var groupSelector = CustomGroupSelector ??
								                    (AllowSameGroup ? WellKnownGroupSelectors.ByRandom : null);
								node = Node.Connect(_network, parameters,
									ConnectedNodes.Select(n => n.RemoteSocketEndpoint).ToArray(), groupSelector);
								var timeout = CancellationTokenSource.CreateLinkedTokenSource(_disconnect.Token);
								timeout.CancelAfter(5000);
								node.VersionHandshake(Requirements, timeout.Token);
								NodeServerTrace.Information("Node successfully connected to and handshaked");
							}
							catch (OperationCanceledException ex)
							{
								if (_disconnect.Token.IsCancellationRequested)
								{
									break;
								}

								NodeServerTrace.Error("Timeout for picked node", ex);
								if (node != null)
								{
									node.DisconnectAsync("Handshake timeout", ex);
								}
							}
							catch (Exception ex)
							{
								NodeServerTrace.Error("Error while connecting to node", ex);
								if (node != null)
								{
									node.DisconnectAsync("Error while connecting", ex);
								}
							}
						}
					}
					finally
					{
						Monitor.Exit(_cs);
						_connecting = false;
						if (scope != null)
						{
							scope.Dispose();
						}
					}
				}
			}, TaskCreationOptions.LongRunning);
		}


		public static NodesGroup GetNodeGroup(Node node)
		{
			return GetNodeGroup(node.Behaviors);
		}

		public static NodesGroup GetNodeGroup(NodeConnectionParameters parameters)
		{
			return GetNodeGroup(parameters.TemplateBehaviors);
		}

		public static NodesGroup GetNodeGroup(NodeBehaviorsCollection behaviors)
		{
			return behaviors.OfType<NodesGroupBehavior>().Select(c => c._parent).FirstOrDefault();
		}

		/// <summary>
		///     Asynchronously create a new set of nodes
		/// </summary>
		public void Purge(string reason)
		{
			Task.Factory.StartNew(() =>
			{
				var initialNodes = ConnectedNodes.ToDictionary(n => n);
				while (!_disconnect.IsCancellationRequested && initialNodes.Count != 0)
				{
					var node = initialNodes.First();
					node.Value.Disconnect(reason);
					initialNodes.Remove(node.Value);
					_disconnect.Token.WaitHandle.WaitOne(5000);
				}
			});
		}
	}
}