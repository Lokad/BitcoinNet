using BitcoinNet.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BitcoinNet.Tests
{
	public class cmpctblock_tests
	{
		[Fact]
		[Trait("CoreBeta", "CoreBeta")]
		public void CanRoundtripCmpctBlock()
		{
			Block block = new Block();
			block.Transactions.Add(new Transaction());
			var cmpct = new CmpctBlockPayload(block);
			cmpct.Clone();
		}
	}
}
