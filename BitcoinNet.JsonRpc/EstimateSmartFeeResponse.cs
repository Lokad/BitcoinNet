namespace BitcoinNet.JsonRpc
{
	public class EstimateSmartFeeResponse
	{
		public FeeRate FeeRate { get; set; }

		public int Blocks { get; set; }
	}
}