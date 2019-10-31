using System;
using System.Linq;
using System.Net;
using System.Text;

namespace BitcoinNet.JsonRpc
{
	public class RPCCredentialString
	{
		private string _cookieFile;
		private NetworkCredential _userNamePassword;

		public string Server { get; set; }


		/// <summary>
		///     Use default connection settings of the chain
		/// </summary>
		public bool UseDefault => CookieFile == null && UserPassword == null;

		/// <summary>
		///     Path to cookie file
		/// </summary>
		public string CookieFile
		{
			get => _cookieFile;
			set
			{
				if (value != null)
				{
					Reset();
				}

				_cookieFile = value;
			}
		}

		/// <summary>
		///     Username and password
		/// </summary>
		public NetworkCredential UserPassword
		{
			get => _userNamePassword;
			set
			{
				if (value != null)
				{
					Reset();
				}

				_userNamePassword = value;
			}
		}

		public static RPCCredentialString Parse(string str)
		{
			RPCCredentialString r;
			if (!TryParse(str, out r))
			{
				throw new FormatException("Invalid RPC Credential string");
			}

			return r;
		}

		public static bool TryParse(string str, out RPCCredentialString connectionString)
		{
			connectionString = null;
			if (str == null)
			{
				throw new ArgumentNullException(nameof(str));
			}

			var parts = str.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);
			string server = null;
			foreach (var part in parts)
			{
				if (part == parts[parts.Length - 1])
				{
					TryParseAuth(part, out connectionString);
					break;
				}

				if (part.StartsWith("server=", StringComparison.OrdinalIgnoreCase))
				{
					server = part.Substring("server=".Length);
				}
				else
				{
					return false;
				}
			}

			if (connectionString == null)
			{
				return false;
			}

			connectionString.Server = server;
			return true;
		}

		private static bool TryParseAuth(string str, out RPCCredentialString connectionString)
		{
			str = str.Trim();
			if (str.Equals("default", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(str))
			{
				connectionString = new RPCCredentialString();
				return true;
			}

			if (str.StartsWith("cookiefile=", StringComparison.OrdinalIgnoreCase))
			{
				var path = str.Substring("cookiefile=".Length);
				connectionString = new RPCCredentialString {CookieFile = path};
				return true;
			}

			if (str.IndexOf(':') != -1)
			{
				var parts = str.Split(new[] {':'}, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length >= 2)
				{
					parts[1] = string.Join(":", parts.Skip(1).ToArray());
					connectionString = new RPCCredentialString
					{
						UserPassword = new NetworkCredential(parts[0], parts[1])
					};
					return true;
				}
			}

			connectionString = null;
			return false;
		}

		private void Reset()
		{
			_cookieFile = null;
			_userNamePassword = null;
		}

		public override string ToString()
		{
			var builder = new StringBuilder();
			if (!string.IsNullOrEmpty(Server))
			{
				builder.Append($"server={Server};");
			}

			var authPath = UseDefault ? "default" :
				CookieFile != null ? "cookiefile=" + CookieFile :
				UserPassword != null ? $"{UserPassword.UserName}:{UserPassword.Password}" :
				"default";
			builder.Append(authPath);
			return builder.ToString();
		}
	}
}