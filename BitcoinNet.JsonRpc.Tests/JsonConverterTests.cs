﻿using System;
using BitcoinNet.JsonRpc.JsonConverters;
using BitcoinNet.Scripting;
using Xunit;

namespace BitcoinNet.JsonRpc.Tests
{
	public class JsonConverterTests
	{
		private T CanSerializeInJsonCore<T>(T value)
		{
			var str = Serializer.ToString(value);
			var obj2 = Serializer.ToObject<T>(str);
			Assert.Equal(str, Serializer.ToString(obj2));
			return obj2;
		}

		private class DummyClass
		{
			public BitcoinExtPubKey ExtPubKey { get; set; }
		}

		[Fact]
		public void CanSerializeCustomClass()
		{
			var str = Serializer.ToString(new DummyClass {ExtPubKey = new ExtKey().Neuter().GetWif(Network.RegTest)},
				Network.RegTest);
			Assert.NotNull(Serializer.ToObject<DummyClass>(str, Network.RegTest));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanSerializeInJson()
		{
			var k = new Key();
			CanSerializeInJsonCore(DateTimeOffset.UtcNow);
			CanSerializeInJsonCore(new byte[] {1, 2, 3});
			CanSerializeInJsonCore(k);
			CanSerializeInJsonCore(Money.Coins(5.0m));
			CanSerializeInJsonCore(k.PubKey.GetAddress(Network.Main));
			CanSerializeInJsonCore(new KeyPath("1/2"));
			CanSerializeInJsonCore(Network.Main);
			CanSerializeInJsonCore(new uint256(RandomUtils.GetBytes(32)));
			CanSerializeInJsonCore(new uint160(RandomUtils.GetBytes(20)));
			CanSerializeInJsonCore(k.PubKey.ScriptPubKey);
			CanSerializeInJsonCore(new Key().PubKey.Hash.GetAddress(Network.Main));
			CanSerializeInJsonCore(new Key().PubKey.Hash.ScriptPubKey.GetScriptAddress(Network.Main));
			var sig = k.Sign(new uint256(RandomUtils.GetBytes(32)));
			CanSerializeInJsonCore(sig);
			CanSerializeInJsonCore(new TransactionSignature(sig, SigHash.All));
			CanSerializeInJsonCore(k.PubKey.Hash);
			CanSerializeInJsonCore(k.PubKey.ScriptPubKey.Hash);
			CanSerializeInJsonCore(k);
			CanSerializeInJsonCore(k.PubKey);
			//CanSerializeInJsonCore(new WitScript(new Script(Op.GetPushOp(sig.ToDER()), Op.GetPushOp(sig.ToDER()))));
			CanSerializeInJsonCore(new LockTime(1));
			CanSerializeInJsonCore(new LockTime(DateTime.UtcNow));
		}
	}
}