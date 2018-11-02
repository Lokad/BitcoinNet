﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinNet.Protocol
{
	public class VarString : IBitcoinSerializable
	{
		public VarString()
		{

		}
		byte[] _Bytes = new byte[0];
		public int Length
		{
			get
			{
				return _Bytes.Length;
			}
		}
		public VarString(byte[] bytes)
		{
			if(bytes == null)
				throw new ArgumentNullException(nameof(bytes));
			_Bytes = bytes;
		}
		public byte[] GetString()
		{
			return GetString(false);
		}
		public byte[] GetString(bool @unsafe)
		{
			if(@unsafe)
				return _Bytes;
			return _Bytes.ToArray();
		}
		#region IBitcoinSerializable Members

		public void ReadWrite(BitcoinStream stream)
		{
			var len = new VarInt((ulong)_Bytes.Length);
			stream.ReadWrite(ref len);
			if(!stream.Serializing)
			{
				if(len.ToLong() > (uint)stream.MaxArraySize)
					throw new ArgumentOutOfRangeException("Array size not big");
				_Bytes = new byte[len.ToLong()];
			}
			stream.ReadWrite(ref _Bytes);
		}

		internal static void StaticWrite(BitcoinStream bs, byte[] bytes)
		{
			VarInt.StaticWrite(bs, (ulong)bytes.Length);
			bs.ReadWrite(ref bytes);
		}

		internal static void StaticRead(BitcoinStream bs, ref byte[] bytes)
		{
			var len = VarInt.StaticRead(bs);
			bytes = new byte[len];
			bs.ReadWrite(ref bytes);
		}

		#endregion
	}
}