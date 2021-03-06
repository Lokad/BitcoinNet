using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using BCashAddr;
using BitcoinNet.DataEncoders;
using BitcoinNet.Protocol;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	public class DNSSeedData
	{
		private IPAddress[] _addresses;

		public DNSSeedData(string name, string host)
		{
			Name = name;
			Host = host;
		}

		public string Name { get; }

		public string Host { get; }

		public IPAddress[] GetAddressNodes()
		{
			if (_addresses != null)
			{
				return _addresses;
			}

			_addresses = Dns.GetHostAddressesAsync(Host).GetAwaiter().GetResult();
			return _addresses;
		}

		public override string ToString()
		{
			return Name + " (" + Host + ")";
		}
	}

	public enum NetworkType
	{
		Mainnet,
		Testnet,
		Regtest
	}

	public enum Base58Type
	{
		PUBKEY_ADDRESS,
		SCRIPT_ADDRESS,
		SECRET_KEY,
		EXT_PUBLIC_KEY,
		EXT_SECRET_KEY,
		MAX_BASE58_TYPES
	}

	public partial class Network
	{
		internal byte[][] Base58Prefixes = new byte[(int) Base58Type.MAX_BASE58_TYPES][];

		public uint MaxP2PVersion { get; internal set; }

		public byte[] GetVersionBytes(Base58Type type, bool throws)
		{
			var prefix = Base58Prefixes[(int) type];
			if (prefix == null && throws)
			{
				throw new NotImplementedException("The network " + this + " does not have any prefix for base58 " +
				                                  Enum.GetName(typeof(Base58Type), type));
			}

			return prefix?.ToArray();
		}

		internal static string CreateBase58(Base58Type type, byte[] bytes, Network network)
		{
			if (network == null)
			{
				throw new ArgumentNullException(nameof(network));
			}

			if (bytes == null)
			{
				throw new ArgumentNullException(nameof(bytes));
			}

			var versionBytes = network.GetVersionBytes(type, true);
			return Encoders.Base58Check.EncodeData(versionBytes.Concat(bytes));
		}

		public Transaction CreateTransaction()
		{
			return Consensus.ConsensusFactory.CreateTransaction();
		}
	}

	public enum BuriedDeployments
	{
		/// <summary>
		///     Height in coinbase
		/// </summary>
		BIP34,

		/// <summary>
		///     Height in OP_CLTV
		/// </summary>
		BIP65,

		/// <summary>
		///     Strict DER signature
		/// </summary>
		BIP66
	}

	public class Consensus
	{
		private uint256 _bip34Hash;
		private int _coinbaseMaturity = 100;
		private int _coinType;
		private ConsensusFactory _consensusFactory = new ConsensusFactory();
		private bool _frozen;
		private Lazy<uint256> _hashGenesisBlock;
		private bool _litecoinWorkCalculation;
		private int _majorityEnforceBlockUpgrade;
		private int _majorityRejectBlockOutdated;
		private int _majorityWindow;
		private int _minerConfirmationWindow;
		private uint256 _minimumChainWork;
		private bool _powAllowMinDifficultyBlocks;
		private Target _powLimit;
		private bool _powNoRetargeting;
		private TimeSpan _powTargetSpacing;
		private TimeSpan _powTargetTimespan;
		private int _ruleChangeActivationThreshold;
		private int _subsidyHalvingInterval;

		public Consensus()
		{
			BuriedDeployments = new BuriedDeploymentsArray(this);
		}

		public static Consensus Main => Network.Main.Consensus;

		public static Consensus TestNet => Network.TestNet.Consensus;

		public static Consensus RegTest => Network.RegTest.Consensus;

		public BuriedDeploymentsArray BuriedDeployments { get; }

		public int SubsidyHalvingInterval
		{
			get => _subsidyHalvingInterval;
			set
			{
				EnsureNotFrozen();
				_subsidyHalvingInterval = value;
			}
		}

		public ConsensusFactory ConsensusFactory
		{
			get => _consensusFactory;
			set
			{
				EnsureNotFrozen();
				_consensusFactory = value;
			}
		}

		public int MajorityEnforceBlockUpgrade
		{
			get => _majorityEnforceBlockUpgrade;
			set
			{
				EnsureNotFrozen();
				_majorityEnforceBlockUpgrade = value;
			}
		}

		public int MajorityRejectBlockOutdated
		{
			get => _majorityRejectBlockOutdated;
			set
			{
				EnsureNotFrozen();
				_majorityRejectBlockOutdated = value;
			}
		}

		public int MajorityWindow
		{
			get => _majorityWindow;
			set
			{
				EnsureNotFrozen();
				_majorityWindow = value;
			}
		}

		public uint256 BIP34Hash
		{
			get => _bip34Hash;
			set
			{
				EnsureNotFrozen();
				_bip34Hash = value;
			}
		}

		public Target PowLimit
		{
			get => _powLimit;
			set
			{
				EnsureNotFrozen();
				_powLimit = value;
			}
		}

		public TimeSpan PowTargetTimespan
		{
			get => _powTargetTimespan;
			set
			{
				EnsureNotFrozen();
				_powTargetTimespan = value;
			}
		}

		public TimeSpan PowTargetSpacing
		{
			get => _powTargetSpacing;
			set
			{
				EnsureNotFrozen();
				_powTargetSpacing = value;
			}
		}

		public bool PowAllowMinDifficultyBlocks
		{
			get => _powAllowMinDifficultyBlocks;
			set
			{
				EnsureNotFrozen();
				_powAllowMinDifficultyBlocks = value;
			}
		}

		public bool PowNoRetargeting
		{
			get => _powNoRetargeting;
			set
			{
				EnsureNotFrozen();
				_powNoRetargeting = value;
			}
		}

		public uint256 HashGenesisBlock => _hashGenesisBlock.Value;

		public uint256 MinimumChainWork
		{
			get => _minimumChainWork;
			set
			{
				EnsureNotFrozen();
				_minimumChainWork = value;
			}
		}

		public long DifficultyAdjustmentInterval =>
			(long) PowTargetTimespan.TotalSeconds / (long) PowTargetSpacing.TotalSeconds;

		public int MinerConfirmationWindow
		{
			get => _minerConfirmationWindow;
			set
			{
				EnsureNotFrozen();
				_minerConfirmationWindow = value;
			}
		}

		public int RuleChangeActivationThreshold
		{
			get => _ruleChangeActivationThreshold;
			set
			{
				EnsureNotFrozen();
				_ruleChangeActivationThreshold = value;
			}
		}

		public int CoinbaseMaturity
		{
			get => _coinbaseMaturity;
			set
			{
				EnsureNotFrozen();
				_coinbaseMaturity = value;
			}
		}

		/// <summary>
		///     Specify the BIP44 coin type for this network
		/// </summary>
		public int CoinType
		{
			get => _coinType;
			set
			{
				EnsureNotFrozen();
				_coinType = value;
			}
		}

		/// <summary>
		///     Specify using litecoin calculation for difficulty
		/// </summary>
		public bool LitecoinWorkCalculation
		{
			get => _litecoinWorkCalculation;
			set
			{
				EnsureNotFrozen();
				_litecoinWorkCalculation = value;
			}
		}

		internal void SetBlock(byte[] genesis)
		{
			EnsureNotFrozen();
			_hashGenesisBlock = new Lazy<uint256>(() =>
			{
				var block = ConsensusFactory.CreateBlock();
				block.ReadWrite(genesis, ConsensusFactory);
				return block.GetHash();
			}, true);
		}

		public void Freeze()
		{
			_frozen = true;
		}

		private void EnsureNotFrozen()
		{
			if (_frozen)
			{
				throw new InvalidOperationException("This instance can't be modified");
			}
		}

		public virtual Consensus Clone()
		{
			var consensus = new Consensus();
			Fill(consensus);
			return consensus;
		}

		public TimeSpan GetExpectedTimeFor(double blockCount)
		{
			return TimeSpan.FromSeconds(blockCount * PowTargetSpacing.TotalSeconds);
		}

		public double GetExpectedBlocksFor(TimeSpan timeSpan)
		{
			return timeSpan.TotalSeconds / PowTargetSpacing.TotalSeconds;
		}

		protected void Fill(Consensus consensus)
		{
			consensus.EnsureNotFrozen();
			consensus._bip34Hash = _bip34Hash;
			consensus._hashGenesisBlock = _hashGenesisBlock;
			consensus._majorityEnforceBlockUpgrade = _majorityEnforceBlockUpgrade;
			consensus._majorityRejectBlockOutdated = _majorityRejectBlockOutdated;
			consensus._majorityWindow = _majorityWindow;
			consensus._minerConfirmationWindow = _minerConfirmationWindow;
			consensus._powAllowMinDifficultyBlocks = _powAllowMinDifficultyBlocks;
			consensus._powLimit = _powLimit;
			consensus._powNoRetargeting = _powNoRetargeting;
			consensus._powTargetSpacing = _powTargetSpacing;
			consensus._powTargetTimespan = _powTargetTimespan;
			consensus._ruleChangeActivationThreshold = _ruleChangeActivationThreshold;
			consensus._subsidyHalvingInterval = _subsidyHalvingInterval;
			consensus._coinbaseMaturity = _coinbaseMaturity;
			consensus._minimumChainWork = _minimumChainWork;
			consensus._coinType = CoinType;
			consensus._consensusFactory = _consensusFactory;
			consensus._litecoinWorkCalculation = _litecoinWorkCalculation;
		}

		public class BuriedDeploymentsArray
		{
			private readonly int[] _heights;
			private readonly Consensus _parent;

			public BuriedDeploymentsArray(Consensus parent)
			{
				_parent = parent;
				_heights = new int[Enum.GetValues(typeof(BuriedDeployments)).Length];
			}

			public int this[BuriedDeployments index]
			{
				get => _heights[(int) index];
				set
				{
					_parent.EnsureNotFrozen();
					_heights[(int) index] = value;
				}
			}
		}
	}

	public partial class Network
	{
		private const uint BitcoinMaxP2PVersion = 70012;
		//static string[] pnSeed = new[] { "1.34.168.128:8333", "1.202.128.218:8333", "2.30.0.210:8333", "5.9.96.203:8333", "5.45.71.130:8333", "5.45.98.141:8333", "5.102.145.68:8333", "5.135.160.77:8333", "5.189.134.246:8333", "5.199.164.132:8333", "5.249.135.102:8333", "8.19.44.110:8333", "8.22.230.8:8333", "14.200.200.145:8333", "18.228.0.188:8333", "18.228.0.200:8333", "23.24.168.97:8333", "23.28.35.227:8333", "23.92.76.170:8333", "23.99.64.119:8333", "23.228.166.128:8333", "23.229.45.32:8333", "24.8.105.128:8333", "24.16.69.137:8333", "24.94.98.96:8333", "24.102.118.7:8333", "24.118.166.228:8333", "24.122.133.49:8333", "24.166.97.162:8333", "24.213.235.242:8333", "24.226.107.64:8333", "24.228.192.171:8333", "27.140.133.18:8333", "31.41.40.25:8333", "31.43.101.59:8333", "31.184.195.181:8333", "31.193.139.66:8333", "37.200.70.102:8333", "37.205.10.151:8333", "42.3.106.227:8333", "42.60.133.106:8333", "45.56.85.231:8333", "45.56.102.228:8333", "45.79.130.235:8333", "46.28.204.61:11101", "46.38.235.229:8333", "46.59.2.74:8333", "46.101.132.37:8333", "46.101.168.50:8333", "46.163.76.230:8333", "46.166.161.103:8333", "46.182.132.100:8333", "46.223.36.94:8333", "46.227.66.132:8333", "46.227.66.138:8333", "46.239.107.74:8333", "46.249.39.100:8333", "46.250.98.108:8333", "50.7.37.114:8333", "50.81.53.151:8333", "50.115.43.253:8333", "50.116.20.87:8333", "50.116.33.92:8333", "50.125.167.245:8333", "50.143.9.51:8333", "50.188.192.133:8333", "54.77.162.76:8333", "54.153.97.109:8333", "54.165.192.125:8333", "58.96.105.85:8333", "59.167.196.135:8333", "60.29.227.163:8333", "61.35.225.19:8333", "62.43.130.178:8333", "62.109.49.26:8333", "62.202.0.97:8333", "62.210.66.227:8333", "62.210.192.169:8333", "64.74.98.205:8333", "64.156.193.100:8333", "64.203.102.86:8333", "64.229.142.48:8333", "65.96.193.165:8333", "66.30.3.7:8333", "66.114.33.49:8333", "66.118.133.194:8333", "66.135.10.126:8333", "66.172.10.4:8333", "66.194.38.250:8333", "66.194.38.253:8333", "66.215.192.104:8333", "67.60.98.115:8333", "67.164.35.36:8333", "67.191.162.244:8333", "67.207.195.77:8333", "67.219.233.140:8333", "67.221.193.55:8333", "67.228.162.228:8333", "68.50.67.199:8333", "68.62.3.203:8333", "68.65.205.226:9000", "68.106.42.191:8333", "68.150.181.198:8333", "68.196.196.106:8333", "68.224.194.81:8333", "69.46.5.194:8333", "69.50.171.238:8333", "69.64.43.152:8333", "69.65.41.13:8333", "69.90.132.200:8333", "69.143.1.243:8333", "69.146.98.216:8333", "69.165.246.38:8333", "69.207.6.135:8333", "69.251.208.26:8333", "70.38.1.101:8333", "70.38.9.66:8333", "70.90.2.18:8333", "71.58.228.226:8333", "71.199.11.189:8333", "71.199.193.202:8333", "71.205.232.181:8333", "71.236.200.162:8333", "72.24.73.186:8333", "72.52.130.110:8333", "72.53.111.37:8333", "72.235.38.70:8333", "73.31.171.149:8333", "73.32.137.72:8333", "73.137.133.238:8333", "73.181.192.103:8333", "73.190.2.60:8333", "73.195.192.137:8333", "73.222.35.117:8333", "74.57.199.180:8333", "74.82.233.205:8333", "74.85.66.82:8333", "74.101.224.127:8333", "74.113.69.16:8333", "74.122.235.68:8333", "74.193.68.141:8333", "74.208.164.219:8333", "75.100.37.122:8333", "75.145.149.169:8333", "75.168.34.20:8333", "76.20.44.240:8333", "76.100.70.17:8333", "76.168.3.239:8333", "76.186.140.103:8333", "77.92.68.221:8333", "77.109.101.142:8333", "77.110.11.86:8333", "77.242.108.18:8333", "78.46.96.150:9020", "78.84.100.95:8333", "79.132.230.144:8333", "79.133.43.63:8333", "79.160.76.153:8333", "79.169.34.24:8333", "79.188.7.78:8333", "80.217.226.25:8333", "80.223.100.179:8333", "80.240.129.221:8333", "81.1.173.243:8333", "81.7.11.50:8333", "81.7.16.17:8333", "81.66.111.3:8333", "81.80.9.71:8333", "81.140.43.138:8333", "81.171.34.37:8333", "81.174.247.50:8333", "81.181.155.53:8333", "81.184.5.253:8333", "81.187.69.130:8333", "81.230.3.84:8333", "82.42.128.51:8333", "82.74.226.21:8333", "82.142.75.50:8333", "82.199.102.10:8333", "82.200.205.30:8333", "82.221.108.21:8333", "82.221.128.35:8333", "82.238.124.41:8333", "82.242.0.245:8333", "83.76.123.110:8333", "83.150.9.196:8333", "83.162.196.192:8333", "83.162.234.224:8333", "83.170.104.91:8333", "83.255.66.118:8334", "84.2.34.104:8333", "84.45.98.91:8333", "84.47.161.150:8333", "84.212.192.131:8333", "84.215.169.101:8333", "84.238.140.176:8333", "84.245.71.31:8333", "85.17.4.212:8333", "85.114.128.134:8333", "85.159.237.191:8333", "85.166.130.189:8333", "85.199.4.228:8333", "85.214.66.168:8333", "85.214.195.210:8333", "85.229.0.73:8333", "86.21.96.45:8333", "87.48.42.199:8333", "87.81.143.82:8333", "87.81.251.72:8333", "87.104.24.185:8333", "87.104.168.104:8333", "87.117.234.71:8333", "87.118.96.197:8333", "87.145.12.57:8333", "87.159.170.190:8333", "88.150.168.160:8333", "88.208.0.79:8333", "88.208.0.149:8333", "88.214.194.226:8343", "89.1.11.32:8333", "89.36.235.108:8333", "89.67.96.2:15321", "89.98.16.41:8333", "89.108.72.195:8333", "89.156.35.157:8333", "89.163.227.28:8333", "89.212.33.237:8333", "89.212.160.165:8333", "89.231.96.83:8333", "89.248.164.64:8333", "90.149.193.199:8333", "91.77.239.245:8333", "91.106.194.97:8333", "91.126.77.77:8333", "91.134.38.195:8333", "91.156.97.181:8333", "91.207.68.144:8333", "91.209.77.101:8333", "91.214.200.205:8333", "91.220.131.242:8333", "91.220.163.18:8333", "91.233.23.35:8333", "92.13.96.93:8333", "92.14.74.114:8333", "92.27.7.209:8333", "92.221.228.13:8333", "92.255.207.73:8333", "93.72.167.148:8333", "93.74.163.234:8333", "93.123.174.66:8333", "93.152.166.29:8333", "93.181.45.188:8333", "94.19.12.244:8333", "94.190.227.112:8333", "94.198.135.29:8333", "94.224.162.65:8333", "94.226.107.86:8333", "94.242.198.161:8333", "95.31.10.209:8333", "95.65.72.244:8333", "95.84.162.95:8333", "95.90.139.46:8333", "95.183.49.27:8005", "95.215.47.133:8333", "96.23.67.85:8333", "96.44.166.190:8333", "97.93.225.74:8333", "98.26.0.34:8333", "98.27.225.102:8333", "98.229.117.229:8333", "98.249.68.125:8333", "98.255.5.155:8333", "99.101.240.114:8333", "101.100.174.138:8333", "101.251.203.6:8333", "103.3.60.61:8333", "103.30.42.189:8333", "103.224.165.48:8333", "104.36.83.233:8333", "104.37.129.22:8333", "104.54.192.251:8333", "104.128.228.252:8333", "104.128.230.185:8334", "104.130.161.47:8333", "104.131.33.60:8333", "104.143.0.156:8333", "104.156.111.72:8333", "104.167.111.84:8333", "104.193.40.248:8333", "104.197.7.174:8333", "104.197.8.250:8333", "104.223.1.133:8333", "104.236.97.140:8333", "104.238.128.214:8333", "104.238.130.182:8333", "106.38.234.84:8333", "106.185.36.204:8333", "107.6.4.145:8333", "107.150.2.6:8333", "107.150.40.234:8333", "107.155.108.130:8333", "107.161.182.115:8333", "107.170.66.231:8333", "107.190.128.226:8333", "107.191.106.115:8333", "108.16.2.61:8333", "109.70.4.168:8333", "109.162.35.196:8333", "109.163.235.239:8333", "109.190.196.220:8333", "109.191.39.60:8333", "109.234.106.191:8333", "109.238.81.82:8333", "114.76.147.27:8333", "115.28.224.127:8333", "115.68.110.82:18333", "118.97.79.218:8333", "118.189.207.197:8333", "119.228.96.233:8333", "120.147.178.81:8333", "121.41.123.5:8333", "121.67.5.230:8333", "122.107.143.110:8333", "123.2.170.98:8333", "123.110.65.94:8333", "123.193.139.19:8333", "125.239.160.41:8333", "128.101.162.193:8333", "128.111.73.10:8333", "128.140.229.73:8333", "128.175.195.31:8333", "128.199.107.63:8333", "128.199.192.153:8333", "128.253.3.193:20020", "129.123.7.7:8333", "130.89.160.234:8333", "131.72.139.164:8333", "131.191.112.98:8333", "133.1.134.162:8333", "134.19.132.53:8333", "137.226.34.42:8333", "141.41.2.172:8333", "141.255.128.204:8333", "142.217.12.106:8333", "143.215.129.126:8333", "146.0.32.101:8337", "147.229.13.199:8333", "149.210.133.244:8333", "149.210.162.187:8333", "150.101.163.241:8333", "151.236.11.189:8333", "153.121.66.211:8333", "154.20.2.139:8333", "159.253.23.132:8333", "162.209.106.123:8333", "162.210.198.184:8333", "162.218.65.121:8333", "162.222.161.49:8333", "162.243.132.6:8333", "162.243.132.58:8333", "162.248.99.164:53011", "162.248.102.117:8333", "163.158.35.110:8333", "164.15.10.189:8333", "164.40.134.171:8333", "166.230.71.67:8333", "167.160.161.199:8333", "168.103.195.250:8333", "168.144.27.112:8333", "168.158.129.29:8333", "170.75.162.86:8333", "172.90.99.174:8333", "172.245.5.156:8333", "173.23.166.47:8333", "173.32.11.194:8333", "173.34.203.76:8333", "173.171.1.52:8333", "173.175.136.13:8333", "173.230.228.139:8333", "173.247.193.70:8333", "174.49.132.28:8333", "174.52.202.72:8333", "174.53.76.87:8333", "174.109.33.28:8333", "176.28.12.169:8333", "176.35.182.214:8333", "176.36.33.113:8333", "176.36.33.121:8333", "176.58.96.173:8333", "176.121.76.84:8333", "178.62.70.16:8333", "178.62.111.26:8333", "178.76.169.59:8333", "178.79.131.32:8333", "178.162.199.216:8333", "178.175.134.35:8333", "178.248.111.4:8333", "178.254.1.170:8333", "178.254.34.161:8333", "179.43.143.120:8333", "179.208.156.198:8333", "180.200.128.58:8333", "183.78.169.108:8333", "183.96.96.152:8333", "184.68.2.46:8333", "184.73.160.160:8333", "184.94.227.58:8333", "184.152.68.163:8333", "185.7.35.114:8333", "185.28.76.179:8333", "185.31.160.202:8333", "185.45.192.129:8333", "185.66.140.15:8333", "186.2.167.23:8333", "186.220.101.142:8333", "188.26.5.33:8333", "188.75.136.146:8333", "188.120.194.140:8333", "188.121.5.150:8333", "188.138.0.114:8333", "188.138.33.239:8333", "188.166.0.82:8333", "188.182.108.129:8333", "188.191.97.208:8333", "188.226.198.102:8001", "190.10.9.217:8333", "190.75.143.144:8333", "190.139.102.146:8333", "191.237.64.28:8333", "192.3.131.61:8333", "192.99.225.3:8333", "192.110.160.122:8333", "192.146.137.1:8333", "192.183.198.204:8333", "192.203.228.71:8333", "193.0.109.3:8333", "193.12.238.204:8333", "193.91.200.85:8333", "193.234.225.156:8333", "194.6.233.38:8333", "194.63.143.136:8333", "194.126.100.246:8333", "195.134.99.195:8333", "195.159.111.98:8333", "195.159.226.139:8333", "195.197.175.190:8333", "198.48.199.108:8333", "198.57.208.134:8333", "198.57.210.27:8333", "198.62.109.223:8333", "198.167.140.8:8333", "198.167.140.18:8333", "199.91.173.234:8333", "199.127.226.245:8333", "199.180.134.116:8333", "200.7.96.99:8333", "201.160.106.86:8333", "202.55.87.45:8333", "202.60.68.242:8333", "202.60.69.232:8333", "202.124.109.103:8333", "203.30.197.77:8333", "203.88.160.43:8333", "203.151.140.14:8333", "203.219.14.204:8333", "205.147.40.62:8333", "207.235.39.214:8333", "207.244.73.8:8333", "208.12.64.225:8333", "208.76.200.200:8333", "209.40.96.121:8333", "209.126.107.176:8333", "209.141.40.149:8333", "209.190.75.59:8333", "209.208.111.142:8333", "210.54.34.164:8333", "211.72.66.229:8333", "212.51.144.42:8333", "212.112.33.157:8333", "212.116.72.63:8333", "212.126.14.122:8333", "213.66.205.194:8333", "213.111.196.21:8333", "213.122.107.102:8333", "213.136.75.175:8333", "213.155.7.24:8333", "213.163.64.31:8333", "213.163.64.208:8333", "213.165.86.136:8333", "213.184.8.22:8333", "216.15.78.182:8333", "216.55.143.154:8333", "216.115.235.32:8333", "216.126.226.166:8333", "216.145.67.87:8333", "216.169.141.169:8333", "216.249.92.230:8333", "216.250.138.230:8333", "217.20.171.43:8333", "217.23.2.71:8333", "217.23.2.242:8333", "217.25.9.76:8333", "217.40.226.169:8333", "217.123.98.9:8333", "217.155.36.62:8333", "217.172.32.18:20993", "218.61.196.202:8333", "218.231.205.41:8333", "220.233.77.200:8333", "223.18.226.85:8333", "223.197.203.82:8333", "223.255.166.142:8333" };

		//Format visual studio
		//{({.*?}), (.*?)}
		//Tuple.Create(new byte[]$1, $2)
		private static readonly Tuple<byte[], int>[] PnSeed6Main =
		{
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x01, 0x20, 0xc8, 0x78
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x02, 0x21, 0x16, 0xa1
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x09, 0x13, 0x6d
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x09, 0x1c, 0x0a
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x09, 0x90, 0x53
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x09, 0xdc, 0x84
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x2c, 0x61, 0x6e
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x38, 0x28, 0x01
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x38, 0x32, 0x71
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x38, 0xf7, 0x45
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x3d, 0x21, 0xdc
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x3d, 0x28, 0x38
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x4f, 0x4f, 0x96
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x67, 0x89, 0x92
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x87, 0x9d, 0x11
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0xbd, 0x90, 0xfa
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0xbd, 0x99, 0x85
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0xbd, 0xa4, 0x93
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0xbd, 0xac, 0xc8
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0xbd, 0xbf, 0x7b
				}, 8335),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0xe6, 0x91, 0x82
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0xf9, 0x3a, 0x4c
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x08, 0x26, 0x58, 0x7e
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0c, 0x17, 0x7f, 0xaf
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x36, 0x5f, 0x93
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x36, 0xe0, 0x54
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x37, 0x83, 0xf0
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x37, 0xc8, 0xb1
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x38, 0xa8, 0x40
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x44, 0xda, 0xf6
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x49, 0x00, 0x3d
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x4e, 0x70, 0x0b
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x52, 0x5c, 0xc9
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x5c, 0x52, 0xeb
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x5e, 0x29, 0x52
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x72, 0x7f, 0xbd
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x72, 0xee, 0xb8
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x72, 0xf5, 0xa0
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7c, 0x6e, 0x65
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7d, 0x17, 0xa4
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7d, 0x3b, 0x87
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0x0a, 0x03
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0x1f, 0x8d
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0x20, 0x67
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0x28, 0x32
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0x5d, 0x82
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0x8b, 0xb6
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0x9b, 0x3f
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0xd1, 0x75
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0xe2, 0xdd
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0xec, 0x29
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0xef, 0x57
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0x7e, 0xef, 0xdb
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xd2, 0x1e, 0x16
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xd2, 0xb0, 0x7c
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xe4, 0x6d, 0x99
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xe5, 0x39, 0x19
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xe5, 0x39, 0xd5
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xe5, 0x3b, 0xf0
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xe5, 0x3e, 0xbf
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xe5, 0x3f, 0x32
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xe5, 0x71, 0x8f
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xe5, 0x7a, 0xc4
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0e, 0x03, 0x26, 0xb3
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0e, 0x22, 0xae, 0xb5
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0e, 0x3f, 0x07, 0x3c
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0e, 0xa1, 0x03, 0x88
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x12, 0x66, 0xde, 0x7d
				}, 8335),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x12, 0x66, 0xde, 0x7e
				}, 8335),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x12, 0x66, 0xde, 0xeb
				}, 8335),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x12, 0xc4, 0x00, 0xf2
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x17, 0x5b, 0xef, 0x4c
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x17, 0x61, 0x4c, 0x60
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x17, 0x63, 0xcc, 0x47
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x17, 0x7e, 0x7e, 0x7b
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x17, 0xf2, 0x89, 0xed
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x04, 0xdf, 0x07
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x06, 0x23, 0x54
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x06, 0xbb, 0x4f
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x10, 0x4b, 0x80
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x1c, 0x1f, 0x2d
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x2c, 0x04, 0x5d
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x30, 0x0d, 0x57
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x41, 0x38, 0x89
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x47, 0x22, 0xc6
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x47, 0x28, 0x2e
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x4c, 0x7a, 0x6c
				}, 8090),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x5b, 0x54, 0xf1
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x71, 0xc1, 0x18
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0x78, 0xaf, 0xf3
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0xb0, 0x0d, 0x0a
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0xbe, 0x32, 0x71
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0xbe, 0x73, 0x7c
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0xbe, 0x7a, 0xd5
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0xc0, 0x36, 0x80
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0xc4, 0xb1, 0x9f
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0xcd, 0x8f, 0xb2
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0xd1, 0x74, 0x86
				}, 8333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x18, 0xd4, 0x8c, 0xa3
				}, 28333)
		};

		private static readonly Tuple<byte[], int>[] PnSeed6Test =
		{
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x09, 0x96, 0x70
				}, 9696),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x05, 0x0a, 0x4a, 0x72
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0d, 0xe6, 0x8c, 0x79
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x23, 0xb8, 0x98, 0xad
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x23, 0xc1, 0x85, 0xf4
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x27, 0x6a, 0xf8, 0x2d
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x2e, 0x65, 0xf0, 0x47
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x2f, 0x34, 0x1f, 0xe8
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x2f, 0x5b, 0xc6, 0xae
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x33, 0xfe, 0xdb, 0xd6
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x34, 0x32, 0x55, 0x9d
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x36, 0xf9, 0x33, 0x6d
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x43, 0xcd, 0xb3, 0xa1
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x57, 0xec, 0xc6, 0x07
				}, 18433),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x5d, 0x7c, 0x04, 0x59
				}, 38333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x68, 0xc6, 0xc2, 0xa6
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x68, 0xee, 0xc6, 0xa5
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x78, 0x4f, 0x35, 0x22
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x80, 0xc7, 0x90, 0xe8
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x82, 0xd3, 0xa2, 0x7c
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x90, 0xd9, 0x49, 0x56
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x96, 0x5f, 0x22, 0x61
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x9e, 0x45, 0x77, 0x23
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xb0, 0x09, 0x59, 0xd9
				}, 9696),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xb0, 0x09, 0x9a, 0x6e
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xb9, 0x0c, 0x07, 0x77
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xbc, 0x28, 0x5d, 0xcd
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xcf, 0x9a, 0xc4, 0x95
				}, 18333),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xcf, 0x9a, 0xd2, 0xde
				}, 10201),
			Tuple.Create(
				new byte[]
				{
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xda, 0xf4, 0x92, 0x6f
				}, 18333)
		};

		private static readonly Dictionary<string, Network> OtherAliases = new Dictionary<string, Network>();
		private static readonly List<Network> OtherNetworks = new List<Network>();
		private readonly List<NetworkAddress> _vFixedSeeds = new List<NetworkAddress>();
		private readonly List<DNSSeedData> _vSeeds = new List<DNSSeedData>();
		private PubKey _alertPubKey;
		private byte[] _genesisBytes;
		private byte[] _magicBytes;
		private byte[] _vAlertPubKey;

		static Network()
		{
			Main = new Network {NetworkSet = BitcoinCash.Instance};
			Main.InitMain();
			Main.Consensus.Freeze();

			TestNet = new Network {NetworkSet = BitcoinCash.Instance};
			TestNet.InitTest();
			TestNet.Consensus.Freeze();

			RegTest = new Network {NetworkSet = BitcoinCash.Instance};
			RegTest.InitReg();
			RegTest.Consensus.Freeze();
		}

		private Network()
		{
		}

		public PubKey AlertPubKey
		{
			get
			{
				if (_alertPubKey == null)
				{
					_alertPubKey = new PubKey(_vAlertPubKey);
				}

				return _alertPubKey;
			}
		}

		public int RPCPort { get; private set; }

		public int DefaultPort { get; private set; }

		public Consensus Consensus { get; private set; } = new Consensus();

		public string Name { get; private set; }

		public NetworkType NetworkType { get; private set; }

		public string Prefix { get; private set; }

		public static Network Main { get; }

		public static Network TestNet { get; }

		public static Network RegTest { get; }

		public INetworkSet NetworkSet { get; private set; }


		public uint256 GenesisHash => Consensus.HashGenesisBlock;

		public IEnumerable<NetworkAddress> SeedNodes => _vFixedSeeds;

		public IEnumerable<DNSSeedData> DNSSeeds => _vSeeds;

		public byte[] MagicBytes
		{
			get
			{
				if (_magicBytes == null)
				{
					var bytes = new[]
					{
						(byte) Magic,
						(byte) (Magic >> 8),
						(byte) (Magic >> 16),
						(byte) (Magic >> 24)
					};
					_magicBytes = bytes;
				}

				return _magicBytes;
			}
		}

		public uint Magic { get; private set; }

		private static IEnumerable<NetworkAddress> ToSeed(Tuple<byte[], int>[] tuples)
		{
			return tuples
				.Select(t => new NetworkAddress(new IPAddress(t.Item1), t.Item2))
				.ToArray();
		}

		internal static Network Register(NetworkBuilder builder)
		{
			if (builder._name == null)
			{
				throw new InvalidOperationException("A network name need to be provided");
			}

			if (GetNetwork(builder._name) != null)
			{
				throw new InvalidOperationException("The network " + builder._name + " is already registered");
			}

			var network = new Network
			{
				Name = builder._name,
				NetworkType = builder._networkType,
				Prefix = builder._prefix,
				Consensus = builder._consensus,
				Magic = builder._magic,
				NetworkSet = builder._networkSet,
				DefaultPort = builder._port,
				RPCPort = builder._rpcPort,
				MaxP2PVersion = builder._maxP2PVersion ?? BitcoinMaxP2PVersion
			};


			foreach (var seed in builder._vSeeds)
			{
				network._vSeeds.Add(seed);
			}

			foreach (var seed in builder._vFixedSeeds)
			{
				network._vFixedSeeds.Add(seed);
			}

			network.Base58Prefixes = Main.Base58Prefixes.ToArray();
			foreach (var kv in builder._base58Prefixes)
			{
				network.Base58Prefixes[(int) kv.Key] = kv.Value;
			}

			lock (OtherAliases)
			{
				foreach (var alias in builder._aliases)
				{
					OtherAliases.Add(alias.ToLowerInvariant(), network);
				}

				OtherAliases.Add(network.Name.ToLowerInvariant(), network);
				var defaultAlias = network.NetworkSet.CryptoCode.ToLowerInvariant() + "-" +
				                   network.NetworkType.ToString().ToLowerInvariant();
				if (!OtherAliases.ContainsKey(defaultAlias))
				{
					OtherAliases.Add(defaultAlias, network);
				}
			}

			lock (OtherNetworks)
			{
				OtherNetworks.Add(network);
			}


			network._genesisBytes = builder._genesis;
			network.Consensus.SetBlock(builder._genesis);
			network.Consensus.Freeze();
			return network;
		}

		private void InitMain()
		{
			Name = "Main";
			NetworkType = NetworkType.Mainnet;
			Prefix = "bitcoincash";
			MaxP2PVersion = BitcoinMaxP2PVersion;

			Consensus.SubsidyHalvingInterval = 210000;
			Consensus.MajorityEnforceBlockUpgrade = 750;
			Consensus.MajorityRejectBlockOutdated = 950;
			Consensus.MajorityWindow = 1000;
			//-consensus.BuriedDeployments[BuriedDeployments.BIP34] = 227931;
			//-consensus.BuriedDeployments[BuriedDeployments.BIP65] = 388381;
			//-consensus.BuriedDeployments[BuriedDeployments.BIP66] = 363725;
			Consensus.BIP34Hash = new uint256("000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
			Consensus.PowLimit =
				new Target(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
			Consensus.MinimumChainWork =
				new uint256("0000000000000000000000000000000000000000007e5dbf54c7f6b58a6853cd");
			Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
			Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
			Consensus.PowAllowMinDifficultyBlocks = false;
			Consensus.PowNoRetargeting = false;
			Consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
			Consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
			Consensus.CoinbaseMaturity = 100;

			//-consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999);
			//-consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1462060800, 1493596800);
			//-consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 1479168000, 1510704000);

			Consensus.CoinType = 0;

			// The message start string is designed to be unlikely to occur in normal data.
			// The characters are rarely used upper ASCII, not valid as UTF-8, and produce
			// a large 4-byte int at any alignment.
			Magic = 0xe8f3e1e3;
			_vAlertPubKey =
				Encoders.Hex.DecodeData(
					"04fc9702847840aaf195de8442ebecedf5b095cdbb9bc716bda9110971b28a49e0ead8564ff0db22209e0374782c093bb899692d524e9d6a6956e7c5ecbcd68284");
			DefaultPort = 8333;
			RPCPort = 8332;
			_genesisBytes = CreateGenesisBlock(1231006505, 2083236893, 0x1d00ffff, 1, Money.Coins(50m)).ToBytes();
			Consensus.SetBlock(_genesisBytes);
			assert(Consensus.HashGenesisBlock ==
			       uint256.Parse("0x000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"));

			_vSeeds.Add(new DNSSeedData("bitcoinabc.org", "seed.bitcoinabc.org"));
			_vSeeds.Add(new DNSSeedData("bitcoinforks.org", "seed-abc.bitcoinforks.org"));
			_vSeeds.Add(new DNSSeedData("bitcoinunlimited.info", "btccash-seeder.bitcoinunlimited.info"));
			_vSeeds.Add(new DNSSeedData("bitprim.org", "seed.bitprim.org"));
			_vSeeds.Add(new DNSSeedData("deadalnix.me", "seed.deadalnix.me"));
			_vSeeds.Add(new DNSSeedData("criptolayer.net", "seeder.criptolayer.net"));

			Base58Prefixes[(int) Base58Type.PUBKEY_ADDRESS] = new byte[] {0};
			Base58Prefixes[(int) Base58Type.SCRIPT_ADDRESS] = new byte[] {5};
			Base58Prefixes[(int) Base58Type.SECRET_KEY] = new byte[] {128};
			Base58Prefixes[(int) Base58Type.EXT_PUBLIC_KEY] = new byte[] {0x04, 0x88, 0xB2, 0x1E};
			Base58Prefixes[(int) Base58Type.EXT_SECRET_KEY] = new byte[] {0x04, 0x88, 0xAD, 0xE4};

			_vFixedSeeds.AddRange(ToSeed(PnSeed6Main));
			//// Convert the pnSeeds array into usable address objects.
			//Random rand = new Random();
			//TimeSpan nOneWeek = TimeSpan.FromDays(7);
			//for(int i = 0; i < pnSeed.Length; i++)
			//{
			//	// It'll only connect to one or two seed nodes because once it connects,
			//	// it'll get a pile of addresses with newer timestamps.				
			//	NetworkAddress addr = new NetworkAddress();
			//	// Seed nodes are given a random 'last seen time' of between one and two
			//	// weeks ago.
			//	addr.Time = DateTime.UtcNow - (TimeSpan.FromSeconds(rand.NextDouble() * nOneWeek.TotalSeconds)) - nOneWeek;
			//	addr.Endpoint = Utils.ParseIpEndpoint(pnSeed[i], DefaultPort);
			//	vFixedSeeds.Add(addr);
			//}
		}

		private void InitTest()
		{
			Name = "TestNet";
			TestNet.NetworkType = NetworkType.Testnet;
			NetworkType = NetworkType.Testnet;
			Prefix = "bchtest";

			MaxP2PVersion = BitcoinMaxP2PVersion;
			Consensus.SubsidyHalvingInterval = 210000;
			Consensus.MajorityEnforceBlockUpgrade = 51;
			Consensus.MajorityRejectBlockOutdated = 75;
			Consensus.MajorityWindow = 2016;
			//-consensus.BuriedDeployments[BuriedDeployments.BIP34] = 21111;
			//-consensus.BuriedDeployments[BuriedDeployments.BIP65] = 581885;
			//-consensus.BuriedDeployments[BuriedDeployments.BIP66] = 330776;
			Consensus.BIP34Hash = new uint256("0000000023b3a96d3484e5abb3755c413e7d41500f8e2a5c3f0dd01299cd8ef8");
			Consensus.PowLimit =
				new Target(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
			Consensus.MinimumChainWork =
				new uint256("00000000000000000000000000000000000000000000002888c34d61b53a244a");
			Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
			Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
			Consensus.PowAllowMinDifficultyBlocks = true;
			Consensus.PowNoRetargeting = false;
			Consensus.RuleChangeActivationThreshold = 1512; // 75% for testchains
			Consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
			Consensus.CoinbaseMaturity = 100;

			//-consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999);
			//-consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1456790400, 1493596800);
			//-consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 1462060800, 1493596800);

			Consensus.CoinType = 1;

			Magic = 0xf4f3e5f4;

			_vAlertPubKey =
				Encoders.Hex.DecodeData(
					"04302390343f91cc401d56d68b123028bf52e5fca1939df127f63c6467cdf9c8e2c14b61104cf817d0b780da337893ecc4aaff1309e536162dabbdb45200ca2b0a");
			DefaultPort = 18333;
			RPCPort = 18332;
			//strDataDir = "testnet3";

			// Modify the testnet genesis block so the timestamp is valid for a later start.
			_genesisBytes = CreateGenesisBlock(1296688602, 414098458, 0x1d00ffff, 1, Money.Coins(50m)).ToBytes();
			Consensus.SetBlock(_genesisBytes);
			assert(Consensus.HashGenesisBlock ==
			       uint256.Parse("0x000000000933ea01ad0ee984209779baaec3ced90fa3f408719526f8d77f4943"));

			_vFixedSeeds.Clear();
			_vFixedSeeds.AddRange(ToSeed(PnSeed6Test));

			_vSeeds.Clear();
			_vSeeds.Add(new DNSSeedData("bitcoinabc.org", "testnet-seed.bitcoinabc.org"));
			_vSeeds.Add(new DNSSeedData("bitcoinforks.org", "testnet-seed-abc.bitcoinforks.org"));
			_vSeeds.Add(new DNSSeedData("bitprim.org", "testnet-seed.bitprim.org"));
			_vSeeds.Add(new DNSSeedData("deadalnix.me", "testnet-seed.deadalnix.me"));
			_vSeeds.Add(new DNSSeedData("criptolayer.net", "testnet-seeder.criptolayer.net"));

			Base58Prefixes = Main.Base58Prefixes.ToArray();
			Base58Prefixes[(int) Base58Type.PUBKEY_ADDRESS] = new byte[] {111};
			Base58Prefixes[(int) Base58Type.SCRIPT_ADDRESS] = new byte[] {196};
			Base58Prefixes[(int) Base58Type.SECRET_KEY] = new byte[] {239};
			Base58Prefixes[(int) Base58Type.EXT_PUBLIC_KEY] = new byte[] {0x04, 0x35, 0x87, 0xCF};
			Base58Prefixes[(int) Base58Type.EXT_SECRET_KEY] = new byte[] {0x04, 0x35, 0x83, 0x94};
		}

		private void InitReg()
		{
			Name = "RegTest";
			NetworkType = NetworkType.Regtest;
			Prefix = "bchreg";

			MaxP2PVersion = BitcoinMaxP2PVersion;
			Consensus.SubsidyHalvingInterval = 150;
			Consensus.MajorityEnforceBlockUpgrade = 750;
			Consensus.MajorityRejectBlockOutdated = 950;
			Consensus.MajorityWindow = 144;
			//-consensus.BuriedDeployments[BuriedDeployments.BIP34] = 100000000;
			//-consensus.BuriedDeployments[BuriedDeployments.BIP65] = 100000000;
			//-consensus.BuriedDeployments[BuriedDeployments.BIP66] = 100000000;
			Consensus.BIP34Hash = new uint256();
			Consensus.PowLimit =
				new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
			Consensus.MinimumChainWork = uint256.Zero;
			Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
			Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
			Consensus.PowAllowMinDifficultyBlocks = true;
			Consensus.PowNoRetargeting = true;
			Consensus.RuleChangeActivationThreshold = 108;
			Consensus.MinerConfirmationWindow = 144;
			Consensus.CoinbaseMaturity = 100;

			Magic = 0xfabfb5da;

			//-consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 0, 999999999);
			//-consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 0, 999999999);
			//-consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, BIP9DeploymentsParameters.AlwaysActive, 999999999);

			//_GenesisBytes = CreateGenesisBlock(1296688602, 2, 0x207fffff, 1, Money.Coins(50m)).ToBytes();
			_genesisBytes = Encoders.Hex.DecodeData(
				"0100000000000000000000000000000000000000000000000000000000000000000000003ba3edfd7a7b12b27ac72c3e67768f617fc81bc3888a51323a9fb8aa4b1e5e4adae5494dffff7f20020000000101000000010000000000000000000000000000000000000000000000000000000000000000ffffffff4d04ffff001d0104455468652054696d65732030332f4a616e2f32303039204368616e63656c6c6f72206f6e206272696e6b206f66207365636f6e64206261696c6f757420666f722062616e6b73ffffffff0100f2052a01000000434104678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5fac00000000");
			Consensus.SetBlock(_genesisBytes);
			DefaultPort = 18444;
			RPCPort = 18443;
			//strDataDir = "regtest";
			//assert(consensus.HashGenesisBlock == uint256.Parse("0x0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206"));

			_vSeeds.Clear(); // Regtest mode doesn't have any DNS seeds.

			Base58Prefixes = TestNet.Base58Prefixes.ToArray();
			Base58Prefixes[(int) Base58Type.PUBKEY_ADDRESS] = new byte[] {111};
			Base58Prefixes[(int) Base58Type.SCRIPT_ADDRESS] = new byte[] {196};
			Base58Prefixes[(int) Base58Type.SECRET_KEY] = new byte[] {239};
			Base58Prefixes[(int) Base58Type.EXT_PUBLIC_KEY] = new byte[] {0x04, 0x35, 0x87, 0xCF};
			Base58Prefixes[(int) Base58Type.EXT_SECRET_KEY] = new byte[] {0x04, 0x35, 0x83, 0x94};
		}

		private Block CreateGenesisBlock(uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
		{
			var pszTimestamp = "The Times 03/Jan/2009 Chancellor on brink of second bailout for banks";
			var genesisOutputScript =
				new Script(
					Op.GetPushOp(Encoders.Hex.DecodeData(
						"04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5f")),
					OpcodeType.OP_CHECKSIG);
			return CreateGenesisBlock(pszTimestamp, genesisOutputScript, nTime, nNonce, nBits, nVersion, genesisReward);
		}

		private Block CreateGenesisBlock(string pszTimestamp, Script genesisOutputScript, uint nTime, uint nNonce,
			uint nBits, int nVersion, Money genesisReward)
		{
			var txNew = Consensus.ConsensusFactory.CreateTransaction();
			txNew.Version = 1;
			txNew.Inputs.Add(scriptSig: new Script(Op.GetPushOp(486604799), new Op
			{
				Code = (OpcodeType) 0x1,
				PushData = new[] {(byte) 4}
			}, Op.GetPushOp(Encoders.ASCII.DecodeData(pszTimestamp))));
			txNew.Outputs.Add(genesisReward, genesisOutputScript);
			var genesis = Consensus.ConsensusFactory.CreateBlock();
			genesis.Header.BlockTime = Utils.UnixTimeToDateTime(nTime);
			genesis.Header.Bits = nBits;
			genesis.Header.Nonce = nNonce;
			genesis.Header.Version = nVersion;
			genesis.Transactions.Add(txNew);
			genesis.Header.HashPrevBlock = uint256.Zero;
			genesis.UpdateMerkleRoot();
			return genesis;
		}

		private static void assert(bool v)
		{
			if (!v)
			{
				throw new InvalidOperationException("Invalid network");
			}
		}

		public BitcoinSecret CreateBitcoinSecret(string base58)
		{
			return new BitcoinSecret(base58, this);
		}

		/// <summary>
		///     Create a bitcoin address from base58 data, return a BitcoinAddress or BitcoinScriptAddress
		/// </summary>
		/// <param name="base58">base58 address</param>
		/// <exception cref="System.FormatException">Invalid base58 address</exception>
		/// <returns>BitcoinScriptAddress, BitcoinAddress</returns>
		public BitcoinAddress CreateBitcoinAddress(string base58)
		{
			var type = GetBase58Type(base58);
			if (!type.HasValue)
			{
				throw new FormatException("Invalid Base58 version");
			}

			switch (type.Value)
			{
				case Base58Type.PUBKEY_ADDRESS:
					return new BitcoinPubKeyAddress(base58, this);

				case Base58Type.SCRIPT_ADDRESS:
					return new BitcoinScriptAddress(base58, this);

				default:
					throw new FormatException("Invalid Base58 version");
			}
		}

		public BitcoinScriptAddress CreateBitcoinScriptAddress(string base58)
		{
			return new BitcoinScriptAddress(base58, this);
		}

		private Base58Type? GetBase58Type(string base58)
		{
			var bytes = Encoders.Base58Check.DecodeData(base58);
			for (var i = 0; i < Base58Prefixes.Length; i++)
			{
				var prefix = Base58Prefixes[i];
				if (prefix == null)
				{
					continue;
				}

				if (bytes.Length < prefix.Length)
				{
					continue;
				}

				if (Utils.ArrayEqual(bytes, 0, prefix, 0, prefix.Length))
				{
					return (Base58Type) i;
				}
			}

			return null;
		}


		internal static Network GetNetworkFromBase58Data(string base58, Base58Type? expectedType = null)
		{
			foreach (var network in GetNetworks())
			{
				var type = network.GetBase58Type(base58);
				if (type.HasValue)
				{
					if (expectedType != null && expectedType.Value != type.Value)
					{
						continue;
					}

					return network;
				}
			}

			return null;
		}

		public IBitcoinString Parse(string str)
		{
			return Parse<IBitcoinString>(str, this);
		}

		public T Parse<T>(string str) where T : IBitcoinString
		{
			return Parse<T>(str, this);
		}

		public bool TryParse<T>(string str, out T result) where T : IBitcoinString
		{
			return TryParse(str, this, out result);
		}

		public static IBitcoinString Parse(string str, Network expectedNetwork)
		{
			return Parse<IBitcoinString>(str, expectedNetwork);
		}

		public static T Parse<T>(string str, Network expectedNetwork) where T : IBitcoinString
		{
			if (InternalTryParse<T>(str, expectedNetwork, true, out var result))
			{
				return result;
			}

			return default;
		}

		public static bool TryParse<T>(string str, Network expectedNetwork, out T result) where T : IBitcoinString
		{
			return InternalTryParse(str, expectedNetwork, false, out result);
		}

		private static bool InternalTryParse<T>(string str, Network expectedNetwork, bool throwOnFailure, out T result)
			where T : IBitcoinString
		{
			result = default;

			if (str == null)
			{
				if (throwOnFailure)
				{
					throw new ArgumentNullException(nameof(str));
				}

				return false;
			}

			if (!BchAddr.TryDetectFormat(str, out var format))
			{
				if (throwOnFailure)
				{
					throw new FormatException("Address format cannot be recognized.");
				}

				return false;
			}

			if (!ValidateAddressChecksum(format, str))
			{
				if (throwOnFailure)
				{
					throw new FormatException("Address cannot be validated.");
				}

				return false;
			}

			var networks = expectedNetwork == null ? GetNetworks().ToArray() : new[] {expectedNetwork};
			foreach (var network in networks)
			{
				switch (format)
				{
					case CashFormat.Legacy:
						foreach (var candidate in GetCandidates(networks, str))
						{
							var rightNetwork = expectedNetwork == null || candidate.Network == expectedNetwork;
							var rightType = candidate is T;
							if (rightNetwork && rightType)
							{
								result = (T) candidate;
								return true;
							}
						}

						break;

					case CashFormat.Cashaddr:
						if (TryToParseCashAddr(str, network, out T cashAddr))
						{
							result = cashAddr;
							return true;
						}

						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			if (throwOnFailure)
			{
				throw new FormatException("Address cannot be parsed.");
			}

			return false;
		}

		private static bool ValidateAddressChecksum(CashFormat format, string str)
		{
			var valid = true;
			switch (format)
			{
				case CashFormat.Legacy:
					try
					{
						Encoders.Base58Check.DecodeData(str);
					}
					catch
					{
						valid = false;
					}

					break;

				case CashFormat.Cashaddr:
					try
					{
						CashAddr.Decode(str);
					}
					catch
					{
						valid = false;
					}

					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			return valid;
		}

		private static IEnumerable<IBase58Data> GetCandidates(IEnumerable<Network> networks, string base58)
		{
			if (base58 == null)
			{
				throw new ArgumentNullException(nameof(base58));
			}

			foreach (var network in networks)
			{
				var type = network.GetBase58Type(base58);
				if (type.HasValue)
				{
					IBase58Data data = null;
					try
					{
						data = network.CreateBase58Data(type.Value, base58);
					}
					catch (FormatException)
					{
					}

					if (data != null)
					{
						yield return data;
					}
				}
			}
		}

		private static bool TryToParseCashAddr<T>(string str, Network network, out T result)
		{
			if (typeof(IBitcoinString).GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo()))
			{
				var prefix = network.Prefix;
				str = str.Trim();
				if (str.StartsWith($"{prefix}:", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						var addr = BchAddr.DecodeAddress(str, prefix, network);
						if (addr.Type == BchAddr.CashType.P2PKH)
						{
							result = (T) (object) new BitcoinPubKeyAddress(str, addr);
						}
						else
						{
							result = (T) (object) new BitcoinScriptAddress(str, addr);
						}

						return true;
					}
					catch
					{
					}
				}
			}

			result = default;
			return false;
		}

		//internal NetworkStringParser NetworkStringParser
		//{
		//	get;
		//	set;
		//} = null;

		public TransactionBuilder CreateTransactionBuilder()
		{
			return Consensus.ConsensusFactory.CreateTransactionBuilder();
		}

		public TransactionBuilder CreateTransactionBuilder(int seed)
		{
			return Consensus.ConsensusFactory.CreateTransactionBuilder(seed);
		}

		private IBase58Data CreateBase58Data(Base58Type type, string base58)
		{
			if (type == Base58Type.EXT_PUBLIC_KEY)
			{
				return CreateBitcoinExtPubKey(base58);
			}

			if (type == Base58Type.EXT_SECRET_KEY)
			{
				return CreateBitcoinExtKey(base58);
			}

			if (type == Base58Type.PUBKEY_ADDRESS)
			{
				return new BitcoinPubKeyAddress(base58, this);
			}

			if (type == Base58Type.SCRIPT_ADDRESS)
			{
				return new BitcoinScriptAddress(base58, this);
			}

			if (type == Base58Type.SECRET_KEY)
			{
				return CreateBitcoinSecret(base58);
			}

			throw new NotSupportedException("Invalid Base58Data type : " + type);
		}

		//private BitcoinWitScriptAddress CreateWitScriptAddress(string base58)
		//{
		//	return new BitcoinWitScriptAddress(base58, this);
		//}

		//private BitcoinWitPubKeyAddress CreateWitPubKeyAddress(string base58)
		//{
		//	return new BitcoinWitPubKeyAddress(base58, this);
		//}

		private Base58Data CreateBitcoinExtPubKey(string base58)
		{
			return new BitcoinExtPubKey(base58, this);
		}

		public BitcoinExtKey CreateBitcoinExtKey(ExtKey key)
		{
			return new BitcoinExtKey(key, this);
		}

		public BitcoinExtPubKey CreateBitcoinExtPubKey(ExtPubKey pubkey)
		{
			return new BitcoinExtPubKey(pubkey, this);
		}

		public BitcoinExtKey CreateBitcoinExtKey(string base58)
		{
			return new BitcoinExtKey(base58, this);
		}

		public override string ToString()
		{
			return Name;
		}

		public Block GetGenesis()
		{
			var block = Consensus.ConsensusFactory.CreateBlock();
			block.ReadWrite(_genesisBytes, Consensus.ConsensusFactory);
			return block;
		}

		public static IEnumerable<Network> GetNetworks()
		{
			yield return Main;
			yield return TestNet;
			yield return RegTest;

			if (OtherNetworks.Count != 0)
			{
				var others = new List<Network>();
				lock (OtherNetworks)
				{
					others = OtherNetworks.ToList();
				}

				foreach (var network in others)
				{
					yield return network;
				}
			}
		}

		/// <summary>
		///     Get network from name
		/// </summary>
		/// <param name="name">main,mainnet,testnet,test,testnet3,reg,regtest,seg,segnet</param>
		/// <returns>The network or null of the name does not match any network</returns>
		public static Network GetNetwork(string name)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			name = name.ToLowerInvariant();
			switch (name)
			{
				case "main":
				case "mainnet":
				case "bch-main":
				case "bch-mainnet":
				case "bcash-main":
				case "bcash-mainnet":
					return Main;

				case "test":
				case "testnet":
				case "testnet3":
				case "bch-test":
				case "bch-testnet":
				case "bcash-test":
				case "bcash-testnet":
					return TestNet;

				case "reg":
				case "regnet":
				case "regtest":
				case "bch-reg":
				case "bch-regtest":
				case "bcash-reg":
				case "bcash-regtest":
					return RegTest;
			}

			if (OtherAliases.Count != 0)
			{
				return OtherAliases.TryGet(name);
			}

			return null;
		}

		public BitcoinSecret CreateBitcoinSecret(Key key)
		{
			return new BitcoinSecret(key, this);
		}

		public BitcoinPubKeyAddress CreateBitcoinAddress(KeyId dest)
		{
			return CreateBitcoinAddress(CashFormat.Cashaddr, dest);
		}

		public BitcoinPubKeyAddress CreateBitcoinAddress(CashFormat format, KeyId dest)
		{
			if (dest == null)
			{
				throw new ArgumentNullException(nameof(dest));
			}

			var addr = new BchAddr.BchAddrData
			{
				Format = format,
				Prefix = Prefix,
				Hash = dest.ToBytes(true),
				Type = BchAddr.CashType.P2PKH,
				Network = this
			};
			return new BitcoinPubKeyAddress(addr.ToString(), addr);
		}

		public BitcoinScriptAddress CreateBitcoinScriptAddress(ScriptId scriptId)
		{
			return CreateBitcoinScriptAddress(CashFormat.Cashaddr, scriptId);
		}

		public BitcoinScriptAddress CreateBitcoinScriptAddress(CashFormat format, ScriptId scriptId)
		{
			if (scriptId == null)
			{
				throw new ArgumentNullException(nameof(scriptId));
			}

			var addr = new BchAddr.BchAddrData
			{
				Format = format,
				Prefix = Prefix,
				Hash = scriptId.ToBytes(true),
				Type = BchAddr.CashType.P2SH,
				Network = this
			};
			return new BitcoinScriptAddress(addr.ToString(), addr);
		}

		public Message ParseMessage(byte[] bytes, uint? version = null)
		{
			var bstream = new BitcoinStream(bytes) {ConsensusFactory = Consensus.ConsensusFactory};
			var message = new Message();
			using (bstream.ProtocolVersionScope(version))
			{
				message.ReadWrite(bstream);
			}

			if (message.Magic != Magic)
			{
				throw new FormatException("Unexpected magic field in the message");
			}

			return message;
		}

		public Money GetReward(int nHeight)
		{
			long nSubsidy = new Money(50 * Money.Coin);
			var halvings = nHeight / Consensus.SubsidyHalvingInterval;

			// Force block reward to zero when right shift is undefined.
			if (halvings >= 64)
			{
				return Money.Zero;
			}

			// Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
			nSubsidy >>= halvings;

			return new Money(nSubsidy);
		}

		public bool ReadMagic(Stream stream, CancellationToken cancellation, bool throwIfEOF = false)
		{
			var bytes = new byte[1];
			for (var i = 0; i < MagicBytes.Length; i++)
			{
				i = Math.Max(0, i);
				cancellation.ThrowIfCancellationRequested();

				var read = stream.ReadEx(bytes, 0, bytes.Length, cancellation);
				if (read == 0)
				{
					if (throwIfEOF)
					{
						throw new EndOfStreamException("No more bytes to read");
					}

					return false;
				}

				if (read != 1)
				{
					i--;
				}
				else if (_magicBytes[i] != bytes[0])
				{
					i = _magicBytes[0] == bytes[0] ? 0 : -1;
				}
			}

			return true;
		}
	}
}