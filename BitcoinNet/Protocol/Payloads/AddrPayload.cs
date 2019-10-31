using System.Linq;

namespace BitcoinNet.Protocol
{
	/// <summary>
	///     An available peer address in the bitcoin network is announce (unsollicited or after a getaddr)
	/// </summary>
	[Payload("addr")]
	public class AddrPayload : Payload, IBitcoinSerializable
	{
		private NetworkAddress[] _addrList = new NetworkAddress[0];

		public AddrPayload()
		{
		}

		public AddrPayload(NetworkAddress address)
		{
			_addrList = new[] {address};
		}

		public AddrPayload(NetworkAddress[] addresses)
		{
			_addrList = addresses.ToArray();
		}

		public NetworkAddress[] Addresses => _addrList;

		// IBitcoinSerializable Members

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _addrList);
		}

		public override string ToString()
		{
			return Addresses.Length + " address(es)";
		}
	}
}