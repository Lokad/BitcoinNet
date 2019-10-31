using System;
using BitcoinNet.Crypto;
using BitcoinNet.DataEncoders;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	public abstract class TxDestination : IDestination
	{
		private readonly byte[] _destBytes;
		private string _str;

		public TxDestination()
		{
			_destBytes = new byte[] {0};
		}

		public TxDestination(byte[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			_destBytes = value;
		}

		public TxDestination(string value)
		{
			_destBytes = Encoders.Hex.DecodeData(value);
			_str = value;
		}

		// IDestination Members

		public abstract Script ScriptPubKey { get; }

		public abstract BitcoinAddress GetAddress(Network network);

		public byte[] ToBytes()
		{
			return ToBytes(false);
		}

		public byte[] ToBytes(bool @unsafe)
		{
			if (@unsafe)
			{
				return _destBytes;
			}

			var array = new byte[_destBytes.Length];
			Array.Copy(_destBytes, array, _destBytes.Length);
			return array;
		}

		public override bool Equals(object obj)
		{
			var item = obj as TxDestination;
			if (item == null)
			{
				return false;
			}

			return Utils.ArrayEqual(_destBytes, item._destBytes) && item.GetType() == GetType();
		}

		public static bool operator ==(TxDestination a, TxDestination b)
		{
			if (ReferenceEquals(a, b))
			{
				return true;
			}

			if ((object) a == null || (object) b == null)
			{
				return false;
			}

			return Utils.ArrayEqual(a._destBytes, b._destBytes) && a.GetType() == b.GetType();
		}

		public static bool operator !=(TxDestination a, TxDestination b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return Utils.GetHashCode(_destBytes);
		}

		public override string ToString()
		{
			if (_str == null)
			{
				_str = Encoders.Hex.EncodeData(_destBytes);
			}

			return _str;
		}
	}

	public class KeyId : TxDestination
	{
		public KeyId()
			: this(0)
		{
		}

		public KeyId(byte[] value)
			: base(value)
		{
			if (value.Length != 20)
			{
				throw new ArgumentException("value should be 20 bytes", nameof(value));
			}
		}

		public KeyId(uint160 value)
			: base(value.ToBytes())
		{
		}

		public KeyId(string value)
			: base(value)
		{
		}

		public override Script ScriptPubKey => PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(this);

		public override BitcoinAddress GetAddress(Network network)
		{
			return new BitcoinPubKeyAddress(this, network);
		}
	}

	public class ScriptId : TxDestination
	{
		public ScriptId()
			: this(0)
		{
		}

		public ScriptId(byte[] value)
			: base(value)
		{
			if (value.Length != 20)
			{
				throw new ArgumentException("value should be 20 bytes", nameof(value));
			}
		}

		public ScriptId(uint160 value)
			: base(value.ToBytes())
		{
		}

		public ScriptId(string value)
			: base(value)
		{
		}

		public ScriptId(Script script)
			: this(Hashes.Hash160(script._script))
		{
		}

		public override Script ScriptPubKey => PayToScriptHashTemplate.Instance.GenerateScriptPubKey(this);

		public override BitcoinAddress GetAddress(Network network)
		{
			return new BitcoinScriptAddress(this, network);
		}
	}
}