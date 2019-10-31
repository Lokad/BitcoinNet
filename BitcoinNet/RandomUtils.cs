using System;
using System.Security.Cryptography;

namespace BitcoinNet
{
	public class RandomUtils
	{
		static RandomUtils()
		{
			//Thread safe http://msdn.microsoft.com/en-us/library/system.security.cryptography.rngcryptoserviceprovider(v=vs.110).aspx
			Random = RandomNumberGenerator.Create();
		}

		public static RandomNumberGenerator Random { get; set; }

		public static byte[] GetBytes(int length)
		{
			var data = new byte[length];
			if (Random == null)
			{
				throw new InvalidOperationException(
					"You must set the RNG (RandomUtils.Random) before generating random numbers");
			}

			Random.GetBytes(data);
			return data;
		}

		public static uint GetUInt32()
		{
			return BitConverter.ToUInt32(GetBytes(sizeof(uint)), 0);
		}

		public static int GetInt32()
		{
			return BitConverter.ToInt32(GetBytes(sizeof(int)), 0);
		}

		public static ulong GetUInt64()
		{
			return BitConverter.ToUInt64(GetBytes(sizeof(ulong)), 0);
		}

		public static long GetInt64()
		{
			return BitConverter.ToInt64(GetBytes(sizeof(long)), 0);
		}

		public static void GetBytes(byte[] output)
		{
			if (Random == null)
			{
				throw new InvalidOperationException(
					"You must set the RNG (RandomUtils.Random) before generating random numbers");
			}

			Random.GetBytes(output);
		}
	}
}