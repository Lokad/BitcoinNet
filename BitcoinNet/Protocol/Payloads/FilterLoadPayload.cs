using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinNet.Protocol
{
	/// <summary>
	/// Load a bloomfilter in the peer, used by SPV clients
	/// </summary>
	[Payload("filterload")]
	public class FilterLoadPayload : BitcoinSerializablePayload<BloomFilter>
	{
		public FilterLoadPayload()
		{

		}
		public FilterLoadPayload(BloomFilter filter)
			: base(filter)
		{

		}
	}
}
