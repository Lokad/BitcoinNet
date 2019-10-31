using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using BitcoinNet.Crypto;
using BitcoinNet.DataEncoders;

namespace BitcoinNet.Protocol
{
	public class Message : IBitcoinSerializable
	{
		private byte[] _buffer;
		private byte[] _command = new byte[12];
		private uint _magic;
		private Payload _payloadObject;

		/// <summary>
		///     When parsing, maybe Magic is already parsed
		/// </summary>
		private bool _skipMagic;

		public uint Magic
		{
			get => _magic;
			set => _magic = value;
		}

		public string Command
		{
			get => Encoders.ASCII.EncodeData(_command);
			private set => _command = Encoders.ASCII.DecodeData(value.Trim().PadRight(12, '\0'));
		}

		public Payload Payload
		{
			get => _payloadObject;
			set
			{
				_payloadObject = value;
				Command = _payloadObject.Command;
			}
		}

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			if (Payload == null && stream.Serializing)
			{
				throw new InvalidOperationException("Payload not affected");
			}

			if (stream.Serializing || !stream.Serializing && !_skipMagic)
			{
				stream.ReadWrite(ref _magic);
			}

			stream.ReadWrite(ref _command);
			var length = 0;
			uint checksum = 0;
			var hasChecksum = false;
			var payloadBytes = stream.Serializing ? GetPayloadBytes(stream, out length) : null;
			length = payloadBytes == null ? 0 : length;
			stream.ReadWrite(ref length);

			if (stream.ProtocolCapabilities.SupportCheckSum)
			{
				if (stream.Serializing)
				{
					checksum = Hashes.Hash256(payloadBytes, 0, length).GetLow32();
				}

				stream.ReadWrite(ref checksum);
				hasChecksum = true;
			}

			if (stream.Serializing)
			{
				stream.ReadWrite(ref payloadBytes, 0, length);
			}
			else
			{
				if (length > 0x02000000) //MAX_SIZE 0x02000000 Serialize.h
				{
					throw new FormatException("Message payload too big ( > 0x02000000 bytes)");
				}

				payloadBytes = _buffer == null || _buffer.Length < length ? new byte[length] : _buffer;
				stream.ReadWrite(ref payloadBytes, 0, length);

				if (hasChecksum)
				{
					if (!VerifyChecksum(checksum, payloadBytes, length))
					{
						if (NodeServerTrace.Trace.Switch.ShouldTrace(TraceEventType.Verbose))
						{
							NodeServerTrace.Trace.TraceEvent(TraceEventType.Verbose, 0,
								"Invalid message checksum bytes");
						}

						throw new FormatException("Message checksum invalid");
					}
				}

				var payloadStream = new BitcoinStream(payloadBytes);
				payloadStream.CopyParameters(stream);

				var payloadType = PayloadAttribute.GetCommandType(Command);
				var unknown = payloadType == typeof(UnknowPayload);
				if (unknown)
				{
					NodeServerTrace.Trace.TraceEvent(TraceEventType.Warning, 0,
						"Unknown command received : " + Command);
				}

				object payload = _payloadObject;
				payloadStream.ReadWrite(payloadType, ref payload);
				if (unknown)
				{
					((UnknowPayload) payload)._command = Command;
				}

				Payload = (Payload) payload;
			}
		}

		public bool IfPayloadIs<TPayload>(Action<TPayload> action) where TPayload : Payload
		{
			var payload = Payload as TPayload;
			if (payload != null)
			{
				action(payload);
			}

			return payload != null;
		}

		private byte[] GetPayloadBytes(BitcoinStream stream, out int length)
		{
			var ms = _buffer == null ? new MemoryStream() : new MemoryStream(_buffer);
			var stream2 = new BitcoinStream(ms, true);
			stream2.CopyParameters(stream);
			Payload.ReadWrite(stream2);
			length = (int) ms.Position;
			return _buffer ?? GetBuffer(ms);
		}

		private static byte[] GetBuffer(MemoryStream ms)
		{
			// TODO (Osman): Shouldn't it be `ms.GetBuffer()`?
			return ms.ToArray();
		}

		internal static bool VerifyChecksum(uint256 checksum, byte[] payload, int length)
		{
			return checksum == Hashes.Hash256(payload, 0, length).GetLow32();
		}

		public override string ToString()
		{
			return $"{Command} : {Payload}";
		}

		public static Message ReadNext(Socket socket, Network network, uint version,
			CancellationToken cancellationToken)
		{
			return ReadNext(socket, network, version, cancellationToken, out _);
		}

		public static Message ReadNext(Socket socket, Network network, uint version,
			CancellationToken cancellationToken, out PerformanceCounter counter)
		{
			return ReadNext(socket, network, version, cancellationToken, null, out counter);
		}

		public static Message ReadNext(Socket socket, Network network, uint version,
			CancellationToken cancellationToken, byte[] buffer, out PerformanceCounter counter)
		{
			var stream = new NetworkStream(socket, false);
			return ReadNext(stream, network, version, cancellationToken, buffer, out counter);
		}

		public static Message ReadNext(Stream stream, Network network, uint version,
			CancellationToken cancellationToken)
		{
			return ReadNext(stream, network, version, cancellationToken, out _);
		}

		public static Message ReadNext(Stream stream, Network network, uint version,
			CancellationToken cancellationToken, out PerformanceCounter counter)
		{
			return ReadNext(stream, network, version, cancellationToken, null, out counter);
		}

		public static Message ReadNext(Stream stream, Network network, uint version,
			CancellationToken cancellationToken, byte[] buffer, out PerformanceCounter counter)
		{
			var bitStream = new BitcoinStream(stream, false)
			{
				ProtocolVersion = version,
				ReadCancellationToken = cancellationToken,
				ConsensusFactory = network.Consensus.ConsensusFactory
			};

			if (!network.ReadMagic(stream, cancellationToken, true))
			{
				throw new FormatException("Magic incorrect, the message comes from another network");
			}

			var message = new Message {_buffer = buffer};
			using (message.SkipMagicScope(true))
			{
				message.Magic = network.Magic;
				message.ReadWrite(bitStream);
			}

			counter = bitStream.Counter;
			return message;
		}

		private IDisposable SkipMagicScope(bool value)
		{
			var old = _skipMagic;
			return new Scope(() => _skipMagic = value, () => _skipMagic = old);
		}
	}
}