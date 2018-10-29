using System;
using System.Linq;
using BitcoinNet.DataEncoders;
using Xunit;

namespace BitcoinNet.Tests
{
	public class BitcoinAddressTest
	{
		private struct TestVector
		{
			public Base58Type Type;
			public string Hash;
			public string Legacy;
			public string CashAddr;
		}

		// Reference: https://github.com/bitcoincashorg/bitcoincash.org/blob/master/spec/cashaddr.md
		private static readonly TestVector[] TestVectorArray =
		{
			new TestVector
			{
				Type = Base58Type.PUBKEY_ADDRESS,
				Hash = "76a04053bda0a88bda5177b86a15c3b29f559873",
				Legacy = "1BpEi6DfDAUFd7GtittLSdBeYJvcoaVggu",
				CashAddr = "bitcoincash:qpm2qsznhks23z7629mms6s4cwef74vcwvy22gdx6a"
			},
			new TestVector
			{
				Type = Base58Type.PUBKEY_ADDRESS,
				Hash = "cb481232299cd5743151ac4b2d63ae198e7bb0a9",
				Legacy = "1KXrWXciRDZUpQwQmuM1DbwsKDLYAYsVLR",
				CashAddr = "bitcoincash:qr95sy3j9xwd2ap32xkykttr4cvcu7as4y0qverfuy"
			},
			new TestVector
			{
				Type = Base58Type.PUBKEY_ADDRESS,
				Hash = "011f28e473c95f4013d7d53ec5fbc3b42df8ed10",
				Legacy = "16w1D5WRVKJuZUsSRzdLp9w3YGcgoxDXb",
				CashAddr = "bitcoincash:qqq3728yw0y47sqn6l2na30mcw6zm78dzqre909m2r"
			},
			new TestVector
			{
				Type = Base58Type.SCRIPT_ADDRESS,
				Hash = "76a04053bda0a88bda5177b86a15c3b29f559873",
				Legacy = "3CWFddi6m4ndiGyKqzYvsFYagqDLPVMTzC",
				CashAddr = "bitcoincash:ppm2qsznhks23z7629mms6s4cwef74vcwvn0h829pq"
			},
			new TestVector
			{
				Type = Base58Type.SCRIPT_ADDRESS,
				Hash = "cb481232299cd5743151ac4b2d63ae198e7bb0a9",
				Legacy = "3LDsS579y7sruadqu11beEJoTjdFiFCdX4",
				CashAddr = "bitcoincash:pr95sy3j9xwd2ap32xkykttr4cvcu7as4yc93ky28e"
			},
			new TestVector
			{
				Type = Base58Type.SCRIPT_ADDRESS,
				Hash = "011f28e473c95f4013d7d53ec5fbc3b42df8ed10",
				Legacy = "31nwvkZwyPdgzjBJZXfDmSWsC4ZLKpYyUw",
				CashAddr = "bitcoincash:pqq3728yw0y47sqn6l2na30mcw6zm78dzq5ucqzc37"
			}
		};

		private Base58Type GetAddressType(BitcoinAddress address)
		{
			switch (address)
			{
				case BitcoinPubKeyAddress pubKey:
					return pubKey.Type;

				case BitcoinScriptAddress scriptKey:
					return scriptKey.Type;

				default:
					throw new InvalidOperationException();
			}
		}

		private CashFormat GetAddressFormat(BitcoinAddress address)
		{
			switch (address)
			{
				case BitcoinPubKeyAddress pubKey:
					return pubKey.Format;

				case BitcoinScriptAddress scriptKey:
					return scriptKey.Format;

				default:
					throw new InvalidOperationException();
			}
		}

		private BitcoinAddress ConvertAddressToLegacy(BitcoinAddress address)
		{
			switch (address)
			{
				case BitcoinPubKeyAddress pubKey:
					return pubKey.ToLegacy();

				case BitcoinScriptAddress scriptKey:
					return scriptKey.ToLegacy();

				default:
					throw new InvalidOperationException();
			}
		}

		private BitcoinAddress ConvertAddressToCashAddr(BitcoinAddress address)
		{
			switch (address)
			{
				case BitcoinPubKeyAddress pubKey:
					return pubKey.ToCashAddr();

				case BitcoinScriptAddress scriptKey:
					return scriptKey.ToCashAddr();

				default:
					throw new InvalidOperationException();
			}
		}

		private byte[] GetHashBytes(BitcoinAddress address)
		{
			switch (address)
			{
				case BitcoinPubKeyAddress pubKey:
					return pubKey.Hash.ToBytes();

				case BitcoinScriptAddress scriptKey:
					return scriptKey.Hash.ToBytes();

				default:
					throw new InvalidOperationException();
			}
		}

