using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BitcoinNet.Crypto;
using BitcoinNet.DataEncoders;
using BitcoinNet.JsonRpc;
using BitcoinNet.Protocol;

namespace BitcoinNet.Tests
{
	public enum CoreNodeState
	{
		Stopped,
		Starting,
		Running,
		Killed
	}

	public class NodeConfigParameters : Dictionary<string, string>
	{
		public void Import(NodeConfigParameters configParameters, bool overrides)
		{
			foreach (var kv in configParameters)
			{
				if (!ContainsKey(kv.Key))
				{
					Add(kv.Key, kv.Value);
				}
				else if (overrides)
				{
					Remove(kv.Key);
					Add(kv.Key, kv.Value);
				}
			}
		}

		public override string ToString()
		{
			var builder = new StringBuilder();
			foreach (var kv in this)
			{
				builder.AppendLine(kv.Key + "=" + kv.Value);
			}

			return builder.ToString();
		}
	}

	public class NodeOSDownloadData
	{
		public string Archive { get; set; }

		public string DownloadLink { get; set; }

		public string Executable { get; set; }

		public string Hash { get; set; }
	}

	public partial class NodeDownloadData
	{
		public string Version { get; set; }

		public NodeOSDownloadData Linux { get; set; }

		public NodeOSDownloadData Mac { get; set; }

		public NodeOSDownloadData Windows { get; set; }

