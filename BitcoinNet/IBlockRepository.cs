using System.Threading.Tasks;

namespace BitcoinNet
{
	public interface IBlockRepository
	{
		Task<Block> GetBlockAsync(uint256 blockId);
	}
}
