using System;
using System.Linq;
using BitcoinNet.Scripting;

namespace BitcoinNet.Policy
{
	public class TransactionPolicyError
	{
		private readonly string _message;

		public TransactionPolicyError()
			: this(null)
		{
		}

		public TransactionPolicyError(string message)
		{
			_message = message;
		}

		public override string ToString()
		{
			return _message;
		}
	}

	public class TransactionSizePolicyError : TransactionPolicyError
	{
		public TransactionSizePolicyError(int actualSize, int maximumSize)
			: base("Transaction's size is too high. Actual value is " + actualSize + ", but the maximum is " +
			       maximumSize)
		{
			ActualSize = actualSize;
			MaximumSize = maximumSize;
		}

		public int ActualSize { get; }

		public int MaximumSize { get; }
	}

	public class FeeTooHighPolicyError : TransactionPolicyError
	{
		public FeeTooHighPolicyError(Money fees, Money max)
			: base("Fee too high, actual is " + fees.ToString() + ", policy maximum is " + max.ToString())
		{
			ExpectedMaxFee = max;
			Fee = fees;
		}

		public Money Fee { get; }

		public Money ExpectedMaxFee { get; }
	}

	public class DustPolicyError : TransactionPolicyError
	{
		public DustPolicyError(Money value, Money dust)
			: base("Dust output detected, output value is " + value.ToString() + ", policy minimum is " +
			       dust.ToString())
		{
			Value = value;
			DustThreshold = dust;
		}

		public Money Value { get; }

		public Money DustThreshold { get; }
	}

	public class FeeTooLowPolicyError : TransactionPolicyError
	{
		public FeeTooLowPolicyError(Money fees, Money min)
			: base("Fee too low, actual is " + fees.ToString() + ", policy minimum is " + min.ToString())
		{
			ExpectedMinFee = min;
			Fee = fees;
		}

		public Money Fee { get; }

		public Money ExpectedMinFee { get; }
	}

	public class InputPolicyError : TransactionPolicyError
	{
		public InputPolicyError(string message, IndexedTxIn txIn)
			: base(message)
		{
			OutPoint = txIn.PrevOut;
			InputIndex = txIn.Index;
		}

		public OutPoint OutPoint { get; }

		public uint InputIndex { get; }
	}

	public class DuplicateInputPolicyError : TransactionPolicyError
	{
		public DuplicateInputPolicyError(IndexedTxIn[] duplicated)
			: base("Duplicate input " + duplicated[0].PrevOut)
		{
			OutPoint = duplicated[0].PrevOut;
			InputIndices = duplicated.Select(d => d.Index).ToArray();
		}

		public OutPoint OutPoint { get; }

		public uint[] InputIndices { get; }
	}

	public class OutputPolicyError : TransactionPolicyError
	{
		public OutputPolicyError(string message, int outputIndex) :
			base(message)
		{
			OutputIndex = outputIndex;
		}

		public int OutputIndex { get; }
	}

	public class CoinNotFoundPolicyError : InputPolicyError
	{
		private readonly IndexedTxIn _txIn;

		public CoinNotFoundPolicyError(IndexedTxIn txIn)
			: base("No coin matching " + txIn.PrevOut + " was found", txIn)
		{
			_txIn = txIn;
		}

		internal Exception AsException()
		{
			return new CoinNotFoundException(_txIn);
		}
	}

	public class ScriptPolicyError : InputPolicyError
	{
		public ScriptPolicyError(IndexedTxIn input, ScriptError error, ScriptVerify scriptVerify, Script scriptPubKey)
			: base("Script error on input " + input.Index + " (" + error + ")", input)
		{
			ScriptError = error;
			ScriptVerify = scriptVerify;
			ScriptPubKey = scriptPubKey;
		}

		public ScriptError ScriptError { get; }

		public ScriptVerify ScriptVerify { get; }

		public Script ScriptPubKey { get; }
	}

	public interface ITransactionPolicy
	{
		/// <summary>
		///     Check if the given transaction violate the policy
		/// </summary>
		/// <param name="transaction">The transaction</param>
		/// <param name="spentCoins">The previous coins</param>
		/// <returns>Policy errors</returns>
		TransactionPolicyError[] Check(Transaction transaction, ICoin[] spentCoins);
	}
}