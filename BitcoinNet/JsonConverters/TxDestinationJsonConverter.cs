﻿using BitcoinNet;
using BitcoinNet.Crypto;
using BitcoinNet.DataEncoders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BitcoinNet.JsonConverters
{
	public class TxDestinationJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(KeyId) || 
				objectType == typeof(ScriptId);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if(reader.TokenType == JsonToken.Null)
				return null;
			try
			{
				if(objectType == typeof(KeyId))
					return new KeyId(Encoders.Hex.DecodeData((string)reader.Value));
				if(objectType == typeof(ScriptId))
					return new ScriptId(Encoders.Hex.DecodeData((string)reader.Value));
			}
			catch
			{
			}
			throw new JsonObjectException("Invalid signature", reader);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if(value != null)
			{
				if(value is KeyId)
					writer.WriteValue(Encoders.Hex.EncodeData(((KeyId)value).ToBytes()));
				if(value is ScriptId)
					writer.WriteValue(Encoders.Hex.EncodeData(((ScriptId)value).ToBytes()));
			}
		}
	}
}