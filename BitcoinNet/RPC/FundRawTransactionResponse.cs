using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinNet.RPC
{
	public class FundRawTransactionResponse
	{
		public Transaction Transaction
		{
			get; set;
		}
		public Money Fee
		{
			get; set;
		}
		public int ChangePos
		{
			get; set;
		}
	}
}
