﻿using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BitcoinNet.BouncyCastle.Crypto.Digests;

namespace BitcoinNet.Crypto
{
	public static class Hashes
	{
		// Hash256

		public static uint256 Hash256(byte[] data)
		{
			return Hash256(data, 0, data.Length);
		}

		public static uint256 Hash256(byte[] data, int count)
		{
			return Hash256(data, 0, count);
		}

		public static uint256 Hash256(byte[] data, int offset, int count)
		{
			return new uint256(Hash256RawBytes(data, offset, count));
		}

		public static byte[] Hash256RawBytes(byte[] data, int offset, int count)
		{
			using (var sha = new SHA256Managed())
			{
				var h = sha.ComputeHash(data, offset, count);
				return sha.ComputeHash(h, 0, h.Length);
			}
		}

		// Hash160

		public static uint160 Hash160(byte[] data)
		{
			return Hash160(data, 0, data.Length);
		}

		public static uint160 Hash160(byte[] data, int count)
		{
			return Hash160(data, 0, count);
		}

		public static uint160 Hash160(byte[] data, int offset, int count)
		{
			return new uint160(RIPEMD160(SHA256(data, offset, count)));
		}

		// RIPEMD160

		private static byte[] RIPEMD160(byte[] data)
		{
			return RIPEMD160(data, 0, data.Length);
		}

		public static byte[] RIPEMD160(byte[] data, int count)
		{
			return RIPEMD160(data, 0, count);
		}

		public static byte[] RIPEMD160(byte[] data, int offset, int count)
		{
			var ripemd = new RipeMD160Digest();
			ripemd.BlockUpdate(data, offset, count);
			var rv = new byte[20];
			ripemd.DoFinal(rv, 0);
			return rv;
		}

		public static ulong SipHash(ulong k0, ulong k1, uint256 val)
		{
			return SipHasher.SipHashUint256(k0, k1, val);
		}

		public static byte[] SHA1(byte[] data, int offset, int count)
		{
			var sha1 = new Sha1Digest();
			sha1.BlockUpdate(data, offset, count);
			var rv = new byte[20];
			sha1.DoFinal(rv, 0);
			return rv;
		}

		public static byte[] SHA256(byte[] data)
		{
			return SHA256(data, 0, data.Length);
		}

		public static byte[] SHA256(byte[] data, int offset, int count)
		{
			using (var sha = new SHA256Managed())
			{
				return sha.ComputeHash(data, offset, count);
			}
		}


		private static uint Rotl32(uint x, byte r)
		{
			return (x << r) | (x >> (32 - r));
		}

		private static uint FMix(uint h)
		{
			h ^= h >> 16;
			h *= 0x85ebca6b;
			h ^= h >> 13;
			h *= 0xc2b2ae35;
			h ^= h >> 16;
			return h;
		}

		public static uint MurmurHash3(uint nHashSeed, byte[] vDataToHash)
		{
			// The following is MurmurHash3 (x86_32), see https://gist.github.com/automatonic/3725443
			const uint c1 = 0xcc9e2d51;
			const uint c2 = 0x1b873593;

			var h1 = nHashSeed;
			uint k1 = 0;
			uint streamLength = 0;

			using (var reader = new BinaryReader(new MemoryStream(vDataToHash)))
			{
				var chunk = reader.ReadBytes(4);
				while (chunk.Length > 0)
				{
					streamLength += (uint) chunk.Length;
					switch (chunk.Length)
					{
						case 4:
							/* Get four bytes from the input into an uint */
							k1 = (uint)
								(chunk[0]
								 | (chunk[1] << 8)
								 | (chunk[2] << 16)
								 | (chunk[3] << 24));

							/* bitmagic hash */
							k1 *= c1;
							k1 = Rotl32(k1, 15);
							k1 *= c2;

							h1 ^= k1;
							h1 = Rotl32(h1, 13);
							h1 = h1 * 5 + 0xe6546b64;
							break;
						case 3:
							k1 = (uint)
								(chunk[0]
								 | (chunk[1] << 8)
								 | (chunk[2] << 16));
							k1 *= c1;
							k1 = Rotl32(k1, 15);
							k1 *= c2;
							h1 ^= k1;
							break;
						case 2:
							k1 = (uint)
								(chunk[0]
								 | (chunk[1] << 8));
							k1 *= c1;
							k1 = Rotl32(k1, 15);
							k1 *= c2;
							h1 ^= k1;
							break;
						case 1:
							k1 = chunk[0];
							k1 *= c1;
							k1 = Rotl32(k1, 15);
							k1 *= c2;
							h1 ^= k1;
							break;
					}

					chunk = reader.ReadBytes(4);
				}
			}

			// finalization, magic chants to wrap it all up
			h1 ^= streamLength;
			h1 = FMix(h1);

			return h1;
		}

		public static byte[] HMACSHA512(byte[] key, byte[] data)
		{
			return new HMACSHA512(key).ComputeHash(data);
		}

		public static byte[] BIP32Hash(byte[] chainCode, uint nChild, byte header, byte[] data)
		{
			var num = new byte[4];
			num[0] = (byte) ((nChild >> 24) & 0xFF);
			num[1] = (byte) ((nChild >> 16) & 0xFF);
			num[2] = (byte) ((nChild >> 8) & 0xFF);
			num[3] = (byte) ((nChild >> 0) & 0xFF);

			return HMACSHA512(chainCode,
				new[] {header}
					.Concat(data)
					.Concat(num).ToArray());
		}

