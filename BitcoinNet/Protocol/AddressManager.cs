using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BitcoinNet.Crypto;
using BitcoinNet.Protocol.Behaviors;

namespace BitcoinNet.Protocol
{
	/// <summary>
	///     The AddressManager, keep a set of peers discovered on the network in cache can update their actual states.
	///     Replicate AddressManager of Bitcoin Core, the Buckets and BucketPosition are not guaranteed to be coherent with
	///     Bitcoin Core
	/// </summary>
	public class AddressManager : IBitcoinSerializable
	{
		//! total number of buckets for tried addresses
		internal const int ADDRMAN_TRIED_BUCKET_COUNT = 256;

		//! total number of buckets for new addresses
		internal const int ADDRMAN_NEW_BUCKET_COUNT = 1024;

		//! maximum allowed number of entries in buckets for new and tried addresses
		internal const int ADDRMAN_BUCKET_SIZE = 64;

		//! over how many buckets entries with tried addresses from a single group (/16 for IPv4) are spread
		internal const int ADDRMAN_TRIED_BUCKETS_PER_GROUP = 8;

		//! over how many buckets entries with new addresses originating from a single group are spread
		internal const int ADDRMAN_NEW_BUCKETS_PER_SOURCE_GROUP = 64;

		//! in how many buckets for entries with new addresses a single address may occur
		private const int ADDRMAN_NEW_BUCKETS_PER_ADDRESS = 8;


		//! how old addresses can maximally be
		internal const int ADDRMAN_HORIZON_DAYS = 30;

		//! after how many failed attempts we give up on a new node
		internal const int ADDRMAN_RETRIES = 3;

		//! how many successive failures are allowed ...
		internal const int ADDRMAN_MAX_FAILURES = 10;

		//! ... in at least this many days
		internal const int ADDRMAN_MIN_FAIL_DAYS = 7;

		//! the maximum percentage of nodes to return in a getaddr call
		private const int ADDRMAN_GETADDR_MAX_PCT = 23;

		//! the maximum number of nodes to return in a getaddr call
		private const int ADDRMAN_GETADDR_MAX = 2500;

		private readonly object _cs = new object();
		private readonly Dictionary<IPAddress, int> _mapAddr = new Dictionary<IPAddress, int>();

		private readonly Dictionary<int, AddressInfo> _mapInfo = new Dictionary<int, AddressInfo>();
		private int _nIdCount;
		internal uint256 _nKey;
		private byte _nKeySize = 32;
		internal int _nNew;
		internal int _nTried;

		private byte _nVersion = 1;
		private List<int> _vRandom;

		private int[,] _vvNew;
		private int[,] _vvTried;

		public AddressManager()
		{
			Clear();
		}

		internal bool DebugMode { get; set; }

		public int Count => _vRandom.Count;

		// IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			lock (_cs)
			{
				Check();
				if (!stream.Serializing)
				{
					Clear();
				}

				stream.ReadWrite(ref _nVersion);
				stream.ReadWrite(ref _nKeySize);
				if (!stream.Serializing && _nKeySize != 32)
				{
					throw new FormatException("Incorrect keysize in addrman deserialization");
				}

				stream.ReadWrite(ref _nKey);
				stream.ReadWrite(ref _nNew);
				stream.ReadWrite(ref _nTried);

				var nUBuckets = ADDRMAN_NEW_BUCKET_COUNT ^ (1 << 30);
				stream.ReadWrite(ref nUBuckets);
				if (_nVersion != 0)
				{
					nUBuckets ^= 1 << 30;
				}

				if (!stream.Serializing)
				{
					// Deserialize entries from the new table.
					for (var n = 0; n < _nNew; n++)
					{
						var info = new AddressInfo();
						info.ReadWrite(stream);
						_mapInfo.Add(n, info);
						_mapAddr[info.Address.Endpoint.Address] = n;
						info.NRandomPos = _vRandom.Count;
						_vRandom.Add(n);
						if (_nVersion != 1 || nUBuckets != ADDRMAN_NEW_BUCKET_COUNT)
						{
							// In case the new table data cannot be used (nVersion unknown, or bucket count wrong),
							// immediately try to give them a reference based on their primary source address.
							var nUBucket = info.GetNewBucket(_nKey);
							var nUBucketPos = info.GetBucketPosition(_nKey, true, nUBucket);
							if (_vvNew[nUBucket, nUBucketPos] == -1)
							{
								_vvNew[nUBucket, nUBucketPos] = n;
								info.NRefCount++;
							}
						}
					}

					_nIdCount = _nNew;

					// Deserialize entries from the tried table.
					var nLost = 0;
					for (var n = 0; n < _nTried; n++)
					{
						var info = new AddressInfo();
						info.ReadWrite(stream);
						var nKBucket = info.GetTriedBucket(_nKey);
						var nKBucketPos = info.GetBucketPosition(_nKey, false, nKBucket);
						if (_vvTried[nKBucket, nKBucketPos] == -1)
						{
							info.NRandomPos = _vRandom.Count;
							info.fInTried = true;
							_vRandom.Add(_nIdCount);
							_mapInfo[_nIdCount] = info;
							_mapAddr[info.Address.Endpoint.Address] = _nIdCount;
							_vvTried[nKBucket, nKBucketPos] = _nIdCount;
							_nIdCount++;
						}
						else
						{
							nLost++;
						}
					}

					_nTried -= nLost;

					// Deserialize positions in the new table (if possible).
					for (var bucket = 0; bucket < nUBuckets; bucket++)
					{
						var nSize = 0;
						stream.ReadWrite(ref nSize);
						for (var n = 0; n < nSize; n++)
						{
							var nIndex = 0;
							stream.ReadWrite(ref nIndex);
							if (nIndex >= 0 && nIndex < _nNew)
							{
								var info = _mapInfo[nIndex];
								var nUBucketPos = info.GetBucketPosition(_nKey, true, bucket);
								if (_nVersion == 1 && nUBuckets == ADDRMAN_NEW_BUCKET_COUNT &&
								    _vvNew[bucket, nUBucketPos] == -1 &&
								    info.NRefCount < ADDRMAN_NEW_BUCKETS_PER_ADDRESS)
								{
									info.NRefCount++;
									_vvNew[bucket, nUBucketPos] = nIndex;
								}
							}
						}
					}

					// Prune new entries with refcount 0 (as a result of collisions).
					var nLostUnk = 0;
					foreach (var kv in _mapInfo.ToList())
					{
						if (kv.Value.fInTried == false && kv.Value.NRefCount == 0)
						{
							Delete(kv.Key);
							nLostUnk++;
						}
					}
				}
				else
				{
					var mapUnkIds = new Dictionary<int, int>();
					var nIds = 0;
					foreach (var kv in _mapInfo)
					{
						mapUnkIds[kv.Key] = nIds;
						var info = kv.Value;
						if (info.NRefCount != 0)
						{
							Assert(nIds != _nNew); // this means nNew was wrong, oh ow
							info.ReadWrite(stream);
							nIds++;
						}
					}

					nIds = 0;
					foreach (var kv in _mapInfo)
					{
						var info = kv.Value;
						if (info.fInTried)
						{
							Assert(nIds != _nTried); // this means nTried was wrong, oh ow
							info.ReadWrite(stream);
							nIds++;
						}
					}

					for (var bucket = 0; bucket < ADDRMAN_NEW_BUCKET_COUNT; bucket++)
					{
						var nSize = 0;
						for (var i = 0; i < ADDRMAN_BUCKET_SIZE; i++)
						{
							if (_vvNew[bucket, i] != -1)
							{
								nSize++;
							}
						}

						stream.ReadWrite(ref nSize);
						for (var i = 0; i < ADDRMAN_BUCKET_SIZE; i++)
						{
							if (_vvNew[bucket, i] != -1)
							{
								var nIndex = mapUnkIds[_vvNew[bucket, i]];
								stream.ReadWrite(ref nIndex);
							}
						}
					}
				}

				Check();
			}
		}

		public static AddressManager LoadPeerFile(string filePath, Network expectedNetwork = null)
		{
			var addrman = new AddressManager();
			byte[] data, hash;
			using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
			{
				data = new byte[fs.Length - 32];
				fs.Read(data, 0, data.Length);
				hash = new byte[32];
				fs.Read(hash, 0, 32);
			}

			var actual = Hashes.Hash256(data);
			var expected = new uint256(hash);
			if (expected != actual)
			{
				throw new FormatException("Invalid address manager file");
			}

			var stream = new BitcoinStream(data) {Type = SerializationType.Disk};
			uint magic = 0;
			stream.ReadWrite(ref magic);
			if (expectedNetwork != null && expectedNetwork.Magic != magic)
			{
				throw new FormatException("This file is not for the expected network");
			}

			addrman.ReadWrite(stream);
			return addrman;
		}

