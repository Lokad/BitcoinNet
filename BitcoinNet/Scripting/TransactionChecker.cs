using System;

namespace BitcoinNet.Scripting
{
	public class TransactionChecker
	{
		public TransactionChecker(Transaction tx, int index, Money amount, PrecomputedTransactionData precomputedTransactionData)
		{
			if(tx == null)
				throw new ArgumentNullException(nameof(tx));
			_Transaction = tx;
			_Index = index;
			_Amount = amount;
			_PrecomputedTransactionData = precomputedTransactionData;
		}
		public TransactionChecker(Transaction tx, int index, Money amount = null)
		{
			if(tx == null)
				throw new ArgumentNullException(nameof(tx));
			_Transaction = tx;
			_Index = index;
			_Amount = amount;
		}


		private readonly PrecomputedTransactionData _PrecomputedTransactionData;
		public PrecomputedTransactionData PrecomputedTransactionData
		{
			get
			{
				return _PrecomputedTransactionData;
			}
		}

		private readonly Transaction _Transaction;
		public Transaction Transaction
		{
			get
			{
				return _Transaction;
			}
		}

		public TxIn Input
		{
			get
			{
				return Transaction.Inputs[_Index];
			}
		}

		private readonly int _Index;
		public int Index
		{
			get
			{
				return _Index;
			}
		}

		private readonly Money _Amount;
		public Money Amount
		{
			get
			{
				return _Amount;
			}
		}
	}
}