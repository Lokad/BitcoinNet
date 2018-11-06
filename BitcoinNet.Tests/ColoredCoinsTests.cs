using BitcoinNet.DataEncoders;
using BitcoinNet.OpenAsset;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BitcoinNet.Tests
{
	//https://github.com/OpenAssets/open-assets-protocol/blob/master/specification.mediawiki
	public class ColoredCoinsTests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanCreateAssetAddress()
		{
			//The issuer first generates a private key: 18E14A7B6A307F426A94F8114701E7C8E774E7F9A47E2C2035DB29A206321725.
			var key = new Key(TestUtils.ParseHex("18E14A7B6A307F426A94F8114701E7C8E774E7F9A47E2C2035DB29A206321725"));
			//He calculates the corresponding address: 16UwLL9Risc3QfPqBUvKofHmBQ7wMtjvM.
			var address = key.PubKey.Decompress().GetAddress(Network.Main);
			Assert.Equal("16UwLL9Risc3QfPqBUvKofHmBQ7wMtjvM", address.ToLegacy().ToString());

			//Next, he builds the Pay-to-PubKey-Hash script associated to that address: OP_DUP OP_HASH160 010966776006953D5567439E5E39F86A0D273BEE OP_EQUALVERIFY OP_CHECKSIG
			Script script = address.ScriptPubKey;
			Assert.Equal("OP_DUP OP_HASH160 010966776006953D5567439E5E39F86A0D273BEE OP_EQUALVERIFY OP_CHECKSIG", script.ToString().ToUpper());

			var oo = script.GetScriptAddress(Network.Main);
			//The script is hashed: 36e0ea8e93eaa0285d641305f4c81e563aa570a2.
			Assert.Equal("36e0ea8e93eaa0285d641305f4c81e563aa570a2", script.Hash.ToString());

			Assert.Equal("36e0ea8e93eaa0285d641305f4c81e563aa570a2", key.PubKey.Decompress().Hash.ScriptPubKey.Hash.ToString());
			//Finally, the hash is converted to a base 58 string with checksum using version byte 23: ALn3aK1fSuG27N96UGYB1kUYUpGKRhBuBC. 
			Assert.Equal("ALn3aK1fSuG27N96UGYB1kUYUpGKRhBuBC", script.Hash.ToAssetId().GetWif(Network.Main).ToString());
		}

	}
}
