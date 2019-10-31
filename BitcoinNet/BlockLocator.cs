using System.Collections.Generic;

namespace BitcoinNet
{
	/// <summary>
	///     Compact representation of one's chain position which can be used to find forks with another chain
	/// </summary>
	public class BlockLocator : IBitcoinSerializable
	{
		private List<uint256> _vHave = new List<uint256>();

		public List<uint256> Blocks
		{
			get => _vHave;
			set => _vHave = value;
		}

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _vHave);
		}
	}
}