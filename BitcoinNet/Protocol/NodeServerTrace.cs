using System;
using System.Diagnostics;

namespace BitcoinNet.Protocol
{
	public static class NodeServerTrace
	{
		internal static TraceSource Trace { get; } = new TraceSource("BitcoinNet.NodeServer");

		public static void Transfer(Guid activityId)
		{
			Trace.TraceTransfer(0, "t", activityId);
		}

		public static void ErrorWhileRetrievingDNSSeedIp(string name, Exception ex)
		{
			Trace.TraceEvent(TraceEventType.Warning, 0,
				"Impossible to resolve dns for seed " + name + " " + Utils.ExceptionToString(ex));
		}

		public static void Warning(string msg, Exception ex)
		{
			Trace.TraceEvent(TraceEventType.Warning, 0, msg + " " + Utils.ExceptionToString(ex));
		}

		public static void ExternalIpReceived(string ip)
		{
			Trace.TraceInformation("External ip received : " + ip);
		}

		internal static void ExternalIpFailed(Exception ex)
		{
			Trace.TraceEvent(TraceEventType.Error, 0, "External ip cannot be detected " + Utils.ExceptionToString(ex));
		}

		internal static void Information(string info)
		{
			Trace.TraceInformation(info);
		}

		internal static void Error(string msg, Exception ex)
		{
			Trace.TraceEvent(TraceEventType.Error, 0, msg + " " + Utils.ExceptionToString(ex));
		}

		internal static void Warning(string msg)
		{
			Warning(msg, null);
		}

		internal static void PeerTableRemainingPeerToGet(int count)
		{
			Trace.TraceInformation("Remaining peer to get : " + count);
		}

		internal static void ConnectionToSelfDetected()
		{
			Warning("Connection to self detected, abort connection");
		}

		internal static void Verbose(string str)
		{
			Trace.TraceEvent(TraceEventType.Verbose, 0, str);
		}
	}
}