		internal class SipHasher
		{
			private ulong _count;
			private ulong _tmp;
			private ulong _v0;
			private ulong _v1;
			private ulong _v2;
			private ulong _v3;

			public SipHasher(ulong k0, ulong k1)
			{
				_v0 = 0x736f6d6570736575UL ^ k0;
				_v1 = 0x646f72616e646f6dUL ^ k1;
				_v2 = 0x6c7967656e657261UL ^ k0;
				_v3 = 0x7465646279746573UL ^ k1;
				_count = 0;
				_tmp = 0;
			}

			public SipHasher Write(ulong data)
			{
				ulong v0 = _v0, v1 = _v1, v2 = _v2, v3 = _v3;
				v3 ^= data;
				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);
				v0 ^= data;

				_v0 = v0;
				_v1 = v1;
				_v2 = v2;
				_v3 = v3;

				_count += 8;
				return this;
			}

			public SipHasher Write(byte[] data)
			{
				ulong v0 = _v0, v1 = _v1, v2 = _v2, v3 = _v3;
				var size = data.Length;
				var t = _tmp;
				var c = _count;
				var offset = 0;

				while (size-- != 0)
				{
					t |= (ulong) data[offset++] << (int) (8 * (c % 8));
					c++;
					if ((c & 7) == 0)
					{
						v3 ^= t;
						//SIPROUND(ref v0, ref v1, ref v2, ref v3);
						v0 += v1;
						v2 += v3;
						v1 = (v1 << 13) | (v1 >> 51);
						v3 = (v3 << 16) | (v3 >> 48);
						v1 ^= v0;
						v3 ^= v2;
						v0 = (v0 << 32) | (v0 >> 32);
						v2 += v1;
						v0 += v3;
						v1 = (v1 << 17) | (v1 >> 47);
						v3 = (v3 << 21) | (v3 >> 43);
						v1 ^= v2;
						v3 ^= v0;
						v2 = (v2 << 32) | (v2 >> 32);

						//SIPROUND(ref v0, ref v1, ref v2, ref v3);
						v0 += v1;
						v2 += v3;
						v1 = (v1 << 13) | (v1 >> 51);
						v3 = (v3 << 16) | (v3 >> 48);
						v1 ^= v0;
						v3 ^= v2;
						v0 = (v0 << 32) | (v0 >> 32);
						v2 += v1;
						v0 += v3;
						v1 = (v1 << 17) | (v1 >> 47);
						v3 = (v3 << 21) | (v3 >> 43);
						v1 ^= v2;
						v3 ^= v0;
						v2 = (v2 << 32) | (v2 >> 32);
						v0 ^= t;
						t = 0;
					}
				}

				_v0 = v0;
				_v1 = v1;
				_v2 = v2;
				_v3 = v3;
				_count = c;
				_tmp = t;

				return this;
			}

			public ulong Finalize()
			{
				ulong v0 = _v0, v1 = _v1, v2 = _v2, v3 = _v3;

				var t = _tmp | (_count << 56);

				v3 ^= t;
				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				v0 ^= t;
				v2 ^= 0xFF;
				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				return v0 ^ v1 ^ v2 ^ v3;
			}

			public static ulong SipHashUint256(ulong k0, ulong k1, uint256 val)
			{
				/* Specialized implementation for efficiency */
				var d = GetULong(val, 0);

				var v0 = 0x736f6d6570736575UL ^ k0;
				var v1 = 0x646f72616e646f6dUL ^ k1;
				var v2 = 0x6c7967656e657261UL ^ k0;
				var v3 = 0x7465646279746573UL ^ k1 ^ d;

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				v0 ^= d;
				d = GetULong(val, 1);
				v3 ^= d;

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				v0 ^= d;
				d = GetULong(val, 2);
				v3 ^= d;

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				v0 ^= d;
				d = GetULong(val, 3);
				v3 ^= d;

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				v0 ^= d;
				v3 ^= (ulong) 4 << 59;

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				v0 ^= (ulong) 4 << 59;
				v2 ^= 0xFF;

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				//SIPROUND(ref v0, ref v1, ref v2, ref v3);
				v0 += v1;
				v2 += v3;
				v1 = (v1 << 13) | (v1 >> 51);
				v3 = (v3 << 16) | (v3 >> 48);
				v1 ^= v0;
				v3 ^= v2;
				v0 = (v0 << 32) | (v0 >> 32);
				v2 += v1;
				v0 += v3;
				v1 = (v1 << 17) | (v1 >> 47);
				v3 = (v3 << 21) | (v3 >> 43);
				v1 ^= v2;
				v3 ^= v0;
				v2 = (v2 << 32) | (v2 >> 32);

				return v0 ^ v1 ^ v2 ^ v3;
			}

			internal static ulong GetULong(uint256 val, int position)
			{
				switch (position)
				{
					case 0:
						return val.pn0 + ((ulong) val.pn1 << 32);
					case 1:
						return val.pn2 + ((ulong) val.pn3 << 32);
					case 2:
						return val.pn4 + ((ulong) val.pn5 << 32);
					case 3:
						return val.pn6 + ((ulong) val.pn7 << 32);
					default:
						throw new ArgumentOutOfRangeException(nameof(position), "Position should be less than 4");
				}
			}
		}
	}
}