using System;
using System.Collections.Generic;
using BitcoinNet.DataEncoders;
using BitcoinNet.Protocol;

namespace BitcoinNet
{
	public class NetworkBuilder
	{
		internal readonly List<string> _aliases = new List<string>();
		internal readonly Dictionary<Base58Type, byte[]> _base58Prefixes = new Dictionary<Base58Type, byte[]>();
		internal Consensus _consensus;
		internal byte[] _genesis;
		internal uint _magic;
		internal uint? _maxP2PVersion;
		internal string _name;
		internal INetworkSet _networkSet;
		internal NetworkType _networkType;
		internal int _port;
		internal string _prefix;
		internal int _rpcPort;
		internal List<NetworkAddress> _vFixedSeeds = new List<NetworkAddress>();
		internal List<DNSSeedData> _vSeeds = new List<DNSSeedData>();

		public NetworkBuilder SetNetworkSet(INetworkSet networkSet)
		{
			_networkSet = networkSet;
			return this;
		}

		public NetworkBuilder SetMaxP2PVersion(uint version)
		{
			_maxP2PVersion = version;
			return this;
		}

		public NetworkBuilder SetName(string name)
		{
			_name = name;
			return this;
		}

		public void CopyFrom(Network network)
		{
			if (network == null)
			{
				throw new ArgumentNullException(nameof(network));
			}

			_base58Prefixes.Clear();
			for (var i = 0; i < network.Base58Prefixes.Length; i++)
			{
				SetBase58Bytes((Base58Type) i, network.Base58Prefixes[i]);
			}

			SetConsensus(network.Consensus).SetGenesis(Encoders.Hex.EncodeData(network.GetGenesis().ToBytes()))
				.SetMagic(_magic).SetPort(network.DefaultPort).SetRPCPort(network.RPCPort);
			SetNetworkSet(network.NetworkSet);
			SetNetworkType(network.NetworkType);
			SetPrefix(network.Prefix);
		}

		public NetworkBuilder AddAlias(string alias)
		{
			_aliases.Add(alias);
			return this;
		}

		public NetworkBuilder SetRPCPort(int port)
		{
			_rpcPort = port;
			return this;
		}

		public NetworkBuilder SetPort(int port)
		{
			_port = port;
			return this;
		}

		public NetworkBuilder SetMagic(uint magic)
		{
			_magic = magic;
			return this;
		}

		public NetworkBuilder AddDNSSeeds(IEnumerable<DNSSeedData> seeds)
		{
			_vSeeds.AddRange(seeds);
			return this;
		}

		public NetworkBuilder AddSeeds(IEnumerable<NetworkAddress> seeds)
		{
			_vFixedSeeds.AddRange(seeds);
			return this;
		}

		public NetworkBuilder SetConsensus(Consensus consensus)
		{
			_consensus = consensus == null ? null : consensus.Clone();
			return this;
		}

		public NetworkBuilder SetGenesis(string hex)
		{
			_genesis = Encoders.Hex.DecodeData(hex);
			return this;
		}

		public NetworkBuilder SetBase58Bytes(Base58Type type, byte[] bytes)
		{
			_base58Prefixes.AddOrReplace(type, bytes);
			return this;
		}

		public NetworkBuilder SetNetworkType(NetworkType network)
		{
			_networkType = network;
			return this;
		}

		public NetworkBuilder SetPrefix(string prefix)
		{
			_prefix = prefix;
			return this;
		}

		/// <summary>
		///     Create an immutable Network instance, and register it globally so it is queriable through Network.GetNetwork(string
		///     name) and Network.GetNetworks().
		/// </summary>
		/// <returns></returns>
		public Network BuildAndRegister()
		{
			return Network.Register(this);
		}
	}
}