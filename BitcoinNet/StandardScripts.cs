using System.Linq;
using BitcoinNet.Policy;
using BitcoinNet.Scripting;

namespace BitcoinNet
{
	public static class StandardScripts
	{
		private static readonly ScriptTemplate[] StandardTemplates =
		{
			PayToPubkeyHashTemplate.Instance,
			PayToPubkeyTemplate.Instance,
			PayToScriptHashTemplate.Instance,
			PayToMultiSigTemplate.Instance,
			TxNullDataTemplate.Instance
		};

		public static bool IsStandardTransaction(Transaction tx)
		{
			return new StandardTransactionPolicy().Check(tx, null).Length == 0;
		}

		public static bool AreOutputsStandard(Transaction tx)
		{
			return tx.Outputs.All(vout => IsStandardScriptPubKey(vout.ScriptPubKey));
		}

		public static ScriptTemplate GetTemplateFromScriptPubKey(Script script)
		{
			return StandardTemplates.FirstOrDefault(t => t.CheckScriptPubKey(script));
		}

		public static bool IsStandardScriptPubKey(Script scriptPubKey)
		{
			return StandardTemplates.Any(template => template.CheckScriptPubKey(scriptPubKey));
		}

		private static bool IsStandardScriptSig(Script scriptSig, Script scriptPubKey)
		{
			var template = GetTemplateFromScriptPubKey(scriptPubKey);
			if (template == null)
			{
				return false;
			}

			return template.CheckScriptSig(scriptSig, scriptPubKey);
		}
	}
}