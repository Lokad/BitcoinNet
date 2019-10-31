﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BitcoinNet.DataEncoders;

namespace BitcoinNet.Scripting
{
	public class Op
	{
		private const int MaxScriptElementSize = 520;
		private const string Unknown = "OP_UNKNOWN(0x";

		internal static readonly Dictionary<string, OpcodeType> OpcodeByName;
		private static readonly bool[] ValidOpCode;

		private OpcodeType _code;
		private string _name;

		static Op()
		{
			ValidOpCode = GetValidOpCode();
			OpcodeByName = new Dictionary<string, OpcodeType>();
			foreach (var code in Enum.GetValues(typeof(OpcodeType)).Cast<OpcodeType>().Distinct())
			{
				var name = GetOpName(code);
				if (name != "OP_UNKNOWN")
				{
					OpcodeByName.AddOrReplace(name, code);
				}
			}

			OpcodeByName.AddOrReplace("OP_TRUE", OpcodeType.OP_1);
			OpcodeByName.AddOrReplace("OP_FALSE", OpcodeType.OP_0);
			OpcodeByName.AddOrReplace("OP_CHECKLOCKTIMEVERIFY", OpcodeType.OP_CHECKLOCKTIMEVERIFY);
			OpcodeByName.AddOrReplace("OP_HODL", OpcodeType.OP_CHECKLOCKTIMEVERIFY);
			OpcodeByName.AddOrReplace("OP_NOP2", OpcodeType.OP_CHECKLOCKTIMEVERIFY);
			OpcodeByName.AddOrReplace("OP_CHECKSEQUENCEVERIFY", OpcodeType.OP_CHECKSEQUENCEVERIFY);
			OpcodeByName.AddOrReplace("OP_NOP3", OpcodeType.OP_CHECKSEQUENCEVERIFY);

			foreach (var op in new[]
			{
				new object[] {"OP_0", OpcodeType.OP_0},
				new object[] {"OP_1", OpcodeType.OP_1},
				new object[] {"OP_2", OpcodeType.OP_2},
				new object[] {"OP_3", OpcodeType.OP_3},
				new object[] {"OP_4", OpcodeType.OP_4},
				new object[] {"OP_5", OpcodeType.OP_5},
				new object[] {"OP_6", OpcodeType.OP_6},
				new object[] {"OP_7", OpcodeType.OP_7},
				new object[] {"OP_8", OpcodeType.OP_8},
				new object[] {"OP_9", OpcodeType.OP_9}
			})
			{
				OpcodeByName.AddOrReplace((string) op[0], (OpcodeType) op[1]);
			}
		}

		internal Op()
		{
		}

		public string Name
		{
			get
			{
				if (_name == null)
				{
					_name = GetOpName(Code);
				}

				return _name;
			}
		}

		public OpcodeType Code
		{
			get => _code;
			set
			{
				_code = value;
				IsInvalid = !ValidOpCode[(byte) value];
			}
		}

		public byte[] PushData { get; set; }


		public bool IsInvalid { get; private set; }

		public bool IsSmallUInt =>
			Code == OpcodeType.OP_0 ||
			OpcodeType.OP_1 <= Code && Code <= OpcodeType.OP_16;

		public bool IsSmallInt => IsSmallUInt || Code == OpcodeType.OP_1NEGATE;

