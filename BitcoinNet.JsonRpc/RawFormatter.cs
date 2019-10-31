using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitcoinNet.JsonRpc
{
	public abstract class RawFormatter
	{
		protected RawFormatter()
		{
			Network = Network.Main;
		}

		public Network Network { get; set; }

		public Transaction ParseJson(string str)
		{
			var obj = JObject.Parse(str);
			return Parse(obj);
		}

		public Transaction Parse(JObject obj)
		{
			var tx = new Transaction();
			BuildTransaction(obj, tx);
			return tx;
		}

		protected void WritePropertyValue<TValue>(JsonWriter writer, string name, TValue value)
		{
			writer.WritePropertyName(name);
			writer.WriteValue(value);
		}


		protected abstract void BuildTransaction(JObject json, Transaction tx);

		public string ToString(Transaction transaction)
		{
			var strWriter = new StringWriter();
			var jsonWriter = new JsonTextWriter(strWriter) {Formatting = Formatting.Indented};
			jsonWriter.WriteStartObject();
			WriteTransaction(jsonWriter, transaction);
			jsonWriter.WriteEndObject();
			jsonWriter.Flush();
			return strWriter.ToString();
		}

		protected abstract void WriteTransaction(JsonTextWriter writer, Transaction tx);
	}
}