﻿using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitcoinNet.JsonRpc
{
	//{"code":-32601,"message":"Method not found"}
	public class RPCError
	{
		internal RPCError(JObject error)
		{
			Code = (RPCErrorCode) (int) error.GetValue("code");
			Message = (string) error.GetValue("message");
		}

		public RPCErrorCode Code { get; set; }

		public string Message { get; set; }
	}

	//{"result":null,"error":{"code":-32601,"message":"Method not found"},"id":1}
	public class RPCResponse
	{
		internal RPCResponse(JObject json)
		{
			var error = json.GetValue("error") as JObject;
			if (error != null)
			{
				Error = new RPCError(error);
			}

			Result = json.GetValue("result");
		}

		public RPCError Error { get; set; }

		public JToken Result { get; set; }

		public string ResultString
		{
			get
			{
				if (Result == null)
				{
					return null;
				}

				return Result.ToString();
			}
		}

		public static RPCResponse Load(Stream stream)
		{
			var reader = new JsonTextReader(new StreamReader(stream, Encoding.UTF8));
			return new RPCResponse(JObject.Load(reader));
		}

		public void ThrowIfError()
		{
			if (Error != null)
			{
				throw new RPCException(Error.Code, Error.Message, this);
			}
		}
	}
}