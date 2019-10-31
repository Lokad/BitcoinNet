using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using BitcoinNet.Protocol;
using Xunit;

namespace BitcoinNet.Tests
{
	public class addrman_tests
	{
		private IPAddress RandomAddress(Random rand)
		{
			if (rand.Next(0, 100) == 0)
			{
				return IPAddress.Parse("1.2.3.4"); //Simulate collision
			}

			var count = rand.Next(0, 2) % 2 == 0 ? 4 : 16;
			return new IPAddress(RandomUtils.GetBytes(count));
		}

		private NetworkAddress RandomNetworkAddress(Random rand)
		{
			var addr = RandomAddress(rand);
			var p = rand.Next(0, ushort.MaxValue);
			return new NetworkAddress(new IPEndPoint(addr, p));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanSerializeDeserializePeerTable()
		{
			var addrman = new AddressManager();
			addrman.SavePeerFile("CanSerializeDeserializePeerTable.dat", Network.Main);
			AddressManager.LoadPeerFile("CanSerializeDeserializePeerTable.dat", Network.Main);

			addrman = AddressManager.LoadPeerFile("../../../data/peers.dat", Network.Main);
			addrman.DebugMode = true;
			addrman.Check();
			addrman.SavePeerFile("serializerPeer.dat", Network.Main);

			var addrman2 = AddressManager.LoadPeerFile("serializerPeer.dat", Network.Main);
			addrman2.DebugMode = true;
			addrman2.Check();
			addrman2.SavePeerFile("serializerPeer2.dat", Network.Main);

			var original = File.ReadAllBytes("serializerPeer2.dat");
			var after = File.ReadAllBytes("serializerPeer.dat");
			Assert.True(original.SequenceEqual(after));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanStressAddrManager()
		{
			Exception exception = null;
			var addrmanager = new AddressManager();
			var randl = new Random();
			for (var i = 0; i < 30; i++)
			{
				var address = RandomNetworkAddress(randl);
				var addressSource = RandomAddress(randl);
				address.Ago = TimeSpan.FromMinutes(5.0);
				addrmanager.Add(address, addressSource);
			}

			addrmanager.DebugMode = true;
			var threads =
				Enumerable
					.Range(0, 20)
					.Select(t => new Thread(() =>
					{
						try
						{
							var rand = new Random(t);
							for (var i = 0; i < 50; i++)
							{
								var address = RandomNetworkAddress(rand);
								var addressSource = RandomAddress(rand);
								var operation = rand.Next(0, 7);
								switch (operation)
								{
									case 0:
										addrmanager.Attempt(address);
										break;
									case 1:
										addrmanager.Add(address, addressSource);
										break;
									case 2:
										addrmanager.Select();
										break;
									case 3:
										addrmanager.GetAddr();
										break;
									case 4:
									{
										var several = addrmanager.GetAddr();
										addrmanager.Good(several.Length == 0 ? address : several[0]);
									}
										break;

									case 5:
										addrmanager.Connected(address);
										break;
									case 6:
										addrmanager.ToBytes();
										break;
									default:
										throw new NotSupportedException();
								}
							}
						}
						catch (Exception ex)
						{
							exception = ex;
							throw;
						}
					})).ToArray();
			foreach (var t in threads)
			{
				t.Start();
			}

			foreach (var t in threads)
			{
				t.Join();
			}

			Assert.True(addrmanager._nNew != 0);
			Assert.True(addrmanager._nTried != 0);
			Assert.True(addrmanager.GetAddr().Length != 0);
			Assert.Null(exception);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanUseAddrManager()
		{
			var addrman = new AddressManager();
			addrman.DebugMode = true;
			var localhost = new NetworkAddress(IPAddress.Parse("127.0.0.1"), 8333);
			addrman.Add(localhost, localhost.Endpoint.Address);
			Assert.NotNull(addrman._nKey);
			Assert.True(addrman._nKey != new uint256(0));
			Assert.True(addrman._nNew == 1);
			addrman.Good(localhost);
			Assert.True(addrman._nNew == 0);
			Assert.True(addrman._nTried == 1);
			addrman.Attempt(localhost);

			var addr = addrman.Select();
			Assert.False(addr.Ago < TimeSpan.FromSeconds(10.0));

			addrman.Connected(localhost);

			addr = addrman.Select();
			Assert.True(addr.Ago < TimeSpan.FromSeconds(10.0));
		}
	}
}