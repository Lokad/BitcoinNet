using System;
using System.Linq;
using BitcoinNet.BouncyCastle.Crypto;
using BitcoinNet.BouncyCastle.Crypto.Digests;
using BitcoinNet.BouncyCastle.Crypto.Parameters;
using BitcoinNet.BouncyCastle.Crypto.Signers;

namespace BitcoinNet.Crypto
{
	internal static class DeterministicDSAExtensions
	{
		public static void Update(this IMac hmac, byte[] input)
		{
			hmac.BlockUpdate(input, 0, input.Length);
		}

		public static byte[] DoFinal(this IMac hmac)
		{
			var result = new byte[hmac.GetMacSize()];
			hmac.DoFinal(result, 0);
			return result;
		}

		public static void Update(this IDigest digest, byte[] input)
		{
			digest.BlockUpdate(input, 0, input.Length);
		}

		public static void Update(this IDigest digest, byte[] input, int offset, int length)
		{
			digest.BlockUpdate(input, offset, length);
		}

		public static byte[] Digest(this IDigest digest)
		{
			var result = new byte[digest.GetDigestSize()];
			digest.DoFinal(result, 0);
			return result;
		}
	}

	internal class DeterministicECDSA : ECDsaSigner
	{
		private readonly IDigest _digest;
		private byte[] _buffer = new byte[0];

		public DeterministicECDSA()
			: base(new HMacDsaKCalculator(new Sha256Digest()))

		{
			_digest = new Sha256Digest();
		}

		public DeterministicECDSA(Func<IDigest> digest)
			: base(new HMacDsaKCalculator(digest()))
		{
			_digest = digest();
		}


		public void SetPrivateKey(ECPrivateKeyParameters ecKey)
		{
			base.Init(true, ecKey);
		}

		public void Update(byte[] buf)
		{
			_buffer = _buffer.Concat(buf).ToArray();
		}

		public byte[] Sign()
		{
			var hash = new byte[_digest.GetDigestSize()];
			_digest.BlockUpdate(_buffer, 0, _buffer.Length);
			_digest.DoFinal(hash, 0);
			_digest.Reset();
			return SignHash(hash);
		}

		public byte[] SignHash(byte[] hash)
		{
			return new ECDSASignature(GenerateSignature(hash)).ToDER();
		}
	}
}