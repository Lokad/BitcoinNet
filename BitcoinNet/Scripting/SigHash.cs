namespace BitcoinNet.Scripting
{
	/// <summary>
	///     Signature hash types/flags
	/// </summary>
	public enum SigHash : uint
	{
		Undefined = 0,

		/// <summary>
		///     All outputs are signed
		/// </summary>
		All = 1,

		/// <summary>
		///     No outputs as signed
		/// </summary>
		None = 2,

		/// <summary>
		///     Only the output with the same index as this input is signed
		/// </summary>
		Single = 3,

		/// <summary>
		///     If set, no inputs, except this, are part of the signature
		/// </summary>
		AnyoneCanPay = 0x80
	}
}