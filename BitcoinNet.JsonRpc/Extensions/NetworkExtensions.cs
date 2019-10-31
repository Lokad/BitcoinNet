using System;
using BitcoinNet.JsonRpc;

namespace BitcoinNet
{
	public enum RawFormat
	{
		Satoshi,
		BlockExplorer
	}

	public static class NetworkExtensions
	{
		public static RawFormatter GetFormatter(this Network network, RawFormat rawFormat)
		{
			RawFormatter formatter = null;
			switch (rawFormat)
			{
				case RawFormat.Satoshi:
					formatter = new SatoshiFormatter();
					break;
				case RawFormat.BlockExplorer:
					formatter = new BlockExplorerFormatter();
					break;
				default:
					throw new NotSupportedException(rawFormat.ToString());
			}

			formatter.Network = network ?? formatter.Network;
			return formatter;
		}
	}
}