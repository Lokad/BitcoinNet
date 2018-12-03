using BitcoinNet;
using BitcoinNet.DataEncoders;
using Newtonsoft.Json;
using System;
using System.Reflection;

namespace BitcoinNet.JsonRpc.JsonConverters
{
	public class ScriptJsonConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return typeof(Script).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if(reader.TokenType == JsonToken.Null)
				return null;
			try
			{
				if(objectType == typeof(Script))
					return Script.FromBytesUnsafe(Encoders.Hex.DecodeData((string)reader.Value));
			}
			catch(FormatException)
			{
			}
			throw new JsonObjectException("A script should be a byte string", reader);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if(value != null)
			{
				if(value is Script)
					writer.WriteValue(Encoders.Hex.EncodeData(((Script)value).ToBytes(false)));
			}
		}
	}
}