		//Copied from satoshi's code
		public static string GetOpName(OpcodeType opcode)
		{
			if (!ValidOpCode[(byte) opcode])
			{
				return "OP_UNKNOWN";
			}

			switch (opcode)
			{
				// push value
				case OpcodeType.OP_0:
					return "0";
				case OpcodeType.OP_PUSHDATA1:
					return "OP_PUSHDATA1";
				case OpcodeType.OP_PUSHDATA2:
					return "OP_PUSHDATA2";
				case OpcodeType.OP_PUSHDATA4:
					return "OP_PUSHDATA4";
				case OpcodeType.OP_1NEGATE:
					return "-1";
				case OpcodeType.OP_RESERVED:
					return "OP_RESERVED";
				case OpcodeType.OP_1:
					return "1";
				case OpcodeType.OP_2:
					return "2";
				case OpcodeType.OP_3:
					return "3";
				case OpcodeType.OP_4:
					return "4";
				case OpcodeType.OP_5:
					return "5";
				case OpcodeType.OP_6:
					return "6";
				case OpcodeType.OP_7:
					return "7";
				case OpcodeType.OP_8:
					return "8";
				case OpcodeType.OP_9:
					return "9";
				case OpcodeType.OP_10:
					return "OP_10";
				case OpcodeType.OP_11:
					return "OP_11";
				case OpcodeType.OP_12:
					return "OP_12";
				case OpcodeType.OP_13:
					return "OP_13";
				case OpcodeType.OP_14:
					return "OP_14";
				case OpcodeType.OP_15:
					return "OP_15";
				case OpcodeType.OP_16:
					return "OP_16";

				// control
				case OpcodeType.OP_NOP:
					return "OP_NOP";
				case OpcodeType.OP_VER:
					return "OP_VER";
				case OpcodeType.OP_IF:
					return "OP_IF";
				case OpcodeType.OP_NOTIF:
					return "OP_NOTIF";
				case OpcodeType.OP_VERIF:
					return "OP_VERIF";
				case OpcodeType.OP_VERNOTIF:
					return "OP_VERNOTIF";
				case OpcodeType.OP_ELSE:
					return "OP_ELSE";
				case OpcodeType.OP_ENDIF:
					return "OP_ENDIF";
				case OpcodeType.OP_VERIFY:
					return "OP_VERIFY";
				case OpcodeType.OP_RETURN:
					return "OP_RETURN";

				// stack ops
				case OpcodeType.OP_TOALTSTACK:
					return "OP_TOALTSTACK";
				case OpcodeType.OP_FROMALTSTACK:
					return "OP_FROMALTSTACK";
				case OpcodeType.OP_2DROP:
					return "OP_2DROP";
				case OpcodeType.OP_2DUP:
					return "OP_2DUP";
				case OpcodeType.OP_3DUP:
					return "OP_3DUP";
				case OpcodeType.OP_2OVER:
					return "OP_2OVER";
				case OpcodeType.OP_2ROT:
					return "OP_2ROT";
				case OpcodeType.OP_2SWAP:
					return "OP_2SWAP";
				case OpcodeType.OP_IFDUP:
					return "OP_IFDUP";
				case OpcodeType.OP_DEPTH:
					return "OP_DEPTH";
				case OpcodeType.OP_DROP:
					return "OP_DROP";
				case OpcodeType.OP_DUP:
					return "OP_DUP";
				case OpcodeType.OP_NIP:
					return "OP_NIP";
				case OpcodeType.OP_OVER:
					return "OP_OVER";
				case OpcodeType.OP_PICK:
					return "OP_PICK";
				case OpcodeType.OP_ROLL:
					return "OP_ROLL";
				case OpcodeType.OP_ROT:
					return "OP_ROT";
				case OpcodeType.OP_SWAP:
					return "OP_SWAP";
				case OpcodeType.OP_TUCK:
					return "OP_TUCK";

				// splice ops
				case OpcodeType.OP_CAT:
					return "OP_CAT";
				case OpcodeType.OP_SUBSTR:
					return "OP_SUBSTR";
				case OpcodeType.OP_LEFT:
					return "OP_LEFT";
				case OpcodeType.OP_RIGHT:
					return "OP_RIGHT";
				case OpcodeType.OP_SIZE:
					return "OP_SIZE";

				// bit logic
				case OpcodeType.OP_INVERT:
					return "OP_INVERT";
				case OpcodeType.OP_AND:
					return "OP_AND";
				case OpcodeType.OP_OR:
					return "OP_OR";
				case OpcodeType.OP_XOR:
					return "OP_XOR";
				case OpcodeType.OP_EQUAL:
					return "OP_EQUAL";
				case OpcodeType.OP_EQUALVERIFY:
					return "OP_EQUALVERIFY";
				case OpcodeType.OP_RESERVED1:
					return "OP_RESERVED1";
				case OpcodeType.OP_RESERVED2:
					return "OP_RESERVED2";

				// numeric
				case OpcodeType.OP_1ADD:
					return "OP_1ADD";
				case OpcodeType.OP_1SUB:
					return "OP_1SUB";
				case OpcodeType.OP_2MUL:
					return "OP_2MUL";
				case OpcodeType.OP_2DIV:
					return "OP_2DIV";
				case OpcodeType.OP_NEGATE:
					return "OP_NEGATE";
				case OpcodeType.OP_ABS:
					return "OP_ABS";
				case OpcodeType.OP_NOT:
					return "OP_NOT";
				case OpcodeType.OP_0NOTEQUAL:
					return "OP_0NOTEQUAL";
				case OpcodeType.OP_ADD:
					return "OP_ADD";
				case OpcodeType.OP_SUB:
					return "OP_SUB";
				case OpcodeType.OP_MUL:
					return "OP_MUL";
				case OpcodeType.OP_DIV:
					return "OP_DIV";
				case OpcodeType.OP_MOD:
					return "OP_MOD";
				case OpcodeType.OP_LSHIFT:
					return "OP_LSHIFT";
				case OpcodeType.OP_RSHIFT:
					return "OP_RSHIFT";
				case OpcodeType.OP_BOOLAND:
					return "OP_BOOLAND";
				case OpcodeType.OP_BOOLOR:
					return "OP_BOOLOR";
				case OpcodeType.OP_NUMEQUAL:
					return "OP_NUMEQUAL";
				case OpcodeType.OP_NUMEQUALVERIFY:
					return "OP_NUMEQUALVERIFY";
				case OpcodeType.OP_NUMNOTEQUAL:
					return "OP_NUMNOTEQUAL";
				case OpcodeType.OP_LESSTHAN:
					return "OP_LESSTHAN";
				case OpcodeType.OP_GREATERTHAN:
					return "OP_GREATERTHAN";
				case OpcodeType.OP_LESSTHANOREQUAL:
					return "OP_LESSTHANOREQUAL";
				case OpcodeType.OP_GREATERTHANOREQUAL:
					return "OP_GREATERTHANOREQUAL";
				case OpcodeType.OP_MIN:
					return "OP_MIN";
				case OpcodeType.OP_MAX:
					return "OP_MAX";
				case OpcodeType.OP_WITHIN:
					return "OP_WITHIN";

				// crypto
				case OpcodeType.OP_RIPEMD160:
					return "OP_RIPEMD160";
				case OpcodeType.OP_SHA1:
					return "OP_SHA1";
				case OpcodeType.OP_SHA256:
					return "OP_SHA256";
				case OpcodeType.OP_HASH160:
					return "OP_HASH160";
				case OpcodeType.OP_HASH256:
					return "OP_HASH256";
				case OpcodeType.OP_CODESEPARATOR:
					return "OP_CODESEPARATOR";
				case OpcodeType.OP_CHECKSIG:
					return "OP_CHECKSIG";
				case OpcodeType.OP_CHECKSIGVERIFY:
					return "OP_CHECKSIGVERIFY";
				case OpcodeType.OP_CHECKMULTISIG:
					return "OP_CHECKMULTISIG";
				case OpcodeType.OP_CHECKMULTISIGVERIFY:
					return "OP_CHECKMULTISIGVERIFY";
				case OpcodeType.OP_CHECKLOCKTIMEVERIFY:
					return "OP_CLTV";
				case OpcodeType.OP_CHECKSEQUENCEVERIFY:
					return "OP_CSV";

				// expanson
				case OpcodeType.OP_NOP1:
					return "OP_NOP1";
				case OpcodeType.OP_NOP4:
					return "OP_NOP4";
				case OpcodeType.OP_NOP5:
					return "OP_NOP5";
				case OpcodeType.OP_NOP6:
					return "OP_NOP6";
				case OpcodeType.OP_NOP7:
					return "OP_NOP7";
				case OpcodeType.OP_NOP8:
					return "OP_NOP8";
				case OpcodeType.OP_NOP9:
					return "OP_NOP9";
				case OpcodeType.OP_NOP10:
					return "OP_NOP10";

				default:
					return Enum.GetName(typeof(OpcodeType), opcode);
			}
		}