		private void ValidateConversion<T>(TestVector vector, BitcoinAddress address) where T : BitcoinAddress
		{
			Assert.NotNull(address);

			var key = address as T;

			// Perform some validations
			Assert.NotNull(key);
			Assert.Equal(vector.Type, GetAddressType(key));
			Assert.Equal(CashFormat.Legacy, GetAddressFormat(key));
			Assert.Equal(vector.Legacy, key.ToString());

			// Convert to CashAddr format
			var cashAddrKey = ConvertAddressToCashAddr(key);

			// And perform some validations
			Assert.NotNull(cashAddrKey);
			Assert.Equal(vector.Type, GetAddressType(cashAddrKey));
			Assert.Equal(CashFormat.Cashaddr, GetAddressFormat(cashAddrKey));
			Assert.Equal(vector.CashAddr, cashAddrKey.ToString());

			// Convert back to legacy and validate it
			var legacyAddrKey = ConvertAddressToLegacy(cashAddrKey);
			Assert.NotNull(legacyAddrKey);
			Assert.Equal(vector.Type, GetAddressType(legacyAddrKey));
			Assert.Equal(CashFormat.Legacy, GetAddressFormat(legacyAddrKey));
			Assert.Equal(vector.Legacy, legacyAddrKey.ToString());

			// Raw mode validation
			var legacyHashBytes = GetHashBytes(legacyAddrKey);
			var legacyHashString = Encoders.Hex.EncodeData(legacyHashBytes);
			Assert.Equal(vector.Hash, legacyHashString);

			var cashAddrHashBytes = GetHashBytes(cashAddrKey);
			var cashAddrHashString = Encoders.Hex.EncodeData(cashAddrHashBytes);
			Assert.Equal(vector.Hash, cashAddrHashString);
			
			// Try to recreate addresses with hashes
			switch (address)
			{
				case BitcoinPubKeyAddress pubKey:
					var pubKey2 = new BitcoinPubKeyAddress(pubKey.Hash, pubKey.Network);
					Assert.Equal(vector.CashAddr, pubKey2.ToString());
					break;

				case BitcoinScriptAddress scriptKey:
					var scriptKey2 = new BitcoinScriptAddress(scriptKey.Hash, scriptKey.Network);
					Assert.Equal(vector.CashAddr, scriptKey2.ToString());
					break;

				default:
					throw new InvalidOperationException();
			}
		}

		[Fact]
		public void PublicKeyAddressConversion()
		{
			foreach (var vector in TestVectorArray.Where(x => x.Type == Base58Type.PUBKEY_ADDRESS))
			{
				var addr = BitcoinAddress.Create(vector.Legacy, Network.Main);
				Assert.NotNull(addr);

				var legacyPubKey = new BitcoinPubKeyAddress(vector.Legacy);
				Assert.Equal(vector.Legacy, legacyPubKey.ToString());
				Assert.Equal(Network.Main, legacyPubKey.Network);
				Assert.Equal(vector.Hash, Encoders.Hex.EncodeData(legacyPubKey.Hash.ToBytes()));

				var cashAddrPubKey = new BitcoinPubKeyAddress(vector.CashAddr);
				Assert.Equal(vector.CashAddr, cashAddrPubKey.ToString());
				Assert.Equal(Network.Main, cashAddrPubKey.Network);
				Assert.Equal(vector.Hash, Encoders.Hex.EncodeData(cashAddrPubKey.Hash.ToBytes()));

				ValidateConversion<BitcoinPubKeyAddress>(vector, addr);
			}
		}

		[Fact]
		public void ScriptKeyAddressConversion()
		{
			foreach (var vector in TestVectorArray.Where(x => x.Type == Base58Type.SCRIPT_ADDRESS))
			{
				var addr = BitcoinAddress.Create(vector.Legacy, Network.Main);
				Assert.NotNull(addr);

				var legacyScriptKey = new BitcoinScriptAddress(vector.Legacy);
				Assert.Equal(vector.Legacy, legacyScriptKey.ToString());
				Assert.Equal(Network.Main, legacyScriptKey.Network);
				Assert.Equal(vector.Hash, Encoders.Hex.EncodeData(legacyScriptKey.Hash.ToBytes()));

				var cashAddrScriptKey = new BitcoinScriptAddress(vector.CashAddr);
				Assert.Equal(vector.CashAddr, cashAddrScriptKey.ToString());
				Assert.Equal(Network.Main, cashAddrScriptKey.Network);
				Assert.Equal(vector.Hash, Encoders.Hex.EncodeData(cashAddrScriptKey.Hash.ToBytes()));

				ValidateConversion<BitcoinScriptAddress>(vector, addr);
			}
		}


		[Fact]
		public void ShouldThrowBase58Exception()
		{
			var key = "";
			Assert.Throws<FormatException>(() => BitcoinAddress.Create(key, Network.Main));

			key = null;
			Assert.Throws<ArgumentNullException>(() => BitcoinAddress.Create(key, Network.Main));
		}

		[Fact]
		public void TestVectorValidation()
		{
			foreach (var vector in TestVectorArray)
			{
				Assert.True(vector.Type == Base58Type.PUBKEY_ADDRESS || vector.Type == Base58Type.SCRIPT_ADDRESS);

				var legacyAddr = BitcoinAddress.Create(vector.Legacy, Network.Main);
				Assert.NotNull(legacyAddr);
				Assert.Equal(CashFormat.Legacy, GetAddressFormat(legacyAddr));
				Assert.Equal(vector.Legacy, legacyAddr.ToString());

				var cashAddr = BitcoinAddress.Create(vector.CashAddr, Network.Main);
				Assert.NotNull(cashAddr);
				Assert.Equal(CashFormat.Cashaddr, GetAddressFormat(cashAddr));
				Assert.Equal(vector.CashAddr, cashAddr.ToString());
			}
		}
	}
}