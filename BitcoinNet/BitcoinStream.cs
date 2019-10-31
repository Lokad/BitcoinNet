using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using BitcoinNet.Protocol;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	public enum SerializationType
	{
		Disk,
		Network,
		Hash
	}

	public class Scope : IDisposable
	{
		private readonly Action _close;

		public Scope(Action open, Action close)
		{
			_close = close;
			open();
		}

		public static IDisposable Nothing
		{
			get { return new Scope(() => { }, () => { }); }
		}

		// IDisposable Members

		public void Dispose()
		{
			_close();
		}
	}

	public partial class BitcoinStream
	{
		//ReadWrite<T>(ref T data)
		private static readonly MethodInfo ReadWriteTyped;
		private readonly bool _isNetworkStream;
		private ConsensusFactory _consensusFactory = Consensus.Main.ConsensusFactory;
		private PerformanceCounter _counter;

		static BitcoinStream()
		{
			ReadWriteTyped = typeof(BitcoinStream)
				.GetTypeInfo()
				.DeclaredMethods
				.Where(m => m.Name == "ReadWrite")
				.Where(m => m.IsGenericMethodDefinition)
				.Where(m => m.GetParameters().Length == 1)
				.First(m => m.GetParameters().Any(p =>
					p.ParameterType.IsByRef && p.ParameterType.HasElementType &&
					!p.ParameterType.GetElementType().IsArray));
		}

		public BitcoinStream(Stream inner, bool serializing)
		{
			Serializing = serializing;
			_isNetworkStream = inner is NetworkStream;
			Inner = inner;
		}

		public BitcoinStream(byte[] bytes)
			: this(new MemoryStream(bytes), false)
		{
		}

		public int MaxArraySize { get; set; } = 1024 * 1024;

		public Stream Inner { get; }

		public bool Serializing { get; }

		/// <summary>
		///     Set the format to use when serializing and deserializing consensus related types.
		/// </summary>
		public ConsensusFactory ConsensusFactory
		{
			get => _consensusFactory;
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}

				_consensusFactory = value;
			}
		}

		public PerformanceCounter Counter
		{
			get
			{
				if (_counter == null)
				{
					_counter = new PerformanceCounter();
				}

				return _counter;
			}
		}

		public bool IsBigEndian { get; set; }

		public TransactionOptions TransactionOptions { get; set; } = TransactionOptions.All;


		public SerializationType Type { get; set; }

		public CancellationToken ReadCancellationToken { get; set; }

		public Script ReadWrite(Script data)
		{
			if (Serializing)
			{
				var bytes = data == null ? Script.Empty.ToBytes(true) : data.ToBytes(true);
				ReadWriteAsVarString(ref bytes);
				return data;
			}
			else
			{
				byte[] bytes = null;
				VarString.StaticRead(this, ref bytes);
				return Script.FromBytesUnsafe(bytes);
			}
		}

		public void ReadWrite(ref Script script)
		{
			if (Serializing)
			{
				ReadWrite(script);
			}
			else
			{
				script = ReadWrite(script);
			}
		}

		public T ReadWrite<T>(T data) where T : IBitcoinSerializable
		{
			ReadWrite(ref data);
			return data;
		}

		public void ReadWriteAsVarString(ref byte[] bytes)
		{
			if (Serializing)
			{
				VarString.StaticWrite(this, bytes);
			}
			else
			{
				VarString.StaticRead(this, ref bytes);
			}
		}

		public void ReadWrite(Type type, ref object obj)
		{
			try
			{
				var parameters = new[] {obj};
				ReadWriteTyped.MakeGenericMethod(type).Invoke(this, parameters);
				obj = parameters[0];
			}
			catch (TargetInvocationException ex)
			{
				throw ex.InnerException;
			}
		}

		public void ReadWrite(ref byte data)
		{
			ReadWriteByte(ref data);
		}

		public byte ReadWrite(byte data)
		{
			ReadWrite(ref data);
			return data;
		}

		public void ReadWrite(ref bool data)
		{
			var d = data ? (byte) 1 : (byte) 0;
			ReadWriteByte(ref d);
			data = d != 0;
		}

		public void ReadWriteStruct<T>(ref T data) where T : struct, IBitcoinSerializable
		{
			data.ReadWrite(this);
		}

		public void ReadWriteStruct<T>(T data) where T : struct, IBitcoinSerializable
		{
			data.ReadWrite(this);
		}

		public void ReadWrite<T>(ref T data) where T : IBitcoinSerializable
		{
			var obj = data;
			if (obj == null)
			{
				if (!ConsensusFactory.TryCreateNew(out obj))
				{
					obj = Activator.CreateInstance<T>();
				}
			}

			obj.ReadWrite(this);
			if (!Serializing)
			{
				data = obj;
			}
		}

		public void ReadWrite<T>(ref List<T> list) where T : IBitcoinSerializable, new()
		{
			ReadWriteList<List<T>, T>(ref list);
		}

		public void ReadWrite<TList, TItem>(ref TList list)
			where TList : List<TItem>, new()
			where TItem : IBitcoinSerializable, new()
		{
			ReadWriteList<TList, TItem>(ref list);
		}

		private void ReadWriteList<TList, TItem>(ref TList data)
			where TList : List<TItem>, new()
			where TItem : IBitcoinSerializable, new()
		{
			var dataArray = data?.ToArray();
			if (Serializing && dataArray == null)
			{
				dataArray = new TItem[0];
			}

			ReadWriteArray(ref dataArray);
			if (!Serializing)
			{
				if (data == null)
				{
					data = new TList();
				}
				else
				{
					data.Clear();
				}

				data.AddRange(dataArray);
			}
		}

		public void ReadWrite(ref byte[] arr)
		{
			ReadWriteBytes(ref arr);
		}

		public void ReadWrite(ref Span<byte> arr)
		{
			ReadWriteBytes(arr);
		}

		public void ReadWrite(ref byte[] arr, int offset, int count)
		{
			ReadWriteBytes(ref arr, offset, count);
		}

		public void ReadWrite<T>(ref T[] arr) where T : IBitcoinSerializable, new()
		{
			ReadWriteArray(ref arr);
		}

		private void ReadWriteNumber(ref long value, int size)
		{
			var uvalue = unchecked((ulong) value);
			ReadWriteNumber(ref uvalue, size);
			value = unchecked((long) uvalue);
		}

		private void ReadWriteNumber(ref ulong value, int size)
		{
			if (_isNetworkStream && ReadCancellationToken.CanBeCanceled)
			{
				ReadWriteNumberInefficient(ref value, size);
				return;
			}

			Span<byte> bytes = stackalloc byte[size];
			for (var i = 0; i < size; i++)
			{
				bytes[i] = (byte) (value >> (i * 8));
			}

			if (IsBigEndian)
			{
				bytes.Reverse();
			}

			ReadWriteBytes(bytes);
			if (IsBigEndian)
			{
				bytes.Reverse();
			}

			ulong valueTemp = 0;
			for (var i = 0; i < bytes.Length; i++)
			{
				var v = (ulong) bytes[i];
				valueTemp += v << (i * 8);
			}

			value = valueTemp;
		}

		private void ReadWriteNumberInefficient(ref ulong value, int size)
		{
			var bytes = new byte[size];

			for (var i = 0; i < size; i++)
			{
				bytes[i] = (byte) (value >> (i * 8));
			}

			if (IsBigEndian)
			{
				Array.Reverse(bytes);
			}

			ReadWriteBytes(ref bytes);
			if (IsBigEndian)
			{
				Array.Reverse(bytes);
			}

			ulong valueTemp = 0;
			for (var i = 0; i < bytes.Length; i++)
			{
				var v = (ulong) bytes[i];
				valueTemp += v << (i * 8);
			}

			value = valueTemp;
		}

		private void ReadWriteBytes(ref byte[] data, int offset = 0, int count = -1)
		{
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			if (data.Length == 0)
			{
				return;
			}

			count = count == -1 ? data.Length : count;
			if (count == 0)
			{
				return;
			}

			ReadWriteBytes(new Span<byte>(data, offset, count));
		}

		private void ReadWriteBytes(Span<byte> data)
		{
			if (Serializing)
			{
				Inner.Write(data);
				Counter.AddBytesWritten(data.Length);
			}
			else
			{
				var readen = Inner.ReadEx(data, ReadCancellationToken);
				if (readen == 0)
				{
					throw new EndOfStreamException("No more byte to read");
				}

				Counter.AddBytesRead(readen);
			}
		}

		private void ReadWriteByte(ref byte data)
		{
			if (Serializing)
			{
				Inner.WriteByte(data);
				Counter.AddBytesWritten(1);
			}
			else
			{
				var readen = Inner.ReadByte();
				if (readen == -1)
				{
					throw new EndOfStreamException("No more byte to read");
				}

				data = (byte) readen;
				Counter.AddBytesRead(1);
			}
		}

		public IDisposable BigEndianScope()
		{
			var old = IsBigEndian;
			return new Scope(() => { IsBigEndian = true; },
				() => { IsBigEndian = old; });
		}


		public IDisposable ProtocolVersionScope(uint? version)
		{
			var old = ProtocolVersion;
			return new Scope(() => { ProtocolVersion = version; },
				() => { ProtocolVersion = old; });
		}

		public void CopyParameters(BitcoinStream from)
		{
			if (from == null)
			{
				throw new ArgumentNullException(nameof(from));
			}

			ProtocolVersion = from.ProtocolVersion;
			ConsensusFactory = from.ConsensusFactory;
			_protocolCapabilities = from._protocolCapabilities;
			IsBigEndian = from.IsBigEndian;
			MaxArraySize = from.MaxArraySize;
			Type = from.Type;
		}

		public IDisposable SerializationTypeScope(SerializationType value)
		{
			var old = Type;
			return new Scope(() => { Type = value; }, () => { Type = old; });
		}

		public IDisposable ConsensusFactoryScope(ConsensusFactory consensusFactory)
		{
			var old = ConsensusFactory;
			return new Scope(() => { ConsensusFactory = consensusFactory; }, () => { ConsensusFactory = old; });
		}

		public void ReadWriteAsVarInt(ref uint val)
		{
			if (Serializing)
			{
				VarInt.StaticWrite(this, val);
			}
			else
			{
				val = (uint) Math.Min(uint.MaxValue, VarInt.StaticRead(this));
			}
		}

		public void ReadWriteAsVarInt(ref ulong val)
		{
			if (Serializing)
			{
				VarInt.StaticWrite(this, val);
			}
			else
			{
				val = VarInt.StaticRead(this);
			}
		}

		public void ReadWriteAsCompactVarInt(ref uint val)
		{
			var value = new CompactVarInt(val, sizeof(uint));
			ReadWrite(ref value);
			if (!Serializing)
			{
				val = (uint) value.ToLong();
			}
		}

		public void ReadWriteAsCompactVarInt(ref ulong val)
		{
			var value = new CompactVarInt(val, sizeof(ulong));
			ReadWrite(ref value);
			if (!Serializing)
			{
				val = value.ToLong();
			}
		}

#pragma warning disable CS0618 // Type or member is obsolete
		private uint? _protocolVersion;

		public uint? ProtocolVersion
		{
			get => _protocolVersion;
			set
			{
				_protocolVersion = value;
				_protocolCapabilities = null;
			}
		}


		private ProtocolCapabilities _protocolCapabilities;
		public ProtocolCapabilities ProtocolCapabilities
		{
			get
			{
				var capabilities = _protocolCapabilities;
				if (capabilities == null)
				{
					capabilities = ProtocolVersion == null
						? ProtocolCapabilities.CreateSupportAll()
						: ConsensusFactory.GetProtocolCapabilities(ProtocolVersion.Value);
					_protocolCapabilities = capabilities;
				}

				return capabilities;
			}
		}
#pragma warning restore CS0618 // Type or member is obsolete
	}
}