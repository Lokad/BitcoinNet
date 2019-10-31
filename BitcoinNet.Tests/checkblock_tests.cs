using System.IO;
using BitcoinNet.DataEncoders;
using Xunit;

namespace BitcoinNet.Tests
{
	public class checkblock_tests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanCalculateMerkleRoot()
		{
			var block = Consensus.Main.ConsensusFactory.CreateBlock();
			block.ReadWrite(Encoders.Hex.DecodeData(File.ReadAllText(@"data/block169482.txt")));
			Assert.Equal(block.Header.HashMerkleRoot, block.GetMerkleRoot().Hash);
		}
	}
}