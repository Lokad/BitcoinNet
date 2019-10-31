using System;
using System.Threading;

namespace BitcoinNet
{
	public class PerformanceSnapshot
	{
		public PerformanceSnapshot(long bytesRead, long bytesWritten)
		{
			TotalBytesWritten = bytesWritten;
			TotalBytesRead = bytesRead;
		}

		public long TotalBytesWritten { get; }

		public long TotalBytesRead { get; set; }

		public TimeSpan Elapsed => Taken - Start;

		public ulong BytesReadPerSecond => (ulong) (TotalBytesRead / Elapsed.TotalSeconds);

		public ulong BytesWrittenPerSecond => (ulong) (TotalBytesWritten / Elapsed.TotalSeconds);

		public DateTime Start { get; set; }

		public DateTime Taken { get; set; }

		public static PerformanceSnapshot operator -(PerformanceSnapshot end, PerformanceSnapshot start)
		{
			if (end.Start != start.Start)
			{
				throw new InvalidOperationException("Performance snapshot should be taken from the same point of time");
			}

			if (end.Taken < start.Taken)
			{
				throw new InvalidOperationException("The difference of snapshot can't be negative");
			}

			return new PerformanceSnapshot(end.TotalBytesRead - start.TotalBytesRead,
				end.TotalBytesWritten - start.TotalBytesWritten)
			{
				Start = start.Taken,
				Taken = end.Taken
			};
		}

		public override string ToString()
		{
			return "Read : " + ToKBSec(BytesReadPerSecond) + ", Write : " + ToKBSec(BytesWrittenPerSecond);
		}

		private string ToKBSec(ulong bytesPerSec)
		{
			var speed = bytesPerSec / 1024.0;
			return speed.ToString("0.00") + " KB/S)";
		}
	}

	public class PerformanceCounter
	{
		private long _bytesRead;
		private long _bytesWritten;

		public PerformanceCounter()
		{
			Start = DateTime.UtcNow;
		}

		public long BytesWritten => _bytesWritten;

		public long BytesRead => _bytesRead;

		public DateTime Start { get; }

		public TimeSpan Elapsed => DateTime.UtcNow - Start;

		public void AddBytesWritten(long count)
		{
			Interlocked.Add(ref _bytesWritten, count);
		}

		public void AddBytesRead(long count)
		{
			Interlocked.Add(ref _bytesRead, count);
		}

		public PerformanceSnapshot Snapshot()
		{
			var snap = new PerformanceSnapshot(BytesRead, BytesWritten)
			{
				Start = Start,
				Taken = DateTime.UtcNow
			};
			return snap;
		}

		public override string ToString()
		{
			return Snapshot().ToString();
		}

		internal void Add(PerformanceCounter counter)
		{
			AddBytesWritten(counter.BytesWritten);
			AddBytesRead(counter.BytesRead);
		}
	}
}