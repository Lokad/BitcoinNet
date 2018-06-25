using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.BitcoinNet;

namespace BitcoinNet.Payment
{
	static class UriHelper
	{
		public static Dictionary<string, string> DecodeQueryParameters(string uri)
		{
			if(uri == null)
				throw new ArgumentNullException(nameof(uri));

			if(uri.Length == 0)
				return new Dictionary<string, string>();

			return uri
					.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(kvp => kvp.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries))
					.ToDictionary(kvp => kvp[0],
									kvp => kvp.Length > 2 ?
										System.Web.BitcoinNet.HttpUtility.UrlDecode(string.Join("=", kvp, 1, kvp.Length - 1)) :
									(kvp.Length > 1 ? System.Web.BitcoinNet.HttpUtility.UrlDecode(kvp[1]) : ""));
		}
	}
}
