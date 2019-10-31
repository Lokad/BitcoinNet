using System;
using System.Collections.Generic;
using System.Linq;
using BitcoinNet.Crypto;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	//TODO : Is*Conform can be used to parses the script

	public enum TxOutType
	{
		TX_NONSTANDARD,

		// 'standard' transaction types:
		TX_PUBKEY,
		TX_PUBKEYHASH,
		TX_SCRIPTHASH,
		TX_MULTISIG,
		TX_NULL_DATA
	}

	public class TxNullDataTemplate : ScriptTemplate
	{
		public const int MAX_OP_RETURN_RELAY = 83; //! bytes (+1 for OP_RETURN, +2 for the pushdata opcodes)

		public TxNullDataTemplate(int maxScriptSize)
		{
			MaxScriptSizeLimit = maxScriptSize;
		}

		public static TxNullDataTemplate Instance { get; } = new TxNullDataTemplate(MAX_OP_RETURN_RELAY);

		public int MaxScriptSizeLimit { get; }

		public override TxOutType Type => TxOutType.TX_NULL_DATA;

		protected override bool FastCheckScriptPubKey(Script scriptPubKey, out bool needMoreCheck)
		{
			var bytes = scriptPubKey.ToBytes(true);
			if (bytes.Length == 0 ||
			    bytes[0] != (byte) OpcodeType.OP_RETURN ||
			    bytes.Length > MaxScriptSizeLimit)
			{
				needMoreCheck = false;
				return false;
			}

			needMoreCheck = true;
			return true;
		}

		protected override bool CheckScriptPubKeyCore(Script scriptPubKey, Op[] scriptPubKeyOps)
		{
			return scriptPubKeyOps.Skip(1).All(o => o.PushData != null && !o.IsInvalid);
		}

		public byte[][] ExtractScriptPubKeyParameters(Script scriptPubKey)
		{
			bool needMoreCheck;
			if (!FastCheckScriptPubKey(scriptPubKey, out needMoreCheck))
			{
				return null;
			}

			var ops = scriptPubKey.ToOps().ToArray();
			if (!CheckScriptPubKeyCore(scriptPubKey, ops))
			{
				return null;
			}

			return ops.Skip(1).Select(o => o.PushData).ToArray();
		}

		protected override bool CheckScriptSigCore(Script scriptSig, Op[] scriptSigOps, Script scriptPubKey,
			Op[] scriptPubKeyOps)
		{
			return false;
		}

		public Script GenerateScriptPubKey(params byte[][] data)
		{
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			var ops = new Op[data.Length + 1];
			ops[0] = OpcodeType.OP_RETURN;
			for (var i = 0; i < data.Length; i++)
			{
				ops[1 + i] = Op.GetPushOp(data[i]);
			}

			var script = new Script(ops);
			if (script.ToBytes(true).Length > MaxScriptSizeLimit)
			{
				throw new ArgumentOutOfRangeException(nameof(data),
					"Data in OP_RETURN should have a maximum size of " + MaxScriptSizeLimit + " bytes");
			}

			return script;
		}
	}

	public class PayToMultiSigTemplateParameters
	{
		public int SignatureCount { get; set; }

		public PubKey[] PubKeys { get; set; }

		public byte[][] InvalidPubKeys { get; set; }
	}

	public class PayToMultiSigTemplate : ScriptTemplate
	{
		public static PayToMultiSigTemplate Instance { get; } = new PayToMultiSigTemplate();

		public override TxOutType Type => TxOutType.TX_MULTISIG;

		public Script GenerateScriptPubKey(int sigCount, params PubKey[] keys)
		{
			var ops = new List<Op>();
			var push = Op.GetPushOp(sigCount);
			if (!push.IsSmallUInt)
			{
				throw new ArgumentOutOfRangeException(nameof(sigCount), "sigCount should be less or equal to 16");
			}

			ops.Add(push);
			var keyCount = Op.GetPushOp(keys.Length);
			if (!keyCount.IsSmallUInt)
			{
				throw new ArgumentOutOfRangeException(nameof(keys), "key count should be less or equal to 16");
			}

			foreach (var key in keys)
			{
				ops.Add(Op.GetPushOp(key.ToBytes()));
			}

			ops.Add(keyCount);
			ops.Add(OpcodeType.OP_CHECKMULTISIG);
			return new Script(ops);
		}

		protected override bool CheckScriptPubKeyCore(Script scriptPubKey, Op[] scriptPubKeyOps)
		{
			var ops = scriptPubKeyOps;
			if (ops.Length < 3)
			{
				return false;
			}

			var sigCount = ops[0].GetInt();
			var keyCount = ops[ops.Length - 2].GetInt();

			if (sigCount == null || keyCount == null)
			{
				return false;
			}

			if (keyCount.Value < 0 || keyCount.Value > 20)
			{
				return false;
			}

			if (sigCount.Value < 0 || sigCount.Value > keyCount.Value)
			{
				return false;
			}

			if (1 + keyCount + 1 + 1 != ops.Length)
			{
				return false;
			}

			for (var i = 1; i < keyCount + 1; i++)
			{
				if (ops[i].PushData == null)
				{
					return false;
				}
			}

			return ops[ops.Length - 1].Code == OpcodeType.OP_CHECKMULTISIG;
		}

		public PayToMultiSigTemplateParameters ExtractScriptPubKeyParameters(Script scriptPubKey)
		{
			if (!FastCheckScriptPubKey(scriptPubKey, out _))
			{
				return null;
			}

			var ops = scriptPubKey.ToOps().ToArray();
			if (!CheckScriptPubKeyCore(scriptPubKey, ops))
			{
				return null;
			}

			//already checked in CheckScriptPubKeyCore
			var sigCount = ops[0].GetInt().Value;
			var keyCount = ops[ops.Length - 2].GetInt().Value;
			var keys = new List<PubKey>();
			var invalidKeys = new List<byte[]>();
			for (var i = 1; i < keyCount + 1; i++)
			{
				if (!PubKey.Check(ops[i].PushData, false))
				{
					invalidKeys.Add(ops[i].PushData);
				}
				else
				{
					try
					{
						keys.Add(new PubKey(ops[i].PushData));
					}
					catch (FormatException)
					{
						invalidKeys.Add(ops[i].PushData);
					}
				}
			}

			return new PayToMultiSigTemplateParameters
			{
				SignatureCount = sigCount,
				PubKeys = keys.ToArray(),
				InvalidPubKeys = invalidKeys.ToArray()
			};
		}

		protected override bool FastCheckScriptSig(Script scriptSig, Script scriptPubKey, out bool needMoreCheck)
		{
			var bytes = scriptSig.ToBytes(true);
			if (bytes.Length == 0 ||
			    bytes[0] != (byte) OpcodeType.OP_0)
			{
				needMoreCheck = false;
				return false;
			}

			needMoreCheck = true;
			return true;
		}

		protected override bool CheckScriptSigCore(Script scriptSig, Op[] scriptSigOps, Script scriptPubKey,
			Op[] scriptPubKeyOps)
		{
			if (!scriptSig.IsPushOnly)
			{
				return false;
			}

			if (scriptSigOps[0].Code != OpcodeType.OP_0)
			{
				return false;
			}

			if (scriptSigOps.Length == 1)
			{
				return false;
			}

			if (!scriptSigOps.Skip(1).All(s =>
				TransactionSignature.ValidLength(s.PushData.Length) || s.Code == OpcodeType.OP_0))
			{
				return false;
			}

			if (scriptPubKeyOps != null)
			{
				if (!CheckScriptPubKeyCore(scriptPubKey, scriptPubKeyOps))
				{
					return false;
				}

				var sigCountExpected = scriptPubKeyOps[0].GetInt();
				if (sigCountExpected == null)
				{
					return false;
				}

				return sigCountExpected == scriptSigOps.Length + 1;
			}

			return true;
		}

		public TransactionSignature[] ExtractScriptSigParameters(Script scriptSig)
		{
			if (!FastCheckScriptSig(scriptSig, null, out _))
			{
				return null;
			}

			var ops = scriptSig.ToOps().ToArray();
			if (!CheckScriptSigCore(scriptSig, ops, null, null))
			{
				return null;
			}

			try
			{
				return ops.Skip(1).Select(i => i.Code == OpcodeType.OP_0 ? null : new TransactionSignature(i.PushData))
					.ToArray();
			}
			catch (FormatException)
			{
				return null;
			}
		}

		public Script GenerateScriptSig(TransactionSignature[] signatures)
		{
			return GenerateScriptSig((IEnumerable<TransactionSignature>) signatures);
		}

		public Script GenerateScriptSig(IEnumerable<TransactionSignature> signatures)
		{
			var ops = new List<Op> {OpcodeType.OP_0};
			foreach (var sig in signatures)
			{
				if (sig == null)
				{
					ops.Add(OpcodeType.OP_0);
				}
				else
				{
					ops.Add(Op.GetPushOp(sig.ToBytes()));
				}
			}

			return new Script(ops);
		}
	}

	public class PayToScriptHashSigParameters
	{
		public Script RedeemScript { get; set; }

		public byte[][] Pushes { get; set; }

		public TransactionSignature[] GetMultisigSignatures()
		{
			return PayToMultiSigTemplate.Instance.ExtractScriptSigParameters(
				new Script(Pushes.Select(p => Op.GetPushOp(p)).ToArray()));
		}
	}

	//https://github.com/bitcoin/bips/blob/master/bip-0016.mediawiki
	public class PayToScriptHashTemplate : ScriptTemplate
	{
		public static PayToScriptHashTemplate Instance { get; } = new PayToScriptHashTemplate();


		public override TxOutType Type => TxOutType.TX_SCRIPTHASH;

		public Script GenerateScriptPubKey(ScriptId scriptId)
		{
			return new Script(
				OpcodeType.OP_HASH160,
				Op.GetPushOp(scriptId.ToBytes()),
				OpcodeType.OP_EQUAL);
		}

		public Script GenerateScriptPubKey(Script scriptPubKey)
		{
			return GenerateScriptPubKey(scriptPubKey.Hash);
		}

		protected override bool FastCheckScriptPubKey(Script scriptPubKey, out bool needMoreCheck)
		{
			var bytes = scriptPubKey.ToBytes(true);
			needMoreCheck = false;
			return
				bytes.Length == 23 &&
				bytes[0] == (byte) OpcodeType.OP_HASH160 &&
				bytes[1] == 0x14 &&
				bytes[22] == (byte) OpcodeType.OP_EQUAL;
		}

		protected override bool CheckScriptPubKeyCore(Script scriptPubKey, Op[] scriptPubKeyOps)
		{
			return true;
		}

		public Script GenerateScriptSig(Op[] ops, Script redeemScript)
		{
			var pushScript = Op.GetPushOp(redeemScript._script);
			return new Script(ops.Concat(new[] {pushScript}));
		}

		public PayToScriptHashSigParameters ExtractScriptSigParameters(Script scriptSig)
		{
			return ExtractScriptSigParameters(scriptSig, null as Script);
		}

		public PayToScriptHashSigParameters ExtractScriptSigParameters(Script scriptSig, ScriptId expectedScriptId)
		{
			if (expectedScriptId == null)
			{
				return ExtractScriptSigParameters(scriptSig, null as Script);
			}

			return ExtractScriptSigParameters(scriptSig, expectedScriptId.ScriptPubKey);
		}

		public PayToScriptHashSigParameters ExtractScriptSigParameters(Script scriptSig, Script scriptPubKey)
		{
			var ops = scriptSig.ToOps().ToArray();
			var ops2 = scriptPubKey == null ? null : scriptPubKey.ToOps().ToArray();
			if (!CheckScriptSigCore(scriptSig, ops, scriptPubKey, ops2))
			{
				return null;
			}

			var result = new PayToScriptHashSigParameters
			{
				RedeemScript = Script.FromBytesUnsafe(ops[ops.Length - 1].PushData),
				Pushes = ops.Take(ops.Length - 1).Select(o => o.PushData).ToArray()
			};
			return result;
		}

		public Script GenerateScriptSig(byte[][] pushes, Script redeemScript)
		{
			var ops = new List<Op>();
			foreach (var push in pushes)
			{
				ops.Add(Op.GetPushOp(push));
			}

			ops.Add(Op.GetPushOp(redeemScript.ToBytes(true)));
			return new Script(ops);
		}

		public Script GenerateScriptSig(TransactionSignature[] signatures, Script redeemScript)
		{
			var ops = new List<Op>();
			var multiSigTemplate = new PayToMultiSigTemplate();
			var multiSig = multiSigTemplate.CheckScriptPubKey(redeemScript);
			if (multiSig)
			{
				ops.Add(OpcodeType.OP_0);
			}

			foreach (var sig in signatures)
			{
				ops.Add(sig == null ? OpcodeType.OP_0 : Op.GetPushOp(sig.ToBytes()));
			}

			return GenerateScriptSig(ops.ToArray(), redeemScript);
		}

		public Script GenerateScriptSig(ECDSASignature[] signatures, Script redeemScript)
		{
			return GenerateScriptSig(signatures.Select(s => new TransactionSignature(s, SigHash.All)).ToArray(),
				redeemScript);
		}

		protected override bool CheckScriptSigCore(Script scriptSig, Op[] scriptSigOps, Script scriptPubKey,
			Op[] scriptPubKeyOps)
		{
			var ops = scriptSigOps;
			if (ops.Length == 0)
			{
				return false;
			}

			if (!scriptSig.IsPushOnly)
			{
				return false;
			}

			if (scriptPubKey != null)
			{
				var expectedHash = ExtractScriptPubKeyParameters(scriptPubKey);
				if (expectedHash == null)
				{
					return false;
				}

				if (expectedHash != Script.FromBytesUnsafe(ops[ops.Length - 1].PushData).Hash)
				{
					return false;
				}
			}

			var redeemBytes = ops[ops.Length - 1].PushData;
			if (redeemBytes.Length > 520)
			{
				return false;
			}

			return Script.FromBytesUnsafe(ops[ops.Length - 1].PushData).IsValid;
		}

		public ScriptId ExtractScriptPubKeyParameters(Script scriptPubKey)
		{
			if (!FastCheckScriptPubKey(scriptPubKey, out _))
			{
				return null;
			}

			return new ScriptId(scriptPubKey.ToBytes(true).SafeSubArray(2, 20));
		}

		public Script GenerateScriptSig(PayToScriptHashSigParameters parameters)
		{
			return GenerateScriptSig(parameters.Pushes, parameters.RedeemScript);
		}
	}

	public class PayToPubkeyTemplate : ScriptTemplate
	{
		public static PayToPubkeyTemplate Instance { get; } = new PayToPubkeyTemplate();

		public override TxOutType Type => TxOutType.TX_PUBKEY;

		public Script GenerateScriptPubKey(PubKey pubkey)
		{
			return GenerateScriptPubKey(pubkey.ToBytes(true));
		}

		public Script GenerateScriptPubKey(byte[] pubkey)
		{
			return new Script(
				Op.GetPushOp(pubkey),
				OpcodeType.OP_CHECKSIG
			);
		}

		protected override bool FastCheckScriptPubKey(Script scriptPubKey, out bool needMoreCheck)
		{
			needMoreCheck = false;
			return
				scriptPubKey.Length > 3 &&
				PubKey.Check(scriptPubKey.ToBytes(true), 1, scriptPubKey.Length - 2, false) &&
				scriptPubKey.ToBytes(true)[scriptPubKey.Length - 1] == 0xac;
		}

		protected override bool CheckScriptPubKeyCore(Script scriptPubKey, Op[] scriptPubKeyOps)
		{
			return true;
		}

		public Script GenerateScriptSig(ECDSASignature signature)
		{
			return GenerateScriptSig(new TransactionSignature(signature, SigHash.All));
		}

		public Script GenerateScriptSig(TransactionSignature signature)
		{
			return new Script(
				Op.GetPushOp(signature.ToBytes())
			);
		}

		public TransactionSignature ExtractScriptSigParameters(Script scriptSig)
		{
			var ops = scriptSig.ToOps().ToArray();
			if (!CheckScriptSigCore(scriptSig, ops, null, null))
			{
				return null;
			}

			var data = ops[0].PushData;
			if (!TransactionSignature.ValidLength(data.Length))
			{
				return null;
			}

			try
			{
				return new TransactionSignature(data);
			}
			catch (FormatException)
			{
				return null;
			}
		}

		protected override bool FastCheckScriptSig(Script scriptSig, Script scriptPubKey, out bool needMoreCheck)
		{
			needMoreCheck = true;
			return 67 + 1 <= scriptSig.Length && scriptSig.Length <= 80 + 2 || scriptSig.Length == 9 + 1;
		}

		protected override bool CheckScriptSigCore(Script scriptSig, Op[] scriptSigOps, Script scriptPubKey,
			Op[] scriptPubKeyOps)
		{
			var ops = scriptSigOps;
			if (ops.Length != 1)
			{
				return false;
			}

			return ops[0].PushData != null && TransactionSignature.IsValid(ops[0].PushData);
		}

		/// <summary>
		///     Extract the public key or null from the script, perform quick check on pubkey
		/// </summary>
		/// <param name="scriptPubKey"></param>
		/// <returns>The public key</returns>
		public PubKey ExtractScriptPubKeyParameters(Script scriptPubKey)
		{
			if (!FastCheckScriptPubKey(scriptPubKey, out _))
			{
				return null;
			}

			try
			{
				return new PubKey(scriptPubKey.ToBytes(true).SafeSubArray(1, scriptPubKey.Length - 2), true);
			}
			catch (FormatException)
			{
				return null;
			}
		}

		/// <summary>
		///     Extract the public key or null from the script
		/// </summary>
		/// <param name="scriptPubKey"></param>
		/// <param name="deepCheck">Whether deep checks are done on public key</param>
		/// <returns>The public key</returns>
		public PubKey ExtractScriptPubKeyParameters(Script scriptPubKey, bool deepCheck)
		{
			var result = ExtractScriptPubKeyParameters(scriptPubKey);
			if (result == null || !deepCheck)
			{
				return result;
			}

			return PubKey.Check(result.ToBytes(true), true) ? result : null;
		}
	}

	public class PayToPubkeyHashScriptSigParameters : IDestination
	{
		public TransactionSignature TransactionSignature { get; set; }

		public PubKey PublicKey { get; set; }

		public virtual TxDestination Hash => PublicKey.Hash;

		// IDestination Members

		public Script ScriptPubKey => Hash.ScriptPubKey;
	}

	public class PayToPubkeyHashTemplate : ScriptTemplate
	{
		public static PayToPubkeyHashTemplate Instance { get; } = new PayToPubkeyHashTemplate();

		public override TxOutType Type => TxOutType.TX_PUBKEYHASH;

		public Script GenerateScriptPubKey(BitcoinPubKeyAddress address)
		{
			if (address == null)
			{
				throw new ArgumentNullException(nameof(address));
			}

			return GenerateScriptPubKey(address.Hash);
		}

		public Script GenerateScriptPubKey(PubKey pubKey)
		{
			if (pubKey == null)
			{
				throw new ArgumentNullException(nameof(pubKey));
			}

			return GenerateScriptPubKey(pubKey.Hash);
		}

		public Script GenerateScriptPubKey(KeyId pubkeyHash)
		{
			return new Script(
				OpcodeType.OP_DUP,
				OpcodeType.OP_HASH160,
				Op.GetPushOp(pubkeyHash.ToBytes()),
				OpcodeType.OP_EQUALVERIFY,
				OpcodeType.OP_CHECKSIG
			);
		}

		public Script GenerateScriptSig(TransactionSignature signature, PubKey publicKey)
		{
			if (publicKey == null)
			{
				throw new ArgumentNullException(nameof(publicKey));
			}

			return new Script(
				signature == null ? OpcodeType.OP_0 : Op.GetPushOp(signature.ToBytes()),
				Op.GetPushOp(publicKey.ToBytes())
			);
		}

		protected override bool FastCheckScriptPubKey(Script scriptPubKey, out bool needMoreCheck)
		{
			var bytes = scriptPubKey.ToBytes(true);
			needMoreCheck = false;
			return bytes.Length == 25 &&
			       bytes[0] == (byte) OpcodeType.OP_DUP &&
			       bytes[1] == (byte) OpcodeType.OP_HASH160 &&
			       bytes[2] == 0x14 &&
			       bytes[24] == (byte) OpcodeType.OP_CHECKSIG;
		}

		protected override bool CheckScriptPubKeyCore(Script scriptPubKey, Op[] scriptPubKeyOps)
		{
			return true;
		}

		public KeyId ExtractScriptPubKeyParameters(Script scriptPubKey)
		{
			if (!FastCheckScriptPubKey(scriptPubKey, out _))
			{
				return null;
			}

			return new KeyId(scriptPubKey.ToBytes(true).SafeSubArray(3, 20));
		}

		protected override bool CheckScriptSigCore(Script scriptSig, Op[] scriptSigOps, Script scriptPubKey,
			Op[] scriptPubKeyOps)
		{
			var ops = scriptSigOps;
			if (ops.Length != 2)
			{
				return false;
			}

			return ops[0].PushData != null &&
			       (ops[0].Code == OpcodeType.OP_0 ||
			        TransactionSignature.IsValid(ops[0].PushData, ScriptVerify.None)) &&
			       ops[1].PushData != null && PubKey.Check(ops[1].PushData, false);
		}

		public bool CheckScriptSig(Script scriptSig)
		{
			return CheckScriptSig(scriptSig, null);
		}

		public PayToPubkeyHashScriptSigParameters ExtractScriptSigParameters(Script scriptSig)
		{
			var ops = scriptSig.ToOps().ToArray();
			if (!CheckScriptSigCore(scriptSig, ops, null, null))
			{
				return null;
			}

			try
			{
				return new PayToPubkeyHashScriptSigParameters
				{
					TransactionSignature =
						ops[0].Code == OpcodeType.OP_0 ? null : new TransactionSignature(ops[0].PushData),
					PublicKey = new PubKey(ops[1].PushData, true)
				};
			}
			catch (FormatException)
			{
				return null;
			}
		}


		public Script GenerateScriptSig(PayToPubkeyHashScriptSigParameters parameters)
		{
			return GenerateScriptSig(parameters.TransactionSignature, parameters.PublicKey);
		}
	}

	public abstract class ScriptTemplate
	{
		public abstract TxOutType Type { get; }

		public virtual bool CheckScriptPubKey(Script scriptPubKey)
		{
			if (scriptPubKey == null)
			{
				throw new ArgumentNullException(nameof(scriptPubKey));
			}

			var result = FastCheckScriptPubKey(scriptPubKey, out var needMoreCheck);
			if (needMoreCheck)
			{
				result &= CheckScriptPubKeyCore(scriptPubKey, scriptPubKey.ToOps().ToArray());
			}

			return result;
		}

		protected virtual bool FastCheckScriptPubKey(Script scriptPubKey, out bool needMoreCheck)
		{
			needMoreCheck = true;
			return true;
		}

		protected abstract bool CheckScriptPubKeyCore(Script scriptPubKey, Op[] scriptPubKeyOps);

		public virtual bool CheckScriptSig(Script scriptSig, Script scriptPubKey)
		{
			if (scriptSig == null)
			{
				throw new ArgumentNullException(nameof(scriptSig));
			}

			var result = FastCheckScriptSig(scriptSig, scriptPubKey, out var needMoreCheck);
			if (needMoreCheck)
			{
				result &= CheckScriptSigCore(scriptSig, scriptSig.ToOps().ToArray(), scriptPubKey,
					scriptPubKey == null ? null : scriptPubKey.ToOps().ToArray());
			}

			return result;
		}

		protected virtual bool FastCheckScriptSig(Script scriptSig, Script scriptPubKey, out bool needMoreCheck)
		{
			needMoreCheck = true;
			return true;
		}

		protected abstract bool CheckScriptSigCore(Script scriptSig, Op[] scriptSigOps, Script scriptPubKey,
			Op[] scriptPubKeyOps);
	}
}