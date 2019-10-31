using System.Net;
using System.Runtime.CompilerServices;
using BitcoinNet.Tests;

namespace BitcoinNet.JsonRpc.Tests
{
	public class NodeBuilderEx
	{
		public static NodeBuilder Create([CallerMemberName] string caller = null)
		{
			ServicePointManager.Expect100Continue = true;
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

			var builder = NodeBuilder.Create(NodeDownloadData.BitcoinCash.v0_18_2, BitcoinCash.Instance.Regtest,
				caller);
			return builder;
		}
	}
}