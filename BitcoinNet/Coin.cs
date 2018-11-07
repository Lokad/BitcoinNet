using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinNet
{
	public interface ICoin
	{
		IMoney Amount
		{
			get;
		}
		OutPoint Outpoint
		{
			get;
		}
		TxOut TxOut
		{
			get;
		}

		/// <summary>
		/// Returns the script actually signed and executed
		/// </summary>
		/// <exception cref="System.InvalidOperationException">Additional information needed to get the ScriptCode</exception>
		/// <returns>The executed script</returns>
		Script GetScriptCode();
		void OverrideScriptCode(Script scriptCode);
		bool CanGetScriptCode
		{
			get;
		}
	}

	public class Coin : ICoin
	{
		public Coin()
		{

		}
		public Coin(OutPoint fromOutpoint, TxOut fromTxOut)
		{
			Outpoint = fromOutpoint;
			TxOut = fromTxOut;
		}

		public Coin(Transaction fromTx, uint fromOutputIndex)
		{
			if(fromTx == null)
				throw new ArgumentNullException(nameof(fromTx));
			Outpoint = new OutPoint(fromTx, fromOutputIndex);
			TxOut = fromTx.Outputs[fromOutputIndex];
		}

		public Coin(Transaction fromTx, TxOut fromOutput)
		{
			if(fromTx == null)
				throw new ArgumentNullException(nameof(fromTx));
			if(fromOutput == null)
				throw new ArgumentNullException(nameof(fromOutput));
			uint outputIndex = (uint)fromTx.Outputs.FindIndex(r => Object.ReferenceEquals(fromOutput, r));
			Outpoint = new OutPoint(fromTx, outputIndex);
			TxOut = fromOutput;
		}
		public Coin(IndexedTxOut txOut)
		{
			Outpoint = new OutPoint(txOut.Transaction.GetHash(), txOut.N);
			TxOut = txOut.TxOut;
		}

		public Coin(uint256 fromTxHash, uint fromOutputIndex, Money amount, Script scriptPubKey)
		{
			Outpoint = new OutPoint(fromTxHash, fromOutputIndex);
			TxOut = new TxOut(amount, scriptPubKey);
		}

		public virtual Script GetScriptCode()
		{
			if(!CanGetScriptCode)
				throw new InvalidOperationException("You need to provide P2WSH or P2SH redeem script with Coin.ToScriptCoin()");
			if(_OverrideScriptCode != null)
				return _OverrideScriptCode;
			var key = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(ScriptPubKey);
			if(key != null)
				return key.ScriptPubKey;
			return ScriptPubKey;
		}

		public virtual bool CanGetScriptCode
		{
			get
			{
				return _OverrideScriptCode != null || !ScriptPubKey.IsPayToScriptHash && !PayToScriptHashTemplate.Instance.CheckScriptPubKey(ScriptPubKey);
			}
		}

		public ScriptCoin ToScriptCoin(Script redeemScript)
		{
			if(redeemScript == null)
				throw new ArgumentNullException(nameof(redeemScript));
			var scriptCoin = this as ScriptCoin;
			if(scriptCoin != null)
				return scriptCoin;
			return new ScriptCoin(this, redeemScript);
		}

		public OutPoint Outpoint
		{
			get;
			set;
		}
		public TxOut TxOut
		{
			get;
			set;
		}

		// ICoin Members

		public Money Amount
		{
			get
			{
				if(TxOut == null)
					return Money.Zero;
				return TxOut.Value;
			}
			set
			{
				EnsureTxOut();
				TxOut.Value = value;
			}
		}

		private void EnsureTxOut()
		{
			if(TxOut == null)
				TxOut = new TxOut();
		}

		protected Script _OverrideScriptCode;
		public void OverrideScriptCode(Script scriptCode)
		{
			_OverrideScriptCode = scriptCode;
		}

		public Script ScriptPubKey
		{
			get
			{
				if(TxOut == null)
					return Script.Empty;
				return TxOut.ScriptPubKey;
			}
			set
			{
				EnsureTxOut();
				TxOut.ScriptPubKey = value;
			}
		}

		// ICoin Members

		IMoney ICoin.Amount
		{
			get
			{
				return Amount;
			}
		}

		OutPoint ICoin.Outpoint
		{
			get
			{
				return Outpoint;
			}
		}

		TxOut ICoin.TxOut
		{
			get
			{
				return TxOut;
			}
		}
	}


	public enum RedeemType
	{
		P2SH,
		WitnessV0
	}


	/// <summary>
	/// Represent a coin which need a redeem script to be spent (P2SH or P2WSH)
	/// </summary>
	public class ScriptCoin : Coin
	{
		public ScriptCoin()
		{

		}

		public ScriptCoin(OutPoint fromOutpoint, TxOut fromTxOut, Script redeem)
			: base(fromOutpoint, fromTxOut)
		{
			Redeem = redeem;
			AssertCoherent();
		}

		public ScriptCoin(Transaction fromTx, uint fromOutputIndex, Script redeem)
			: base(fromTx, fromOutputIndex)
		{
			Redeem = redeem;
			AssertCoherent();
		}

		public ScriptCoin(Transaction fromTx, TxOut fromOutput, Script redeem)
			: base(fromTx, fromOutput)
		{
			Redeem = redeem;
			AssertCoherent();
		}
		public ScriptCoin(ICoin coin, Script redeem)
			: base(coin.Outpoint, coin.TxOut)
		{
			Redeem = redeem;
			AssertCoherent();
		}

		public bool IsP2SH
		{
			get
			{
				return ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_HASH160;
			}
		}


		public Script GetP2SHRedeem()
		{
			if(!IsP2SH)
				return null;
			var p2shRedeem = RedeemType == RedeemType.P2SH ? Redeem : null;
			if(p2shRedeem == null)
				throw new NotSupportedException("RedeemType not supported for getting the P2SH script, contact the library author");
			return p2shRedeem;
		}

		public RedeemType RedeemType
		{
			get
			{
				return
					Redeem.Hash.ScriptPubKey == TxOut.ScriptPubKey ?
					RedeemType.P2SH :
					RedeemType.WitnessV0;
			}
		}

		private void AssertCoherent()
		{
			if(Redeem == null)
				throw new ArgumentException("redeem cannot be null", "redeem");

			var expectedDestination = GetRedeemHash(TxOut.ScriptPubKey);
			if(expectedDestination == null)
			{
				throw new ArgumentException("the provided scriptPubKey is not P2SH or P2WSH");
			}
			if(expectedDestination is ScriptId)
			{
				if(PayToScriptHashTemplate.Instance.CheckScriptPubKey(Redeem))
				{
					throw new ArgumentException("The redeem script provided must be the witness one, not the P2SH one");
				}

				if(expectedDestination.ScriptPubKey != Redeem.Hash.ScriptPubKey)
				{
					if(Redeem.Hash.ScriptPubKey.Hash.ScriptPubKey != expectedDestination.ScriptPubKey)
						throw new ArgumentException("The redeem provided does not match the scriptPubKey of the coin");
				}
			}
			else
				throw new NotSupportedException("Not supported redeemed scriptPubkey");
		}

		public ScriptCoin(IndexedTxOut txOut, Script redeem)
			: base(txOut)
		{
			Redeem = redeem;
			AssertCoherent();
		}

		public ScriptCoin(uint256 txHash, uint outputIndex, Money amount, Script scriptPubKey, Script redeem)
			: base(txHash, outputIndex, amount, scriptPubKey)
		{
			Redeem = redeem;
			AssertCoherent();
		}

		public Script Redeem
		{
			get;
			set;
		}

		public override Script GetScriptCode()
		{
			if(!CanGetScriptCode)
				throw new InvalidOperationException("You need to provide the P2WSH redeem script with ScriptCoin.ToScriptCoin()");
			if(_OverrideScriptCode != null)
				return _OverrideScriptCode;
			var key = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(Redeem);
			if (key != null)
				return key.ScriptPubKey;
			return Redeem;
		}

		public override bool CanGetScriptCode
		{
			get
			{
				return _OverrideScriptCode != null || !IsP2SH || !PayToScriptHashTemplate.Instance.CheckScriptPubKey(Redeem);
			}
		}

		/// <summary>
		/// Returns the hash contained in the scriptPubKey (P2SH or P2WSH)
		/// </summary>
		/// <param name="scriptPubKey">The scriptPubKey</param>
		/// <returns>The hash of the scriptPubkey</returns>
		public static TxDestination GetRedeemHash(Script scriptPubKey)
		{
			if(scriptPubKey == null)
				throw new ArgumentNullException(nameof(scriptPubKey));
			return PayToScriptHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey) as TxDestination;
		}
	}
}
