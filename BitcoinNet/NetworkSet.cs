using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BitcoinNet.Protocol;

namespace BitcoinNet
{
	public interface INetworkSet
	{
		Network Mainnet { get; }

		Network Testnet { get; }

		Network Regtest { get; }

		string CryptoCode { get; }

		Network GetNetwork(NetworkType networkType);
	}

	public abstract class NetworkSetBase : INetworkSet
	{
		private readonly object _lock = new object();
		private Network _mainnet;
		private volatile bool _registered;
		private volatile bool _registering;
		private Network _regtest;
		private Network _testnet;

		public Network GetNetwork(NetworkType networkType)
		{
			switch (networkType)
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

		public Network Mainnet
		{
			get
			{
				if (_mainnet == null)
				{
					EnsureRegistered();
				}

				return _mainnet;
			}
		}

		public Network Testnet
		{
			get
			{
				if (_testnet == null)
				{
					EnsureRegistered();
				}

				return _testnet;
			}
		}

		public Network Regtest
		{
			get
			{
				if (_regtest == null)
				{
					EnsureRegistered();
				}

				return _regtest;
			}
		}

		public abstract string CryptoCode { get; }

		public void EnsureRegistered()
		{
			if (_registered)
			{
				return;
			}

			lock (_lock)
			{
				if (_registered)
				{
					return;
				}

				if (_registering)
				{
					throw new InvalidOperationException(
						"It seems like you are recursively accessing a Network which is not yet built.");
				}

				_registering = true;
				var builder = CreateMainnet();
				builder.SetNetworkType(NetworkType.Mainnet);
				builder.SetNetworkSet(this);
				_mainnet = builder.BuildAndRegister();
				builder = CreateTestnet();
				builder.SetNetworkType(NetworkType.Testnet);
				builder.SetNetworkSet(this);
				_testnet = builder.BuildAndRegister();
				builder = CreateRegtest();
				builder.SetNetworkType(NetworkType.Regtest);
				builder.SetNetworkSet(this);
				_regtest = builder.BuildAndRegister();
				PostInit();
				_registered = true;
				_registering = false;
			}
		}

		protected virtual void PostInit()
		{
		}

		protected abstract NetworkBuilder CreateMainnet();
		protected abstract NetworkBuilder CreateTestnet();
		protected abstract NetworkBuilder CreateRegtest();

		protected static IEnumerable<NetworkAddress> ToSeed(Tuple<byte[], int>[] tuples)
		{
			return tuples
				.Select(t => new NetworkAddress(new IPAddress(t.Item1), t.Item2))
				.ToArray();
		}
	}
}