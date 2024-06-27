using System;
using System.Collections.Generic;
using System.Reflection;
using API.Assembly;
using API.Events;
using HarmonyLib;
using Logger = Carbon.Logger;
using Object = UnityEngine.Object;

namespace RustServerMetrics;

public class RustServerMetricsLoader : IModulePackage
{
    public static bool __serverStarted = false;

    public static Harmony __harmonyInstance;
    public static List<Harmony> __modTimeWarningsHarmonyInstances = new ();

    public void AddModTimeWarnings(List<MethodInfo> methods)
    {
        var instance = new Harmony($"RustServerMetrics.ModTimeWarnings.{__modTimeWarningsHarmonyInstances.Count}");
        __modTimeWarningsHarmonyInstances.Add(instance);

        ModTimeWarnings.Methods.Clear();
        ModTimeWarnings.Methods.AddRange(methods);

        var patchProcessor = new PatchClassProcessor(instance, typeof(ModTimeWarnings));
        patchProcessor.Patch();

        foreach (var method in methods)
        {
            Logger.Log($"{method.DeclaringType?.Name}.{method.Name}");
        }

        Logger.Log($"[ServerMetrics]: Added {methods.Count} ModTimeWarnings");
    }

    public void Awake(EventArgs args)
    {
	    Logger.Log($"[ServerMetrics]: Carbon Community version {typeof(RustServerMetricsLoader).Assembly.GetName().Version} [module]");
    }

    public void OnLoaded(EventArgs args)
    {
	    if (!Bootstrap.bootstrapInitRun)
		    return;

	    Carbon.Community.Runtime.Events.Subscribe(CarbonEvent.HookValidatorRefreshed, _ =>
	    {
		    Carbon.Components.Harmony.PatchAll(Assembly.GetExecutingAssembly());

		    MetricsLogger.Initialize();
	    });
    }

    public void OnUnloaded(EventArgs args)
    {
	    __harmonyInstance?.UnpatchAll();
	    foreach (var instance in __modTimeWarningsHarmonyInstances)
	    {
		    instance?.UnpatchAll();
	    }

	    if (MetricsLogger.Instance != null)
		    Object.DestroyImmediate(MetricsLogger.Instance);
    }
}
