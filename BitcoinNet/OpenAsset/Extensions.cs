using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinNet.OpenAsset
{
	public static class Extensions
	{
		public static AssetId ToAssetId(this ScriptId id)
		{
			return new AssetId(id);
		}
	}
}
