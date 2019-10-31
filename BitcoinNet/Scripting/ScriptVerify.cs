using System;

namespace BitcoinNet.Scripting
{
	/// <summary>
	///     Script verification flags
	/// </summary>
	[Flags]
	public enum ScriptVerify : uint
	{
		None = 0,

		/// <summary>
		///     Evaluate P2SH subscripts (softfork safe, BIP16).
		/// </summary>
		P2SH = 1U << 0,

		/// <summary>
		///     Passing a non-strict-DER signature or one with undefined hashtype to a checksig operation causes script failure.
		///     Passing a pubkey that is not (0x04 + 64 bytes) or (0x02 or 0x03 + 32 bytes) to checksig causes that pubkey to be
		///     +
		///     skipped (not softfork safe: this flag can widen the validity of OP_CHECKSIG OP_NOT).
		/// </summary>
		StrictEnc = 1U << 1,

		/// <summary>
		///     Passing a non-strict-DER signature to a checksig operation causes script failure (softfork safe, BIP62 rule 1)
		/// </summary>
		DerSig = 1U << 2,

		/// <summary>
		///     Passing a non-strict-DER signature or one with S > order/2 to a checksig operation causes script failure
		///     (softfork safe, BIP62 rule 5).
		/// </summary>
		LowS = 1U << 3,

		/// <summary>
		///     verify dummy stack item consumed by CHECKMULTISIG is of zero-length (softfork safe, BIP62 rule 7).
		/// </summary>
		NullDummy = 1U << 4,

		/// <summary>
		///     Using a non-push operator in the scriptSig causes script failure (softfork safe, BIP62 rule 2).
		/// </summary>
		SigPushOnly = 1U << 5,

		/// <summary>
		///     Require minimal encodings for all push operations (OP_0... OP_16, OP_1NEGATE where possible, direct
		///     pushes up to 75 bytes, OP_PUSHDATA up to 255 bytes, OP_PUSHDATA2 for anything larger). Evaluating
		///     any other push causes the script to fail (BIP62 rule 3).
		///     In addition, whenever a stack element is interpreted as a number, it must be of minimal length (BIP62 rule 4).
		///     (softfork safe)
		/// </summary>
		MinimalData = 1U << 6,

		/// <summary>
		///     Discourage use of NOPs reserved for upgrades (NOP1-10)
		///     Provided so that nodes can avoid accepting or mining transactions
		///     containing executed NOP's whose meaning may change after a soft-fork,
		///     thus rendering the script invalid; with this flag set executing
		///     discouraged NOPs fails the script. This verification flag will never be
		///     a mandatory flag applied to scripts in a block. NOPs that are not
		///     executed, e.g.  within an unexecuted IF ENDIF block, are *not* rejected.
		/// </summary>
		DiscourageUpgradableNops = 1U << 7,

		/// <summary>
		///     Require that only a single stack element remains after evaluation. This changes the success criterion from
		///     "At least one stack element must remain, and when interpreted as a boolean, it must be true" to
		///     "Exactly one stack element must remain, and when interpreted as a boolean, it must be true".
		///     (softfork safe, BIP62 rule 6)
		///     Note: CLEANSTACK should never be used without P2SH.
		/// </summary>
		CleanStack = 1U << 8,

		/// <summary>
		///     Verify CHECKLOCKTIMEVERIFY
		///     See BIP65 for details.
		/// </summary>
		CheckLockTimeVerify = 1U << 9,

		/// <summary>
		///     See BIP68 for details.
		/// </summary>
		CheckSequenceVerify = 1U << 10,

		/// <summary>
		///     Support segregated witness
		/// </summary>
		Witness = 1U << 11,

		/// <summary>
		///     Making v2-v16 witness program non-standard
		/// </summary>
		DiscourageUpgradableWitnessProgram = 1U << 12,

		/// <summary>
		///     Segwit script only: Require the argument of OP_IF/NOTIF to be exactly 0x01 or empty vector
		/// </summary>
		MinimalIf = 1U << 13,

		/// <summary>
		///     Signature(s) must be empty vector if an CHECK(MULTI)SIG operation failed
		/// </summary>
		NullFail = 1U << 14,

		/// <summary>
		///     Public keys in segregated witness scripts must be compressed
		/// </summary>
		WitnessPubkeyType = 1U << 15,

		/// <summary>
		///     Some altcoins like BCash and BGold requires ForkId inside the sigHash
		/// </summary>
		ForkId = 1U << 29,

		/// <summary>
		///     Mandatory script verification flags that all new blocks must comply with for
		///     them to be valid. (but old blocks may not comply with) Currently just P2SH,
		///     but in the future other flags may be added, such as a soft-fork to enforce
		///     strict DER encoding.
		///     Failing one of these tests may trigger a DoS ban - see CheckInputs() for
		///     details.
		/// </summary>
		Mandatory = P2SH,

		/// <summary>
		///     Standard script verification flags that standard transactions will comply
		///     with. However scripts violating these flags may still be present in valid
		///     blocks and we must accept those blocks.
		/// </summary>
		Standard =
			Mandatory
			| DerSig
			| StrictEnc
			| MinimalData
			| NullDummy
			| DiscourageUpgradableNops
			| CleanStack
			| CheckLockTimeVerify
			| CheckSequenceVerify
			| LowS
			| Witness
			| DiscourageUpgradableWitnessProgram
			| NullFail
			| MinimalIf
	}
}