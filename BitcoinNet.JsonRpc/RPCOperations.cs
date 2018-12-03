using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinNet.JsonRpc
{
	//from rpcserver.h
	public enum RPCOperations
	{
		getconnectioncount,
		getpeerinfo,
		ping,
		addnode,
		getaddednodeinfo,
		getnettotals,

		getgenerate,
		setgenerate,
		generate,
		getnetworkhashps,
		gethashespersec,
		getmininginfo,
		prioritisetransaction,
		getwork,
		getblocktemplate,
		submitblock,
		estimatefee,
		estimatesmartfee,

		verifymessage,
		createmultisig,
		validateaddress,
		[Obsolete("Deprecated in Bitcoin Core 0.16.0 use getblockchaininfo, getnetworkinfo, getwalletinfo or getmininginfo instead")]
		getinfo,
		getblockchaininfo,
		getnetworkinfo,

		getrawtransaction,
		createrawtransaction,
		decoderawtransaction,
		decodescript,
		signrawtransaction,
		sendrawtransaction,
		gettxoutproof,
		verifytxoutproof,

		getblockcount,
		getbestblockhash,
		getdifficulty,
		getmempoolinfo,
		getrawmempool,
		getblockhash,
		getblock,
		gettxoutsetinfo,
		gettxout,
		verifychain,
		getchaintips,
		invalidateblock
	}
}
