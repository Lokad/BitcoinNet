using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;

namespace BitcoinNet.Crypto
{
	public abstract class HashStreamBase : Stream
	{
		public abstract uint256 GetHash();
	}

	/// <summary>
	///     Double SHA256 hash stream
	/// </summary>
	public class HashStream : HashStreamBase
	{
		private static readonly byte[] Empty = new byte[0];

		private readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(32 * 10);
		private readonly SHA256Managed _sha = new SHA256Managed();
		private int _pos;

		public override bool CanRead => throw new NotImplementedException();

		public override bool CanSeek => throw new NotImplementedException();

		public override bool CanWrite => throw new NotImplementedException();

		public override long Length => throw new NotImplementedException();

		public override long Position
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public override void Flush()
		{
			throw new NotImplementedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			var copied = 0;
			var toCopy = 0;
			var innerSpan = new Span<byte>(_buffer, _pos, _buffer.Length - _pos);
			while (!buffer.IsEmpty)
			{
				toCopy = Math.Min(innerSpan.Length, buffer.Length);
				buffer.Slice(0, toCopy).CopyTo(innerSpan.Slice(0, toCopy));
				buffer = buffer.Slice(toCopy);
				innerSpan = innerSpan.Slice(toCopy);
				copied += toCopy;
				_pos += toCopy;
				if (ProcessBlockIfNeeded())
				{
					innerSpan = _buffer.AsSpan();
				}
			}
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			var copied = 0;
			var toCopy = 0;
			while (copied != count)
			{
				toCopy = Math.Min(_buffer.Length - _pos, count - copied);
				Buffer.BlockCopy(buffer, offset + copied, _buffer, _pos, toCopy);
				copied += toCopy;
				_pos += toCopy;
				ProcessBlockIfNeeded();
			}
		}

		public override void WriteByte(byte value)
		{
			_buffer[_pos++] = value;
			ProcessBlockIfNeeded();
		}

		private bool ProcessBlockIfNeeded()
		{
			if (_pos == _buffer.Length)
			{
				ProcessBlock();
				return true;
			}

			return false;
		}

		private void ProcessBlock()
		{
			_sha.TransformBlock(_buffer, 0, _pos, null, -1);
			_pos = 0;
		}

		public override uint256 GetHash()
		{
			ProcessBlock();
			_sha.TransformFinalBlock(Empty, 0, 0);
			var hash1 = _sha.Hash;
			Buffer.BlockCopy(_sha.Hash, 0, _buffer, 0, 32);
			_sha.Initialize();
			_sha.TransformFinalBlock(_buffer, 0, 32);
			var hash2 = _sha.Hash;
			return new uint256(hash2);
		}

		protected override void Dispose(bool disposing)
		{
			ArrayPool<byte>.Shared.Return(_buffer);
			if (disposing)
			{
				_sha.Dispose();
			}

			base.Dispose(disposing);
		}
	}

	/// <summary>
	///     Unoptimized hash stream, bufferize all the data
	/// </summary>
	public abstract class BufferedHashStream : HashStreamBase
	{
		private readonly MemoryStream _ms;

		public BufferedHashStream(int capacity)
		{
			_ms = new MemoryStream(capacity);
		}

		public override bool CanRead => throw new NotImplementedException();

		public override bool CanSeek => throw new NotImplementedException();

		public override bool CanWrite => throw new NotImplementedException();

		public override long Length => throw new NotImplementedException();

		public override long Position
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public static BufferedHashStream CreateFrom(Func<byte[], int, int, byte[]> calculateHash, int capacity = 0)
		{
			return new FuncBufferedHashStream(calculateHash, capacity);
		}

		public override void Flush()
		{
			throw new NotImplementedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			_ms.Write(buffer, offset, count);
		}

		public override void WriteByte(byte value)
		{
			_ms.WriteByte(value);
		}

		public override uint256 GetHash()
		{
			return GetHash(_ms.GetBuffer(), 0, (int) _ms.Length);
		}

		protected abstract uint256 GetHash(byte[] data, int offset, int length);

		private class FuncBufferedHashStream : BufferedHashStream
		{
			private readonly Func<byte[], int, int, byte[]> _calculateHash;

			public FuncBufferedHashStream(Func<byte[], int, int, byte[]> calculateHash, int capacity) : base(capacity)
			{
				if (calculateHash == null)
				{
					throw new ArgumentNullException(nameof(calculateHash));
				}

				_calculateHash = calculateHash;
			}

			protected override uint256 GetHash(byte[] data, int offset, int length)
			{
				return new uint256(_calculateHash(data, offset, length), 0, 32);
			}
		}
	}
}