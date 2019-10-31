using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BitcoinNet.Policy
{
	/// <summary>
	///     Error when not enough funds are present for verifying or building a transaction
	/// </summary>
	public class NotEnoughFundsPolicyError : TransactionPolicyError
	{
		public NotEnoughFundsPolicyError()
		{
		}

		public NotEnoughFundsPolicyError(string message, IMoney missing)
			: base(BuildMessage(message, missing))
		{
			Missing = missing;
		}

		public NotEnoughFundsPolicyError(string message)
			: base(message)
		{
		}

		/// <summary>
		///     Amount of Money missing
		/// </summary>
		public IMoney Missing { get; }

		private static string BuildMessage(string message, IMoney missing)
		{
			var builder = new StringBuilder();
			builder.Append(message);
			if (missing != null)
			{
				builder.Append(" with missing amount " + missing);
			}

			return builder.ToString();
		}

		internal Exception AsException()
		{
			return new NotEnoughFundsException(ToString(), null, Missing);
		}
	}

	public class MinerTransactionPolicy : ITransactionPolicy
	{
		private MinerTransactionPolicy()
		{
		}

		public static MinerTransactionPolicy Instance { get; } = new MinerTransactionPolicy();

		// ITransactionPolicy Members

		public TransactionPolicyError[] Check(Transaction transaction, ICoin[] spentCoins)
		{
			spentCoins = spentCoins ?? new ICoin[0];
			var errors = new List<TransactionPolicyError>();

			if (transaction.Version > Transaction.CurrentVersion || transaction.Version < 1)
			{
				errors.Add(new TransactionPolicyError("Invalid transaction version, expected " +
				                                      Transaction.CurrentVersion));
			}

			var dups = transaction.Inputs.AsIndexedInputs().GroupBy(i => i.PrevOut);
			foreach (var dup in dups)
			{
				var duplicates = dup.ToArray();
				if (duplicates.Length != 1)
				{
					errors.Add(new DuplicateInputPolicyError(duplicates));
				}
			}

			foreach (var input in transaction.Inputs.AsIndexedInputs())
			{
				var coin = spentCoins.FirstOrDefault(s => s.Outpoint == input.PrevOut);
				if (coin == null)
				{
					errors.Add(new CoinNotFoundPolicyError(input));
				}
			}

			foreach (var output in transaction.Outputs.AsCoins())
			{
				if (output.Amount < Money.Zero)
				{
					errors.Add(new OutputPolicyError("Output value should not be less than zero",
						(int) output.Outpoint.N));
				}
			}

			var fees = transaction.GetFee(spentCoins);
			if (fees != null)
			{
				if (fees < Money.Zero)
				{
					errors.Add(new NotEnoughFundsPolicyError("Not enough funds in this transaction", -fees));
				}
			}

			var check = transaction.Check();
			if (check != TransactionCheckResult.Success)
			{
				errors.Add(new TransactionPolicyError("Context free check of the transaction failed " + check));
			}

			return errors.ToArray();
		}
	}
}