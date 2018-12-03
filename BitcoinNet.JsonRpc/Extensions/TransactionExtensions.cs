using System;
using BitcoinNet.JsonRpc;

namespace BitcoinNet
{
	public static class TransactionExtensions
	{
		public static string ToJsonString(this Transaction transaction)
		{
			return ToJsonString(transaction, RawFormat.BlockExplorer);
		}

		public static string ToJsonString(this Transaction transaction, RawFormat rawFormat, Network network = null)
		{
			var formatter = network.GetFormatter(rawFormat);
			return ToJsonString(transaction, formatter);
		}
		
		private static string ToJsonString(Transaction transaction, RawFormatter formatter)
		{
			if (formatter == null)
			{
				throw new ArgumentNullException(nameof(formatter));
			}

			return formatter.ToString(transaction);
		}
	}
}