﻿namespace BitcoinNet.Protocol
{
	/// <summary>
	///     Ask for known peer addresses in the network
	/// </summary>
	[Payload("getaddr")]
	public class GetAddrPayload : Payload
	{
	}
}