using System;
using System.Collections;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BitcoinNet.Crypto;

namespace BitcoinNet.Mnemonic
{
	/// <summary>
	///     A .NET implementation of the Bitcoin Improvement Proposal - 39 (BIP39)
	///     BIP39 specification used as reference located here: https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki
	///     Made by thashiznets@yahoo.com.au
	///     v1.0.1.1
	///     I ♥ Bitcoin :)
	///     Bitcoin:1ETQjMkR1NNh4jwLuN5LxY7bMsHC9PUPSV
	/// </summary>
	public class MnemonicSequence
	{
		private static readonly int[] MsArray = {12, 15, 18, 21, 24};
		private static readonly int[] CsArray = {4, 5, 6, 7, 8};
		private static readonly int[] EntArray = {128, 160, 192, 224, 256};
		private static readonly Encoding NoBomUtf8 = new UTF8Encoding(false);
		private static bool? _supportOsNormalization;
		private readonly string _mnemonic;
		private bool? _isValidChecksum;

		public MnemonicSequence(string mnemonic, WordList wordList = null)
		{
			if (mnemonic == null)
			{
				throw new ArgumentNullException(nameof(mnemonic));
			}

			_mnemonic = mnemonic.Trim();

			if (wordList == null)
			{
				wordList = Mnemonic.WordList.AutoDetect(mnemonic) ?? Mnemonic.WordList.English;
			}

			var words = mnemonic.Split(new[] {' ', '　'}, StringSplitOptions.RemoveEmptyEntries);
			//if the sentence is not at least 12 characters or cleanly divisible by 3, it is bad!
			if (!CorrectWordCount(words.Length))
			{
				throw new FormatException("Word count should be 12,15,18,21 or 24");
			}

			Words = words;
			WordList = wordList;
			Indices = wordList.ToIndices(words);
		}

		/// <summary>
		///     Generate a mnemonic
		/// </summary>
		/// <param name="wordList"></param>
		/// <param name="entropy"></param>
		public MnemonicSequence(WordList wordList, byte[] entropy = null)
		{
			wordList = wordList ?? Mnemonic.WordList.English;
			WordList = wordList;
			if (entropy == null)
			{
				entropy = RandomUtils.GetBytes(32);
			}

			var i = Array.IndexOf(EntArray, entropy.Length * 8);
			if (i == -1)
			{
				throw new ArgumentException("The length for entropy should be : " + string.Join(",", EntArray),
					nameof(entropy));
			}

			var cs = CsArray[i];
			var checksum = Hashes.SHA256(entropy);
			var entcsResult = new BitWriter();

			entcsResult.Write(entropy);
			entcsResult.Write(checksum, cs);
			Indices = entcsResult.ToIntegers();
			Words = WordList.GetWords(Indices);
			_mnemonic = WordList.GetSentence(Indices);
		}

		public MnemonicSequence(WordList wordList, WordCount wordCount)
			: this(wordList, GenerateEntropy(wordCount))
		{
		}

		public bool IsValidChecksum
		{
			get
			{
				if (_isValidChecksum == null)
				{
					var i = Array.IndexOf(MsArray, Indices.Length);
					var cs = CsArray[i];
					var ent = EntArray[i];

					var writer = new BitWriter();
					var bits = Mnemonic.WordList.ToBits(Indices);
					writer.Write(bits, ent);
					var entropy = writer.ToBytes();
					var checksum = Hashes.SHA256(entropy);

					writer.Write(checksum, cs);
					var expectedIndices = writer.ToIntegers();
					_isValidChecksum = expectedIndices.SequenceEqual(Indices);
				}

				return _isValidChecksum.Value;
			}
		}

		public WordList WordList { get; }

		public int[] Indices { get; }

		public string[] Words { get; }

		private static byte[] GenerateEntropy(WordCount wordCount)
		{
			var ms = (int) wordCount;
			if (!CorrectWordCount(ms))
			{
				throw new ArgumentException("Word count should be 12,15,18,21 or 24", nameof(wordCount));
			}

			var i = Array.IndexOf(MsArray, (int) wordCount);
			return RandomUtils.GetBytes(EntArray[i] / 8);
		}

		private static bool CorrectWordCount(int ms)
		{
			return MsArray.Any(_ => _ == ms);
		}


		// FIXME: this method is not used. Shouldn't we delete it?
		private int ToInt(BitArray bits)
		{
			if (bits.Length != 11)
			{
				throw new InvalidOperationException("Should never happen, bug in BitcoinNet.");
			}

			var number = 0;
			var base2Divide = 1024; //it's all downhill from here...literally we halve this for each bit we move to.

			//literally picture this loop as going from the most significant bit across to the least in the 11 bits, dividing by 2 for each bit as per binary/base 2
			foreach (bool b in bits)
			{
				if (b)
				{
					number = number + base2Divide;
				}

				base2Divide = base2Divide / 2;
			}

			return number;
		}

		public byte[] DeriveSeed(string passphrase = null)
		{
			passphrase = passphrase ?? "";
			var salt = Concat(NoBomUtf8.GetBytes("mnemonic"), Normalize(passphrase));
			var bytes = Normalize(_mnemonic);

			var rfcKey = new Rfc2898DeriveBytes(bytes, salt, 2048, HashAlgorithmName.SHA512);
			return rfcKey.GetBytes(64);
		}

		internal static byte[] Normalize(string str)
		{
			return NoBomUtf8.GetBytes(NormalizeString(str));
		}

		internal static string NormalizeString(string word)
		{
			if (!SupportOsNormalization())
			{
				return KDTable.NormalizeKD(word);
			}

			return word.Normalize(NormalizationForm.FormKD);
		}

		internal static bool SupportOsNormalization()
		{
			if (_supportOsNormalization == null)
			{
				var notNormalized = "あおぞら";
				var normalized = "あおぞら";
				if (notNormalized.Equals(normalized, StringComparison.Ordinal))
				{
					_supportOsNormalization = false;
				}
				else
				{
					try
					{
						_supportOsNormalization = notNormalized.Normalize(NormalizationForm.FormKD)
							.Equals(normalized, StringComparison.Ordinal);
					}
					catch
					{
						_supportOsNormalization = false;
					}
				}
			}

			return _supportOsNormalization.Value;
		}

		public ExtKey DeriveExtKey(string passphrase = null)
		{
			return new ExtKey(DeriveSeed(passphrase));
		}

		private static byte[] Concat(byte[] source1, byte[] source2)
		{
			//Most efficient way to merge two arrays this according to http://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp
			var buffer = new byte[source1.Length + source2.Length];
			Buffer.BlockCopy(source1, 0, buffer, 0, source1.Length);
			Buffer.BlockCopy(source2, 0, buffer, source1.Length, source2.Length);

			return buffer;
		}

		public override string ToString()
		{
			return _mnemonic;
		}
	}

	public enum WordCount
	{
		Twelve = 12,
		Fifteen = 15,
		Eighteen = 18,
		TwentyOne = 21,
		TwentyFour = 24
	}
}