		internal static bool IsPushCode(OpcodeType opcode)
		{
			return 0 <= opcode && opcode <= OpcodeType.OP_16 && opcode != OpcodeType.OP_RESERVED;
		}

		public static bool GetOpCode(string name, out OpcodeType result)
		{
			return OpcodeByName.TryGetValue(name, out result);
		}

		public static Op GetPushOp(long value)
		{
			byte[] bytes;

			if (value != 0)
			{
				var isPositive = true;
				if (value < 0)
				{
					value = -value;
					isPositive = false;
				}

				// Calculate required bits. We need extra 1 bit for sign.
				var numBits = Utils.IntLog2(value) + 1;
				var numBytes = (numBits + 7) / 8;

				var input = (ulong) value;
				bytes = new byte[numBytes];
				for (var i = 0; i < numBytes; ++i)
				{
					bytes[i] = (byte) (input & 0xff);
					input >>= 8;
				}

				if (!isPositive)
				{
					bytes[numBytes - 1] |= 0x80;
				}
			}
			else
			{
				bytes = new byte[0];
			}

			return GetPushOp(bytes);
		}

		public static Op GetPushOp(byte[] data)
		{
			var op = new Op();
			op.PushData = data;
			if (data.Length == 0)
			{
				op.Code = OpcodeType.OP_0;
			}
			else if (data.Length == 1 && 1 <= data[0] && data[0] <= 16)
			{
				op.Code = (OpcodeType) (data[0] + (byte) OpcodeType.OP_1 - 1);
			}
			else if (data.Length == 1 && 0x81 == data[0])
			{
				op.Code = OpcodeType.OP_1NEGATE;
			}
			else if (0x01 <= data.Length && data.Length <= 0x4b)
			{
				op.Code = (OpcodeType) (byte) data.Length;
			}
			else if (data.Length <= 0xFF)
			{
				op.Code = OpcodeType.OP_PUSHDATA1;
			}
			else if (data.Length <= 0xFFFF)
			{
				op.Code = OpcodeType.OP_PUSHDATA2;
			}
			else
			{
				throw new NotSupportedException("Data length should not be bigger than 0xFFFFFFFF");
			}

			return op;
		}

