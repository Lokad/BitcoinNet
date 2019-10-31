using System;
using System.Net;

namespace BitcoinNet.Protocol
{
	public class NetworkAddress : IBitcoinSerializable
	{
		private byte[] _ip = new byte[16];
		internal uint _nTime;
		private ushort _port;
		private ulong _service = 1;
		private uint _version = 100100;

		public NetworkAddress()
		{
		}

		public NetworkAddress(IPEndPoint endpoint)
		{
			Endpoint = endpoint;
		}

		public NetworkAddress(IPAddress address, int port)
		{
			Endpoint = new IPEndPoint(address, port);
		}

		public ulong Service
		{
			get => _service;
			set => _service = value;
		}

		public TimeSpan Ago
		{
			get => DateTimeOffset.UtcNow - Time;
			set => Time = DateTimeOffset.UtcNow - value;
		}

		public IPEndPoint Endpoint
		{
			get => new IPEndPoint(new IPAddress(_ip), _port);
			set
			{
				_port = (ushort) value.Port;
				var ipBytes = value.Address.GetAddressBytes();
				if (ipBytes.Length == 16)
				{
					_ip = ipBytes;
				}
				else if (ipBytes.Length == 4)
				{
					//Convert to ipv4 mapped to ipv6
					//In these addresses, the first 80 bits are zero, the next 16 bits are one, and the remaining 32 bits are the IPv4 address
					_ip = new byte[16];
					Array.Copy(ipBytes, 0, _ip, 12, 4);
					Array.Copy(new byte[] {0xFF, 0xFF}, 0, _ip, 10, 2);
				}
				else
				{
					throw new NotSupportedException("Invalid IP address type");
				}
			}
		}

		public DateTimeOffset Time
		{
			get => Utils.UnixTimeToDateTime(_nTime);
			set => _nTime = Utils.DateTimeToUnixTime(value);
		}

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			if (stream.Type == SerializationType.Disk)
			{
				stream.ReadWrite(ref _version);
			}

			if (
				stream.Type == SerializationType.Disk ||
				stream.ProtocolCapabilities.SupportTimeAddress && stream.Type != SerializationType.Hash)
			{
				stream.ReadWrite(ref _nTime);
			}

			stream.ReadWrite(ref _service);
			stream.ReadWrite(ref _ip);
			using (stream.BigEndianScope())
			{
				stream.ReadWrite(ref _port);
			}
		}

		public void Adjust()
		{
			var nNow = Utils.DateTimeToUnixTime(DateTimeOffset.UtcNow);
			if (_nTime <= 100000000 || _nTime > nNow + 10 * 60)
			{
				_nTime = nNow - 5 * 24 * 60 * 60;
			}
		}

		public void ZeroTime()
		{
			_nTime = 0;
		}

		internal byte[] GetKey()
		{
			var vKey = new byte[18];
			Array.Copy(_ip, vKey, 16);
			vKey[16] = (byte) (_port / 0x100);
			vKey[17] = (byte) (_port & 0x0FF);
			return vKey;
		}
	}
}