		public NodeOSDownloadData GetCurrentOSDownloadData()
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Windows :
				RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Linux :
				RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Mac :
				throw new NotSupportedException();
		}
	}

	public class NodeBuilder : IDisposable
	{
		private readonly List<IDisposable> _disposables = new List<IDisposable>();
		private readonly string _root;
		private int _last;

		public NodeBuilder(string root, string bitcoindPath)
		{
			_root = root;
			BitcoinD = bitcoindPath;
		}

		public string BitcoinD { get; }

		public bool CleanBeforeStartingNode { get; set; } = true;

		public List<CoreNode> Nodes { get; } = new List<CoreNode>();

		public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

		public Network Network { get; set; } = Network.RegTest;

		public bool SupportCookieFile { get; set; } = true;

		public void Dispose()
		{
			foreach (var node in Nodes)
			{
				node.Kill();
			}

			foreach (var disposable in _disposables)
			{
				disposable.Dispose();
			}
		}

		public static NodeBuilder Create(NodeDownloadData downloadData, Network network = null,
			[CallerMemberName] string caller = null)
		{
			network = network ?? Network.RegTest;
			var isFilePath = downloadData.Version.Length >= 2 && downloadData.Version[1] == ':';
			var path = isFilePath ? downloadData.Version : EnsureDownloaded(downloadData);
			if (!Directory.Exists(caller))
			{
				Directory.CreateDirectory(caller);
			}

			return new NodeBuilder(caller, path) {Network = network};
		}

		public static string EnsureDownloaded(NodeDownloadData downloadData)
		{
			if (!Directory.Exists("TestData"))
			{
				Directory.CreateDirectory("TestData");
			}

			var osDownloadData = downloadData.GetCurrentOSDownloadData();
			var bitcoind = Path.Combine("TestData", string.Format(osDownloadData.Executable, downloadData.Version));
			var zip = Path.Combine("TestData", string.Format(osDownloadData.Archive, downloadData.Version));
			if (File.Exists(bitcoind))
			{
				return bitcoind;
			}

			var url = string.Format(osDownloadData.DownloadLink, downloadData.Version);
			var client = new HttpClient {Timeout = TimeSpan.FromMinutes(10.0)};
			var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
			CheckHash(osDownloadData, data);
			File.WriteAllBytes(zip, data);

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				ZipFile.ExtractToDirectory(zip, new FileInfo(zip).Directory.FullName);
			}
			else
			{
				Process.Start("tar", "-zxvf " + zip + " -C TestData").WaitForExit();
			}

			File.Delete(zip);
			return bitcoind;
		}

		private static void CheckHash(NodeOSDownloadData osDownloadData, byte[] data)
		{
			var actual = Encoders.Hex.EncodeData(Hashes.SHA256(data));
			if (!actual.Equals(osDownloadData.Hash, StringComparison.OrdinalIgnoreCase))
			{
				throw new Exception(
					$"Hash of downloaded file does not match (Expected: {osDownloadData.Hash}, Actual: {actual})");
			}
		}

		public CoreNode CreateNode(bool start = false)
		{
			var child = Path.Combine(_root, _last.ToString());
			_last++;
			var node = new CoreNode(child, this) {Network = Network};
			Nodes.Add(node);
			if (start)
			{
				node.Start();
			}

			return node;
		}

		public void StartAll()
		{
			Task.WaitAll(Nodes.Where(n => n.State == CoreNodeState.Stopped).Select(n => n.StartAsync()).ToArray());
		}

		internal void AddDisposable(IDisposable group)
		{
			_disposables.Add(group);
		}
	}

	public class CoreNode
	{
		private readonly NodeBuilder _builder;
		private readonly NetworkCredential _credential;
		private readonly string _dataDir;
		private readonly object _lock = new object();
		private readonly int[] _ports;
		private Process _process;

		public CoreNode(string folder, NodeBuilder builder)
		{
			_builder = builder;
			Folder = folder;
			State = CoreNodeState.Stopped;

			_dataDir = Path.Combine(folder, "data");
			var pass = Hashes.Hash256(Encoding.UTF8.GetBytes(folder)).ToString();
			_credential = new NetworkCredential(pass, pass);
			Config = Path.Combine(_dataDir, "bitcoin.conf");
			ConfigParameters.Import(builder.ConfigParameters, true);
			_ports = new int[2];

			if (builder.CleanBeforeStartingNode && File.Exists(Config))
			{
				var oldCreds = ExtractCreds(File.ReadAllText(Config));
				CookieAuth = oldCreds == null;
				ExtractPorts(_ports, File.ReadAllText(Config));

				try
				{
					CreateRPCClient().SendCommand("stop");
				}
				catch
				{
					try
					{
						CleanFolder();
					}
					catch
					{
						throw new InvalidOperationException(
							"A running instance of bitcoind of a previous run prevent this test from starting. Please close bitcoind process manually and restart the test.");
					}
				}

				var cts = new CancellationTokenSource();
				cts.CancelAfter(10000);
				while (!cts.IsCancellationRequested && Directory.Exists(Folder))
				{
					try
					{
						CleanFolder();
						break;
					}
					catch
					{
					}

					Thread.Sleep(100);
				}

				if (cts.IsCancellationRequested)
				{
					throw new InvalidOperationException(
						"You seem to have a running node from a previous test, please kill the process and restart the test.");
				}
			}

			CookieAuth = builder.SupportCookieFile;
			Directory.CreateDirectory(folder);
			Directory.CreateDirectory(_dataDir);
			FindPorts(_ports);
		}

		public string Folder { get; }

		public IPEndPoint Endpoint => new IPEndPoint(IPAddress.Parse("127.0.0.1"), _ports[0]);

		public string Config { get; }

		public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

		public Network Network { get; set; } = Network.RegTest;

		public CoreNodeState State { get; private set; }

		public int ProtocolPort => _ports[0];

		public Uri RPCUri => new Uri("http://127.0.0.1:" + _ports[1] + "/");

		public IPEndPoint NodeEndpoint => new IPEndPoint(IPAddress.Parse("127.0.0.1"), _ports[0]);

		/// <summary>
		///     Nodes connecting to this node will be whitelisted (default: false)
		/// </summary>
		public bool WhiteBind { get; set; }

		public BitcoinSecret MinerSecret { get; private set; }

		public bool CookieAuth { get; set; } = true;

		public string GetRPCAuth()
		{
			if (!CookieAuth)
			{
				return _credential.UserName + ":" + _credential.Password;
			}

			return "cookiefile=" + Path.Combine(_dataDir, "regtest", ".cookie");
		}

		private void ExtractPorts(int[] ports, string config)
		{
			var p = Regex.Match(config, "rpcport=(.*)");
			ports[1] = int.Parse(p.Groups[1].Value.Trim());
		}

		private NetworkCredential ExtractCreds(string config)
		{
			var user = Regex.Match(config, "rpcuser=(.*)");
			if (!user.Success)
			{
				return null;
			}

			var pass = Regex.Match(config, "rpcpassword=(.*)");
			return new NetworkCredential(user.Groups[1].Value.Trim(), pass.Groups[1].Value.Trim());
		}

		private void CleanFolder()
		{
			try
			{
				Directory.Delete(Folder, true);
			}
			catch (DirectoryNotFoundException)
			{
			}
		}

		public void Sync(CoreNode node, bool keepConnection = false)
		{
			var rpc = CreateRPCClient();
			var rpc1 = node.CreateRPCClient();
			rpc.AddNode(node.Endpoint, true);
			while (rpc.GetBestBlockHash() != rpc1.GetBestBlockHash())
			{
				Task.Delay(200).GetAwaiter().GetResult();
			}

			if (!keepConnection)
			{
				rpc.RemoveNode(node.Endpoint);
			}
		}

		public void Start()
		{
			StartAsync().Wait();
		}

		public RPCClient CreateRPCClient()
		{
			return new RPCClient(GetRPCAuth(), RPCUri, Network);
		}

		public RestClient CreateRESTClient()
		{
			return new RestClient(new Uri("http://127.0.0.1:" + _ports[1] + "/"));
		}

		public Node CreateNodeClient()
		{
			return Node.Connect(Network, NodeEndpoint);
		}

		public Node CreateNodeClient(NodeConnectionParameters parameters)
		{
			return Node.Connect(Network, "127.0.0.1:" + _ports[0], parameters);
		}

		public async Task StartAsync()
		{
			var config = new NodeConfigParameters {{"regtest", "1"}, {"rest", "1"}, {"server", "1"}, {"txindex", "1"}};
			if (!CookieAuth)
			{
				config.Add("rpcuser", _credential.UserName);
				config.Add("rpcpassword", _credential.Password);
			}

			if (!WhiteBind)
			{
				config.Add("port", _ports[0].ToString());
			}
			else
			{
				config.Add("whitebind", "127.0.0.1:" + _ports[0]);
			}

			config.Add("rpcport", _ports[1].ToString());
			config.Add("printtoconsole", "1");
			config.Add("keypool", "10");
			config.Import(ConfigParameters, true);
			File.WriteAllText(Config, config.ToString());
			await Run();
		}

		private async Task Run()
		{
			lock (_lock)
			{
				_process = Process.Start(new FileInfo(_builder.BitcoinD).FullName,
					"-conf=bitcoin.conf" + " -datadir=" + _dataDir + " -debug=net");
				State = CoreNodeState.Starting;
			}

			while (true)
			{
				try
				{
					await CreateRPCClient().GetBlockHashAsync(0).ConfigureAwait(false);
					State = CoreNodeState.Running;
					break;
				}
				catch
				{
				}

				if (_process == null || _process.HasExited)
				{
					break;
				}
			}
		}

		private void FindPorts(int[] ports)
		{
			var i = 0;
			while (i < ports.Length)
			{
				var port = RandomUtils.GetUInt32() % 4000;
				port = port + 10000;
				if (ports.Any(p => p == port))
				{
					continue;
				}

				try
				{
					var l = new TcpListener(IPAddress.Loopback, (int) port);
					l.Start();
					l.Stop();
					ports[i] = (int) port;
					i++;
				}
				catch (SocketException)
				{
				}
			}
		}

		public uint256[] Generate(int blockCount)
		{
			return CreateRPCClient().Generate(blockCount);
		}

		public void Broadcast(params Transaction[] transactions)
		{
			var rpc = CreateRPCClient();
			var batch = rpc.PrepareBatch();
			foreach (var tx in transactions)
			{
				batch.SendRawTransactionAsync(tx);
			}

			rpc.SendBatch();
		}

		public void Kill(bool cleanFolder = true)
		{
			lock (_lock)
			{
				if (_process != null && !_process.HasExited)
				{
					_process.Kill();
					_process.WaitForExit();
				}

				State = CoreNodeState.Killed;
				if (cleanFolder)
				{
					CleanFolder();
				}
			}
		}

		public void WaitForExit()
		{
			if (_process != null && !_process.HasExited)
			{
				_process.WaitForExit();
			}
		}

		private List<Transaction> Reorder(List<Transaction> transactions)
		{
			if (transactions.Count == 0)
			{
				return transactions;
			}

			var result = new List<Transaction>();
			var dictionary = transactions.ToDictionary(t => t.GetHash(), t => new TransactionNode(t));
			foreach (var transaction in dictionary.Select(d => d.Value))
			{
				foreach (var input in transaction.Transaction.Inputs)
				{
					var node = dictionary.TryGet(input.PrevOut.Hash);
					if (node != null)
					{
						transaction.DependsOn.Add(node);
					}
				}
			}

			while (dictionary.Count != 0)
			{
				foreach (var node in dictionary.Select(d => d.Value).ToList())
				{
					foreach (var parent in node.DependsOn.ToList())
					{
						if (!dictionary.ContainsKey(parent.Hash))
						{
							node.DependsOn.Remove(parent);
						}
					}

					if (node.DependsOn.Count == 0)
					{
						result.Add(node.Transaction);
						dictionary.Remove(node.Hash);
					}
				}
			}

			return result;
		}

		public void Restart()
		{
			Kill(false);
			Run().GetAwaiter().GetResult();
		}

		private class TransactionNode
		{
			public readonly List<TransactionNode> DependsOn = new List<TransactionNode>();
			public readonly uint256 Hash;
			public readonly Transaction Transaction;

			public TransactionNode(Transaction tx)
			{
				Transaction = tx;
				Hash = tx.GetHash();
			}
		}
	}
}