		public void SavePeerFile(string filePath, Network network)
		{
			if (network == null)
			{
				throw new ArgumentNullException(nameof(network));
			}

			if (filePath == null)
			{
				throw new ArgumentNullException(nameof(filePath));
			}

			using (var ms = new MemoryStream())
			{
				var stream = new BitcoinStream(ms, true) {Type = SerializationType.Disk};
				stream.ReadWrite(network.Magic);
				stream.ReadWrite(this);
				var hash = Hashes.Hash256(ms.ToArray());
				stream.ReadWrite(hash.AsBitcoinSerializable());

				var dirPath = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrWhiteSpace(dirPath))
				{
					Directory.CreateDirectory(dirPath);
				}

				File.WriteAllBytes(filePath, ms.ToArray());
			}
		}

		private AddressInfo Find(IPAddress addr)
		{
			return Find(addr, out _);
		}

		private AddressInfo Find(IPAddress addr, out int pnId)
		{
			if (!_mapAddr.TryGetValue(addr, out pnId))
			{
				return null;
			}

			return _mapInfo.TryGet(pnId);
		}


		private void Clear()
		{
			_vRandom = new List<int>();
			_nKey = new uint256(RandomUtils.GetBytes(32));
			_vvNew = new int[ADDRMAN_NEW_BUCKET_COUNT, ADDRMAN_BUCKET_SIZE];
			for (var i = 0; i < ADDRMAN_NEW_BUCKET_COUNT; i++)
			for (var j = 0; j < ADDRMAN_BUCKET_SIZE; j++)
			{
				_vvNew[i, j] = -1;
			}

			_vvTried = new int[ADDRMAN_TRIED_BUCKET_COUNT, ADDRMAN_BUCKET_SIZE];
			for (var i = 0; i < ADDRMAN_TRIED_BUCKET_COUNT; i++)
			for (var j = 0; j < ADDRMAN_BUCKET_SIZE; j++)
			{
				_vvTried[i, j] = -1;
			}

			_nIdCount = 0;
			_nTried = 0;
			_nNew = 0;
		}

		//! Add a single address.
		public bool Add(NetworkAddress addr, IPAddress source)
		{
			return Add(addr, source, TimeSpan.Zero);
		}

		public bool Add(NetworkAddress addr)
		{
			return Add(addr, IPAddress.Loopback);
		}

		public bool Add(NetworkAddress addr, IPAddress source, TimeSpan nTimePenalty)
		{
			var fRet = false;
			lock (_cs)
			{
				Check();
				fRet |= Add_(addr, source, nTimePenalty);
				Check();
			}

			return fRet;
		}

		public bool Add(IEnumerable<NetworkAddress> vAddr, IPAddress source)
		{
			return Add(vAddr, source, TimeSpan.FromSeconds(0));
		}

		public bool Add(IEnumerable<NetworkAddress> vAddr, IPAddress source, TimeSpan nTimePenalty)
		{
			var nAdd = 0;
			lock (_cs)
			{
				Check();
				foreach (var addr in vAddr)
				{
					nAdd += Add_(addr, source, nTimePenalty) ? 1 : 0;
				}

				Check();
			}

			return nAdd > 0;
		}

		private bool Add_(NetworkAddress addr, IPAddress source, TimeSpan nTimePenalty)
		{
			if (!addr.Endpoint.Address.IsRoutable(true))
			{
				return false;
			}

			var fNew = false;
			int nId;
			var pinfo = Find(addr, out nId);
			if (pinfo != null)
			{
				// periodically update nTime
				var fCurrentlyOnline = DateTimeOffset.UtcNow - addr.Time < TimeSpan.FromSeconds(24 * 60 * 60);
				var nUpdateInterval = TimeSpan.FromSeconds(fCurrentlyOnline ? 60 * 60 : 24 * 60 * 60);
				if (addr._nTime != 0 && (pinfo.Address._nTime == 0 ||
				                         pinfo.Address.Time < addr.Time - nUpdateInterval - nTimePenalty))
				{
					pinfo.Address._nTime = (uint) Math.Max(0L, Utils.DateTimeToUnixTime(addr.Time - nTimePenalty));
				}

				// add services
				pinfo.Address.Service |= addr.Service;

				// do not update if no new information is present
				if (addr._nTime == 0 || pinfo.Address._nTime != 0 && addr.Time <= pinfo.Address.Time)
				{
					return false;
				}

				// do not update if the entry was already in the "tried" table
				if (pinfo.fInTried)
				{
					return false;
				}

				// do not update if the max reference count is reached
				if (pinfo.NRefCount == ADDRMAN_NEW_BUCKETS_PER_ADDRESS)
				{
					return false;
				}

				// stochastic test: previous nRefCount == N: 2^N times harder to increase it
				var nFactor = 1;
				for (var n = 0; n < pinfo.NRefCount; n++)
				{
					nFactor *= 2;
				}

				if (nFactor > 1 && GetRandInt(nFactor) != 0)
				{
					return false;
				}
			}
			else
			{
				pinfo = Create(addr, source, out nId);
				pinfo.Address._nTime =
					(uint) Math.Max(0, (long) Utils.DateTimeToUnixTime(pinfo.Address.Time - nTimePenalty));
				_nNew++;
				fNew = true;
			}

			var nUBucket = pinfo.GetNewBucket(_nKey, source);
			var nUBucketPos = pinfo.GetBucketPosition(_nKey, true, nUBucket);
			if (_vvNew[nUBucket, nUBucketPos] != nId)
			{
				var fInsert = _vvNew[nUBucket, nUBucketPos] == -1;
				if (!fInsert)
				{
					var infoExisting = _mapInfo[_vvNew[nUBucket, nUBucketPos]];
					if (infoExisting.IsTerrible || infoExisting.NRefCount > 1 && pinfo.NRefCount == 0)
					{
						// Overwrite the existing new table entry.
						fInsert = true;
					}
				}

				if (fInsert)
				{
					ClearNew(nUBucket, nUBucketPos);
					pinfo.NRefCount++;
					_vvNew[nUBucket, nUBucketPos] = nId;
				}
				else
				{
					if (pinfo.NRefCount == 0)
					{
						Delete(nId);
					}
				}
			}

			return fNew;
		}

		private void ClearNew(int nUBucket, int nUBucketPos)
		{
			// if there is an entry in the specified bucket, delete it.
			if (_vvNew[nUBucket, nUBucketPos] != -1)
			{
				var nIdDelete = _vvNew[nUBucket, nUBucketPos];
				var infoDelete = _mapInfo[nIdDelete];
				Assert(infoDelete.NRefCount > 0);
				infoDelete.NRefCount--;
				infoDelete.NRefCount = Math.Max(0, infoDelete.NRefCount);
				_vvNew[nUBucket, nUBucketPos] = -1;
				if (infoDelete.NRefCount == 0)
				{
					Delete(nIdDelete);
				}
			}
		}

		private void Delete(int nId)
		{
			Assert(_mapInfo.ContainsKey(nId));
			var info = _mapInfo[nId];
			Assert(!info.fInTried);
			Assert(info.NRefCount == 0);

			SwapRandom(info.NRandomPos, _vRandom.Count - 1);
			_vRandom.RemoveAt(_vRandom.Count - 1);
			_mapAddr.Remove(info.Address.Endpoint.Address);
			_mapInfo.Remove(nId);
			_nNew--;
		}

		private void SwapRandom(int nRndPos1, int nRndPos2)
		{
			if (nRndPos1 == nRndPos2)
			{
				return;
			}

			Assert(nRndPos1 < _vRandom.Count && nRndPos2 < _vRandom.Count);

			var nId1 = _vRandom[nRndPos1];
			var nId2 = _vRandom[nRndPos2];

			Assert(_mapInfo.ContainsKey(nId1));
			Assert(_mapInfo.ContainsKey(nId2));

			_mapInfo[nId1].NRandomPos = nRndPos2;
			_mapInfo[nId2].NRandomPos = nRndPos1;

			_vRandom[nRndPos1] = nId2;
			_vRandom[nRndPos2] = nId1;
		}

		private AddressInfo Create(NetworkAddress addr, IPAddress addrSource, out int pnId)
		{
			var nId = _nIdCount++;
			_mapInfo[nId] = new AddressInfo(addr, addrSource);
			_mapAddr[addr.Endpoint.Address] = nId;
			_mapInfo[nId].NRandomPos = _vRandom.Count;
			_vRandom.Add(nId);
			pnId = nId;
			return _mapInfo[nId];
		}

		private AddressInfo Find(NetworkAddress addr, out int nId)
		{
			return Find(addr.Endpoint.Address, out nId);
		}

		internal void Check()
		{
			if (!DebugMode)
			{
				return;
			}

			lock (_cs)
			{
				Assert(Check_() == 0);
			}
		}

		private int Check_()
		{
			var setTried = new List<int>();
			var mapNew = new Dictionary<int, int>();

			if (_vRandom.Count != _nTried + _nNew)
			{
				return -7;
			}

			foreach (var kv in _mapInfo)
			{
				var n = kv.Key;
				var info = kv.Value;
				if (info.fInTried)
				{
					if (info._nLastSuccess == 0)
					{
						return -1;
					}

					if (info.NRefCount != 0)
					{
						return -2;
					}

					setTried.Add(n);
				}
				else
				{
					if (info.NRefCount < 0 || info.NRefCount > ADDRMAN_NEW_BUCKETS_PER_ADDRESS)
					{
						return -3;
					}

					if (info.NRefCount == 0)
					{
						return -4;
					}

					mapNew[n] = info.NRefCount;
				}

				if (_mapAddr[info.Address.Endpoint.Address] != n)
				{
					return -5;
				}

				if (info.NRandomPos < 0 || info.NRandomPos >= _vRandom.Count || _vRandom[info.NRandomPos] != n)
				{
					return -14;
				}

				if (info._nLastTry < 0)
				{
					return -6;
				}

				if (info._nLastSuccess < 0)
				{
					return -8;
				}
			}

			if (setTried.Count != _nTried)
			{
				return -9;
			}

			if (mapNew.Count != _nNew)
			{
				return -10;
			}

			for (var n = 0; n < ADDRMAN_TRIED_BUCKET_COUNT; n++)
			{
				for (var i = 0; i < ADDRMAN_BUCKET_SIZE; i++)
				{
					if (_vvTried[n, i] != -1)
					{
						if (!setTried.Contains(_vvTried[n, i]))
						{
							return -11;
						}

						if (_mapInfo[_vvTried[n, i]].GetTriedBucket(_nKey) != n)
						{
							return -17;
						}

						if (_mapInfo[_vvTried[n, i]].GetBucketPosition(_nKey, false, n) != i)
						{
							return -18;
						}

						setTried.Remove(_vvTried[n, i]);
					}
				}
			}

			for (var n = 0; n < ADDRMAN_NEW_BUCKET_COUNT; n++)
			{
				for (var i = 0; i < ADDRMAN_BUCKET_SIZE; i++)
				{
					if (_vvNew[n, i] != -1)
					{
						if (!mapNew.ContainsKey(_vvNew[n, i]))
						{
							return -12;
						}

						if (_mapInfo[_vvNew[n, i]].GetBucketPosition(_nKey, true, n) != i)
						{
							return -19;
						}

						if (--mapNew[_vvNew[n, i]] == 0)
						{
							mapNew.Remove(_vvNew[n, i]);
						}
					}
				}
			}

			if (setTried.Count != 0)
			{
				return -13;
			}

			if (mapNew.Count != 0)
			{
				return -15;
			}

			if (_nKey == null || _nKey == uint256.Zero)
			{
				return -16;
			}

			return 0;
		}

		public void Good(NetworkAddress addr)
		{
			Good(addr, DateTimeOffset.UtcNow);
		}

		public void Good(NetworkAddress addr, DateTimeOffset nTime)
		{
			lock (_cs)
			{
				Check();
				Good_(addr, nTime);
				Check();
			}
		}

		private void Good_(NetworkAddress addr, DateTimeOffset nTime)
		{
			int nId;
			var pinfo = Find(addr, out nId);

			// if not found, bail out
			if (pinfo == null)
			{
				return;
			}

			var info = pinfo;

			// check whether we are talking about the exact same CService (including same port)
			if (!info.Match(addr))
			{
				return;
			}

			// update info
			info.LastSuccess = nTime;
			info.LastTry = nTime;
			info._nAttempts = 0;
			// nTime is not updated here, to avoid leaking information about
			// currently-connected peers.

			// if it is already in the tried set, don't do anything else
			if (info.fInTried)
			{
				return;
			}

			// find a bucket it is in now
			var nRnd = GetRandInt(ADDRMAN_NEW_BUCKET_COUNT);
			var nUBucket = -1;
			for (var n = 0; n < ADDRMAN_NEW_BUCKET_COUNT; n++)
			{
				var nB = (n + nRnd) % ADDRMAN_NEW_BUCKET_COUNT;
				var nBpos = info.GetBucketPosition(_nKey, true, nB);
				if (_vvNew[nB, nBpos] == nId)
				{
					nUBucket = nB;
					break;
				}
			}

			// if no bucket is found, something bad happened;
			// TODO: maybe re-add the node, but for now, just bail out
			if (nUBucket == -1)
			{
				return;
			}

			// move nId to the tried tables
			MakeTried(info, nId);
		}

		private void MakeTried(AddressInfo info, int nId)
		{
			// remove the entry from all new buckets
			for (var bucket = 0; bucket < ADDRMAN_NEW_BUCKET_COUNT; bucket++)
			{
				var pos = info.GetBucketPosition(_nKey, true, bucket);
				if (_vvNew[bucket, pos] == nId)
				{
					_vvNew[bucket, pos] = -1;
					info.NRefCount--;
				}
			}

			_nNew--;

			Assert(info.NRefCount == 0);

			// which tried bucket to move the entry to
			var nKBucket = info.GetTriedBucket(_nKey);
			var nKBucketPos = info.GetBucketPosition(_nKey, false, nKBucket);

			// first make space to add it (the existing tried entry there is moved to new, deleting whatever is there).
			if (_vvTried[nKBucket, nKBucketPos] != -1)
			{
				// find an item to evict
				var nIdEvict = _vvTried[nKBucket, nKBucketPos];
				Assert(_mapInfo.ContainsKey(nIdEvict));
				var infoOld = _mapInfo[nIdEvict];

				// Remove the to-be-evicted item from the tried set.
				infoOld.fInTried = false;
				_vvTried[nKBucket, nKBucketPos] = -1;
				_nTried--;

				// find which new bucket it belongs to
				var nUBucket = infoOld.GetNewBucket(_nKey);
				var nUBucketPos = infoOld.GetBucketPosition(_nKey, true, nUBucket);
				ClearNew(nUBucket, nUBucketPos);
				Assert(_vvNew[nUBucket, nUBucketPos] == -1);

				// Enter it into the new set again.
				infoOld.NRefCount = 1;
				_vvNew[nUBucket, nUBucketPos] = nIdEvict;
				_nNew++;
			}

			Assert(_vvTried[nKBucket, nKBucketPos] == -1);

			_vvTried[nKBucket, nKBucketPos] = nId;
			_nTried++;
			info.fInTried = true;
		}

		private static void Assert(bool value)
		{
			if (!value)
			{
				throw new InvalidOperationException(
					"Bug in AddressManager, should never happen, contact BitcoinNet developers if you see this exception.");
			}
		}

		//! Mark an entry as connection attempted to.
		public void Attempt(NetworkAddress addr)
		{
			Attempt(addr, DateTimeOffset.UtcNow);
		}

		//! Mark an entry as connection attempted to.
		public void Attempt(NetworkAddress addr, DateTimeOffset nTime)
		{
			lock (_cs)
			{
				Check();
				Attempt_(addr, nTime);
				Check();
			}
		}

		private void Attempt_(NetworkAddress addr, DateTimeOffset nTime)
		{
			var pinfo = Find(addr.Endpoint.Address);

			// if not found, bail out
			if (pinfo == null)
			{
				return;
			}

			var info = pinfo;

			// check whether we are talking about the exact same CService (including same port)
			if (!info.Match(addr))
			{
				return;
			}

			// update info
			info.LastTry = nTime;
			info._nAttempts++;
		}

		//! Mark an entry as currently-connected-to.
		public void Connected(NetworkAddress addr)
		{
			Connected(addr, DateTimeOffset.UtcNow);
		}

		//! Mark an entry as currently-connected-to.
		public void Connected(NetworkAddress addr, DateTimeOffset nTime)
		{
			lock (_cs)
			{
				Check();
				Connected_(addr, nTime);
				Check();
			}
		}

		private void Connected_(NetworkAddress addr, DateTimeOffset nTime)
		{
			var pinfo = Find(addr, out var unused);

			// if not found, bail out
			if (pinfo == null)
			{
				return;
			}

			var info = pinfo;

			// check whether we are talking about the exact same CService (including same port)
			if (!info.Match(addr))
			{
				return;
			}

			// update info
			var nUpdateInterval = TimeSpan.FromSeconds(20 * 60);
			if (nTime - info.nTime > nUpdateInterval)
			{
				info.nTime = nTime;
			}
		}

		/// <summary>
		///     Choose an address to connect to.
		/// </summary>
		/// <returns>The network address of a peer, or null if none are found</returns>
		public NetworkAddress Select()
		{
			AddressInfo addrRet = null;
			lock (_cs)
			{
				Check();
				addrRet = Select_();
				Check();
			}

			return addrRet?.Address;
		}

		private AddressInfo Select_()
		{
			if (_vRandom.Count == 0)
			{
				return null;
			}

			// Use a 50% chance for choosing between tried and new table entries.
			if (_nTried > 0 && (_nNew == 0 || GetRandInt(2) == 0))
			{
				// use a tried node
				var fChanceFactor = 1.0;
				while (true)
				{
					var nKBucket = GetRandInt(ADDRMAN_TRIED_BUCKET_COUNT);
					var nKBucketPos = GetRandInt(ADDRMAN_BUCKET_SIZE);
					if (_vvTried[nKBucket, nKBucketPos] == -1)
					{
						continue;
					}

					var nId = _vvTried[nKBucket, nKBucketPos];
					Assert(_mapInfo.ContainsKey(nId));
					var info = _mapInfo[nId];
					if (GetRandInt(1 << 30) < fChanceFactor * info.Chance * (1 << 30))
					{
						return info;
					}

					fChanceFactor *= 1.2;
				}
			}
			else
			{
				// use a new node
				var fChanceFactor = 1.0;
				while (true)
				{
					var nUBucket = GetRandInt(ADDRMAN_NEW_BUCKET_COUNT);
					var nUBucketPos = GetRandInt(ADDRMAN_BUCKET_SIZE);
					if (_vvNew[nUBucket, nUBucketPos] == -1)
					{
						continue;
					}

					var nId = _vvNew[nUBucket, nUBucketPos];
					Assert(_mapInfo.ContainsKey(nId));
					var info = _mapInfo[nId];
					if (GetRandInt(1 << 30) < fChanceFactor * info.Chance * (1 << 30))
					{
						return info;
					}

					fChanceFactor *= 1.2;
				}
			}
		}

		private static int GetRandInt(int max)
		{
			return (int) (RandomUtils.GetUInt32() % (uint) max);
		}

		/// <summary>
		///     Return a bunch of addresses, selected at random.
		/// </summary>
		/// <returns></returns>
		public NetworkAddress[] GetAddr()
		{
			NetworkAddress[] result = null;
			lock (_cs)
			{
				Check();
				result = GetAddr_().ToArray();
				Check();
			}

			return result;
		}

		private IEnumerable<NetworkAddress> GetAddr_()
		{
			var vAddr = new List<NetworkAddress>();
			var nNodes = ADDRMAN_GETADDR_MAX_PCT * _vRandom.Count / 100;
			if (nNodes > ADDRMAN_GETADDR_MAX)
			{
				nNodes = ADDRMAN_GETADDR_MAX;
			}

			// gather a list of random nodes, skipping those of low quality
			for (var n = 0; n < _vRandom.Count; n++)
			{
				if (vAddr.Count >= nNodes)
				{
					break;
				}

				var nRndPos = GetRandInt(_vRandom.Count - n) + n;
				SwapRandom(n, nRndPos);
				Assert(_mapInfo.ContainsKey(_vRandom[n]));

				var ai = _mapInfo[_vRandom[n]];
				if (!ai.IsTerrible)
				{
					vAddr.Add(ai.Address);
				}
			}

			return vAddr;
		}

		internal void DiscoverPeers(Network network, NodeConnectionParameters parameters, int peerToFind)
		{
			var traceCorrelation = new TraceCorrelation(NodeServerTrace.Trace, "Discovering nodes");
			var found = 0;

			using (traceCorrelation.Open())
			{
				while (found < peerToFind)
				{
					parameters.ConnectCancellation.ThrowIfCancellationRequested();
					NodeServerTrace.PeerTableRemainingPeerToGet(-found + peerToFind);
					var peers = new List<NetworkAddress>();
					peers.AddRange(GetAddr());
					if (peers.Count == 0)
					{
						PopulateTableWithDNSNodes(network, peers);
						PopulateTableWithHardNodes(network, peers);
						peers = new List<NetworkAddress>(peers.OrderBy(a => RandomUtils.GetInt32()));
						if (peers.Count == 0)
						{
							return;
						}
					}


					var peerTableFull = new CancellationTokenSource();
					var loopCancel = CancellationTokenSource
						.CreateLinkedTokenSource(peerTableFull.Token, parameters.ConnectCancellation).Token;
					try
					{
						Parallel.ForEach(peers, new ParallelOptions
						{
							MaxDegreeOfParallelism = 5,
							CancellationToken = loopCancel
						}, p =>
						{
							var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
							var cancelConnection =
								CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, loopCancel);
							Node n = null;
							try
							{
								var param2 = parameters.Clone();
								param2.ConnectCancellation = cancelConnection.Token;
								var addrman = param2.TemplateBehaviors.Find<AddressManagerBehavior>();
								param2.TemplateBehaviors.Clear();
								param2.TemplateBehaviors.Add(addrman);
								n = Node.Connect(network, p.Endpoint, param2);
								n.VersionHandshake(cancelConnection.Token);
								n.MessageReceived += (s, a) =>
								{
									var addr = a.Message.Payload as AddrPayload;
									if (addr != null)
									{
										Interlocked.Add(ref found, addr.Addresses.Length);
										if (found >= peerToFind)
										{
											peerTableFull.Cancel();
										}
									}
								};
								n.SendMessageAsync(new GetAddrPayload());
								loopCancel.WaitHandle.WaitOne(2000);
							}
							catch
							{
							}
							finally
							{
								if (n != null)
								{
									n.DisconnectAsync();
								}
							}

							if (found >= peerToFind)
							{
								peerTableFull.Cancel();
							}
							else
							{
								NodeServerTrace.Information("Need " + (-found + peerToFind) + " more peers");
							}
						});
					}
					catch (OperationCanceledException)
					{
						if (parameters.ConnectCancellation.IsCancellationRequested)
						{
							throw;
						}
					}
				}
			}
		}

		private static void PopulateTableWithDNSNodes(Network network, List<NetworkAddress> peers)
		{
			peers.AddRange(network.DNSSeeds
				.SelectMany(d =>
				{
					try
					{
						return d.GetAddressNodes();
					}
					catch (Exception)
					{
						return new IPAddress[0];
					}
				})
				.Select(d => new NetworkAddress(d, network.DefaultPort))
				.ToArray());
		}

		private static void PopulateTableWithHardNodes(Network network, List<NetworkAddress> peers)
		{
			peers.AddRange(network.SeedNodes);
		}

		internal class AddressInfo : IBitcoinSerializable
		{
			private NetworkAddress _address;
			internal int _nAttempts;
			internal long _nLastSuccess;
			internal long _nLastTry;
			private byte[] _source = new byte[16];
			public bool fInTried;
			public int NRandomPos = -1;
			public int NRefCount;

			public AddressInfo()
			{
			}

			public AddressInfo(NetworkAddress addr, IPAddress addrSource)
			{
				Address = addr;
				Source = addrSource;
			}

			public DateTimeOffset LastSuccess
			{
				get => Utils.UnixTimeToDateTime((uint) _nLastSuccess);
				set => _nLastSuccess = Utils.DateTimeToUnixTime(value);
			}

			public IPAddress Source
			{
				get => new IPAddress(_source);
				set
				{
					var ipBytes = value.GetAddressBytes();
					if (ipBytes.Length == 16)
					{
						_source = ipBytes;
					}
					else if (ipBytes.Length == 4)
					{
						//Convert to ipv4 mapped to ipv6
						//In these addresses, the first 80 bits are zero, the next 16 bits are one, and the remaining 32 bits are the IPv4 address
						_source = new byte[16];
						Array.Copy(ipBytes, 0, _source, 12, 4);
						Array.Copy(new byte[] {0xFF, 0xFF}, 0, _source, 10, 2);
					}
					else
					{
						throw new NotSupportedException("Invalid IP address type");
					}
				}
			}

			internal DateTimeOffset nTime
			{
				get => Address.Time;
				set => Address.Time = value;
			}

			public bool IsTerrible => _IsTerrible(DateTimeOffset.UtcNow);

			internal DateTimeOffset LastTry
			{
				get => Utils.UnixTimeToDateTime((uint) _nLastSuccess);
				set => _nLastTry = Utils.DateTimeToUnixTime(value);
			}

			public NetworkAddress Address
			{
				get => _address;
				set => _address = value;
			}

			internal double Chance => GetChance(DateTimeOffset.UtcNow);
			// IBitcoinSerializable Members

			public void ReadWrite(BitcoinStream stream)
			{
				stream.ReadWrite(ref _address);
				stream.ReadWrite(ref _source);
				stream.ReadWrite(ref _nLastSuccess);
				stream.ReadWrite(ref _nAttempts);
			}

			internal int GetNewBucket(uint256 nKey)
			{
				return GetNewBucket(nKey, Source);
			}

			internal int GetNewBucket(uint256 nKey, IPAddress src)
			{
				var vchSourceGroupKey = src.GetGroup();
				var hash1 = Cheap(Hashes.Hash256(
					nKey.ToBytes(true)
						.Concat(Address.Endpoint.Address.GetGroup())
						.Concat(vchSourceGroupKey)
						.ToArray()));

				var hash2 = Cheap(Hashes.Hash256(
					nKey.ToBytes(true)
						.Concat(vchSourceGroupKey)
						.Concat(Utils.ToBytes(hash1 % ADDRMAN_NEW_BUCKETS_PER_SOURCE_GROUP, true))
						.ToArray()));
				return (int) (hash2 % ADDRMAN_NEW_BUCKET_COUNT);
			}

			private ulong Cheap(uint256 v)
			{
				return Utils.ToUInt64(v.ToBytes(), true);
			}

			internal int GetBucketPosition(uint256 nKey, bool fNew, int nBucket)
			{
				var hash1 = Cheap(
					Hashes.Hash256(
						nKey.ToBytes()
							.Concat(new[] {fNew ? (byte) 'N' : (byte) 'K'})
							.Concat(Utils.ToBytes((uint) nBucket, false))
							.Concat(Address.GetKey())
							.ToArray()));
				return (int) (hash1 % ADDRMAN_BUCKET_SIZE);
			}

			internal int GetTriedBucket(uint256 nKey)
			{
				var hash1 = Cheap(Hashes.Hash256(nKey.ToBytes().Concat(Address.GetKey()).ToArray()));
				var hash2 = Cheap(Hashes.Hash256(nKey.ToBytes().Concat(Address.Endpoint.Address.GetGroup())
					.Concat(Utils.ToBytes(hash1 % ADDRMAN_TRIED_BUCKETS_PER_GROUP, true)).ToArray()));
				return (int) (hash2 % ADDRMAN_TRIED_BUCKET_COUNT);
			}

			internal bool _IsTerrible(DateTimeOffset now)
			{
				if (_nLastTry != 0 && LastTry >= now - TimeSpan.FromSeconds(60)
				) // never remove things tried in the last minute
				{
					return false;
				}

				if (Address.Time > now + TimeSpan.FromSeconds(10 * 60)) // came in a flying DeLorean
				{
					return true;
				}

				if (Address._nTime == 0 ||
				    now - Address.Time > TimeSpan.FromSeconds(ADDRMAN_HORIZON_DAYS * 24 * 60 * 60)
				) // not seen in recent history
				{
					return true;
				}

				if (_nLastSuccess == 0 && _nAttempts >= ADDRMAN_RETRIES) // tried N times and never a success
				{
					return true;
				}

				if (now - LastSuccess > TimeSpan.FromSeconds(ADDRMAN_MIN_FAIL_DAYS * 24 * 60 * 60) &&
				    _nAttempts >= ADDRMAN_MAX_FAILURES) // N successive failures in the last week
				{
					return true;
				}

				return false;
			}

			internal bool Match(NetworkAddress addr)
			{
				return
					Address.Endpoint.Address.Equals(addr.Endpoint.Address) &&
					Address.Endpoint.Port == addr.Endpoint.Port;
			}

			//! Calculate the relative chance this entry should be given when selecting nodes to connect to
			internal double GetChance(DateTimeOffset nNow)
			{
				var fChance = 1.0;

				var nSinceLastSeen = nNow - nTime;
				var nSinceLastTry = nNow - LastTry;

				if (nSinceLastSeen < TimeSpan.Zero)
				{
					nSinceLastSeen = TimeSpan.Zero;
				}

				if (nSinceLastTry < TimeSpan.Zero)
				{
					nSinceLastTry = TimeSpan.Zero;
				}

				// deprioritize very recent attempts away
				if (nSinceLastTry < TimeSpan.FromSeconds(60 * 10))
				{
					fChance *= 0.01;
				}

				// deprioritize 66% after each failed attempt, but at most 1/28th to avoid the search taking forever or overly penalizing outages.
				fChance *= Math.Pow(0.66, Math.Min(_nAttempts, 8));

				return fChance;
			}
		}
	}
}