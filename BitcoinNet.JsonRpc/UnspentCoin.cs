using BitcoinNet.DataEncoders;
using BitcoinNet.Scripting;
using Newtonsoft.Json.Linq;

namespace BitcoinNet.JsonRpc
{
	public class UnspentCoin
	{
		public UnspentCoin(JObject unspent, Network network)
		{
			OutPoint = new OutPoint(uint256.Parse((string) unspent["txid"]), (uint) unspent["vout"]);
			var address = (string) unspent["address"];
			if (address != null)
			{
				Address = network.Parse<BitcoinAddress>(address);
			}

			Account = (string) unspent["account"];
			ScriptPubKey = new Script(Encoders.Hex.DecodeData((string) unspent["scriptPubKey"]));
			var redeemScriptHex = (string) unspent["redeemScript"];
			if (redeemScriptHex != null)
			{
				RedeemScript = new Script(Encoders.Hex.DecodeData(redeemScriptHex));
			}

			var amount = (decimal) unspent["amount"];
			Amount = new Money((long) (amount * Money.Coin));
			Confirmations = (uint) unspent["confirmations"];

			// Added in Bitcoin Core 0.10.0
			if (unspent["spendable"] != null)
			{
				IsSpendable = (bool) unspent["spendable"];
			}
			else
			{
				// Default to True for earlier versions, i.e. if not present
				IsSpendable = true;
			}
		}

		public OutPoint OutPoint { get; }

		public BitcoinAddress Address { get; }

		public string Account { get; }

		public Script ScriptPubKey { get; }

		public Script RedeemScript { get; }

		public uint Confirmations { get; }

		public Money Amount { get; }

		public bool IsSpendable { get; }

		public Coin AsCoin()
		{
			var coin = new Coin(OutPoint, new TxOut(Amount, ScriptPubKey));
			if (RedeemScript != null)
			{
				coin = coin.ToScriptCoin(RedeemScript);
			}

			return coin;
		}
	}
}