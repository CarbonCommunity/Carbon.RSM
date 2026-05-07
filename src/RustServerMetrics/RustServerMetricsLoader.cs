using System.Collections.Generic;
using System.Reflection;
using API.Assembly;
using API.Events;
using HarmonyLib;
using UnityEngine;

namespace RustServerMetrics;

public class RustServerMetricsLoader : IModulePackage
{
    public static bool __serverStarted = false;
    
    public static Harmony __harmonyInstance;
    
    public static List<Harmony> __modTimeWarningsHarmonyInstances = [];

	public void Awake(System.EventArgs args)
	{
		Carbon.Logger.Log($"[ServerMetrics]: Carbon Community version {typeof(RustServerMetricsLoader).Assembly.GetName().Version} [module]");
	}

	public void OnLoaded(System.EventArgs args)
	{
		if (!Bootstrap.bootstrapInitRun)
			return;

		Carbon.Community.Runtime.Events.Subscribe(CarbonEvent.HookValidatorRefreshed, _ =>
		{
			Carbon.Components.Harmony.PatchAll(Assembly.GetExecutingAssembly());

			MetricsLogger.Initialize();
		});
	}

	public void OnUnloaded(System.EventArgs args)
	{
		MetricsLogger.IsReady = false;

		__harmonyInstance?.UnpatchAll();
		foreach (var instance in __modTimeWarningsHarmonyInstances)
		{
			instance?.UnpatchAll();
		}

		if (MetricsLogger.Instance != null)
		{
			Object.DestroyImmediate(MetricsLogger.Instance);
		}
	}
}
