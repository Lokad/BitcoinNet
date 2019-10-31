using System;
using System.IO;
using BitcoinNet.BouncyCastle.Asn1;
using BitcoinNet.BouncyCastle.Math;

namespace BitcoinNet.Crypto
{
	public class ECDSASignature
	{
		private const string InvalidDERSignature = "Invalid DER signature";

		public ECDSASignature(BigInteger r, BigInteger s)
		{
			R = r;
			S = s;
		}

		public ECDSASignature(BigInteger[] rs)
		{
			R = rs[0];
			S = rs[1];
		}

		public ECDSASignature(byte[] derSig)
		{
			try
			{
				var decoder = new Asn1InputStream(derSig);
				var seq = decoder.ReadObject() as DerSequence;
				if (seq == null || seq.Count != 2)
				{
					throw new FormatException(InvalidDERSignature);
				}

				R = ((DerInteger) seq[0]).Value;
				S = ((DerInteger) seq[1]).Value;
			}
			catch (Exception ex)
			{
				throw new FormatException(InvalidDERSignature, ex);
			}
		}

		public ECDSASignature(Stream derSig)
		{
			try
			{
				var decoder = new Asn1InputStream(derSig);
				var seq = decoder.ReadObject() as DerSequence;
				if (seq == null || seq.Count != 2)
				{
					throw new FormatException(InvalidDERSignature);
				}

				R = ((DerInteger) seq[0]).Value;
				S = ((DerInteger) seq[1]).Value;
			}
			catch (Exception ex)
			{
				throw new FormatException(InvalidDERSignature, ex);
			}
		}

		public BigInteger R { get; }

		public BigInteger S { get; }

		public bool IsLowS => S.CompareTo(ECKey.HalfCurveOrder) <= 0;

		/**
		* What we get back from the signer are the two components of a signature, r and s. To get a flat byte stream
		* of the type used by Bitcoin we have to encode them using DER encoding, which is just a way to pack the two
		* components into a structure.
		*/
		public byte[] ToDER()
		{
			// Usually 70-72 bytes.
			using (var bos = new MemoryStream(72))
			{
				var seq = new DerSequenceGenerator(bos);
				seq.AddObject(new DerInteger(R));
				seq.AddObject(new DerInteger(S));
				seq.Close();
				return bos.ToArray();
			}
		}

		public static ECDSASignature FromDER(byte[] sig)
		{
			return new ECDSASignature(sig);
		}

		/// <summary>
		///     Enforce LowS on the signature
		/// </summary>
		public ECDSASignature MakeCanonical()
		{
			if (!IsLowS)
			{
				return new ECDSASignature(R, ECKey.CurveOrder.Subtract(S));
			}

			return this;
		}


		public static bool IsValidDER(byte[] bytes)
		{
			try
			{
				FromDER(bytes);
				return true;
			}
			catch (FormatException)
			{
				return false;
			}
			catch (Exception ex)
			{
				Utils.error("Unexpected exception in ECDSASignature.IsValidDER " + ex.Message);
				return false;
			}
		}
	}
}