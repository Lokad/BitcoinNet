using System;
using System.Collections.Generic;
using System.Text;

namespace BitcoinNet
{
    public interface IHasForkId
    {
		uint ForkId
		{
			get;
		}
    }
}
