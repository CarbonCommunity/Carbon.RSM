using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Carbon;
using Carbon.Pooling;

namespace RustServerMetrics.HarmonyPatches
{
    [HarmonyPatch]
    public static class OxideMod_OnFrame_Patch
    {
        const string OxideCore_AssemblyName = "Carbon.Common";
        const string OxideOxideModType_FullName = "Oxide.Core.OxideMod";

        static Assembly _oxideCoreAssembly = null;
        public static float nextTick = 0f;

        [HarmonyPrepare]
        public static bool Prepare()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (!string.Equals(assembly.GetName().Name, OxideCore_AssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _oxideCoreAssembly = assembly;

                break;
            }

            if (_oxideCoreAssembly == null)
            {
                return false;
            }

            return true;
        }

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods(Harmony harmonyInstance)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (!string.Equals(assembly.GetName().Name, OxideCore_AssemblyName, StringComparison.OrdinalIgnoreCase)) continue;
                _oxideCoreAssembly = assembly;
                break;
            }

            if (_oxideCoreAssembly == null)
                return Array.Empty<MethodBase>();

            var oxideModType = _oxideCoreAssembly.GetType(OxideOxideModType_FullName, false);

            if (oxideModType == null)
                return Array.Empty<MethodBase>();

            var targetMethod = Type.GetType("Carbon.Managers.CarbonProcessor, Carbon")?
	            .GetMethod("Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (targetMethod == null)
                return Array.Empty<MethodBase>();

            return new MethodBase[] { targetMethod };
        }

        [HarmonyPrefix]
        public static void Prefix()
        {
	        var time = UnityEngine.Time.time;

	        if (nextTick > time)
	        {
		        return;
	        }

	        nextTick = time + 1f;
	        var temp = PoolEx.GetDictionary<string, double>();

	        foreach (var plugin in Community.Runtime.Plugins.Plugins)
	        {
		        temp.Add(plugin.Name, plugin.TotalHookTime.TotalMilliseconds);
	        }

	        MetricsLogger.Instance.OnOxidePluginMetrics(temp);

	        PoolEx.FreeDictionary(ref temp);
        }
    }
}
