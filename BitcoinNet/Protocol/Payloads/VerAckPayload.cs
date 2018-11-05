using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinNet.Protocol
{
	[Payload("verack")]
	public class VerAckPayload : Payload, IBitcoinSerializable
	{
		// IBitcoinSerializable Members

		public override void ReadWriteCore(BitcoinStream stream)
		{
		}
	}
}
