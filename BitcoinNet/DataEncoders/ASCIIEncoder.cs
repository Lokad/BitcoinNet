using System.Linq;

namespace BitcoinNet.DataEncoders
{
	public class ASCIIEncoder : DataEncoder
	{
		//Do not using Encoding.ASCII (not portable)
		public override byte[] DecodeData(string encoded)
		{
			if (string.IsNullOrEmpty(encoded))
			{
				return new byte[0];
			}

			return encoded.ToCharArray().Select(o => (byte) o).ToArray();
		}

		public override string EncodeData(byte[] data, int offset, int count)
		{
			return new string(data.Skip(offset).Take(count).Select(o => (char) o).ToArray()).Replace("\0", "");
		}
	}
}