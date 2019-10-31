using System;
using BitcoinNet.BouncyCastle.Math;
using BitcoinNet.Crypto;
using BitcoinNet.DataEncoders;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	public class TransactionSignature
	{
		private string _id;

		public TransactionSignature(ECDSASignature signature, SigHash sigHash)
		{
			if (sigHash == SigHash.Undefined)
			{
				throw new ArgumentException("sigHash should not be Undefined");
			}

			SigHash = sigHash;
			Signature = signature;
		}

		public TransactionSignature(ECDSASignature signature)
			: this(signature, SigHash.All)
		{
		}

		public TransactionSignature(byte[] sigSigHash)
		{
			Signature = ECDSASignature.FromDER(sigSigHash);
			SigHash = (SigHash) sigSigHash[sigSigHash.Length - 1];
		}

		public TransactionSignature(byte[] sig, SigHash sigHash)
		{
			Signature = ECDSASignature.FromDER(sig);
			SigHash = sigHash;
		}

		public static TransactionSignature Empty { get; } =
			new TransactionSignature(new ECDSASignature(BigInteger.ValueOf(0), BigInteger.ValueOf(0)), SigHash.All);

		public ECDSASignature Signature { get; }

		public SigHash SigHash { get; }

		private string Id
		{
			get
			{
				if (_id == null)
				{
					_id = Encoders.Hex.EncodeData(ToBytes());
				}

				return _id;
			}
		}

		public bool IsLowS => Signature.IsLowS;

		/// <summary>
		///     Check if valid transaction signature
		/// </summary>
		/// <param name="sig">Signature in bytes</param>
		/// <param name="scriptVerify">Verification rules</param>
		/// <returns>True if valid</returns>
		public static bool IsValid(byte[] sig, ScriptVerify scriptVerify = ScriptVerify.DerSig | ScriptVerify.StrictEnc)
		{
			return IsValid(sig, scriptVerify, out _);
		}


		/// <summary>
		///     Check if valid transaction signature
		/// </summary>
		/// <param name="sig">The signature</param>
		/// <param name="scriptVerify">Verification rules</param>
		/// <param name="error">Error</param>
		/// <returns>True if valid</returns>
		public static bool IsValid(byte[] sig, ScriptVerify scriptVerify, out ScriptError error)
		{
			if (sig == null)
			{
				throw new ArgumentNullException(nameof(sig));
			}

			if (sig.Length == 0)
			{
				error = ScriptError.SigDer;
				return false;
			}

			error = ScriptError.OK;
			var ctx = new ScriptEvaluationContext
			{
				ScriptVerify = scriptVerify
			};
			if (!ctx.CheckSignatureEncoding(sig))
			{
				error = ctx.Error;
				return false;
			}

			return true;
		}

		public byte[] ToBytes()
		{
			var sig = Signature.ToDER();
			var result = new byte[sig.Length + 1];
			Array.Copy(sig, 0, result, 0, sig.Length);
			result[result.Length - 1] = (byte) SigHash;
			return result;
		}

		public static bool ValidLength(int length)
		{
			return 67 <= length && length <= 80 || length == 9; //9 = Empty signature
		}

		public bool Check(PubKey pubKey, Script scriptPubKey, IndexedTxIn txIn,
			ScriptVerify verify = ScriptVerify.Standard)
		{
			return Check(pubKey, scriptPubKey, txIn.Transaction, txIn.Index, verify);
		}

		public bool Check(PubKey pubKey, Script scriptPubKey, Transaction tx, uint nIndex,
			ScriptVerify verify = ScriptVerify.Standard)
		{
			return new ScriptEvaluationContext
			{
				ScriptVerify = verify,
				SigHash = SigHash
			}.CheckSig(this, pubKey, scriptPubKey, tx, nIndex);
		}

		public override bool Equals(object obj)
		{
			var item = obj as TransactionSignature;
			if (item == null)
			{
				return false;
			}

			return Id.Equals(item.Id);
		}

		public static bool operator ==(TransactionSignature a, TransactionSignature b)
		{
			if (ReferenceEquals(a, b))
			{
				return true;
			}

			if ((object) a == null || (object) b == null)
			{
				return false;
			}

			return a.Id == b.Id;
		}

		public static bool operator !=(TransactionSignature a, TransactionSignature b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		public override string ToString()
		{
			return Encoders.Hex.EncodeData(ToBytes());
		}


		/// <summary>
		///     Enforce LowS on the signature
		/// </summary>
		public TransactionSignature MakeCanonical()
		{
			if (IsLowS)
			{
				return this;
			}

			return new TransactionSignature(Signature.MakeCanonical(), SigHash);
		}
	}
}