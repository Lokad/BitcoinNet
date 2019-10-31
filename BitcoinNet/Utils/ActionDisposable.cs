using System;

namespace BitcoinNet
{
	internal class ActionDisposable : IDisposable
	{
		private readonly Action _onEnter;
		private readonly Action _onLeave;

		public ActionDisposable(Action onEnter, Action onLeave)
		{
			_onEnter = onEnter;
			_onLeave = onLeave;
			_onEnter();
		}

		// IDisposable Members

		public void Dispose()
		{
			_onLeave();
		}
	}
}