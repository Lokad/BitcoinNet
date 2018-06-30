﻿#if !NOFILEIO
using BitcoinNet.DataEncoders;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BitcoinNet.Tests
{
	public class checkblock_tests
	{

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanCalculateMerkleRoot()
		{
			Block block = Consensus.Main.ConsensusFactory.CreateBlock();
			block.ReadWrite(Encoders.Hex.DecodeData(File.ReadAllText(@"data/block169482.txt")));
			Assert.Equal(block.Header.HashMerkleRoot, block.GetMerkleRoot().Hash);
		}		
	}
}
#endif