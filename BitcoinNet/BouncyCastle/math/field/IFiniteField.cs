namespace BitcoinNet.BouncyCastle.Math.Field
{
	internal interface IFiniteField
	{
		BigInteger Characteristic
		{
			get;
		}

		int Dimension
		{
			get;
		}
	}
}
