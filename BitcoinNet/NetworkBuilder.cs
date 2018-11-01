using BitcoinNet.Protocol;
using BitcoinNet.DataEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinNet
{
	public class NetworkBuilder
	{
		internal string _Name;
		internal NetworkType _NetworkType;
		internal string _prefix;
		internal Dictionary<Base58Type, byte[]> _Base58Prefixes = new Dictionary<Base58Type, byte[]>();
		internal List<string> _Aliases = new List<string>();
		internal int _RPCPort;
		internal int _Port;
		internal uint _Magic;
		internal Consensus _Consensus;
		internal List<DNSSeedData> vSeeds = new List<DNSSeedData>();
		internal List<NetworkAddress> vFixedSeeds = new List<NetworkAddress>();
		internal byte[] _Genesis;
		internal uint? _MaxP2PVersion;
		internal INetworkSet _NetworkSet;

		public NetworkBuilder SetNetworkSet(INetworkSet networkSet)
		{
			_NetworkSet = networkSet;
			return this;
		}

		public NetworkBuilder SetMaxP2PVersion(uint version)
		{
			_MaxP2PVersion = version;
			return this;
		}
	
		public NetworkBuilder SetName(string name)
		{
			_Name = name;
			return this;
		}

		public void CopyFrom(Network network)
		{
			if(network == null)
				throw new ArgumentNullException(nameof(network));
			_Base58Prefixes.Clear();
			for(int i = 0; i < network.base58Prefixes.Length; i++)
			{
				SetBase58Bytes((Base58Type)i, network.base58Prefixes[i]);
			}
			SetConsensus(network.Consensus).
			SetGenesis(Encoders.Hex.EncodeData(network.GetGenesis().ToBytes())).
			SetMagic(_Magic).
			SetPort(network.DefaultPort).
			SetRPCPort(network.RPCPort);
			SetNetworkSet(network.NetworkSet);
			SetNetworkType(network.NetworkType);
			SetPrefix(network.Prefix);
		}

		public NetworkBuilder AddAlias(string alias)
		{
			_Aliases.Add(alias);
			return this;
		}

		public NetworkBuilder SetRPCPort(int port)
		{
			_RPCPort = port;
			return this;
		}

		public NetworkBuilder SetPort(int port)
		{
			_Port = port;
			return this;
		}


		public NetworkBuilder SetMagic(uint magic)
		{
			_Magic = magic;
			return this;
		}

		public NetworkBuilder AddDNSSeeds(IEnumerable<DNSSeedData> seeds)
		{
			vSeeds.AddRange(seeds);
			return this;
		}
		public NetworkBuilder AddSeeds(IEnumerable<NetworkAddress> seeds)
		{
			vFixedSeeds.AddRange(seeds);
			return this;
		}

		public NetworkBuilder SetConsensus(Consensus consensus)
		{
			_Consensus = consensus == null ? null : consensus.Clone();
			return this;
		}
		
		public NetworkBuilder SetGenesis(string hex)
		{
			_Genesis = Encoders.Hex.DecodeData(hex);
			return this;
		}		

		public NetworkBuilder SetBase58Bytes(Base58Type type, byte[] bytes)
		{
			_Base58Prefixes.AddOrReplace(type, bytes);
			return this;
		}

		public NetworkBuilder SetNetworkType(NetworkType network)
		{
			_NetworkType = network;
			return this;
		}

		public NetworkBuilder SetPrefix(string prefix)
		{
			_prefix = prefix;
			return this;
		}

		/// <summary>
		/// Create an immutable Network instance, and register it globally so it is queriable through Network.GetNetwork(string name) and Network.GetNetworks().
		/// </summary>
		/// <returns></returns>
		public Network BuildAndRegister()
		{
			return Network.Register(this);
		}
	}
}
