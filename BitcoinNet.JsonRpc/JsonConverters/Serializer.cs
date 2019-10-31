﻿using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BitcoinNet.JsonRpc.JsonConverters
{
	public class Serializer
	{
		public static void RegisterFrontConverters(JsonSerializerSettings settings, Network network = null)
		{
			settings.Converters.Add(new MoneyJsonConverter());
			settings.Converters.Add(new KeyJsonConverter());
			settings.Converters.Add(new CoinJsonConverter(network));
			settings.Converters.Add(new ScriptJsonConverter());
			settings.Converters.Add(new UInt160JsonConverter());
			settings.Converters.Add(new UInt256JsonConverter());
			settings.Converters.Add(new BitcoinSerializableJsonConverter());
			settings.Converters.Add(new NetworkJsonConverter());
			settings.Converters.Add(new KeyPathJsonConverter());
			settings.Converters.Add(new SignatureJsonConverter());
			settings.Converters.Add(new HexJsonConverter());
			settings.Converters.Add(new DateTimeToUnixTimeConverter());
			settings.Converters.Add(new TxDestinationJsonConverter());
			settings.Converters.Add(new LockTimeJsonConverter());
			settings.Converters.Add(new BitcoinStringJsonConverter
			{
				Network = network
			});
			settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
		}

		public static T ToObject<T>(string data)
		{
			return ToObject<T>(data, null);
		}

		public static T ToObject<T>(string data, Network network)
		{
			var settings = new JsonSerializerSettings
			{
				Formatting = Formatting.Indented
			};
			RegisterFrontConverters(settings, network);
			return JsonConvert.DeserializeObject<T>(data, settings);
		}

		public static string ToString<T>(T response, Network network)
		{
			var settings = new JsonSerializerSettings
			{
				Formatting = Formatting.Indented
			};
			RegisterFrontConverters(settings, network);
			return JsonConvert.SerializeObject(response, settings);
		}

		public static string ToString<T>(T response)
		{
			return ToString(response, null);
		}
	}
}