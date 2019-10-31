using System;
using System.Net;
using System.Reflection;
using BitcoinNet.DataEncoders;

namespace BitcoinNet.Protocol
{
	[Flags]
	public enum NodeServices : ulong
	{
		Nothing = 0,

		/// <summary>
		///     NODE_NETWORK means that the node is capable of serving the block chain. It is currently
		///     set by all Bitcoin Core nodes, and is unset by SPV clients or other peers that just want
		///     network services but don't provide them.
		/// </summary>
		Network = 1 << 0,

		/// <summary>
		///     NODE_GETUTXO means the node is capable of responding to the getutxo protocol request.
		///     Bitcoin Core does not support this but a patch set called Bitcoin XT does.
		///     See BIP 64 for details on how this is implemented.
		/// </summary>
		GetUTXO = 1 << 1,

		/// <summary>
		///     NODE_BLOOM means the node is capable and willing to handle bloom-filtered connections.
		///     Bitcoin Core nodes used to support this by default, without advertising this bit,
		///     but no longer do as of protocol version 70011 (= NO_BLOOM_VERSION)
		/// </summary>
		NODE_BLOOM = 1 << 2,

		/// <summary>
		///     Indicates that a node can be asked for blocks and transactions including
		///     witness data.
		/// </summary>
		NODE_WITNESS = 1 << 3,

		/// <summary>
		///     NODE_NETWORK_LIMITED means the same as NODE_NETWORK with the limitation of only
		///     serving the last 288 (2 day) blocks
		///     See BIP159 for details on how this is implemented.
		/// </summary>
		NODE_NETWORK_LIMITED = 1 << 10
	}

	[Payload("version")]
	public class VersionPayload : Payload, IBitcoinSerializable
	{
		private static string _nUserAgent;
		private NetworkAddress _addrFrom = new NetworkAddress();
		private NetworkAddress _addrRecv = new NetworkAddress();

		private ulong _nonce;

		private bool _relay;
		private ulong _services;
		private int _startHeight;

		private long _timestamp;

		private VarString _userAgent;
		private uint _version;

		public uint Version
		{
			get => _version;
			set => _version = value;
		}

		public NodeServices Services
		{
			get => (NodeServices) _services;
			set => _services = (ulong) value;
		}

		public DateTimeOffset Timestamp
		{
			get => Utils.UnixTimeToDateTime((uint) _timestamp);
			set => _timestamp = Utils.DateTimeToUnixTime(value);
		}

		public IPEndPoint AddressReceiver
		{
			get => _addrRecv.Endpoint;
			set => _addrRecv.Endpoint = value;
		}

		public IPEndPoint AddressFrom
		{
			get => _addrFrom.Endpoint;
			set => _addrFrom.Endpoint = value;
		}

		public ulong Nonce
		{
			get => _nonce;
			set => _nonce = value;
		}

		public int StartHeight
		{
			get => _startHeight;
			set => _startHeight = value;
		}

		public bool Relay
		{
			get => _relay;
			set => _relay = value;
		}

		public string UserAgent
		{
			get => Encoders.ASCII.EncodeData(_userAgent.GetString());
			set => _userAgent = new VarString(Encoders.ASCII.DecodeData(value));
		}

		public static string GetBitcoinNetUserAgent()
		{
			if (_nUserAgent == null)
			{
				var version = typeof(VersionPayload).GetTypeInfo().Assembly.GetName().Version;
				_nUserAgent = "/BitcoinNet:" + version.Major + "." + version.MajorRevision + "." + version.Build + "/";
			}

			return _nUserAgent;
		}

		// IBitcoinSerializable Members

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite(ref _version);
			using (stream.ProtocolVersionScope(_version))
			{
				stream.ReadWrite(ref _services);
				stream.ReadWrite(ref _timestamp);
				using (stream.SerializationTypeScope(SerializationType.Hash)) //No time field in version message
				{
					stream.ReadWrite(ref _addrRecv);
				}

				if (_version >= 106)
				{
					using (stream.SerializationTypeScope(SerializationType.Hash)) //No time field in version message
					{
						stream.ReadWrite(ref _addrFrom);
					}

					stream.ReadWrite(ref _nonce);
					stream.ReadWrite(ref _userAgent);
					if (!stream.ProtocolCapabilities.SupportUserAgent)
					{
						if (_userAgent.Length != 0)
						{
							throw new FormatException("Should not find user agent for current version " + _version);
						}
					}

					stream.ReadWrite(ref _startHeight);
					if (_version >= 70001)
					{
						stream.ReadWrite(ref _relay);
					}
				}
			}
		}

		public override string ToString()
		{
			return Version.ToString();
		}
	}
}