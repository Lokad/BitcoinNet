using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinNet.JsonRpc
{
    public class EstimateSmartFeeResponse
    {
		public FeeRate FeeRate
		{
			get; set;
		}
		public int Blocks
		{
			get; set;
		}
	}
}
