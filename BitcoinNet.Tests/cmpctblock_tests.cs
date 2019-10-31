using BitcoinNet.Protocol;
using Xunit;

namespace BitcoinNet.Tests
{
	public class cmpctblock_tests
	{
		[Fact]
		[Trait("CoreBeta", "CoreBeta")]
		public void CanRoundtripCmpctBlock()
		{
			var block = Consensus.Main.ConsensusFactory.CreateBlock();
			block.Transactions.Add(new Transaction());
			var cmpct = new CmpctBlockPayload(block);
			cmpct.Clone();
		}
	}
}