		private static bool[] GetValidOpCode()
		{
			var valid = new bool[256];
			foreach (var val in Enum.GetValues(typeof(OpcodeType)))
			{
				valid[(byte) val] = true;
			}

			for (byte i = 0;; i++)
			{
				if (IsPushCode((OpcodeType) i))
				{
					valid[i] = true;
				}

				if (i == 255)
				{
					break;
				}
			}

			return valid;
		}

		private void PushDataToStream(byte[] data, Stream result)
		{
			var bitStream = new BitcoinStream(result, true);

			if (Code == OpcodeType.OP_0)
			{
				//OP_0 already pushed
				return;
			}

			if (OpcodeType.OP_1 <= Code && Code <= OpcodeType.OP_16)
			{
				//OP_1 to OP_16 already pushed
				return;
			}

			if (Code == OpcodeType.OP_1NEGATE)
			{
				//OP_1Negate already pushed
				return;
			}

			if (0x01 <= (byte) Code && (byte) Code <= 0x4b)
			{
				//Data length already pushed
			}
			else if (Code == OpcodeType.OP_PUSHDATA1)
			{
				bitStream.ReadWrite((byte) data.Length);
			}
			else if (Code == OpcodeType.OP_PUSHDATA2)
			{
				bitStream.ReadWrite((ushort) data.Length);
			}
			else if (Code == OpcodeType.OP_PUSHDATA4)
			{
				bitStream.ReadWrite((uint) data.Length);
			}
			else
			{
				throw new NotSupportedException("Data length should not be bigger than 0xFFFFFFFF");
			}

			result.Write(data, 0, data.Length);
		}

		internal byte[] ReadData(Stream stream)
		{
			uint len = 0;
			var bitStream = new BitcoinStream(stream, false);
			if (Code == 0)
			{
				return new byte[0];
			}

			if ((byte) OpcodeType.OP_1 <= (byte) Code && (byte) Code <= (byte) OpcodeType.OP_16)
			{
				return new[] {(byte) (Code - OpcodeType.OP_1 + 1)};
			}

			if (Code == OpcodeType.OP_1NEGATE)
			{
				return new byte[] {0x81};
			}

			try
			{
				if (0x01 <= (byte) Code && (byte) Code <= 0x4b)
				{
					len = (uint) Code;
				}
				else if (Code == OpcodeType.OP_PUSHDATA1)
				{
					len = bitStream.ReadWrite((byte) 0);
				}
				else if (Code == OpcodeType.OP_PUSHDATA2)
				{
					len = bitStream.ReadWrite((ushort) 0);
				}
				else if (Code == OpcodeType.OP_PUSHDATA4)
				{
					len = bitStream.ReadWrite((uint) 0);
				}
				else
				{
					IsInvalid = true;
					return new byte[0];
				}


				byte[] data = null;

				if (len <= MaxScriptElementSize) //Most of the time
				{
					data = new byte[len];
					var readen = stream.Read(data, 0, data.Length);
					if (readen != data.Length)
					{
						IsInvalid = true;
						return new byte[0];
					}
				}
				else //Mitigate against a big array allocation
				{
					var bytes = new List<byte>();
					for (var i = 0; i < len; i++)
					{
						var b = stream.ReadByte();
						if (b < 0)
						{
							IsInvalid = true;
							return new byte[0];
						}

						bytes.Add((byte) b);
					}

					data = bytes.ToArray();
				}

				return data;
			}
			catch (EndOfStreamException)
			{
				IsInvalid = true;
				return new byte[0];
			}
		}

