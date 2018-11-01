namespace System.Diagnostics
{
	public class TraceSourceFactory
	{
		public static TraceSource CreateTraceSource(string name)
		{
			return new TraceSource(name);
		}
	}
}
