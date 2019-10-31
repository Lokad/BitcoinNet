using System;
using System.IO;

namespace BitcoinNet
{
	public interface IBitcoinSerializable
	{
		void ReadWrite(BitcoinStream stream);
	}

	public static class BitcoinSerializableExtensions
	{
		[Obsolete(
			"Use ReadWrite(this IBitcoinSerializable serializable, Stream stream, bool serializing, Network network, uint? version = null) or ReadWrite(new BitcoinStream(bytes)) if no network context")]
		public static void ReadWrite(this IBitcoinSerializable serializable, Stream stream, bool serializing,
			uint? version = null)
		{
			var s = new BitcoinStream(stream, serializing)
			{
				ProtocolVersion = version
			};
			serializable.ReadWrite(s);
		}

		public static void ReadWrite(this IBitcoinSerializable serializable, Stream stream, bool serializing,
			Network network, uint? version = null)
		{
			serializable.ReadWrite(stream, serializing, network?.Consensus?.ConsensusFactory, version);
		}

		public static void ReadWrite(this IBitcoinSerializable serializable, Stream stream, bool serializing,
			ConsensusFactory consensusFactory, uint? version = null)
		{
			var s = new BitcoinStream(stream, serializing)
			{
				ProtocolVersion = version
			};
			if (consensusFactory != null)
			{
				s.ConsensusFactory = consensusFactory;
			}

			serializable.ReadWrite(s);
		}

		public static int GetSerializedSize(this IBitcoinSerializable serializable, uint? version,
			SerializationType serializationType)
		{
			var s = new BitcoinStream(Stream.Null, true) {Type = serializationType, ProtocolVersion = version};
			s.ReadWrite(serializable);
			return (int) s.Counter.BytesWritten;
		}

		public static int GetSerializedSize(this IBitcoinSerializable serializable, TransactionOptions options)
		{
			var bms = new BitcoinStream(Stream.Null, true) {TransactionOptions = options};
			serializable.ReadWrite(bms);
			return (int) bms.Counter.BytesWritten;
		}

		public static int GetSerializedSize(this IBitcoinSerializable serializable, uint? version = null)
		{
			return GetSerializedSize(serializable, version, SerializationType.Disk);
		}

		[Obsolete(
			"Use ReadWrite(this IBitcoinSerializable serializable, byte[] bytes, Network network, uint? version = null) or ReadWrite(new BitcoinStream(bytes)) if no network context")]
		public static void ReadWrite(this IBitcoinSerializable serializable, byte[] bytes, uint? version = null)
		{
			ReadWrite(serializable, new MemoryStream(bytes), false, version);
		}

		public static void ReadWrite(this IBitcoinSerializable serializable, byte[] bytes, Network network,
			uint? version = null)
		{
			ReadWrite(serializable, new MemoryStream(bytes), false, network, version);
		}

		public static void ReadWrite(this IBitcoinSerializable serializable, byte[] bytes,
			ConsensusFactory consensusFactory, uint? version = null)
		{
			ReadWrite(serializable, new MemoryStream(bytes), false, consensusFactory, version);
		}

		public static void FromBytes(this IBitcoinSerializable serializable, byte[] bytes, uint? version = null)
		{
			serializable.ReadWrite(new BitcoinStream(bytes)
			{
				ProtocolVersion = version
			});
		}

		public static T Clone<T>(this T serializable, uint? version = null) where T : IBitcoinSerializable, new()
		{
			var instance = new T();
			instance.FromBytes(serializable.ToBytes(version), version);
			return instance;
		}

		public static byte[] ToBytes(this IBitcoinSerializable serializable, uint? version = null)
		{
			var ms = new MemoryStream();
			serializable.ReadWrite(new BitcoinStream(ms, true)
			{
				ProtocolVersion = version
			});
			return ToArrayEfficient(ms);
		}

		public static byte[] ToArrayEfficient(this MemoryStream ms)
		{
			var bytes = ms.GetBuffer();
			Array.Resize(ref bytes, (int) ms.Length);
			return bytes;
		}
	}
}