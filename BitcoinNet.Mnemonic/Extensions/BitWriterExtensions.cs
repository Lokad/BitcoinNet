using BitcoinNet.Mnemonic;

namespace BitcoinNet
{
	public static class BitWriterExtensions
	{
		public static int[] ToIntegers(this BitWriter writer)
		{
			var array = writer.ToBitArray();
			return WordList.ToIntegers(array);
		}
	}
}