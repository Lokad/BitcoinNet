using System;
using System.Collections.Generic;
using System.Text;

namespace BitcoinNet.Tests
{
	public partial class NodeDownloadData
	{
		public class BitcoinCashNodeDownloadData
		{
			public NodeDownloadData v0_18_2 = new NodeDownloadData()
			{
				Version = "0.18.2",
				Windows = new NodeOSDownloadData()
				{
					DownloadLink = "https://download.bitcoinabc.org/{0}/win/bitcoin-abc-{0}-win64.zip",
					Archive = "bitcoin-abc-{0}-win64.zip",
					Executable = "bitcoin-abc-{0}/bin/bitcoind.exe",
					Hash = "0F05B2D7157898D14F8E7B13270D93173862680E99B972E18D13BFF4E339ACE0"
				},
				Linux = new NodeOSDownloadData()
				{
					DownloadLink = "https://download.bitcoinabc.org/{0}/linux/bitcoin-abc-{0}-x86_64-linux-gnu.tar.gz",
					Archive = "bitcoin-abc-{0}-x86_64-linux-gnu.tar.gz",
					Executable = "bitcoin-abc-{0}/bin/bitcoind",
					Hash = "28D8511789A126AFF16E256A03288948F2660C3C8CB0A4C809C5A8618A519A16"
				},
				Mac = new NodeOSDownloadData()
				{
					DownloadLink = "https://download.bitcoinabc.org/{0}/osx/bitcoin-abc-{0}-osx64.tar.gz",
					Archive = "bitcoin-abc-{0}-osx64.tar.gz",
					Executable = "bitcoin-abc-{0}/bin/bitcoind",
					Hash = "1AE9CA15C2CE2546F7EF0B22029F26B72377B844D02D57C23D95AF11FAB4CDD9"
				},
			};
		}
		
		public static BitcoinCashNodeDownloadData BitcoinCash
		{
			get; set;
		} = new BitcoinCashNodeDownloadData();
	}
}
