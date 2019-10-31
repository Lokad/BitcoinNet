using System;
using System.Reflection;
using BitcoinNet.Scripting;
using Newtonsoft.Json;

namespace BitcoinNet.JsonRpc.JsonConverters
{
	public class CoinJsonConverter : JsonConverter
	{
		public CoinJsonConverter(Network network)
		{
			Network = network;
		}

		public Network Network { get; set; }

		public override bool CanConvert(Type objectType)
		{
			return typeof(ICoin).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
			JsonSerializer serializer)
		{
			return reader.TokenType == JsonToken.Null ? null : serializer.Deserialize<CoinJson>(reader).ToCoin();
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			serializer.Serialize(writer, new CoinJson((ICoin) value, Network));
		}

		public class CoinJson
		{
			public CoinJson()
			{
			}

			public CoinJson(ICoin coin, Network network)
			{
				if (network == null)
				{
					network = Network.Main;
				}

				TransactionId = coin.Outpoint.Hash;
				Index = coin.Outpoint.N;
				ScriptPubKey = coin.TxOut.ScriptPubKey;
				if (coin is ScriptCoin)
				{
					RedeemScript = ((ScriptCoin) coin).Redeem;
				}

				if (coin is Coin)
				{
					Value = ((Coin) coin).Amount;
				}
			}

			public uint256 TransactionId { get; set; }

			public uint Index { get; set; }

			public Money Value { get; set; }

			public Script ScriptPubKey { get; set; }

			public Script RedeemScript { get; set; }

			[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
			public long Quantity { get; set; }

			public ICoin ToCoin()
			{
				var coin = RedeemScript == null
					? new Coin(new OutPoint(TransactionId, Index), new TxOut(Value, ScriptPubKey))
					: new ScriptCoin(new OutPoint(TransactionId, Index), new TxOut(Value, ScriptPubKey), RedeemScript);
				return coin;
			}
		}
	}
}