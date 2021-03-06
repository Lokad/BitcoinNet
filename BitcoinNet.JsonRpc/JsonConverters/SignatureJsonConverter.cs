﻿using System;
using BitcoinNet.Crypto;
using BitcoinNet.DataEncoders;
using Newtonsoft.Json;

namespace BitcoinNet.JsonRpc.JsonConverters
{
	public class SignatureJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(ECDSASignature) || objectType == typeof(TransactionSignature);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
			JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
			{
				return null;
			}

			try
			{
				if (objectType == typeof(ECDSASignature))
				{
					return new ECDSASignature(Encoders.Hex.DecodeData((string) reader.Value));
				}

				if (objectType == typeof(TransactionSignature))
				{
					return new TransactionSignature(Encoders.Hex.DecodeData((string) reader.Value));
				}
			}
			catch
			{
			}

			throw new JsonObjectException("Invalid signature", reader);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if (value != null)
			{
				if (value is ECDSASignature)
				{
					writer.WriteValue(Encoders.Hex.EncodeData(((ECDSASignature) value).ToDER()));
				}

				if (value is TransactionSignature)
				{
					writer.WriteValue(Encoders.Hex.EncodeData(((TransactionSignature) value).ToBytes()));
				}
			}
		}
	}
}