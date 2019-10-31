namespace BitcoinNet.Scripting
{
	public class SignedHash
	{
		public TransactionSignature Signature { get; internal set; }

		public Script ScriptCode { get; internal set; }

		public uint256 Hash { get; internal set; }
	}
}