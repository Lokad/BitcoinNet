using System;

namespace BitcoinNet.Protocol
{
	public class ProtocolException : Exception
	{
		public ProtocolException(string message)
			: base(message)
		{
		}
	}
}