		public byte[] ToBytes()
		{
			var ms = new MemoryStream();
			WriteTo(ms);
			return ms.ToArray();
		}

		public override string ToString()
		{
			if (PushData != null)
			{
				if (PushData.Length == 0)
				{
					return "0";
				}

				var result = Encoders.Hex.EncodeData(PushData);
				return result.Length == 2 && result[0] == '0' ? result.Substring(1) : result;
			}

			if (Name == "OP_UNKNOWN")
			{
				return Name + "(" + string.Format("0x{0:x2}", (byte) Code) + ")";
			}

			return Name;
		}

		public void WriteTo(Stream stream)
		{
			stream.WriteByte((byte) Code);
			if (PushData != null)
			{
				PushDataToStream(PushData, stream);
			}
		}

		internal static Op Read(TextReader textReader)
		{
			var opname = ReadWord(textReader);
			OpcodeType opcode;

			var isOpCode = GetOpCode(opname, out opcode);

			if (
				(!isOpCode || IsPushCode(opcode))
				&& !opname.StartsWith(Unknown))
			{
				if (isOpCode && opcode == OpcodeType.OP_0)
				{
					return GetPushOp(new byte[0]);
				}

				opname = opname.Replace("OP_", "");
				if (opname.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
				{
					opname = "1";
				}

				if (opname.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
				{
					opname = "0";
				}

				return GetPushOp(Encoders.Hex.DecodeData(opname.Length == 1 ? "0" + opname : opname));
			}

			if (opname.StartsWith(Unknown))
			{
				try
				{
					if (opname.StartsWith(Unknown))
					{
						opcode = (OpcodeType) Encoders.Hex.DecodeData(opname.Substring(Unknown.Length, 2))[0];
					}
				}
				catch (Exception ex)
				{
					throw new FormatException("Invalid unknown opcode", ex);
				}
			}

			return new Op
			{
				Code = opcode
			};
		}

		public static implicit operator Op(OpcodeType codeType)
		{
			if (!IsPushCode(codeType))
			{
				return new Op
				{
					Code = codeType
				};
			}

			if (OpcodeType.OP_1 <= codeType && codeType <= OpcodeType.OP_16)
			{
				return new Op
				{
					Code = codeType,
					PushData = new[] {(byte) ((byte) codeType - (byte) OpcodeType.OP_1 + 1)}
				};
			}

			if (codeType == OpcodeType.OP_0)
			{
				return new Op
				{
					Code = codeType,
					PushData = new byte[0]
				};
			}

			if (codeType == OpcodeType.OP_1NEGATE)
			{
				return new Op
				{
					Code = codeType,
					PushData = new byte[] {0x81}
				};
			}

			throw new InvalidOperationException("Push OP without any data provided detected, Op.PushData instead");
		}

		private static string ReadWord(TextReader textReader)
		{
			var builder = new StringBuilder();
			int r;
			while ((r = textReader.Read()) != -1)
			{
				var ch = (char) r;
				var isSpace = DataEncoder.IsSpace(ch);
				if (isSpace && builder.Length == 0)
				{
					continue;
				}

				if (isSpace && builder.Length != 0)
				{
					break;
				}

				builder.Append((char) r);
			}

			return builder.ToString();
		}

		public int? GetInt()
		{
			var l = GetLong();
			if (l == null)
			{
				return null;
			}

			if (l.Value > int.MaxValue)
			{
				return int.MaxValue;
			}

			if (l.Value < int.MinValue)
			{
				return int.MinValue;
			}

			return (int) l.Value;
		}

		public long? GetLong()
		{
			if (PushData == null)
			{
				return null;
			}

			var vch = PushData;
			if (vch.Length == 0)
			{
				return 0;
			}

			long result = 0;
			for (var i = 0; i != vch.Length; ++i)
			{
				result |= (long) vch[i] << (8 * i);
			}

			// If the input vector's most significant byte is 0x80, remove it from
			// the result's msb and return a negative.
			if ((vch[vch.Length - 1] & 0x80) != 0)
			{
				var temp = ~(0x80UL << (8 * (vch.Length - 1)));
				return -(long) ((ulong) result & temp);
			}

			return result;
		}
	}
}