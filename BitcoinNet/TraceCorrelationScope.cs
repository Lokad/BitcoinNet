using System;
using System.Diagnostics;

namespace BitcoinNet
{
	public class TraceCorrelationScope : IDisposable
	{
		private readonly TraceSource _source;
		private readonly bool _transferred;

		public TraceCorrelationScope(Guid activity, TraceSource source, bool traceTransfer)
		{
			OldActivity = Trace.CorrelationManager.ActivityId;

			_transferred = OldActivity != activity && traceTransfer;
			if (_transferred)
			{
				_source = source;
				_source.TraceTransfer(0, "t", activity);
			}

			Trace.CorrelationManager.ActivityId = activity;
		}

		public Guid OldActivity { get; }

		// IDisposable Members

		public void Dispose()
		{
			if (_transferred)
			{
				_source.TraceTransfer(0, "transfer", OldActivity);
			}

			Trace.CorrelationManager.ActivityId = OldActivity;
		}
	}

	public class TraceCorrelation
	{
		private readonly string _activityName;
		private readonly TraceSource _source;
		private volatile bool _first = true;

		public TraceCorrelation(TraceSource source, string activityName)
			: this(Guid.NewGuid(), source, activityName)
		{
		}

		public TraceCorrelation(Guid activity, TraceSource source, string activityName)
		{
			_source = source;
			_activityName = activityName;
			Activity = activity;
		}

		public Guid Activity { get; }

		public TraceCorrelationScope Open(bool traceTransfer = true)
		{
			var scope = new TraceCorrelationScope(Activity, _source, traceTransfer);
			if (_first)
			{
				_first = false;
				_source.TraceEvent(TraceEventType.Start, 0, _activityName);
			}

			return scope;
		}

		public void LogInside(Action act, bool traceTransfer = true)
		{
			using (Open(traceTransfer))
			{
				act();
			}
		}

		public override string ToString()
		{
			return _activityName;
		}
	}
}