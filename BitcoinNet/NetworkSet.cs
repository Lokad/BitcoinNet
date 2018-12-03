using BitcoinNet.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinNet
{
	public interface INetworkSet
	{
		Network GetNetwork(NetworkType networkType);
		Network Mainnet
		{
			get;
		}
		Network Testnet
		{
			get;
		}
		Network Regtest
		{
			get;
		}
		string CryptoCode
		{
			get;
		}
	}
	public abstract class NetworkSetBase : INetworkSet
	{
		object l = new object();
		public NetworkSetBase()
		{
		}
		public Network GetNetwork(NetworkType networkType)
		{
			switch(networkType)
			{
				case NetworkType.Mainnet:
					return Mainnet;
				case NetworkType.Testnet:
					return Testnet;
				case NetworkType.Regtest:
					return Regtest;
			}
			throw new NotSupportedException(networkType.ToString());
		}

		volatile bool _Registered;
		volatile bool _Registering;
		public void EnsureRegistered()
		{
			if(_Registered)
				return;
			lock(l)
			{
				if(_Registered)
					return;
				if(_Registering)
					throw new InvalidOperationException("It seems like you are recursively accessing a Network which is not yet built.");
				_Registering = true;
				var builder = CreateMainnet();
				builder.SetNetworkType(NetworkType.Mainnet);
				builder.SetNetworkSet(this);
				_Mainnet = builder.BuildAndRegister();
				builder = CreateTestnet();
				builder.SetNetworkType(NetworkType.Testnet);
				builder.SetNetworkSet(this);
				_Testnet = builder.BuildAndRegister();
				builder = CreateRegtest();
				builder.SetNetworkType(NetworkType.Regtest);
				builder.SetNetworkSet(this);
				_Regtest = builder.BuildAndRegister();
				PostInit();
				_Registered = true;
				_Registering = false;
			}
		}

		protected virtual void PostInit()
		{
		}

		protected abstract NetworkBuilder CreateMainnet();
		protected abstract NetworkBuilder CreateTestnet();
		protected abstract NetworkBuilder CreateRegtest();



		private Network _Mainnet;
		public Network Mainnet
		{
			get
			{
				if(_Mainnet == null)
					EnsureRegistered();
				return _Mainnet;
			}
		}

		private Network _Testnet;
		public Network Testnet
		{
			get
			{
				if(_Testnet == null)
					EnsureRegistered();
				return _Testnet;
			}
		}

		private Network _Regtest;
		public Network Regtest
		{
			get
			{
				if(_Regtest == null)
					EnsureRegistered();
				return _Regtest;
			}
		}

		public abstract string CryptoCode
		{
			get;
		}

		protected static IEnumerable<NetworkAddress> ToSeed(Tuple<byte[], int>[] tuples)
		{
			return tuples
					.Select(t => new NetworkAddress(new IPAddress(t.Item1), t.Item2))
					.ToArray();
		}
	}
}
