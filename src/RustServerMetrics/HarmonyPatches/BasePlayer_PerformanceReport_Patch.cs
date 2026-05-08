using HarmonyLib;
using ProtoBuf;
using UnityEngine;
using static BaseEntity;

// ReSharper disable InconsistentNaming

namespace RustServerMetrics.HarmonyPatches;

[HarmonyPatch(typeof(BasePlayer), nameof(BasePlayer.PerformanceReport))]
public class BasePlayer_PerformanceReport_Patch
{
	public static void Prefix(BasePlayer __instance, RPCMessage msg)
	{
		if (!MetricsLogger.IsReady)
		{
			return;
		}
		var position = msg.read.Position;
		_ = msg.read.String(); // We don't need kind
		using PerformanceReport report = msg.read.Proto<PerformanceReport>();
		if (report.user_id != __instance.UserIDString)
		{
			DebugEx.Log($"Client performance report from {__instance} has incorrect user_id ({__instance.UserIDString})");
			return;
		}
		MetricsLogger.Instance.OnClientPerformanceReport(report);
		msg.read.Position = position;
	}
}
