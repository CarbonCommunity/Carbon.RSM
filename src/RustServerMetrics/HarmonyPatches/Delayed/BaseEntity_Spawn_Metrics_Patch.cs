using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

// ReSharper disable InconsistentNaming

namespace RustServerMetrics.HarmonyPatches.Delayed;

[DelayedHarmonyPatch]
[HarmonyPatch]
internal static class BaseEntity_Spawn_Metrics_Patch
{
    private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

    [HarmonyPrepare]
    public static bool Prepare()
    {
        if (!RustServerMetricsLoader.__serverStarted)
        {
            Debug.Log("Note: Cannot patch BaseEntity_Spawn_Metrics_Patch yet. We will patch it upon server start.");
            return false;
        }

        return true;
    }

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods(Harmony harmonyInstance)
    {
        yield return AccessTools.Method(typeof(BaseEntity), nameof(BaseEntity.Spawn));
    }

    [HarmonyPrefix]
    public static void Prefix(ref long __state)
    {
        __state = Stopwatch.GetTimestamp();
    }

    [HarmonyPostfix]
    public static void Postfix(long __state)
    {
        if (!MetricsLogger.IsReady)
        {
            return;
        }

        var ms = (Stopwatch.GetTimestamp() - __state) * TicksToMs;
        MetricsLogger.Instance.ServerUpdate.LogTime("BaseEntity.Spawn", ms);
    }
}
