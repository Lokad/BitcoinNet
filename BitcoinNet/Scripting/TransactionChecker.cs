using System;

namespace BitcoinNet.Scripting
{
	public class TransactionChecker
	{
		public TransactionChecker(Transaction tx, int index, Money amount,
			PrecomputedTransactionData precomputedTransactionData)
		{
			if (tx == null)
			{
				throw new ArgumentNullException(nameof(tx));
			}

			Transaction = tx;
			Index = index;
			Amount = amount;
			PrecomputedTransactionData = precomputedTransactionData;
		}

		public TransactionChecker(Transaction tx, int index, Money amount = null)
		{
			if (tx == null)
			{
				throw new ArgumentNullException(nameof(tx));
			}

			Transaction = tx;
			Index = index;
			Amount = amount;
		}

		public PrecomputedTransactionData PrecomputedTransactionData { get; }

		public Transaction Transaction { get; }

		public TxIn Input => Transaction.Inputs[Index];

		public int Index { get; }

		public Money Amount { get; }
	}
}