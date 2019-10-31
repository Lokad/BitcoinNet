using System;
using System.IO;
using System.Net.Http;
using Xunit;

namespace BitcoinNet.Tests
{
	public class pow_tests
	{
		private static void EnsureDownloaded(string file, string url)
		{
			if (File.Exists(file))
			{
				return;
			}

			var client = new HttpClient();
			client.Timeout = TimeSpan.FromMinutes(5);
			var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
			File.WriteAllBytes(file, data);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public static void CanCalculatePowCorrectly()
		{
			var chain = new ConcurrentChain(Network.Main);
			EnsureDownloaded("MainChain.dat", "https://aois.blob.core.windows.net/public/MainChain.dat");
			chain.Load(File.ReadAllBytes("MainChain.dat"), Consensus.Main.ConsensusFactory);
			foreach (var block in chain.EnumerateAfter(chain.Genesis))
			{
				var thisWork = block.GetWorkRequired(Network.Main);
				var thisWork2 = block.Previous.GetNextWorkRequired(Network.Main);
				Assert.Equal(thisWork, thisWork2);
				Assert.True(block.CheckProofOfWorkAndTarget(Network.Main));
			}
		}
	}
}