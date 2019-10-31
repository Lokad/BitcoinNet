using System;

namespace BitcoinNet
{
	public class FeeRate : IEquatable<FeeRate>, IComparable<FeeRate>
	{
		public FeeRate(Money feePerK)
		{
			if (feePerK == null)
			{
				throw new ArgumentNullException(nameof(feePerK));
			}

			if (feePerK.Satoshi < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(feePerK));
			}

			FeePerK = feePerK;
		}

		public FeeRate(Money feePaid, int size)
		{
			if (feePaid == null)
			{
				throw new ArgumentNullException(nameof(feePaid));
			}

			if (feePaid.Satoshi < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(feePaid));
			}

			if (size > 0)
			{
				FeePerK = feePaid * 1000 / size;
			}
			else
			{
				FeePerK = 0;
			}
		}

		/// <summary>
		///     Fee per KB
		/// </summary>
		public Money FeePerK { get; }

		public static FeeRate Zero { get; } = new FeeRate(Money.Zero);

		public int CompareTo(FeeRate other)
		{
			return other == null
				? 1
				: FeePerK.CompareTo(other.FeePerK);
		}

		// IEquatable<FeeRate> Members

		public bool Equals(FeeRate other)
		{
			return other != null && FeePerK.Equals(other.FeePerK);
		}

		/// <summary>
		///     Get fee for the size
		/// </summary>
		/// <param name="virtualSize">Size in bytes</param>
		/// <returns></returns>
		public Money GetFee(int virtualSize)
		{
			Money nFee = FeePerK.Satoshi * virtualSize / 1000;
			if (nFee == 0 && FeePerK.Satoshi > 0)
			{
				nFee = FeePerK.Satoshi;
			}

			return nFee;
		}

		public Money GetFee(Transaction tx)
		{
			return GetFee(tx.GetVirtualSize());
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
			{
				return true;
			}

			if ((object) this == null || obj == null)
			{
				return false;
			}

			var left = this;
			var right = obj as FeeRate;
			if (right == null)
			{
				return false;
			}

			return left.FeePerK == right.FeePerK;
		}

		public override string ToString()
		{
			return $"{FeePerK} BTC/kB";
		}

		// IComparable Members

		public int CompareTo(object obj)
		{
			if (obj == null)
			{
				return 1;
			}

			var m = obj as FeeRate;
			if (m != null)
			{
				return FeePerK.CompareTo(m.FeePerK);
			}

			return FeePerK.CompareTo((long) obj);
		}

		public static bool operator <(FeeRate left, FeeRate right)
		{
			if (left == null)
			{
				throw new ArgumentNullException(nameof(left));
			}

			if (right == null)
			{
				throw new ArgumentNullException(nameof(right));
			}

			return left.FeePerK < right.FeePerK;
		}

		public static bool operator >(FeeRate left, FeeRate right)
		{
			if (left == null)
			{
				throw new ArgumentNullException(nameof(left));
			}

			if (right == null)
			{
				throw new ArgumentNullException(nameof(right));
			}

			return left.FeePerK > right.FeePerK;
		}

		public static bool operator <=(FeeRate left, FeeRate right)
		{
			if (left == null)
			{
				throw new ArgumentNullException(nameof(left));
			}

			if (right == null)
			{
				throw new ArgumentNullException(nameof(right));
			}

			return left.FeePerK <= right.FeePerK;
		}

		public static bool operator >=(FeeRate left, FeeRate right)
		{
			if (left == null)
			{
				throw new ArgumentNullException(nameof(left));
			}

			if (right == null)
			{
				throw new ArgumentNullException(nameof(right));
			}

			return left.FeePerK >= right.FeePerK;
		}

		public static bool operator ==(FeeRate left, FeeRate right)
		{
			if (ReferenceEquals(left, right))
			{
				return true;
			}

			if ((object) left == null || (object) right == null)
			{
				return false;
			}

			return left.FeePerK == right.FeePerK;
		}

		public static bool operator !=(FeeRate left, FeeRate right)
		{
			return !(left == right);
		}

		public override int GetHashCode()
		{
			return FeePerK.GetHashCode();
		}

		public static FeeRate Min(FeeRate left, FeeRate right)
		{
			if (left == null)
			{
				throw new ArgumentNullException(nameof(left));
			}

			if (right == null)
			{
				throw new ArgumentNullException(nameof(right));
			}

			return left <= right
				? left
				: right;
		}

		public static FeeRate Max(FeeRate left, FeeRate right)
		{
			if (left == null)
			{
				throw new ArgumentNullException(nameof(left));
			}

			if (right == null)
			{
				throw new ArgumentNullException(nameof(right));
			}

			return left >= right
				? left
				: right;
		}
	}
}