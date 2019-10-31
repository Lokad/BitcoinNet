using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace BitcoinNet.Tests
{
	[JsonArray]
	public class TestCase : List<object>
	{
		public int Index { get; set; }

		public T GetValue<T>(int i)
		{
			return (T) Convert.ChangeType(this[i], typeof(T));
		}

		public dynamic GetDynamic(int i)
		{
			return this[i];
		}

		public static TestCase[] read_json(string fileName)
		{
			using (var fs = File.Open(fileName, FileMode.Open))
			{
				var seria = new JsonSerializer();
				var result = (TestCase[]) seria.Deserialize(new StreamReader(fs), typeof(TestCase[]));
				for (var i = 0; i < result.Length; i++)
				{
					result[i].Index = i;
				}

				return result;
			}
		}

		public override string ToString()
		{
			return "[" + string.Join(",", this.Select(s => Convert.ToString(s)).ToArray()) + "]";
		}
	}
}