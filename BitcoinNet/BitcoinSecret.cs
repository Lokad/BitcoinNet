using System;
using System.Linq;
using BitcoinNet.DataEncoders;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	public class BitcoinSecret : Base58Data, IDestination, ISecret
	{
		private BitcoinPubKeyAddress _address;
		private Key _key;

		public BitcoinSecret(Key key, Network network)
			: base(ToBytes(key), network)
		{
		}

		public BitcoinSecret(string base58, Network expectedAddress = null)
		{
			Init<BitcoinSecret>(base58, expectedAddress);
		}

		public virtual KeyId PubKeyHash => PrivateKey.PubKey.Hash;

		public PubKey PubKey => PrivateKey.PubKey;

		protected override bool IsValid
		{
			get
			{
				if (_vchData.Length != 33 && _vchData.Length != 32)
				{
					return false;
				}

				if (_vchData.Length == 33 && IsCompressed)
				{
					return true;
				}

				if (_vchData.Length == 32 && !IsCompressed)
				{
					return true;
				}

				return false;
			}
		}

		public bool IsCompressed => _vchData.Length > 32 && _vchData[32] == 1;

		public override Base58Type Type => Base58Type.SECRET_KEY;

		// IDestination Members

		public Script ScriptPubKey => GetAddress().ScriptPubKey;

		public Key PrivateKey => _key ?? (_key = new Key(_vchData, 32, IsCompressed));

		private static byte[] ToBytes(Key key)
		{
			var keyBytes = key.ToBytes();
			if (!key.IsCompressed)
			{
				return keyBytes;
			}

			return keyBytes.Concat(new byte[] {0x01}).ToArray();
		}

		public BitcoinPubKeyAddress GetAddress()
		{
			return _address ?? (_address = PrivateKey.PubKey.GetAddress(Network));
		}

		public BitcoinSecret Copy(bool? compressed)
		{
			if (compressed == null)
			{
				compressed = IsCompressed;
			}

			if (compressed.Value && IsCompressed)
			{
				return new BitcoinSecret(_wifData, Network);
			}

			var result = Encoders.Base58Check.DecodeData(_wifData);
			var resultList = result.ToList();

			if (compressed.Value)
			{
				resultList.Insert(resultList.Count, 0x1);
			}
			else
			{
				resultList.RemoveAt(resultList.Count - 1);
			}

			return new BitcoinSecret(Encoders.Base58Check.EncodeData(resultList.ToArray()), Network);
		}
	}
}